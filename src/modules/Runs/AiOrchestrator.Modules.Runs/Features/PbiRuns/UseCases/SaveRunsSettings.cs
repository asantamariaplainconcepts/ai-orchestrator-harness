using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Runs.Persistence;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns.UseCases;

/// <summary>
/// Changes Runs' settings from Settings: the stale threshold (#562), read every time a run's liveness
/// is, and the permission mode (#578), read every time a run or a session by hand starts. Either takes
/// effect on the next read, without a restart. A field left out keeps what is stored.
/// </summary>
sealed class SaveRunsSettings : IUseCase
{
    /// <summary>An agent thinking hard can be quiet for a minute and is not stuck.</summary>
    internal const int Fewest = 60;

    internal const int Most = 86_400;

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPut(
                "/api/runs/settings",
                async (Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(request.StaleAfterSeconds, request.PermissionMode), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReadRunsSettings.Response>()
            .ProducesValidationProblem()
            .WithName(nameof(SaveRunsSettings))
            .WithTags("Runs");

    /// <param name="StaleAfterSeconds">The new threshold, or null to keep the stored one.</param>
    /// <param name="PermissionMode"><c>allow-list</c>, <c>auto</c> or <c>yolo</c>, or null to keep the stored one.</param>
    internal sealed record Request(int? StaleAfterSeconds = null, string? PermissionMode = null);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(int? StaleAfterSeconds, string? PermissionMode) : ICommand<ErrorOr<ReadRunsSettings.Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.StaleAfterSeconds)
                .InclusiveBetween(Fewest, Most)
                .When(command => command.StaleAfterSeconds is not null)
                .WithMessage($"A run reads as stale after between a minute ({Fewest} seconds) and a day ({Most} seconds) of silence.");

            RuleFor(command => command.PermissionMode)
                .Must(mode => PermissionModeSetting.Parse(mode) is not null)
                .When(command => command.PermissionMode is not null)
                .WithMessage($"The permission mode is one of {string.Join(", ", PermissionModeSetting.Modes)}.");
        }
    }

    internal sealed class Handler(StaleThreshold threshold, RunsDbContext database)
        : IAppCommandHandler<Command, ErrorOr<ReadRunsSettings.Response>>
    {
        public async Task<ErrorOr<ReadRunsSettings.Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            if (command.StaleAfterSeconds is { } seconds)
            {
                await threshold.SetAsync(database, seconds, cancellationToken);
            }

            if (PermissionModeSetting.Parse(command.PermissionMode) is { } mode)
            {
                await PermissionModeSetting.SetAsync(database, mode, cancellationToken);
            }

            return new ReadRunsSettings.Response(
                (int)(await threshold.ReadAsync(database, cancellationToken)).TotalSeconds,
                PermissionModeSetting.Spell(await PermissionModeSetting.ReadAsync(database, cancellationToken))
            );
        }
    }
}
