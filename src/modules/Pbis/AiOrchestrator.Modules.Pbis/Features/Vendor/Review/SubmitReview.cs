using AiOrchestrator.BuildingBlocks.Api;
using AiOrchestrator.BuildingBlocks.CQS;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Identity;
using AiOrchestrator.BuildingBlocks.Modules;
using AiOrchestrator.BuildingBlocks.Ports;
using ErrorOr;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using static AiOrchestrator.Modules.Pbis.Features.Vendor.Review.ReviewVendor;

namespace AiOrchestrator.Modules.Pbis.Features.Vendor.Review;

/// <summary>
/// A review of the PBI's pull request on its current head, for <c>aio pr review</c> (#651): what
/// <c>/aio:review-diff</c> posts its findings as. Only the review: the loop does not move, because
/// moving is the engine's. The Review tab's <i>Approve</i> and <i>Request changes</i> are the Loop
/// module's, which release or set the hold after the same write.
/// <para>
/// An approval or a request for changes from the pull request's author is kept as a comment, and
/// the answer says why in <c>AsComment</c> (#544): the loop opens its pull requests as the person
/// until it has an account of its own (#550), so on this host the reviewer is usually the author.
/// </para>
/// </summary>
sealed class SubmitReview : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapPost(
                "/api/pbi/review/submit",
                async (Command command, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(command, cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReviewWritten>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(SubmitReview))
            .WithTags("Pbis");

    /// <param name="Verdict"><c>comment</c>, <c>approve</c> or <c>request-changes</c>.</param>
    /// <param name="Body">What the review says; only an approval may say nothing.</param>
    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Command(string Repository, string Id, string Verdict, string? Body) : ICommand<ErrorOr<ReviewWritten>>;

    /// <param name="Verdict">What the vendor kept, spelt as the command's: <c>comment</c> when it kept a comment instead.</param>
    /// <param name="AsComment">Why the vendor kept a comment and not the verdict asked for, in its words, or null.</param>
    internal sealed record ReviewWritten(string Id, string? Verdict, string? Author, string Body, string? AsComment);

    static readonly Dictionary<string, ReviewVerdict> Verdicts = new(StringComparer.Ordinal)
    {
        ["comment"] = ReviewVerdict.Comment,
        ["approve"] = ReviewVerdict.Approve,
        ["request-changes"] = ReviewVerdict.RequestChanges,
    };

    internal sealed class Validator : AbstractValidator<Command>
    {
        public Validator()
        {
            RuleFor(command => command.Repository).NotEmpty();
            RuleFor(command => command.Id).NotEmpty();
            RuleFor(command => command.Verdict)
                .Must(Verdicts.ContainsKey)
                .WithMessage(command => $"The verdict is comment, approve or request-changes, not '{command.Verdict}'.");
            RuleFor(command => command.Body)
                .NotEmpty()
                .When(command => command.Verdict != "approve")
                .WithMessage("A review that is not an approval says why, and the body is empty.");
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppCommandHandler<Command, ErrorOr<ReviewWritten>>
    {
        public async Task<ErrorOr<ReviewWritten>> Handle(Command command, CancellationToken cancellationToken)
        {
            var resolved = await ResolveAsync(folders, providers, command.Repository, command.Id, cancellationToken);
            if (resolved.IsError)
            {
                return resolved.Errors;
            }

            try
            {
                var written = await resolved.Value.Provider.SubmitReviewAsync(
                    resolved.Value.Pbi,
                    Verdicts[command.Verdict],
                    command.Body ?? string.Empty,
                    cancellationToken
                );
                return written is { Matches: true, Observed: { } review }
                    ? new ReviewWritten(review.Id, ReadConversation.Worded(review.Verdict), review.Author, review.Body, review.AsComment)
                    : Refused(written.Mismatch);
            }
            catch (PbiProviderException failure)
            {
                return Unread(failure);
            }
        }
    }
}
