using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Persistence;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// The live sessions, each once: what the tray lists (DESIGN.md#the-screens). A session is a
/// run whose terminal is running in this process now, the loop's and the manual ones alike; one
/// that ended is a recording in its PBI's Journey, not a row here. A session a person opened on a
/// folder has no row and no PBI (#516); it is listed from memory, with its folder.
/// </summary>
sealed class ListSessions : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/sessions",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ListSessions))
            .WithTags("Runs");

    /// <param name="RunId">The run; its terminal is <c>/api/sessions/{runId}/attach</c>.</param>
    /// <param name="Repository">The PBI's repository, or null for a session on a folder.</param>
    /// <param name="Pbi">The PBI, or null for a session on a folder.</param>
    /// <param name="State">The state it belongs to, or null for a session on a folder.</param>
    /// <param name="Manual">A person opened it.</param>
    /// <param name="Provider">The agent CLI, or null for a terminal.</param>
    /// <param name="Command">What the agent was asked, or empty.</param>
    /// <param name="LastOutputAt">When the session last wrote.</param>
    /// <param name="Stale">Silent for longer than the stale threshold (<see cref="StaleThreshold"/>).</param>
    /// <param name="FolderPath">The checkout it works on.</param>
    internal sealed record LiveSession(
        Guid RunId,
        string? Repository,
        string? Pbi,
        string? State,
        bool Manual,
        string? Provider,
        string Command,
        DateTimeOffset StartedAt,
        DateTimeOffset LastOutputAt,
        bool Stale,
        string FolderPath
    );

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query : IQuery<IReadOnlyList<LiveSession>>;

    internal sealed class Handler(LiveRuns live, RunsDbContext database, TimeProvider clock, StaleThreshold threshold)
        : IAppQueryHandler<Query, IReadOnlyList<LiveSession>>
    {
        public async Task<IReadOnlyList<LiveSession>> Handle(Query query, CancellationToken cancellationToken)
        {
            var ids = live.Ids();
            var rows = await database
                .PbiRuns.AsNoTracking()
                .Where(run => ids.Contains(run.Id) && run.EndedAt == null)
                .ToListAsync(cancellationToken);

            var now = clock.GetUtcNow();
            var staleAfter = await threshold.ReadAsync(database, cancellationToken);

            // Ordered here: SQLite cannot order by a DateTimeOffset column.
            return
            [
                .. rows.OrderBy(row => row.StartedAt)
                    .Select(row => (row, current: live.Find(row.Id)))
                    .Where(pair => pair.current?.Session is not null)
                    .Select(pair => new LiveSession(
                        pair.row.Id,
                        pair.row.Repository,
                        pair.row.PbiId,
                        pair.row.State,
                        pair.row.Manual,
                        pair.row.Provider,
                        pair.row.Command,
                        pair.row.StartedAt,
                        pair.current!.LastOutputAt,
                        pair.current.IsStale(now, staleAfter),
                        pair.row.FolderPath
                    )),
                .. live.OnFolders()
                    .Where(entry => entry.Run.Session is not null)
                    .OrderBy(entry => entry.Run.StartedAt)
                    .Select(entry => new LiveSession(
                        entry.Id,
                        null,
                        null,
                        null,
                        true,
                        entry.Run.Folder!.Provider,
                        entry.Run.Folder.Command,
                        entry.Run.StartedAt,
                        entry.Run.LastOutputAt,
                        entry.Run.IsStale(now, staleAfter),
                        entry.Run.Folder.FolderPath
                    )),
            ];
        }
    }
}
