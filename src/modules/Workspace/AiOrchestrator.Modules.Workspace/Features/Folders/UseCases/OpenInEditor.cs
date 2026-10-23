using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.Modules.Workspace.Features.Files;
using AiOrchestrator.Modules.Workspace.Features.Preferences.UseCases;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.Modules.Workspace.Features.Folders.UseCases;

/// <summary>
/// <i>Open in editor</i> (#515): a file of a folder's checkout, or the checkout itself, in the
/// editor Settings names. Settings opens each folder's <c>.aio/loop.yaml</c> with it, because
/// the console reads the loop and never writes it.
/// <para>
/// The file is named relative to the checkout and must stay inside it, so the route opens what a
/// folder holds and not any path a caller can spell.
/// </para>
/// <para>
/// The Files tab (#514) opens through the same route with <c>pbi</c>: the file is then one of the
/// PBI's branch, opened in the checkout that holds the branch, which is often its worktree rather
/// than the folder, and only a path the branch lists is opened.
/// </para>
/// </summary>
sealed class OpenInEditor : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/folders/{id:guid}/open-in-editor",
                async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new Command(id, request.File, request.Pbi, request.AsIs ?? false),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(OpenInEditor))
            .WithTags("Workspace");

    /// <param name="File">A path relative to the checkout, such as <c>.aio/loop.yaml</c>, or null for the checkout.</param>
    /// <param name="Pbi">The PBI whose branch the file is read from, for the Files tab; null for the folder's checkout.</param>
    /// <param name="AsIs">With <paramref name="Pbi"/>, the folder's checkout as it is rather than the PBI's branch.</param>
    internal sealed record Request(string? File, string? Pbi = null, bool? AsIs = null);

    /// <param name="Opened">The absolute path the editor was given.</param>
    /// <param name="Editor">The editor it was given to.</param>
    internal sealed record Response(string Opened, string Editor);

    [Requires(WorkspacePermissions.OpenOnDesktop)]
    internal sealed record Command(Guid FolderId, string? File, string? Pbi = null, bool AsIs = false)
        : ICommand<ErrorOr<Response>>,
            IScopedToProject
    {
        public Guid ProjectId => FolderId;
    }

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() =>
            RuleFor(command => command.File)
                .Must(file => file is null || Inside(file))
                .WithMessage("Name a file inside the folder, relative to it, such as .aio/loop.yaml.");

        static bool Inside(string file) =>
            file.Length is > 0 and <= 1000
            && !Path.IsPathRooted(file)
            && !file.Split('/', '\\').Contains("..", StringComparer.Ordinal);
    }

    internal sealed class Handler(
        WorkspaceDbContext database,
        IDesktopIntegration desktop,
        IConfiguration configuration,
        IBranchFiles files
    ) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var folder = await database.Folders.AsNoTracking().FirstOrDefaultAsync(entity => entity.Id == command.FolderId, cancellationToken);
            if (folder is null)
            {
                return FolderErrors.NotFound(command.FolderId);
            }

            var root = LocalPath.Expand(folder.Path);

            if (command.Pbi is { Length: > 0 } pbi)
            {
                var tree = await files.TreeAsync(root, PbiCheckout.Branch(pbi, command.AsIs), cancellationToken);
                if (tree.Checkout is null)
                {
                    return PbiCheckout.Refused(
                        $"No checkout on this machine is on {tree.Branch}, so there is nothing on disk to open."
                    );
                }

                if (command.File is { } listed && !tree.Files.Contains(listed, StringComparer.Ordinal))
                {
                    return FolderErrors.NoSuchFile(listed);
                }

                root = tree.Checkout;
            }

            var path = command.File is { } file ? Path.Combine(root, file) : root;
            var editor = GetPreferences.Handler.Editor(await GetPreferences.Handler.Read(database, cancellationToken), configuration);

            var opened = await desktop.OpenInEditor(path, editor, cancellationToken);

            return opened.Succeeded
                ? new Response(path, editor)
                : FolderErrors.DesktopUnavailable(
                    opened.Reason switch
                    {
                        DesktopFailure.NotFound => $"{path} is not on disk.",
                        DesktopFailure.LaunchFailed => $"{editor} did not start. Choose another editor in Settings.",
                        _ => "This host cannot reach your desktop.",
                    }
                );
        }
    }
}
