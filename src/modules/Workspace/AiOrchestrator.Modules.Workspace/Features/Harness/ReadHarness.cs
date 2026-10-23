using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Workspace.Features.Harness;

/// <summary>
/// The harness package this build carries (#563): its version and the path of every file and link
/// it would write. A folder whose <c>.aio/version</c> says something else was installed from
/// another build.
/// </summary>
sealed class ReadHarness : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/harness",
                async (ISender sender, CancellationToken cancellationToken) =>
                    TypedResults.Ok(await sender.Send(new Query(), cancellationToken))
            )
            .WithName(nameof(ReadHarness))
            .WithTags("Workspace");

    /// <param name="Target">Where a link points, from its directory; null for a file.</param>
    internal sealed record Entry(string Path, bool Executable, string? Target);

    internal sealed record Package(string Version, IReadOnlyList<Entry> Files);

    // Nothing to scope to: the package is the build's, not a folder's.
    [Requires(Access.AnyCaller)]
    internal sealed record Query : IQuery<Package>;

    internal sealed class Handler : IAppQueryHandler<Query, Package>
    {
        public Task<Package> Handle(Query query, CancellationToken cancellationToken) =>
            Task.FromResult(
                new Package(
                    HarnessPackage.Version,
                    [.. HarnessPackage.Files.Select(file => new Entry(file.Path, file.Executable, file.Target))]
                )
            );
    }
}
