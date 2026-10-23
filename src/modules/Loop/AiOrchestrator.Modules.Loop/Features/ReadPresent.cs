using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Engine;
using AiOrchestrator.Modules.Loop.Persistence;
using AiOrchestrator.Modules.Loop.Settings;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// The engine now, for the strip at the top of the Loop screen (DESIGN.md#the-screens): how
/// often it looks and when it last did, which runs and scripts it has live, the WIP each folder's loop allows,
/// and its latest decision; and each folder with its switch and whether its loop can be read, which
/// is what the strip's switches show (#564). Read from what the engine wrote; nothing here is deduced.
/// </summary>
sealed class ReadPresent : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/loop/present",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ReadPresent))
            .WithTags("Loop");

    /// <param name="Enabled">Whether the engine runs on its own: at least one folder is on (#562).</param>
    /// <param name="IntervalSeconds">Seconds between two passes, as stored or configured.</param>
    /// <param name="LastPass">The latest pass since the process started, or null.</param>
    /// <param name="NextPassAt">When the next scheduled pass runs, or null when none is scheduled.</param>
    /// <param name="LiveRuns">The loop's runs not ended yet, which is what the WIP counts.</param>
    /// <param name="LiveScripts">The scripts a pass is running now (#597). The WIP does not count them.</param>
    /// <param name="Folders">Each folder the loop walks, with the WIP its loop allows.</param>
    /// <param name="LastDecision">The journal's newest line, or null.</param>
    internal sealed record Response(
        bool Enabled,
        int IntervalSeconds,
        EnginePass? LastPass,
        DateTimeOffset? NextPassAt,
        IReadOnlyList<EngineRun> LiveRuns,
        IReadOnlyList<EngineScript> LiveScripts,
        IReadOnlyList<EngineFolder> Folders,
        ReadJournal.Line? LastDecision
    );

    /// <param name="Failure">Why the pass stopped before the end, or null when it finished.</param>
    internal sealed record EnginePass(DateTimeOffset At, int Folders, int Pbis, IReadOnlyList<string> Skipped, int Written, string? Failure);

    /// <param name="LastOutputAt">When the run last wrote to its terminal.</param>
    /// <param name="Stale">Live and silent for longer than <c>Runs:StaleAfter</c>.</param>
    internal sealed record EngineRun(Guid RunId, string Repository, string Pbi, string State, DateTimeOffset? LastOutputAt, bool Stale);

    /// <param name="Script">Its name, as <c>loop.yaml</c> names it.</param>
    /// <param name="Since">When the engine started it.</param>
    /// <param name="Stale">
    /// Running for longer than a run may stay silent. A script writes to no terminal, so its whole run
    /// is its silence, and a gate past the threshold needs a person as a silent run does.
    /// </param>
    internal sealed record EngineScript(string Repository, string Pbi, string State, string Script, DateTimeOffset Since, bool Stale);

    /// <param name="On">The folder's engine switch as a person left it, or <c>Loop:Enabled</c> (#564).</param>
    /// <param name="Wip">How many runs the folder's loop allows at once, or null for no limit.</param>
    /// <param name="Problem">
    /// Why the loop cannot be read, or null. A folder with a problem cannot be switched on, and when it
    /// is on the passes skip it.
    /// </param>
    internal sealed record EngineFolder(string Path, string Vendor, string Repository, bool On, int? Wip, string? Problem);

    // Nothing to scope to: the engine spans every folder, and this host has one person (AddIdentity).
    [Requires(Access.AnyCaller)]
    internal sealed record Query : IQuery<Response>;

    internal sealed class Handler(
        Passes passes,
        ControlSettings settings,
        IRunRequests runs,
        IFolderDirectory folders,
        LoopDbContext database,
        TimeProvider clock
    ) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken)
        {
            var live = await runs.ListLiveAsync(cancellationToken);
            var scripts = passes.Scripts;
            var staleAfter = scripts.Count == 0 ? TimeSpan.Zero : await runs.StaleAfterAsync(cancellationToken);
            var now = clock.GetUtcNow();
            var control = await settings.ReadAsync(cancellationToken);
            var listed = await folders.ListAsync(cancellationToken);

            var walked = new List<EngineFolder>();
            foreach (var folder in listed)
            {
                var (loop, problem) = await LoopReader.ReadAsync(folder, cancellationToken);
                walked.Add(new EngineFolder(folder.Path, folder.Vendor, folder.Repository, control.IsOn(folder.Repository), loop?.Wip, problem));
            }

            var newest = await database.Journal.AsNoTracking().OrderByDescending(entry => entry.Id).FirstOrDefaultAsync(cancellationToken);

            return new Response(
                control.IntervalSeconds > 0 && listed.Any(folder => control.IsOn(folder.Repository)),
                control.IntervalSeconds,
                passes.Last is { } last
                    ? new EnginePass(last.At, last.Folders, last.Pbis, last.Skipped, last.Written, last.Failure)
                    : null,
                passes.Next,
                [.. live.Select(run => new EngineRun(run.RunId, run.Pbi.Repository, run.Pbi.Id, run.State, run.LastOutputAt, run.Stale))],
                [
                    .. scripts.Select(script => new EngineScript(
                        script.Repository,
                        script.Pbi,
                        script.State,
                        script.Script,
                        script.Since,
                        now - script.Since > staleAfter
                    )),
                ],
                walked,
                newest is null ? null : ReadJournal.Line.Of(newest)
            );
        }
    }
}
