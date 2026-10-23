using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Workspace.Features.Files;

/// <summary>
/// <i>Save</i> in the Files tab (#514): writes the file to disk in the checkout that holds the PBI's
/// branch, the folder's own when it is on the branch, else the PBI's worktree. Nothing is committed;
/// the commit is a person's from a terminal or an agent's in its run (DESIGN.md#the-screens).
/// When no checkout holds the branch, it says so and writes nothing. Answers the tree after the
/// write, so the count of modified files is the one on disk.
/// </summary>
sealed class SavePbiFile : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/files/content",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<PbiFiles>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(SavePbiFile))
            .WithTags("Workspace");

    /// <param name="Pbi">The vendor's id for the PBI, whose branch is <c>change/&lt;id&gt;</c>.</param>
    /// <param name="AsIs">Save to the checkout as it is rather than to the PBI's branch.</param>
    /// <param name="Path">The file, relative to the repository's root.</param>
    /// <param name="Text">The whole file as it is to be written.</param>
    // Saving changes the repository's files, which is what ManageWorktrees guards.
    [Requires(WorkspacePermissions.ManageWorktrees)]
    internal sealed record Command(Guid FolderId, string Pbi, bool AsIs, string Path, string Text)
        : ICommand<ErrorOr<PbiFiles>>,
            IScopedToProject
    {
        public Guid ProjectId => FolderId;
    }

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Pbi).NotEmpty();
            RuleFor(command => command.Path).NotEmpty();
            RuleFor(command => command.Text).NotNull();
        }
    }

    internal sealed class Handler(WorkspaceDbContext database, IBranchFiles files)
        : IAppCommandHandler<Command, ErrorOr<PbiFiles>>
    {
        public async Task<ErrorOr<PbiFiles>> Handle(Command command, CancellationToken cancellationToken)
        {
            var folder = await PbiCheckout.FolderPath(database, command.FolderId, cancellationToken);
            if (folder.IsError)
            {
                return folder.Errors;
            }

            var written = await files.WriteAsync(
                folder.Value,
                PbiCheckout.Branch(command.Pbi, command.AsIs),
                command.Path,
                command.Text,
                cancellationToken
            );

            return written.Refusal is { } refusal
                ? PbiCheckout.Refused(refusal)
                : PbiCheckout.View(written.Tree, folder.Value);
        }
    }
}
