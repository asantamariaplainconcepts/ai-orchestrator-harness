using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Loop.Journal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AiOrchestrator.Modules.Loop.Engine;

sealed partial class LoopEngine
{
    /// <summary>
    /// #636. After a transition that closed <paramref name="child"/>, closes the PBI it is part of when
    /// every one of that PBI's children is closed too: a comment that says what it took, the close, and
    /// a <c>moved</c> line to <paramref name="done"/> in the parent's own journal. A child still open,
    /// held or not, leaves the parent alone: the last child to close closes it.
    /// <para>
    /// It never reopens a parent and never moves a child. The parent's label is left as it is, because
    /// an epic carries no loop state. A vendor that cannot answer is logged and nothing more: the child
    /// is done whatever its parent says, and a wait written on it would be read as the child's.
    /// </para>
    /// </summary>
    async Task CloseParent(IPbiProvider provider, Pbi child, string done, CancellationToken ct)
    {
        try
        {
            if (await provider.ReadThreadAsync(child.Ref, ct) is not { Parent: { Closed: false } parent })
            {
                return;
            }

            // The child counts as closed whatever the list says: the close was read back a moment ago,
            // and a list the vendor builds from another index may not have caught up.
            var children = await provider.ListChildrenAsync(parent.Ref, ct);
            if (children.Count == 0 || children.Any(other => !other.Closed && other.Ref != child.Ref))
            {
                return;
            }

            if (await provider.ReadThreadAsync(parent.Ref, ct) is not { } epic)
            {
                return;
            }

            var pulls = new List<PullRequestInfo?>();
            foreach (var each in children)
            {
                pulls.Add(await provider.GetPullRequestAsync(each.Ref, ct));
            }

            var followUps = new List<Pbi>();
            foreach (var named in NamedIn(epic.Body).Where(id => id != parent.Ref.Id && children.All(each => each.Ref.Id != id)))
            {
                if ((await provider.ReadThreadAsync(new PbiRef(parent.Ref.Repository, named), ct))?.AsPbi is { Closed: false } open)
                {
                    followUps.Add(open);
                }
            }

            var commented = await provider.CommentAsync(parent.Ref, Closing(epic, children, pulls, followUps, clock.GetUtcNow()), [], ct);
            var closed = await provider.MoveAsync(parent.Ref, new PbiMove(Close: true), ct);
            var from = epic.Pbi.State ?? "no state";
            if (!closed.Matches)
            {
                _journal.Write(epic.Pbi, from, JournalKind.Waited, null, $"every sub-issue is closed, and closing it did not hold: {closed.Mismatch}");
                return;
            }

            var why = $"all {children.Count} sub-issues are closed, the last one {child.Named()}";
            _journal.Write(epic.Pbi, from, JournalKind.Moved, done, commented.Matches ? why : $"{why}; the closing comment did not hold: {commented.Mismatch}");
        }
        catch (PbiProviderException failure)
        {
            LogParentUnread(logger, child.Named(), failure.Message);
        }
    }

