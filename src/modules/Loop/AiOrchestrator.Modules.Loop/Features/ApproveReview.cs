using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Loop.Engine;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// <i>Approve</i> in the Review tab (DESIGN.md, Review; #513): approves the PBI's pull
/// request at the vendor, on its current head, and then releases the hold of <c>merge-review</c> the
/// way the engine does when it sees an approval, so the PBI goes on to <c>retro</c> now rather than
/// on the next pass. Answers the review written and the journal line, or why the loop did not move.
/// </summary>
sealed class ApproveReview : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/review/approve",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReviewVerdicts.ReviewSubmitted>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ApproveReview))
            .WithTags("Loop");

    /// <param name="Repository">The PBI's repository, such as <c>owner/name</c>.</param>
    /// <param name="Id">The vendor's id for the PBI.</param>
    /// <param name="Body">What the approval says beside it, or nothing.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string? Body) : ICommand<ErrorOr<ReviewVerdicts.ReviewSubmitted>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers, PersonDecisions decisions)
        : IAppCommandHandler<Command, ErrorOr<ReviewVerdicts.ReviewSubmitted>>
    {
        public Task<ErrorOr<ReviewVerdicts.ReviewSubmitted>> Handle(Command command, CancellationToken cancellationToken) =>
            ReviewVerdicts.SubmitAsync(
                folders,
                providers,
                decisions,
                command.Repository,
                command.Id,
                ReviewVerdict.Approve,
                command.Body ?? string.Empty,
                null,
                (loop, pbi, last) => Planner.Approved(loop, pbi, last),
                cancellationToken
            );
    }
}
