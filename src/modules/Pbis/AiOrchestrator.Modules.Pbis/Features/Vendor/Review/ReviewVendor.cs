using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using ErrorOr;

namespace AiOrchestrator.Modules.Pbis.Features.Vendor.Review;

/// <summary>
/// What the Review tab's use cases share (#513): the PBI named by repository and id, resolved to the
/// folder that is a checkout of that repository and the adapter that reads its vendor, with the same
/// refusals <see cref="ReadPbi"/> gives. And the rows the tab reads, one shape for a read and a write.
/// </summary>
static class ReviewVendor
{
    public static async Task<ErrorOr<(PbiRef Pbi, IPbiProvider Provider)>> ResolveAsync(
        IFolderDirectory folders,
        IEnumerable<IPbiProvider> providers,
        string repository,
        string id,
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

        return (new PbiRef(folder.Repository, id), provider);
    }

    /// <summary>A write the vendor did not keep, in its own sentence, for the person to act on.</summary>
    public static Error Refused(string? mismatch) => Error.Conflict("Review.VendorRefused", mismatch ?? "The vendor did not keep the write.");

    public static Error Unread(PbiProviderException failure) => Error.Conflict("Pbi.VendorUnread", failure.Message);

    /// <summary>A review comment as the tab shows it.</summary>
    /// <param name="Id">The vendor's id, which a reply is addressed to.</param>
    internal sealed record ReviewCommentRow(string Id, string? Author, DateTimeOffset At, string Body)
    {
        public static ReviewCommentRow Of(ReviewComment comment) => new(comment.Id, comment.Author, comment.CreatedAt, comment.Body);
    }

    /// <summary>A thread anchored to a line, as the tab shows it.</summary>
    /// <param name="Id">The vendor's id, which resolving names.</param>
    /// <param name="Line">The line in the file's new version, or null when the vendor no longer places it.</param>
    internal sealed record ReviewThreadRow(
        string Id,
        string Path,
        int? Line,
        bool IsResolved,
        bool IsOutdated,
        IReadOnlyList<ReviewCommentRow> Comments
    )
    {
        public static ReviewThreadRow Of(ReviewThread thread) =>
            new(thread.Id, thread.Path, thread.Line, thread.IsResolved, thread.IsOutdated, [.. thread.Comments.Select(ReviewCommentRow.Of)]);
    }
}
