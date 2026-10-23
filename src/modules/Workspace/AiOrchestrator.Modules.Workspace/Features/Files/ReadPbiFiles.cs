using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Workspace.Features.Files;

/// <summary>
/// The Files tab's tree (#514): every file of the repository on the PBI's branch, read with
/// <c>git ls-tree</c> so it answers with no worktree, and which of them are modified in the
/// checkout that holds the branch. A branch the repository no longer has answers with
/// <c>exists: false</c>, and the tab offers the checkout as it is.
/// </summary>
sealed class ReadPbiFiles : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/files",
                async (Guid folderId, string pbi, bool? asIs, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(folderId, pbi, asIs ?? false), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<PbiFiles>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(ReadPbiFiles))
            .WithTags("Workspace");

    /// <param name="Pbi">The vendor's id for the PBI, whose branch is <c>change/&lt;id&gt;</c>.</param>
    /// <param name="AsIs">Read the checkout as it is rather than the PBI's branch.</param>
    [Requires(WorkspacePermissions.Read)]
    internal sealed record Query(Guid FolderId, string Pbi, bool AsIs) : IQuery<ErrorOr<PbiFiles>>, IScopedToProject
    {
        public Guid ProjectId => FolderId;
    }

    internal sealed class Handler(WorkspaceDbContext database, IBranchFiles files)
        : IAppQueryHandler<Query, ErrorOr<PbiFiles>>
    {
        public async Task<ErrorOr<PbiFiles>> Handle(Query query, CancellationToken cancellationToken)
        {
            var folder = await PbiCheckout.FolderPath(database, query.FolderId, cancellationToken);
            if (folder.IsError)
            {
                return folder.Errors;
            }

            var tree = await files.TreeAsync(folder.Value, PbiCheckout.Branch(query.Pbi, query.AsIs), cancellationToken);
            return PbiCheckout.View(tree, folder.Value);
        }
    }
}
