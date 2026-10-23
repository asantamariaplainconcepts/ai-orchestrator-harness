using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// A run's recording, the asciicast v2 file its session wrote, for <c>asciinema-player</c> in the
/// Journey to replay (ARCHITECTURE.md#what-we-did-not-invent). A live run's recording is
/// served as far as it has been written.
/// </summary>
sealed class GetRecording : IUseCase
{
    /// <summary>The media type asciinema registers for the format.</summary>
    public const string MediaType = "application/x-asciicast";

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/runs/{runId:guid}/recording",
                async (Guid runId, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(runId), cancellationToken);
                    return result.Match(path => Results.File(path, MediaType), ApiResults.Problem);
                }
            )
            .Produces(StatusCodes.Status200OK, contentType: MediaType)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(GetRecording))
            .WithTags("Runs");

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(Guid RunId) : IQuery<ErrorOr<string>>;

    internal sealed class Handler(RunsDbContext database) : IAppQueryHandler<Query, ErrorOr<string>>
    {
        public async Task<ErrorOr<string>> Handle(Query query, CancellationToken cancellationToken)
        {
            var path = await database
                .PbiRuns.AsNoTracking()
                .Where(run => run.Id == query.RunId)
                .Select(run => run.RecordingPath)
                .FirstOrDefaultAsync(cancellationToken);

            if (path is null)
            {
                return Error.NotFound("Run.NotFound", $"There is no run {query.RunId}.");
            }

            return File.Exists(path)
                ? path
                : Error.NotFound("Recording.NotFound", $"Run {query.RunId} has no recording: its session never started, or {path} was removed.");
        }
    }
}
