using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Workspace.Features.Folders;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Workspace.Features.Files;

/// <summary>
/// One file of the PBI's branch for the Files tab's editor (#514): from the checkout that holds the
/// branch, so what was saved is what is shown, else from git on the branch. A path the branch does
/// not list is a 404, whatever the reason.
/// </summary>
sealed class ReadPbiFile : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/files/content",
                async (
                    Guid folderId,
                    string pbi,
                    string path,
                    bool? asIs,
                    ISender sender,
                    CancellationToken cancellationToken
                ) =>
                {
                    var result = await sender.Send(new Query(folderId, pbi, asIs ?? false, path), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(ReadPbiFile))
            .WithTags("Workspace");

    /// <param name="Text">Empty when <paramref name="IsBinary"/>, and not to be saved when <paramref name="Truncated"/>.</param>
    /// <param name="Truncated">The file is larger than what is read here, and is not saved from here.</param>
    /// <param name="Bytes">The whole file's size.</param>
    internal sealed record Response(string Path, string Text, bool IsBinary, bool Truncated, long Bytes);

    [Requires(WorkspacePermissions.Read)]
    internal sealed record Query(Guid FolderId, string Pbi, bool AsIs, string Path)
        : IQuery<ErrorOr<Response>>,
            IScopedToProject
    {
        public Guid ProjectId => FolderId;
    }

    internal sealed class Handler(WorkspaceDbContext database, IBranchFiles files)
        : IAppQueryHandler<Query, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Query query, CancellationToken cancellationToken)
        {
            var folder = await PbiCheckout.FolderPath(database, query.FolderId, cancellationToken);
            if (folder.IsError)
            {
                return folder.Errors;
            }

            var text = await files.ReadAsync(
                folder.Value,
                PbiCheckout.Branch(query.Pbi, query.AsIs),
                query.Path,
                cancellationToken
            );

            return text is null
                ? FolderErrors.NoSuchFile(query.Path)
                : new Response(query.Path, text.Text, text.IsBinary, text.Truncated, text.Bytes);
        }
    }
}
