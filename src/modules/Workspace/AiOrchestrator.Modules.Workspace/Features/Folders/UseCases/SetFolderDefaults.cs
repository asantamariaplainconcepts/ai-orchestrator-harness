using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace AiOrchestrator.Modules.Workspace.Features.Folders.UseCases;

/// <summary>
/// The agent and the model a folder's sessions start with, chosen in Settings (#515). The
/// dialog of <i>Explore with agent</i> preselects both (DESIGN.md#the-screens), reading them
/// from <c>GET /api/folders</c>.
/// <para>
/// <b>What the loop runs only where its file is silent.</b> A state of <c>loop.yaml</c> that names
/// its agent runs that one; a state that names none runs the folder's, with its model (#546). The
/// agent is not checked against the providers this host has, for the reason
/// <see cref="SetFolderAgent"/> gives: they are another module's, and the dialog shows a provider it
/// does not know as one it cannot choose.
/// </para>
/// </summary>
sealed class SetFolderDefaults : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPut(
                "/api/folders/{id:guid}/defaults",
                async (Guid id, Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(id, request.Agent, request.Model), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithName(nameof(SetFolderDefaults))
            .WithTags("Workspace");

    /// <param name="Agent">A provider's name, such as <c>claude-code</c>.</param>
    /// <param name="Model">A model of that agent, or null or empty for the agent's own default.</param>
    internal sealed record Request(string Agent, string? Model);

    internal sealed record Response(Guid Id, string DefaultAgent, string? DefaultModel);

    [Requires(WorkspacePermissions.Configure)]
    internal sealed record Command(Guid FolderId, string Agent, string? Model) : ICommand<ErrorOr<Response>>, IScopedToProject
    {
        public Guid ProjectId => FolderId;
    }

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Agent).NotEmpty().MaximumLength(50).WithMessage("Name the agent this folder starts with.");
            RuleFor(command => command.Model)
                .MaximumLength(100)
                .Must(model => model is null || !model.Trim().Any(char.IsWhiteSpace))
                .WithMessage("A model is one name, such as claude-opus-5-5, with no spaces.");
        }
    }

    internal sealed class Handler(WorkspaceDbContext database) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var folder = await database.Folders.FirstOrDefaultAsync(entity => entity.Id == command.FolderId, cancellationToken);
            if (folder is null)
            {
                return FolderErrors.NotFound(command.FolderId);
            }

            folder.ChooseDefaults(command.Agent.Trim(), command.Model?.Trim());
            await database.SaveChangesAsync(cancellationToken);

            return new Response(folder.Id, folder.DefaultAgentId, folder.DefaultModel);
        }
    }
}
