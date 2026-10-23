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
/// <i>Request changes</i> on the Proposal tab (DESIGN.md#the-screens, #512): the review is
/// written on the PBI's pull request beside the comments the person anchored to the change's lines,
/// and the PBI goes to its state's <c>back</c> through <see cref="Planner.ProposalChangesRequested"/>,
/// its hold taken off on the way. The coder's next <c>/aio:propose</c> reads the review and its
/// threads. The same path as the Review tab's (<see cref="ReviewVerdicts"/>), with a review that only
/// comments: GitHub refuses a request for changes from the account that opened the pull request,
/// which is the one the loop acts as.
/// </summary>
sealed class RequestProposalChanges : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/proposal/request-changes",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReviewVerdicts.ReviewSubmitted>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(RequestProposalChanges))
            .WithTags("Loop");

    /// <param name="Repository">The PBI's repository, such as <c>owner/name</c>.</param>
    /// <param name="Id">The vendor's id for the PBI.</param>
    /// <param name="Body">What the review says, the summary of the changes asked for.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string Body) : ICommand<ErrorOr<ReviewVerdicts.ReviewSubmitted>>;

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.Body).NotEmpty().WithMessage("Say what has to change.");
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
                ReviewVerdict.Comment,
                command.Body,
                null,
                (loop, pbi, _) => Planner.ProposalChangesRequested(loop, pbi),
                cancellationToken
            );
    }
}