    /// <summary>
    /// The comment that closes an epic: how many sub-issues and pull requests, when it was opened and
    /// when it closed, each sub-issue with its pull request, and the PBIs its body names that are still
    /// open, which the epic leaves as follow-ups.
    /// </summary>
    internal static string Closing(
        PbiThread epic,
        IReadOnlyList<Pbi> children,
        IReadOnlyList<PullRequestInfo?> pulls,
        IReadOnlyList<Pbi> followUps,
        DateTimeOffset now
    )
    {
        var text = new StringBuilder();
        var merged = pulls.Count(pull => pull is not null);
        text.Append(
            CultureInfo.InvariantCulture,
            $"Closed by the loop: all {children.Count} sub-issues are closed, with {merged} pull request{(merged == 1 ? "" : "s")}. "
                + $"Opened {epic.CreatedAt:yyyy-MM-dd}, closed {now:yyyy-MM-dd}.\n\n"
        );

        for (var index = 0; index < children.Count; index++)
        {
            var pull = pulls[index] is { } found ? $"pull request #{found.Id}" : "no pull request";
            text.Append(CultureInfo.InvariantCulture, $"- #{children[index].Ref.Id} {children[index].Title}: {pull}\n");
        }

        if (followUps.Count > 0)
        {
            text.Append(CultureInfo.InvariantCulture, $"\nStill open, named in the body: {string.Join(", ", followUps.Select(open => $"#{open.Ref.Id}"))}.\n");
        }

        return text.ToString();
    }

    /// <summary>
    /// #708. Whether a sub-issue waits for another its epic's graph puts first: an arrow
    /// <c>i&lt;a&gt; --&gt; i&lt;b&gt;</c> in the epic's mermaid block, with <c>b</c> this PBI, makes it wait
    /// while <c>a</c> is open. On aio-dogfood#29, #32 used the component of #31 without touching its
    /// files, so <c>file-overlap</c> would have let it start first. An arrow out of the epic itself
    /// makes nobody wait, and neither does a graph that cannot be read: that is said, in
    /// <see cref="EpicOrderAnswer.Unread"/>, rather than holding every sub-issue on a typo.
    /// </summary>
    async Task<EpicOrderAnswer> EpicOrder(IPbiProvider provider, Pbi pbi, CancellationToken ct)
    {
        if (await provider.ReadThreadAsync(pbi.Ref, ct) is not { Parent: { } parent } || await provider.ReadThreadAsync(parent.Ref, ct) is not { } epic)
        {
            return new(null, null);
        }

        var graph = ReadGraph(epic.Body);
        if (graph.Problem is { } problem)
        {
            return new(null, $"the graph of #{parent.Ref.Id} was not read, so nothing waits on it: {problem}");
        }

        foreach (var first in graph.Arrows.Where(arrow => arrow.To == $"i{pbi.Ref.Id}" && arrow.From.StartsWith('i')).Select(arrow => arrow.From[1..]).Distinct(StringComparer.Ordinal))
        {
            if ((await provider.ReadThreadAsync(new PbiRef(pbi.Ref.Repository, first), ct))?.AsPbi is { Closed: false })
            {
                return new($"#{first} goes first (the epic's graph)", null);
            }
        }

        return new(null, null);
    }

    /// <summary>Whether <paramref name="pbi"/>'s journal already says <paramref name="why"/>, so a graph that cannot be read is said once.</summary>
    Task<bool> SaidAsync(Pbi pbi, string why, CancellationToken ct) =>
        database.Journal.AnyAsync(entry => entry.Repository == pbi.Ref.Repository && entry.Pbi == pbi.Ref.Id && entry.Why.Contains(why), ct);

    /// <summary>
    /// The arrows of the first mermaid block in an epic's body, as <c>.aio/templates/epic.md</c>
    /// writes it: a flowchart whose nodes are <c>e&lt;n&gt;</c> for the epic and <c>i&lt;n&gt;</c> for each
    /// sub-issue, labelled or not. A body with no block has no arrows; a block that is not closed, not a
    /// flowchart, or has an arrow between anything else is a problem, named. Only <c>--&gt;</c> is an
    /// arrow: the template gives no other kind a meaning.
    /// </summary>
    internal static EpicGraph ReadGraph(string body)
    {
        var lines = body.ReplaceLineEndings("\n").Split('\n');
        var open = Array.FindIndex(lines, line => MermaidFence().IsMatch(line));
        if (open < 0)
        {
            return new([], null);
        }

        var fence = lines[open].Trim()[..3];
        var close = Array.FindIndex(lines, open + 1, line => line.Trim() == fence);
        if (close < 0)
        {
            return new([], "its mermaid block is not closed");
        }

        var block = lines[(open + 1)..close].Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith("%%", StringComparison.Ordinal)).ToList();
        if (block.Count == 0 || !Flowchart().IsMatch(block[0]))
        {
            return new([], "its mermaid block is not a flowchart");
        }

        var arrows = new List<(string From, string To)>();
        foreach (var line in block.Skip(1).Where(line => line.Contains("-->", StringComparison.Ordinal)))
        {
            var nodes = line.TrimEnd(';').Split("-->").Select(part => GraphNode().Match(part)).ToList();
            if (nodes.Any(node => !node.Success))
            {
                return new([], $"\"{line}\" is not an arrow between e<n> and i<n> nodes");
            }

            arrows.AddRange(nodes.Zip(nodes.Skip(1), (from, to) => (from.Groups[1].Value, to.Groups[1].Value)));
        }

        return new(arrows, null);
    }

    [GeneratedRegex(@"^\s*(```|~~~)\s*mermaid\b")]
    private static partial Regex MermaidFence();

    [GeneratedRegex(@"^(flowchart|graph)\b")]
    private static partial Regex Flowchart();

    /// <summary>One end of an arrow: an edge label <c>|…|</c> before it, then <c>e12</c> or <c>i12</c> with or without its text in brackets.</summary>
    [GeneratedRegex(@"^\s*(?:\|[^|]*\|)?\s*([ei]\d+)\s*(?:[\[({].*[\])}])?\s*$")]
    private static partial Regex GraphNode();

    /// <summary>The issue numbers a body names as <c>#123</c>, in order and once each; a URL's fragment is not one.</summary>
    static IEnumerable<string> NamedIn(string body) => References().Matches(body).Select(match => match.Groups[1].Value).Distinct(StringComparer.Ordinal);

    [GeneratedRegex(@"(?<![\w/&])#(\d+)\b")]
    private static partial Regex References();

    [LoggerMessage(EventId = 5055, Level = LogLevel.Warning, Message = "The loop could not read the parent of {Pbi} to close it: {Why}")]
    static partial void LogParentUnread(ILogger logger, string pbi, string why);
}

/// <summary>What an epic's graph says about one sub-issue: why it waits, or null; and, when the graph could not be read, the sentence that says so.</summary>
sealed record EpicOrderAnswer(string? Waits, string? Unread);

/// <summary>The arrows of an epic's graph, from and to a node id such as <c>i31</c>, or why it could not be read.</summary>
sealed record EpicGraph(IReadOnlyList<(string From, string To)> Arrows, string? Problem);
