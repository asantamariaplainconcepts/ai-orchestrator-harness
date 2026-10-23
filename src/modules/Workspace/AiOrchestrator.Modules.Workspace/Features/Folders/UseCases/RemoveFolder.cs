using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Workspace.Features.Folders.UseCases;

/// <summary>
/// Forgets a folder (#515, from Settings). The console stops tracking it; <b>nothing on disk is
/// touched</b>: the checkout and its worktrees are exactly as they were, and adding the path again
/// brings the folder back.
/// <para>
/// <b>Runs are untouched, and that is the intended outcome.</b> A run names its folder by path
/// rather than by key, so a PBI's Journey keeps every run this checkout ever had.
/// </para>
/// </summary>
sealed class RemoveFolder : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapDelete(
                "/api/folders/{id:guid}",
                async (Guid id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(id), cancellationToken);
                    return result.Match(_ => Results.NoContent(), ApiResults.Problem);
                }
            )
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(RemoveFolder))
            .WithTags("Workspace");

    /// <summary>
    /// Its own permission, because this changes nothing in the repository.
    /// <para>
    /// <see cref="AddFolder"/> is the pair that cannot say this: there is no folder to scope a permission
    /// to before one exists, so it declares <see cref="Access.AnyCaller"/>. Removal has one.
    /// </para>
    /// </summary>
    [Requires(WorkspacePermissions.ManageFolders)]
    internal sealed record Command(Guid FolderId) : ICommand<ErrorOr<Success>>, IScopedToProject
    {
        public Guid ProjectId => FolderId;
    }

    internal sealed class Handler(WorkspaceDbContext database)
        : IAppCommandHandler<Command, ErrorOr<Success>>
    {
        public async Task<ErrorOr<Success>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            var folder = await database.Folders.FirstOrDefaultAsync(
                entity => entity.Id == command.FolderId,
                cancellationToken
            );

            if (folder is null)
            {
                // The same refusal the second click gets. Removing a folder twice is a person with two
                // tabs open, not an error worth a different sentence.
                return FolderErrors.NotFound(command.FolderId);
            }

            database.Folders.Remove(folder);
            await database.SaveChangesAsync(cancellationToken);

            return Result.Success;
        }
    }
}
