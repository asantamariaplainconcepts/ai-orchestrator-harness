using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// Runs' settings as Settings shows them: how long a live run may write nothing before it reads as
/// stale (#562), the value a person stored or <c>Runs:StaleAfter</c>, and what the agents may do
/// without asking (#578), <c>allow-list</c> until somebody changes it.
/// </summary>
sealed class ReadRunsSettings : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/runs/settings",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ReadRunsSettings))
            .WithTags("Runs");

    /// <param name="StaleAfterSeconds">Seconds of silence after which a live run reads as stale.</param>
    /// <param name="PermissionMode">What the next run's agent may do without asking: <c>allow-list</c>, <c>auto</c> or <c>yolo</c>.</param>
    internal sealed record Response(int StaleAfterSeconds, string PermissionMode);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query : IQuery<Response>;

    internal sealed class Handler(StaleThreshold threshold, PermissionModeSetting permissionMode) : IAppQueryHandler<Query, Response>
    {
        public async Task<Response> Handle(Query query, CancellationToken cancellationToken) =>
            new(
                (int)(await threshold.ReadAsync(cancellationToken)).TotalSeconds,
                PermissionModeSetting.Spell(await permissionMode.ReadAsync(cancellationToken))
            );
    }
}
