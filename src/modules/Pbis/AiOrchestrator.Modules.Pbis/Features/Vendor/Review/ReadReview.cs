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
/// What the Review tab reads (DESIGN.md, Review; #513), from the vendor now: the
/// pull request with its base and head, its files with their patches, the threads anchored to their
/// lines, the checks on its head, the newest quality measure (#746) and the newest evidence published
/// on the issue, in that order.
/// <para>
/// <b>The checks, the quality and the evidence can fail alone.</b> Each says its own problem and the diff is
/// still shown, because a reviewer with the diff and no checks can still read; a diff that cannot be
/// read is the whole answer failing.
/// </para>
/// </summary>
sealed class ReadReview : IUseCase
{
    public static void AddRoutes(IEndpointRouteBuilder endpoints) =>
        endpoints
            .MapGet(
                "/api/pbi/review",
                async (string repository, string id, ISender sender, CancellationToken cancellationToken) =>
                {
                    var result = await sender.Send(new Query(repository, id), cancellationToken);
                    return result.Match(Results.Ok, ApiResults.Problem);
                }
            )
            .Produces<ReviewRead>()
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName(nameof(ReadReview))
            .WithTags("Pbis");

    /// <param name="PullRequest">The pull request, or null when the PBI has none yet; everything below is then empty.</param>
    internal sealed record ReviewRead(
        ReadPbi.VendorPullRequest? PullRequest,
        ReviewChange? Change,
        IReadOnlyList<ReviewThreadRow> Threads,
        ReviewChecks Checks,
        ReviewQuality Quality,
        ReviewEvidence Evidence
    );

    /// <param name="ChangedFiles">How many files the vendor says it touches; more than <paramref name="Files"/> when it stopped listing.</param>
    internal sealed record ReviewChange(
        string BaseRef,
        string BaseSha,
        string HeadRef,
        string HeadSha,
        int Commits,
        int Additions,
        int Deletions,
        int ChangedFiles,
        IReadOnlyList<ReviewFile> Files
    );

    /// <param name="Patch">Its hunks, or null when the vendor sends none: a binary file, or one too large.</param>
    internal sealed record ReviewFile(string Path, string? PreviousPath, string Status, int Additions, int Deletions, string? Patch);

    /// <param name="Problem">Why the checks could not be read, or null.</param>
    internal sealed record ReviewChecks(IReadOnlyList<ReviewCheck> Rows, string? Problem);

    /// <param name="State"><c>passing</c>, <c>failing</c>, <c>pending</c> or <c>neutral</c>.</param>
    internal sealed record ReviewCheck(string Name, string State, string? Url);

    /// <param name="HeadSha">The commit it measured, or null when no comment names one.</param>
    /// <param name="Stale">It measured a commit that is not the pull request's head.</param>
    /// <param name="Rows">One per check, as <c>.aio/steps/quality</c> wrote them; none without a comment.</param>
    /// <param name="Problem">Why the comments could not be read; null otherwise.</param>
    internal sealed record ReviewQuality(
        string? HeadSha,
        bool Stale,
        string? Author,
        DateTimeOffset? At,
        IReadOnlyList<QualityRow> Rows,
        string? Problem
    );

    /// <param name="Measured">The measure, or why it was not measured.</param>
    /// <param name="Result"><c>ok</c>, <c>over</c> or <c>not measured</c>.</param>
    internal sealed record QualityRow(string Check, string Measured, string Line, string Result);

    /// <param name="HeadSha">The commit the captures were taken of, or null when no comment names one.</param>
    /// <param name="Problem">Why the comments could not be read; null otherwise.</param>
    internal sealed record ReviewEvidence(
        string? HeadSha,
        string? Author,
        DateTimeOffset? At,
        IReadOnlyList<EvidenceCapture> Captures,
        string? Problem
    );

    /// <param name="Url">The vendor's address of its image, read through <c>GET /api/pbi/attachment</c>.</param>
    internal sealed record EvidenceCapture(string Name, string Url, string Screen, int Width, int Height, string Theme);

