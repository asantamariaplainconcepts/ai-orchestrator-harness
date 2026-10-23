using System.Text.RegularExpressions;

namespace AiOrchestrator.Modules.Pbis.Features.Inbox;

/// <summary>
/// The vendor attachments one piece of issue text carries — a body or a single comment. #418.
/// <para>
/// <b>One reading for the two routes that need one.</b> <c>GetPbiEvidence</c> lists what it finds
/// here, and <c>GetPbiAttachmentBytes</c> refuses to fetch anything this does not find in the same
/// origin's current text. Two parsers would let the list name a picture the byte route refuses, or
/// — the dangerous direction — let the byte route clear an address the list never showed.
/// </para>
/// <para>
/// <b>The same shapes the screen used to match on its own</b> (<c>evidence.ts</c>): markdown images
/// and links, and <c>&lt;img&gt;</c> tags, named by their alt text, first occurrence of an address
/// winning. An empty alt is an unnamed attachment, not a missing one.
/// </para>
/// </summary>
static partial class IssueAttachments
{
    /// <summary>One attachment: the vendor's address and the name its markup gave it, possibly empty.</summary>
    internal sealed record Found(string Url, string Name);

    /// <summary>Every supported attachment in <paramref name="text"/>, in the order the text has them.</summary>
    public static IReadOnlyList<Found> In(string text)
    {
        var candidates = new List<(int At, Found Found)>();

        foreach (Match link in MarkdownLink().Matches(text))
        {
            candidates.Add((link.Index, new Found(link.Groups["url"].Value, link.Groups["name"].Value.Trim())));
        }

        foreach (Match image in HtmlImage().Matches(text))
        {
            var source = Attribute(image.Value, "src");
            if (source is not null)
            {
                candidates.Add((image.Index, new Found(source, (Attribute(image.Value, "alt") ?? string.Empty).Trim())));
            }
        }

        return
        [
            .. candidates
                .Where(candidate => IsAsset(candidate.Found.Url))
                .OrderBy(candidate => candidate.At)
                .Select(candidate => candidate.Found)
                .DistinctBy(found => found.Url, StringComparer.Ordinal),
        ];
    }

    /// <summary>
    /// Every address this text clears for a credentialed fetch: what <see cref="In"/> lists, and
    /// every bare asset address besides.
    /// <para>
    /// <b>The bare addresses are the compatibility half.</b> The byte route cleared any asset
    /// address the body named before attachments were parsed for names, and a body caller that
    /// relied on that keeps it. Nothing wider than the two vendor shapes is ever cleared.
    /// </para>
    /// </summary>
    public static HashSet<string> Clearable(string text) =>
        [
            .. Asset()
                .Matches(text)
                .Select(match => match.Value)
                // Markdown puts an address inside `(…)`, and a trailing `)` or punctuation is the
                // sentence's, not the address's.
                .Select(address => address.TrimEnd(')', '.', ',', '"', '\'')),
            .. In(text).Select(found => found.Url),
        ];

    /// <summary>
    /// Whether an address is one of the two vendor asset shapes, whole: the current
    /// <c>user-attachments/assets/…</c> and the older per-repository <c>…/assets/…</c>. Whole
    /// rather than by prefix, so markup cannot smuggle a quote or a bracket into a cleared address.
    /// </summary>
    static bool IsAsset(string url) => WholeAsset().IsMatch(url);

    static string? Attribute(string tag, string name)
    {
        var match = Regex.Match(tag, $@"\b{name}\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase, TimeSpan.FromSeconds(1));
        return match.Success ? match.Groups[1].Value : null;
    }

    const string AssetPattern =
        @"https://github\.com/(?:user-attachments/assets/[^\s)""'<>]+|[^\s/)""'<>]+/[^\s/)""'<>]+/assets/[^\s)""'<>]+)";

    [GeneratedRegex(AssetPattern, RegexOptions.IgnoreCase)]
    private static partial Regex Asset();

    [GeneratedRegex("^" + AssetPattern + "$", RegexOptions.IgnoreCase)]
    private static partial Regex WholeAsset();

    /// <summary><c>![alt](url)</c> and <c>[text](url)</c> alike — the link form is what a non-image gets.</summary>
    [GeneratedRegex(@"!?\[(?<name>[^\]]*)\]\(\s*(?<url>[^)\s]+)[^)]*\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"<img\b[^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlImage();
}
