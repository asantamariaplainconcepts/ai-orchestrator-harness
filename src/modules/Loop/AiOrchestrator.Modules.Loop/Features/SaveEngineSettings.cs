using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Loop.Engine;
using AiOrchestrator.Modules.Loop.Persistence;
using AiOrchestrator.Modules.Loop.Settings;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// Changes the seconds between two scheduled passes, from Settings (#562). The schedule wakes and
/// counts the new interval from the last pass, so it takes effect on the next pass without a restart.
/// </summary>
sealed class SaveEngineSettings : IUseCase
{
    /// <summary>Under ten seconds a pass would read every folder's PBIs at the vendor too often.</summary>
    internal const int Fewest = 10;

    internal const int Most = 86_400;

    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPut(
                "/api/loop/settings",
                async (Request request, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Command(request.IntervalSeconds), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReadEngineSettings.Response>()
            .ProducesValidationProblem()
            .WithName(nameof(SaveEngineSettings))
            .WithTags("Loop");

    internal sealed record Request(int IntervalSeconds);

    // Nothing to scope to: the engine spans every folder, and this host has one person (AddIdentity).
    [Requires(Access.AnyCaller)]
    internal sealed record Command(int IntervalSeconds) : ICommand<ErrorOr<ReadEngineSettings.Response>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator() =>
            RuleFor(command => command.IntervalSeconds)
                .InclusiveBetween(Fewest, Most)
                .WithMessage($"The interval is between {Fewest} seconds and a day ({Most} seconds).");
    }

    internal sealed class Handler(ControlSettings settings, LoopDbContext database, IFolderDirectory folders, Passes passes)
        : IAppCommandHandler<Command, ErrorOr<ReadEngineSettings.Response>>
    {
        public async Task<ErrorOr<ReadEngineSettings.Response>> Handle(Command command, CancellationToken cancellationToken)
        {
            await settings.SetIntervalAsync(command.IntervalSeconds, cancellationToken);
            await database.SaveChangesAsync(cancellationToken);
            passes.Wake();

            return await ReadEngineSettings.Handler.Read(settings, folders, cancellationToken);
        }
    }
}
