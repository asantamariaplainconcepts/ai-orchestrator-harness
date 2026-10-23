using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Folders;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// <i>Terminal</i> and <i>Explore with agent</i> in a folder's <c>…</c> menu on the rail (#516): a
/// session in the checkout as it stands, which leaves nothing behind (<see cref="FolderSessions"/>).
/// <para>
/// The folder is named by its path, the one every read already carries; a path that is not a
/// directory here is refused before anything is spawned.
/// </para>
/// </summary>
sealed class OpenFolderSession : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/folders/sessions",
                async (Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(
                        new Command(request.Path, Blank(request.Provider), Blank(request.Model), Blank(request.Command)),
                        cancellationToken
                    );
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<Response>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(OpenFolderSession))
            .WithTags("Runs");

    /// <param name="Path">The folder's checkout, as <c>GET /api/folders</c> gives it.</param>
    /// <param name="Provider">The agent CLI, such as <c>claude-code</c>; null or empty for a terminal.</param>
    /// <param name="Model">A model of that agent, or null for its default.</param>
    /// <param name="Command">What the agent is asked first, or null to open it waiting.</param>
    internal sealed record Request(string Path, string? Provider, string? Model, string? Command);

    /// <param name="RunId">The session; its terminal is <c>/api/sessions/{runId}/attach</c>.</param>
    internal sealed record Response(Guid RunId);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Path, string? Provider, string? Model, string? Prompt) : ICommand<ErrorOr<Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() => RuleFor(command => command.Path).NotEmpty();
    }

    internal sealed class Handler(FolderSessions sessions, IFolderDirectory folders) : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            var path = LocalPath.Expand(command.Path);
            if (!Directory.Exists(path))
            {
                return Error.NotFound("Session.NoFolder", $"{command.Path} is not a directory on this machine. Check the folder in Settings.");
            }

            // The folder's repository, so `aio issue` in the session reaches its PBIs (#634).
            var repository = (await folders.ListAsync(cancellationToken)).FirstOrDefault(folder => LocalPath.Same(folder.Path, path))?.Repository;
            var opened = await sessions.OpenAsync(path, repository, command.Provider, command.Model, command.Prompt, cancellationToken);
            return opened.IsError ? opened.Errors : new Response(opened.Value);
        }
    }

    static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
