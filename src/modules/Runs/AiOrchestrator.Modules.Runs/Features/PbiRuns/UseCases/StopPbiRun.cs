using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// <i>Stop run</i> in the PBI's header (DESIGN.md, "The one button", #511): kills a live
/// run's session. A loop run ends as <c>Stopped</c>, and the engine's next pass holds the PBI on it
/// with "Stopped by a person", the same way it holds on any other failed run. Nothing here touches
/// the vendor or the journal: those are the engine's.
/// </summary>
sealed class StopPbiRun : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/runs/{runId:guid}/stop",
                async (Guid runId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(runId), cancellationToken);
                    return result.Match(_ => Results.NoContent(), ApiResults.Problem);
                }
            )
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(StopPbiRun))
            .WithTags("Runs");

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(Guid RunId) : ICommand<ErrorOr<Success>>;

    internal sealed class Handler(RunBook book) : IAppCommandHandler<Command, ErrorOr<Success>>
    {
        public async Task<ErrorOr<Success>> Handle(Command command, CancellationToken cancellationToken) =>
            await book.StopAsync(command.RunId, cancellationToken)
                ? Result.Success
                : Error.Conflict(
                    "Run.NotLive",
                    $"Run {command.RunId} has no live session to stop: it ended, or its place is still being prepared. Read the Journey again."
                );
    }
}
