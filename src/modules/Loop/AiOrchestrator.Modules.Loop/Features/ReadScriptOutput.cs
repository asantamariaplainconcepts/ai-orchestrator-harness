using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Loop.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// What a loop script printed, by the journal line that ran it (#638): the file the engine kept
/// beside the PBI's data, served as text so the Journey's link opens it in the browser. The line is
/// the key rather than the path, so the route reads nothing the engine did not write.
/// </summary>
sealed class ReadScriptOutput : IUseCase
{
    public const string MediaType = "text/plain; charset=utf-8";

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/loop/journal/{id:long}/output",
                async (long id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(id), cancellationToken);
                    return result.Match(path => Results.File(path, MediaType), ApiResults.Problem);
                }
            )
            .Produces(StatusCodes.Status200OK, contentType: MediaType)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(ReadScriptOutput))
            .WithTags("Loop");

    // Nothing to scope to: the journal spans every folder, and this host has one person (AddIdentity).
    [Requires(Access.AnyCaller)]
    internal sealed record Query(long Id) : IQuery<ErrorOr<string>>;

    internal sealed class Handler(LoopDbContext database) : IAppQueryHandler<Query, ErrorOr<string>>
    {
        public async Task<ErrorOr<string>> Handle(Query query, CancellationToken cancellationToken)
        {
            var line = await database
                .Journal.AsNoTracking()
                .Where(entry => entry.Id == query.Id)
                .Select(entry => new { entry.Kind, entry.Output })
                .FirstOrDefaultAsync(cancellationToken);

            if (line is null)
            {
                return Error.NotFound("Journal.NotFound", $"There is no journal line {query.Id}.");
            }

            if (line.Output is not { } path)
            {
                return Error.NotFound("Output.NotFound", $"Journal line {query.Id} is a {line.Kind.ToString().ToLowerInvariant()} line with no script output.");
            }

            return File.Exists(path)
                ? path
                : Error.NotFound("Output.NotFound", $"The output of journal line {query.Id} is gone: {path} was removed.");
        }
    }
}
