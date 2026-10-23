using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Journal;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>
/// Runs a state's scripts on one PBI and reads their verdicts: each announced with a <c>started</c>
/// line, counted as running in <see cref="Passes"/>, its output kept, its answer a <c>ran</c> line.
/// One of these lives one pass, because the model server is ensured once a pass. The engine decides
/// with the verdicts; nothing here moves, holds or starts a run.
/// </summary>
sealed partial class StateScripts(
    IRunRequests runs,
    Passes passes,
    PassJournal journal,
    TimeProvider clock,
    ILogger logger,
    IScriptRunner? harness = null,
    ScriptOutputs? outputs = null
)
{
    bool _ensured;

    /// <summary>
    /// The state's scripts and its score, in order, up to the first that fails or waits. A passing
    /// <c>claim</c> is followed by the assignment, because the claim is the branch and the
    /// assignee: the PBI is this control plane's from then on. They share one place, retired after
    /// the last that ran, so what the gate leaves uncommitted is there for <c>quality</c> (#751).
    /// </summary>
    /// <param name="lines">The PBI's journal before this pass, oldest first.</param>
    public async Task<IReadOnlyList<ScriptVerdict>> RunAsync(
        IReadOnlyList<string> names,
        LoopState state,
        LoopFolder folder,
        IPbiProvider provider,
        Pbi pbi,
        IReadOnlyList<JournalEntry> lines,
        CancellationToken ct
    )
    {
        var verdicts = new List<ScriptVerdict>();
        await using var place = runs.OpenScriptPlace(pbi.Ref, folder.Path, pbi.Branch());
        foreach (var name in names)
        {
            if (name == state.Score)
            {
                await EnsureModel(folder, ct);
            }

            var verdict = await Script(name, place, provider, pbi, lines, ct);

            if (name == "claim" && verdict.Kind == VerdictKind.Pass && (await provider.MoveAsync(pbi.Ref, new PbiMove(AssignToSelf: true), ct)).Mismatch is { } unassigned)
            {
                verdict = verdict with { Kind = VerdictKind.Fail, Why = $"{verdict.Why} The assignment did not hold: {unassigned}" };
            }

            // A wait is left to the decision's own line, which is written once however many
            // passes it lasts; every other answer is a line here, a pass included (#593).
            if (verdict.Kind != VerdictKind.Wait)
            {
                journal.Write(pbi, state.Name, JournalKind.Ran, null, $"{name}: {verdict.Why}", verdict.Output);
            }

            // An outcome does not stop the list: `verifying` runs the evidence after the gate
            // whatever the gate said, and the gate's outcome is still the state's.
            verdicts.Add(verdict);
            if (verdict.Kind is VerdictKind.Fail or VerdictKind.Wait)
            {
                break;
            }
        }
        return verdicts;
    }

    /// <summary>
    /// <c>.aio/scorers/laya/laya ensure</c>, at most once a pass, before the first score (#596). It
    /// runs in the folder's own checkout, not on the PBI's branch: the server is one for the machine
    /// on a fixed port, and its venv belongs to a checkout that outlives the PBI's place. A server
    /// that will not start is not a failure here: the score answers <c>unknown</c> and says why.
    /// </summary>
    async Task EnsureModel(LoopFolder folder, CancellationToken ct)
    {
        if (_ensured || harness is null || HarnessScripts.Find(folder.Path, "laya") is null)
        {
            return;
        }

        _ensured = true;
        if (await harness.RunAsync(new ScriptRequest("laya", folder.Path, ["ensure"]), ct) is { ExitCode: not 0 } down)
        {
            LogModelDown(logger, folder.Path, down.Output.Trim());
        }
    }

    async Task<ScriptVerdict> Script(
        string name,
        IScriptPlace place,
        IPbiProvider provider,
        Pbi pbi,
        IReadOnlyList<JournalEntry> lines,
        CancellationToken ct
    )
    {
        // The merge is of the pull request, not of the PBI; the archive finds the change on the
        // branch it runs on, and takes no PBI.
        var pull = name == "merge" ? await provider.GetPullRequestAsync(pbi.Ref, ct) : null;
        if (name == "merge" && pull is null)
        {
            return new(name, VerdictKind.Fail, null, $"{pbi.Named()} has no pull request to merge");
        }

        IReadOnlyList<string> arguments = name switch
        {
            "archive" => [],
            "merge" => [pull!.Id],
            _ => [pbi.Ref.Id],
        };

        // On the PBI's branch, never in the folder's checkout: the claim takes the branch, the gate
        // tests it and the archive commits on it.
        // #597. Announced before it runs, as a run is when it is asked for, and saved at once, so the
        // journal says so while it runs; the Loop screen reads the running script itself from Passes.
        var state = pbi.State!;
        var started = $"running {name}";
        if (!Announced(lines, state, started))
        {
            journal.Write(pbi, state, JournalKind.Started, state, started);
            await journal.SaveAsync(ct);
        }

        ScriptResult result;
        var since = clock.GetUtcNow();
        using (passes.Running(new RunningScript(pbi.Ref.Repository, pbi.Ref.Id, state, name, since)))
        {
            result = await place.RunAsync(name, arguments, ct);
        }

        // #638. Everything it printed, kept whatever it answered: the why of a red gate is one
        // sentence, and the sentence that says which test failed is in here.
        var verdict = ScriptVerdicts.Read(name, result);
        return outputs is null ? verdict : verdict with { Output = await outputs.WriteAsync(pbi.Ref, name, since, result.Output, ct) };
    }

    /// <summary>
    /// Whether the journal already announces this script since the PBI's last transition. A script
    /// that waits runs again every pass and its wait is written once (see <c>LoopEngine.Wait</c>);
    /// its start is written once with it, or a PBI waiting an hour on <c>file-overlap</c> would be
    /// sixty lines again. The scripts that passed before it in the same pass leave their <c>ran</c>
    /// lines between, so those are part of the wait too.
    /// </summary>
    static bool Announced(IReadOnlyList<JournalEntry> lines, string state, string started) =>
        lines
            .Reverse()
            .TakeWhile(line => line.From == state && line.Kind is JournalKind.Waited or JournalKind.Started or JournalKind.Ran)
            .Any(line => line.Kind == JournalKind.Started && line.Why == started);

    [LoggerMessage(EventId = 5054, Level = LogLevel.Warning, Message = "laya ensure did not start the model server in {Folder}: {Output}")]
    static partial void LogModelDown(ILogger logger, string folder, string output);
}
