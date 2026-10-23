using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Journal;
using AiOrchestrator.Modules.Loop.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>What one pass did.</summary>
/// <param name="Folders">Folders whose loop was read and followed.</param>
/// <param name="Pbis">Open PBIs looked at, the loop's or not.</param>
/// <param name="Skipped">Each folder the pass could not follow, and why.</param>
/// <param name="Written">The journal lines this pass wrote, oldest first.</param>
sealed record Pass(int Folders, int Pbis, IReadOnlyList<string> Skipped, IReadOnlyList<JournalEntry> Written);

/// <summary>
/// One pass of the loop over every folder: observe each PBI, let <see cref="Planner"/> decide, and
/// apply what it decided — the label, the vendor step, the hold, a run <see cref="RunAdmission"/> lets
/// start — with every decision written to the journal. <see cref="StateScripts"/> runs the scripts.
/// <para>
/// Built per pass by <c>RunOnce</c> rather than registered: what it needs exists only in the host
/// that runs the loop, not in the migration step that also loads this module.
/// </para>
/// <para>
/// <b>Nothing retries.</b> A script that fails, a run that fails and a write the vendor did not keep
/// all end in a hold with the cause; a vendor that does not answer is a wait, because a hold is
/// itself a write to that vendor.
/// </para>
/// </summary>
sealed partial class LoopEngine(
    IEnumerable<IPbiProvider> providers,
    IRunRequests runs,
    IFolderDirectory folders,
    LoopDbContext database,
    Passes passes,
    TimeProvider clock,
    ILogger logger,
    IScriptRunner? harness = null,
    IEnumerable<IAgentProvider>? agents = null,
    ScriptOutputs? outputs = null
)
{
    readonly PassJournal _journal = new(database, clock);
    readonly RunAdmission _admission = new(runs, agents);

    /// <param name="walks">Which folders to walk, or null for all. The schedule leaves out a folder
    /// whose engine is off (#562): its PBIs are not read, so nothing new starts there, and its live
    /// runs go on in Runs, their outcome applied when it is on again.</param>
    /// <param name="ct">Cancels the pass.</param>
    public async Task<Pass> RunOnceAsync(Func<LoopFolder, bool>? walks, CancellationToken ct)
    {
        _journal.Clear();
        await _admission.CountLiveAsync(ct);
        var scripts = new StateScripts(runs, passes, _journal, clock, logger, harness, outputs);

        var followed = 0;
        var looked = 0;
        var skipped = new List<string>();

        foreach (var folder in await folders.ListAsync(ct))
        {
            if (walks is not null && !walks(folder))
            {
                continue;
            }

            var loop = await LoopReader.ReadAsync(folder, ct);
            if (loop.Problem is { } problem)
            {
                skipped.Add($"{folder.Path}: {problem}");
                LogSkipped(logger, folder.Path, problem);
                continue;
            }

            if (providers.FirstOrDefault(provider => provider.Name == folder.Vendor) is not { } provider)
            {
                skipped.Add($"{folder.Path}: no adapter reads {folder.Vendor}");
                continue;
            }

            string identity;
            IReadOnlyList<Pbi> pbis;
            try
            {
                var probe = await provider.ProbeAsync(ct);
                identity = probe.Account ?? throw new PbiProviderException(probe.Problem ?? $"The {probe.Cli} CLI is not on this machine.");
                pbis = await provider.ListAsync(folder.Repository, new PbiQuery(), ct);
            }
            catch (PbiProviderException failure)
            {
                skipped.Add($"{folder.Path}: {folder.Repository} could not be read: {failure.Message}");
                LogSkipped(logger, folder.Path, failure.Message);
                continue;
            }

            followed++;
            looked += pbis.Count;
            foreach (var pbi in pbis)
            {
                await Step(folder, loop.Definition!, provider, identity, pbi, scripts, ct);
            }
        }

        return new Pass(followed, looked, skipped, [.. _journal.Written]);
    }

    /// <summary>One PBI: the photograph, the decision, the scripts if it asks for them, and the decision again, applied.</summary>
    async Task Step(LoopFolder folder, LoopDefinition loop, IPbiProvider provider, string identity, Pbi pbi, StateScripts scripts, CancellationToken ct)
    {
        var lines = await database
            .Journal.AsNoTracking()
            .Where(entry => entry.Repository == pbi.Ref.Repository && entry.Pbi == pbi.Ref.Id)
            .OrderBy(entry => entry.Id)
            .ToListAsync(ct);

        try
        {
            var asked = pbi.State is null
                ? null
                : await database
                    .AskedRuns.Where(run =>
                        run.Repository == pbi.Ref.Repository && run.Pbi == pbi.Ref.Id && run.State == pbi.State && !run.Consumed
                    )
                    .OrderByDescending(run => run.AskedAt)
                    .FirstOrDefaultAsync(ct);

            RunStatus? status = asked is null ? null : await runs.GetAsync(asked.RunId, ct);
            var last = lines.LastOrDefault(entry => entry.Kind is JournalKind.Moved or JournalKind.Held or JournalKind.Released);
            var situation = new Situation(
                loop,
                pbi,
                identity,
                _admission.Live,
                asked is null ? null : new AskedRunStatus(asked.RunId, status is not null, status?.End, asked.AskedAt),
                null,
                Returns(loop, lines),
                last is null ? null : new LastTransition(last.Kind, last.From, last.To, last.Why, last.At),
                false,
                folder.DefaultAgent is { } agent ? new LoopAgent(agent, folder.DefaultModel) : null
            );

            if (pbi.Held && last is { Kind: JournalKind.Held, To: not null } && last.From == pbi.State)
            {
                situation = situation with { Approved = (await provider.GetPullRequestAsync(pbi.Ref, ct))?.IsApproved == true };
            }

            var already = _journal.Written.Count;
            var decision = Planner.Decide(situation);
            if (decision is RunScripts run)
            {
                var state = loop.States.First(declared => declared.Name == pbi.State);
                situation = situation with { Scripts = await scripts.RunAsync(run.Scripts, state, folder, provider, pbi, lines, ct) };
                decision = Planner.Decide(situation);
            }

            await Apply(decision, folder, provider, pbi, lines.LastOrDefault(), ct);

            // A run is acted on once. A wait — a person held the PBI while it ran — leaves it for
            // the pass after the release.
            if (asked is not null && (status is null || status.End is not null) && decision is Move or Hold or Note)
            {
                asked.Consume();
            }

            await _journal.SaveAsync(ct);
            await WriteJourneys(folder, provider, [.. _journal.Written.Skip(already)], ct);
        }
        catch (PbiProviderException failure)
        {
            await Wait(pbi, $"the vendor did not answer: {failure.Message}", lines.LastOrDefault(), ct);
        }
    }

    /// <summary>
    /// A person's decision from the PBI's header (#511), a <see cref="Release"/> or a
    /// <see cref="Move"/> from <see cref="Planner.Released"/> or <see cref="Planner.MovedByPerson"/>,
    /// applied as the engine applies its own: the same vendor writes, read back, and the same journal
    /// line. Answers the line it wrote.
    /// </summary>
    public async Task<JournalEntry?> ApplyForPersonAsync(Decision decision, LoopFolder folder, IPbiProvider provider, Pbi pbi, CancellationToken ct)
    {
        _journal.Clear();
        await Apply(decision, folder, provider, pbi, null, ct);
        await _journal.SaveAsync(ct);
        var written = _journal.Written.LastOrDefault();
        await WriteJourneys(folder, provider, [.. _journal.Written], ct);
        return written;
    }

    async Task Apply(Decision decision, LoopFolder folder, IPbiProvider provider, Pbi pbi, JournalEntry? previous, CancellationToken ct)
    {
        // A person may move a PBI that carries no state yet, and the journal names it as the wait does.
        var from = pbi.State ?? "no state";
        switch (decision)
        {
            case Ignore:
                return;

            case Wait wait:
                await Wait(pbi, wait.Why, previous, ct);
                return;

            case StartRun start:
                {
                    var order = await EpicOrder(provider, pbi, ct);
                    if (order.Waits is { } first)
                    {
                        await Wait(pbi, first, previous, ct);
                        return;
                    }

                    var request = new RunRequest(pbi.Ref, from, folder.Path, pbi.Branch(), start.Provider, start.Model, start.Command, start.Outcomes);
                    if (await _admission.WaitsAsync(request, ct) is { } why)
                    {
                        await Wait(pbi, why, previous, ct);
                        return;
                    }

                    var id = await _admission.StartAsync(request, ct);
                    database.AskedRuns.Add(new AskedRun(id, pbi.Ref.Repository, pbi.Ref.Id, from, clock.GetUtcNow()));
                    var asked = $"asked for {start.Command} with {start.Provider}{(start.Model is null ? "" : $" ({start.Model})")}, run {id}";
                    _journal.Write(pbi, from, JournalKind.Started, from, order.Unread is { } unread && !await SaidAsync(pbi, unread, ct) ? $"{asked}; {unread}" : asked);
                    return;
                }

            case Move move:
                {
                    // The vendor step first, and the move only when it held.
                    var refused = await Vendor(move.Step, provider, pbi, ct) ?? (await provider.MoveAsync(pbi.Ref, new PbiMove(move.To), ct)).Mismatch;
                    if (refused is not null)
                    {
                        await PutHold(provider, pbi, $"{move.Why}. Then {refused}", null, ct);
                        return;
                    }

                    _journal.Write(pbi, from, JournalKind.Moved, move.To, move.Why);
                    if (move.Step == VendorStep.Close)
                    {
                        await CloseParent(provider, pbi, move.To, ct);
                    }
                    return;
                }

            case Hold hold:
                await PutHold(provider, pbi, hold.Why, hold.Next, ct);
                return;

            case Note note:
                _journal.Write(pbi, from, JournalKind.Ran, null, note.Why);
                return;

            case Release release:
                {
                    var moved = await provider.MoveAsync(pbi.Ref, new PbiMove(release.To, Held: false), ct);
                    if (!moved.Matches)
                    {
                        _journal.Write(pbi, from, JournalKind.Waited, null, $"{release.Why}, and the release did not hold: {moved.Mismatch}");
                        return;
                    }

                    _journal.Write(pbi, from, JournalKind.Released, release.To, release.Why);
                    return;
                }

            default:
                throw new InvalidOperationException($"The engine has no way to apply {decision}.");
        }
    }

    /// <summary>The vendor step of a transition, or what went wrong with it.</summary>
    static async Task<string?> Vendor(VendorStep step, IPbiProvider provider, Pbi pbi, CancellationToken ct) =>
        step switch
        {
            VendorStep.OpenDraftPullRequest => (await provider.OpenPullRequestAsync(pbi.Ref.Repository, new PullRequestDraft(pbi.Branch(), pbi.Title, "", Draft: true, pbi.Ref), ct)).Mismatch,
            VendorStep.MarkReady => (await provider.OpenPullRequestAsync(pbi.Ref.Repository, new PullRequestDraft(pbi.Branch(), pbi.Title, "", Draft: false, pbi.Ref), ct)).Mismatch,
            VendorStep.Close => (await provider.MoveAsync(pbi.Ref, new PbiMove(Close: true), ct)).Mismatch,
            _ => null,
        };

    /// <summary>
    /// Puts the hold on. A hold the vendor did not keep is written without where it leads, so the
    /// next pass does the state again instead of reading the missing hold as a release.
    /// </summary>
    async Task PutHold(IPbiProvider provider, Pbi pbi, string why, string? next, CancellationToken ct)
    {
        var held = await provider.MoveAsync(pbi.Ref, new PbiMove(Held: true), ct);
        _journal.Write(
            pbi,
            pbi.State!,
            JournalKind.Held,
            held.Matches ? next : null,
            held.Matches ? why : $"{why}. The hold did not stick: {held.Mismatch}"
        );
    }

    /// <summary>A wait is written once however many passes it lasts: not when the PBI's last line is the same wait.</summary>
    async Task Wait(Pbi pbi, string why, JournalEntry? previous, CancellationToken ct)
    {
        var from = pbi.State ?? "no state";
        if (previous is { Kind: JournalKind.Waited } && previous.From == from && previous.Why == why)
        {
            return;
        }

        _journal.Write(pbi, from, JournalKind.Waited, null, why);
        await _journal.SaveAsync(ct);
    }

    /// <summary>How many times each state has sent the PBI back to a state above it.</summary>
    static Dictionary<string, int> Returns(LoopDefinition loop, IEnumerable<JournalEntry> lines) =>
        lines
            .Where(entry => entry.Kind is JournalKind.Moved or JournalKind.Released)
            .Where(entry => entry.To is { } to && Planner.Back(loop, entry.From, to))
            .GroupBy(entry => entry.From, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    [LoggerMessage(EventId = 5050, Level = LogLevel.Warning, Message = "The loop skipped {Folder}: {Why}")]
    static partial void LogSkipped(ILogger logger, string folder, string why);
}