    // Nothing to scope to: this host has one person (AddIdentity), as the loop's own routes say.
    [Requires(Access.AnyCaller)]
    internal sealed record Query(string Repository, string Id) : IQuery<ErrorOr<ReviewRead>>;

    internal sealed class Validator : AbstractValidator<Query>
    {
        public Validator()
        {
            RuleFor(query => query.Repository).NotEmpty();
            RuleFor(query => query.Id).NotEmpty();
        }
    }

    internal sealed class Handler(IFolderDirectory folders, IEnumerable<IPbiProvider> providers) : IAppQueryHandler<Query, ErrorOr<ReviewRead>>
    {
        public async Task<ErrorOr<ReviewRead>> Handle(Query query, CancellationToken cancellationToken)
        {
            var resolved = await ResolveAsync(folders, providers, query.Repository, query.Id, cancellationToken);
            if (resolved.IsError)
            {
                return resolved.Errors;
            }

            var (pbi, provider) = resolved.Value;

            // Two questions to the vendor, asked at once (#621): the pull request with its change,
            // threads and checks, and the issue whose comments carry the quality and the evidence.
            var reviewRead = provider.ReadPullRequestReviewAsync(pbi, cancellationToken);
            var commentsRead = Comments(provider, pbi, cancellationToken);

            PullRequestReview? review;
            try
            {
                review = await reviewRead;
            }
            catch (PbiProviderException failure)
            {
                return Unread(failure);
            }

            var (comments, problem) = await commentsRead;
            var evidence = Evidence(comments, problem);
            if (review is null)
            {
                return new ReviewRead(null, null, [], new ReviewChecks([], null), Quality(comments, problem, null), evidence);
            }

            var (change, threads) = (review.Change, review.Change.Threads);
            var pull = change.PullRequest;
            return new ReviewRead(
                new ReadPbi.VendorPullRequest(pull.Id, pull.Url.ToString(), pull.IsDraft, pull.IsMerged, pull.IsApproved, pull.IsClosed),
                new ReviewChange(
                    change.BaseRef,
                    change.BaseSha,
                    change.HeadRef,
                    change.HeadSha,
                    change.Commits,
                    change.Additions,
                    change.Deletions,
                    change.ChangedFiles,
                    [.. change.Files.Select(file => new ReviewFile(file.Path, file.PreviousPath, file.Status, file.Additions, file.Deletions, file.Patch))]
                ),
                [.. threads.Select(ReviewThreadRow.Of)],
                new ReviewChecks(
                    [.. review.Checks.Select(check => new ReviewCheck(check.Name, check.State.ToString().ToLowerInvariant(), check.Url?.ToString()))],
                    review.ChecksUnread
                ),
                Quality(comments, problem, change.HeadSha),
                evidence
            );
        }

        /// <summary>The issue's comments, or why they could not be read.</summary>
        static async Task<(IReadOnlyList<PbiComment> Comments, string? Problem)> Comments(IPbiProvider provider, PbiRef pbi, CancellationToken ct)
        {
            try
            {
                return ((await provider.ReadThreadAsync(pbi, ct))?.Comments ?? [], null);
            }
            catch (PbiProviderException failure)
            {
                return ([], failure.Message);
            }
        }

        static ReviewEvidence Evidence(IReadOnlyList<PbiComment> comments, string? problem)
        {
            var batch = EvidenceComments.Newest(comments);
            return new ReviewEvidence(
                batch.HeadSha,
                batch.Author,
                batch.At,
                [.. batch.Captures.Select(capture => new EvidenceCapture(capture.Name, capture.Url, capture.Screen, capture.Width, capture.Height, capture.Theme))],
                problem
            );
        }

        /// <param name="head">The pull request's head, or null without one: nothing is then stale.</param>
        static ReviewQuality Quality(IReadOnlyList<PbiComment> comments, string? problem, string? head)
        {
            var measure = QualityComments.Newest(comments);
            return new ReviewQuality(
                measure.HeadSha,
                measure.HeadSha is not null && head is not null && measure.HeadSha != head,
                measure.Author,
                measure.At,
                [.. measure.Rows.Select(row => new QualityRow(row.Check, row.Measured, row.Line, row.Result))],
                problem
            );
        }
    }
}
