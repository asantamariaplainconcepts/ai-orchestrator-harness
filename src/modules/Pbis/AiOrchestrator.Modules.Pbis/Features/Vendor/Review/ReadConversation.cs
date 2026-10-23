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
/// The pull request's own conversation, for <c>aio pr view</c> (#651): its title and body, the
/// comments on it as a whole and the reviews with what they said. With <see cref="ReadReview"/>'s
/// threads it is the whole pull request page, the comments anchored to a line among it: the vendor's
/// own CLI left those out, 0 of 2 on PR #457.
/// </summary>
sealed class ReadConversation : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbi/review/conversation",
                async (string repository, string id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository, id), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ConversationRead>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReadConversation))
            .WithTags("Pbis");

    /// <param name="PullRequest">The pull request, or null when the PBI has none yet; everything below is then empty.</param>
    /// <param name="State"><c>open</c>, <c>closed</c> or <c>merged</c>; empty with no pull request.</param>
    internal sealed record ConversationRead(
        ReadPbi.VendorPullRequest? PullRequest,
        string Title,
        string State,
        string? Author,
        string Body,
        IReadOnlyList<ReviewCommentRow> Comments,
        IReadOnlyList<ReviewNote> Reviews
    );

    /// <param name="Verdict"><c>approve</c>, <c>request-changes</c> or <c>comment</c>, or null for anything else the vendor recorded.</param>
    internal sealed record ReviewNote(string Id, string? Verdict, string? Author, DateTimeOffset At, string Body);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, string Id) : IQuery<ErrorOr<ConversationRead>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Id).NotEmpty();
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppQueryHandler<Query, ErrorOr<ConversationRead>>
    {
        public async Task<ErrorOr<ConversationRead>> Handle(Query query, CancellationToken cancellationToken)
        {
            var resolved = await ResolveAsync(folders, providers, query.Repository, query.Id, cancellationToken);
            if (resolved.IsError)
            {
                return resolved.Errors;
            }

            PullRequestReview? review;
            try
            {
                review = await resolved.Value.Provider.ReadPullRequestReviewAsync(resolved.Value.Pbi, cancellationToken);
            }
            catch (PbiProviderException failure)
            {
                return Unread(failure);
            }

            if (review is null)
            {
                return new ConversationRead(null, string.Empty, string.Empty, null, string.Empty, [], []);
            }

            var read = review.Conversation;
            var pull = review.Change.PullRequest;
            return new ConversationRead(
                new ReadPbi.VendorPullRequest(pull.Id, pull.Url.ToString(), pull.IsDraft, pull.IsMerged, pull.IsApproved, pull.IsClosed),
                read.Title,
                read.State,
                read.Author,
                read.Body,
                [.. read.Comments.Select(comment => new ReviewCommentRow(comment.Id, comment.Author, comment.CreatedAt, comment.Body))],
                [.. read.Reviews.Select(review => new ReviewNote(review.Id, Worded(review.Verdict), review.Author, review.At, review.Body))]
            );
        }
    }

    /// <summary>A verdict as <c>aio pr review --verdict</c> spells it.</summary>
    internal static string? Worded(ReviewVerdict? verdict) =>
        verdict switch
        {
            ReviewVerdict.Approve => "approve",
            ReviewVerdict.RequestChanges => "request-changes",
            ReviewVerdict.Comment => "comment",
            _ => null,
        };
}
