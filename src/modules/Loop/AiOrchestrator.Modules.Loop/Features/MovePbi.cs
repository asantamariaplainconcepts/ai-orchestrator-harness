using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.Modules.Loop.Engine;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// <i>Move to…</i> in the PBI's header (DESIGN.md, "The one button", #511): puts the state
/// a person chose on a PBI that sits where the loop does nothing, such as <c>done</c> or
/// <c>needs-refinement</c>. Written to the vendor and read back by the engine, with its journal
/// line. Answers that line.
/// </summary>
sealed class MovePbi : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/move",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReadJournal.Line>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(MovePbi))
            .WithTags("Loop");

    /// <param name="Repository">The PBI's repository, such as <c>owner/name</c>.</param>
    /// <param name="Id">The vendor's id for the PBI.</param>
    /// <param name="To">A state of the loop, declared or not.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string To) : ICommand<ErrorOr<ReadJournal.Line>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.To).NotEmpty();
        }
    }

    internal sealed class Handler(PersonDecisions decisions) : IAppCommandHandler<Command, ErrorOr<ReadJournal.Line>>
    {
        public Task<ErrorOr<ReadJournal.Line>> Handle(Command command, CancellationToken cancellationToken) =>
            decisions.ApplyAsync(
                command.Repository,
                command.Id,
                (loop, pbi, _) => Planner.MovedByPerson(loop, pbi, command.To),
                cancellationToken
            );
    }
}
