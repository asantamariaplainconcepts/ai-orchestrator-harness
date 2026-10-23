using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Engine;
using ErrorOr;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// What <see cref="ApproveReview"/> and <see cref="RequestChanges"/> share (#513): the review is
/// written at the vendor first, and only a review the vendor kept moves the loop, through
/// <see cref="PersonDecisions"/> like a release from the header. A loop that has nothing to do with
/// it is not a failure: the review stands, and the answer says why nothing moved.
/// <para>
/// A verdict the vendor kept as a comment, because the person reviewing wrote the pull request
/// (#544), moves the loop the same way, and the journal line and the answer say it went as a comment.
/// </para>
/// </summary>
static class ReviewVerdicts
{
    /// <param name="Review">The vendor's id for the review written.</param>
    /// <param name="Line">The journal line the loop wrote, or null when it did not move.</param>
    /// <param name="Unmoved">Why the loop did not move, or null when it did.</param>
    /// <param name="Note">What went wrong after the review was written, such as a copy to the issue the vendor did not keep; null when nothing did.</param>
    /// <param name="AsComment">Why the vendor kept the verdict as a comment, in its words; null when it kept the verdict.</param>
    internal sealed record ReviewSubmitted(string Review, ReadJournal.Line? Line, string? Unmoved, string? Note, string? AsComment);

    public static async Task<ErrorOr<ReviewSubmitted>> SubmitAsync(
        IFolderDirectory folders,
        IEnumerable<IPbiProvider> providers,
        PersonDecisions decisions,
        string repository,
        string id,
        ReviewVerdict verdict,
        string body,
        Func<IPbiProvider, PbiRef, CancellationToken, Task<string?>>? afterReview,
        Func<LoopDefinition, Pbi, LastTransition?, Decision> decide,
        CancellationToken ct
    )
    {
        var folder = (await folders.ListAsync(ct)).FirstOrDefault(candidate =>
            string.Equals(candidate.Repository, repository, StringComparison.OrdinalIgnoreCase)
        );
        if (folder is null)
        {
            return Error.NotFound("Pbi.NoFolder", $"No folder is a checkout of {repository}. Add one from the rail.");
        }

        if (providers.FirstOrDefault(provider => provider.Name == folder.Vendor) is not { } provider)
        {
            return Error.NotFound("Pbi.NoAdapter", $"No adapter reads {folder.Vendor}.");
        }

        var pbi = new PbiRef(folder.Repository, id);
        Written<SubmittedReview> written;
        string? afterwards;
        try
        {
            written = await provider.SubmitReviewAsync(pbi, verdict, body, ct);
            if (!written.Matches)
            {
                return Error.Conflict("Review.VendorRefused", written.Mismatch!);
            }

        }
        catch (PbiProviderException failure)
        {
            return Error.Conflict("Pbi.VendorUnread", failure.Message);
        }

        // The review is written by now, so what fails after it is said beside it, not instead of it.
        try
        {
            afterwards = afterReview is null ? null : await afterReview(provider, pbi, ct);
        }
        catch (PbiProviderException failure)
        {
            afterwards = failure.Message;
        }

        var asComment = written.Observed!.AsComment;
        var moved = await decisions.ApplyAsync(
            folder.Repository,
            id,
            asComment is null ? decide : (loop, row, last) => AsComment(decide(loop, row, last), asComment),
            ct
        );
        return new ReviewSubmitted(
            written.Observed.Id,
            moved.IsError ? null : moved.Value,
            moved.IsError ? moved.FirstError.Description : null,
            afterwards,
            asComment
        );
    }

    /// <summary>The decision with the journal's reason saying the verdict went on as a comment, and why.</summary>
    static Decision AsComment(Decision decision, string why) =>
        decision switch
        {
            Release release => release with { Why = $"{release.Why}, written as a comment: {why}" },
            Move move => move with { Why = $"{move.Why}, written as a comment: {why}" },
            _ => decision,
        };
}
