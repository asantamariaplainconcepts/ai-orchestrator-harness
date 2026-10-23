using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Workspace.Features.Folders.UseCases;

/// <summary>
/// The loop states the folder's repository already has at its vendor (#565), for the harness block
/// of the folder page. A route of its own because it asks the vendor, which takes a second, while
/// the rest of the page is read from disk and polled.
/// <para>
/// A state missing here is not a fault: the engine creates it the first time it moves a PBI there.
/// A vendor that cannot answer is a <see cref="FolderStates.Problem"/>, not a failed request, so the
/// page still draws.
/// </para>
/// </summary>
sealed class ReadFolderStates : IUseCase
{
    const string GitHub = "github.com/";

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/folders/{id:guid}/states",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(id), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<FolderStates>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(ReadFolderStates))
            .WithTags("Workspace");

    /// <param name="Vendor">The vendor asked, such as <c>github</c>, or null when the folder names none.</param>
    /// <param name="States">The states it has, ordered by name, or null when it was not asked or did not answer.</param>
    /// <param name="Problem">Why there is no list, in the vendor CLI's own words where it said any.</param>
    internal sealed record FolderStates(string? Vendor, IReadOnlyList<string>? States, string? Problem);

    [Requires(WorkspacePermissions.Read)]
    internal sealed record Query(Guid FolderId) : IQuery<ErrorOr<FolderStates>>, IScopedToProject
    {
        public Guid ProjectId => FolderId;
    }

    internal sealed class Handler(WorkspaceDbContext database, IEnumerable<IPbiProvider> vendors)
        : IAppQueryHandler<Query, ErrorOr<FolderStates>>
    {
        public async Task<ErrorOr<FolderStates>> Handle(Query query, CancellationToken cancellationToken)
        {
            var folder = await database.Folders.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == query.FolderId, cancellationToken);
            if (folder is null)
            {
                return FolderErrors.NotFound(query.FolderId);
            }

            var repository = RepositoryIdentity.Of(folder.RemoteUrl);
            if (repository?.StartsWith(GitHub, StringComparison.Ordinal) != true)
            {
                return new FolderStates(null, null, "Its origin is not on github.com, so there is no vendor to ask.");
            }

            var vendor = vendors.FirstOrDefault(candidate => candidate.Name == "github");
            if (vendor is null)
            {
                return new FolderStates("github", null, "No adapter for github is registered on this host.");
            }

            try
            {
                return new FolderStates(vendor.Name, await vendor.ListStatesAsync(repository[GitHub.Length..], cancellationToken), null);
            }
            catch (PbiProviderException failure)
            {
                return new FolderStates(vendor.Name, null, failure.Message);
            }
        }
    }
}
