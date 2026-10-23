using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Workspace.Domain;
using AiOrchestrator.Modules.Workspace.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.Modules.Workspace.Features.Preferences.UseCases;

/// <summary>
/// Changes the editor <i>Open in editor</i> starts (#515), or records that the requirements screen
/// was seen with every required CLI present (#680). An absent field means "leave it alone", so a
/// preference added later is not reset by a screen that does not know it.
/// </summary>
sealed class SavePreferences : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPut(
                "/api/preferences",
                async (Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(request.Editor, request.RequirementsSeen), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<Response>()
            .ProducesValidationProblem()
            .WithName(nameof(SavePreferences))
            .WithTags("Workspace");

    internal sealed record Request(string? Editor, bool? RequirementsSeen);

    internal sealed record Response(string Editor, bool RequirementsSeen);

    // No project to scope to — a preference is about the machine, which is the same case
    // AddFolder makes for itself and for the same reason.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string? Editor, bool? RequirementsSeen = null) : ICommand<ErrorOr<Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() =>
            RuleFor(command => command.Editor)
                .Must(editor => editor is null || EditorName.IsValid(editor))
                .WithMessage(
                    "An editor is a command name like `code` or `rider` — letters, digits, "
                        + "dot, dash and underscore. Anything longer belongs in Desktop:Editor."
                );
    }

    internal sealed class Handler(WorkspaceDbContext database, IConfiguration configuration)
        : IAppCommandHandler<Command, ErrorOr<Response>>
    {
        public async Task<ErrorOr<Response>> Handle(
            Command command,
            CancellationToken cancellationToken
        )
        {
            if (command.Editor is not null)
            {
                await Upsert(PreferenceNames.Editor, command.Editor.Trim(), cancellationToken);
            }

            if (command.RequirementsSeen is { } seen)
            {
                await Upsert(PreferenceNames.RequirementsSeen, seen.ToString(), cancellationToken);
            }

            await database.SaveChangesAsync(cancellationToken);

            // Read back through the same path the GET uses, so the answer a screen stores is the
            // answer it would next be told — including the configuration fallback it did not send.
            var stored = await GetPreferences.Handler.Read(database, cancellationToken);
            var answer = GetPreferences.Handler.Answer(stored, configuration);

            return new Response(answer.Editor, answer.RequirementsSeen);
        }

        async Task Upsert(string name, string value, CancellationToken cancellationToken)
        {
            var existing = await database.Preferences.FirstOrDefaultAsync(
                row => row.Name == name,
                cancellationToken
            );

            if (existing is null)
            {
                database.Preferences.Add(Preference.Create(name, value));
                return;
            }

            existing.Set(value);
        }
    }
}
