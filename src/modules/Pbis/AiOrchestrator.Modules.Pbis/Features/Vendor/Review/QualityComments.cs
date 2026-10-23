using System.Text.RegularExpressions;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Pbis.Features.Vendor.Review;

/// <summary>
/// What the change's quality measured (#746): the table of the newest comment that opens with
/// <c>&lt;!-- aio:quality &lt;head sha&gt; --&gt;</c>, as <c>.aio/steps/quality</c> posts it. A row is a
/// line of four cells, check, measured, line and result, below the header; any other line of the
/// comment is not one. An older comment is never read in place of the newest, as with the evidence.
/// </summary>
static partial class QualityComments
{
    const string Marker = "<!-- aio:quality";

    /// <param name="Result"><c>ok</c>, <c>over</c> or <c>not measured</c>, as the script wrote it.</param>
    /// <param name="Measured">The measure, or why it was not measured.</param>
    internal sealed record Row(string Check, string Measured, string Line, string Result);

    /// <param name="HeadSha">The commit it measured, or null when no comment, or its marker, names one.</param>
    internal sealed record Measure(string? Author, DateTimeOffset? At, string? HeadSha, IReadOnlyList<Row> Rows);

    public static Measure Newest(IReadOnlyList<PbiComment> comments)
    {
        if (comments.LastOrDefault(candidate => candidate.Body.Contains(Marker, StringComparison.Ordinal)) is not { } comment)
        {
            return new(null, null, null, []);
        }

        var sha = HeadSha().Match(comment.Body);
        var rows = comment.Body
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith('|') && line.EndsWith('|'))
            .Select(line => line[1..^1].Split('|', StringSplitOptions.TrimEntries))
            .Where(cells => cells is [_, _, _, _] && cells[0] != "Check" && !cells[0].StartsWith("---", StringComparison.Ordinal))
            .Select(cells => new Row(cells[0], cells[1], cells[2], cells[3]))
            .ToList();
        return new(comment.Author, comment.CreatedAt, sha.Success ? sha.Groups["sha"].Value : null, rows);
    }

    [GeneratedRegex(@"<!-- aio:quality (?<sha>[0-9a-f]{40}|[0-9a-f]{64}) -->")]
    private static partial Regex HeadSha();
}
