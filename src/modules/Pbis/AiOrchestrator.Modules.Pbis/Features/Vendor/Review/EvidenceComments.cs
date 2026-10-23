using System.Globalization;
using System.Text.RegularExpressions;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Pbis.Features.Vendor.Review;

/// <summary>
/// The evidence a PBI's comments carry (#513, #598): the images of the newest comment that opens with
/// <c>&lt;!-- aio:evidence &lt;head sha&gt; --&gt;</c>, as <c>.aio/steps/evidence</c> posts it.
/// An image is a capture when its alt text says <c>&lt;screen&gt; &lt;width&gt;x&lt;height&gt; &lt;theme&gt;</c>,
/// which the script writes off each capture's sidecar or each route it shot; any other image in the comment
/// is not one. The screen is any text without a space, because a route such as <c>/?day=2026-10-15</c> is
/// one: a narrower class read none of aio-dogfood#43's eight captures (#719).
/// An older comment is never read in place of the newest: the newest is the one the review is about.
/// </summary>
static partial class EvidenceComments
{
    const string Marker = "<!-- aio:evidence";

    /// <summary>One capture: its alt text, the vendor's address of its image, and what it shows.</summary>
    internal sealed record Capture(string Name, string Url, string Screen, int Width, int Height, string Theme);

    /// <param name="CommentId">The comment that carries it, or null when no comment does.</param>
    /// <param name="HeadSha">The commit the captures were taken of, or null when the marker names none.</param>
    internal sealed record Batch(string? CommentId, string? Author, DateTimeOffset? At, string? HeadSha, IReadOnlyList<Capture> Captures);

    public static Batch Newest(IReadOnlyList<PbiComment> comments)
    {
        if (comments.LastOrDefault(candidate => candidate.Body.Contains(Marker, StringComparison.Ordinal)) is not { } comment)
        {
            return new(null, null, null, null, []);
        }

        var sha = HeadSha().Match(comment.Body);
        var captures = Image()
            .Matches(comment.Body)
            .Select(image => (Url: image.Groups["url"].Value, Alt: Alt().Match(image.Groups["alt"].Value.Trim())))
            .Where(image => image.Alt.Success)
            .Select(image => new Capture(
                image.Alt.Value,
                image.Url,
                image.Alt.Groups["screen"].Value,
                int.Parse(image.Alt.Groups["width"].Value, CultureInfo.InvariantCulture),
                int.Parse(image.Alt.Groups["height"].Value, CultureInfo.InvariantCulture),
                image.Alt.Groups["theme"].Value
            ))
            .ToList();
        return new(comment.Id, comment.Author, comment.CreatedAt, sha.Success ? sha.Groups["sha"].Value : null, captures);
    }

    /// <summary>
    /// Every image address the comments carry, which is every address the attachment route may read
    /// with the machine's credential. Only the vendor's own asset shapes, whole.
    /// </summary>
    public static HashSet<string> Assets(IEnumerable<PbiComment> comments) =>
        [.. comments.SelectMany(comment => Asset().Matches(comment.Body).Select(match => match.Value))];

    [GeneratedRegex(@"<!-- aio:evidence (?<sha>[0-9a-f]{40}|[0-9a-f]{64}) -->")]
    private static partial Regex HeadSha();

    [GeneratedRegex(@"^(?<screen>\S+) (?<width>[1-9]\d{0,5})x(?<height>[1-9]\d{0,5}) (?<theme>light|dark)$")]
    private static partial Regex Alt();

    /// <summary>A markdown image whose address is one of the vendor's asset shapes.</summary>
    [GeneratedRegex(@"!\[(?<alt>[^\]]*)\]\((?<url>https://github\.com/(?:user-attachments/assets|[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/assets)/[A-Za-z0-9/_.-]+)\)")]
    private static partial Regex Image();

    /// <summary>The vendor's two asset shapes, whole: <c>user-attachments/assets/…</c> and the older per-repository <c>…/assets/…</c>.</summary>
    [GeneratedRegex(@"https://github\.com/(?:user-attachments/assets|[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+/assets)/[A-Za-z0-9/_.-]+")]
    private static partial Regex Asset();
}
