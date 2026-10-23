using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Engine;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// <i>Release hold</i> in the PBI's header (DESIGN.md, "The one button", #511). The engine
/// takes the hold off and moves the PBI to where the hold leads, its <c>next</c>, the same path an
/// approval of the pull request takes; the journal line says a person released it, so the screen
/// can tell the two apart (openspec/specs/person-decisions). A hold that leads nowhere takes the
/// state the person chose in <c>to</c>. Answers the journal line written.
/// <para>
/// A PBI with a run not ended yet is not released (#707): a run a person stopped takes a moment to
/// end, and its end would hold the PBI again.
/// </para>
/// </summary>
sealed class ReleaseHold : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/release",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReadJournal.Line>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReleaseHold))
            .WithTags("Loop");

    /// <param name="Repository">The PBI's repository, such as <c>owner/name</c>.</param>
    /// <param name="Id">The vendor's id for the PBI.</param>
    /// <param name="To">The state to go to, read only when the hold has no <c>next</c>.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string? To) : ICommand<ErrorOr<ReadJournal.Line>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
        }
    }

    internal sealed class Handler(PersonDecisions decisions, IRunRequests runs) : IAppCommandHandler<Command, ErrorOr<ReadJournal.Line>>
    {
        public async Task<ErrorOr<ReadJournal.Line>> Handle(Command command, CancellationToken cancellationToken)
        {
            var running = (await runs.ListLiveAsync(cancellationToken)).Any(run =>
                string.Equals(run.Pbi.Repository, command.Repository, StringComparison.OrdinalIgnoreCase) && run.Pbi.Id == command.Id
            );

            return await decisions.ApplyAsync(
                command.Repository,
                command.Id,
                (loop, pbi, last) => Planner.Released(loop, pbi, last, command.To, running),
                cancellationToken
            );
        }
    }
}
