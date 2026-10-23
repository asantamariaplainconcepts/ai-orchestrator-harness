using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Journal;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>
/// #696. The PBI's journey as a comment on its issue, so an instance whose database has nothing about
/// the PBI shows what the instance that ran the engine did. One row per line that moved or stopped
/// the PBI (<c>moved</c>, <c>held</c>, <c>released</c>) and per finished run, oldest first; a wait,
/// a start and a script's own line are not rows.
/// <para>
/// The table is read back by <c>src/frontend/features/pbi/journey.ts</c>, so its columns are a
/// contract: <c>When | State | Kind | To | Agent or script | Result | Why | Took | Cost | Recording</c>.
/// A run's kind is <c>run</c>, or <c>manual</c> for one a person opened (#697), its when is when it
/// started, and a failure's result is <c>failed: &lt;cause&gt;</c>. Its recording is the path of its
/// copy on the repository's <c>aio/evidence</c> branch, which the Journey of another instance plays
/// from the vendor (#697); a comment written before that column has nine.
/// </para>
/// </summary>
static partial class JourneyComment
{
    /// <summary>The marker the Issue tab and the Journey recognise the comment by, as <c>aio:&lt;kind&gt;</c>.</summary>
    public const string Marker = "<!-- aio:journey -->";

    /// <summary>Whether a comment is the journey the engine keeps.</summary>
    public static bool Is(string body) => body.StartsWith(Marker, StringComparison.Ordinal);

    /// <summary>Whether a line moved or stopped the PBI, which is what writes the comment again.</summary>
    public static bool Transition(JournalEntry line) => line.Kind is JournalKind.Moved or JournalKind.Held or JournalKind.Released;

    /// <param name="lines">Every journal line of the PBI, oldest first.</param>
    /// <param name="runs">The runs of the PBI that have ended, the loop's and a person's.</param>
    public static string Render(IReadOnlyList<JournalEntry> lines, IReadOnlyList<RunStatus> runs)
    {
        var text = new StringBuilder();
        text.Append(Marker).Append('\n');
        text.Append("### Journey\n\n");
        text.Append("What the loop did with this PBI: one row per transition and per finished run, oldest first. ");
        text.Append("The engine edits this comment at every transition, so an edit by hand is lost at the next one.\n\n");
        text.Append("| When | State | Kind | To | Agent or script | Result | Why | Took | Cost | Recording |\n");
        text.Append("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |\n");

        foreach (var row in Rows(lines, runs))
        {
            text.Append("| ").AppendJoin(" | ", row.Select(Cell)).Append(" |\n");
        }

        return text.ToString();
    }

    static IEnumerable<string?[]> Rows(IReadOnlyList<JournalEntry> lines, IReadOnlyList<RunStatus> runs)
    {
        var rows = new List<(DateTimeOffset At, string?[] Cells)>();

        // The scripts a state ran since its last transition are what decided it. A ran line is
        // "<script>: <why>" when a script wrote it; the engine's own note, such as "the run failed
        // (Stopped): …" (#707), names no script.
        var ran = new List<string>();
        foreach (var line in lines)
        {
            if (line.Kind == JournalKind.Ran)
            {
                if (ScriptName().Match(line.Why) is { Success: true } script)
                {
                    ran.Add(script.Groups[1].Value);
                }

                continue;
            }

            if (!Transition(line))
            {
                continue;
            }

            rows.Add((line.At, [When(line.At), line.From, Kind(line.Kind), line.To, string.Join(", ", ran.Distinct(StringComparer.Ordinal)), null, line.Why, null, null, null]));
            ran.Clear();
        }

        foreach (var run in runs)
        {
            if (run.End is not { } end)
            {
                continue;
            }

            var started = run.StartedAt ?? end.EndedAt ?? default;
            var by = string.Join(" · ", new[] { run.Command, run.Provider, run.Model }.Where(part => !string.IsNullOrWhiteSpace(part)));
            var result = end.Failure is { } failure ? $"failed: {failure}" : end.Outcome;
            var took = end.EndedAt is { } ended && run.StartedAt is { } from ? Took(ended - from) : null;
            var cost = end.CostUsd is { } dollars ? $"${dollars.ToString("0.00", CultureInfo.InvariantCulture)}" : null;
            var state = run.State.Length > 0 ? run.State : "no state";
            rows.Add((started, [When(started), state, run.Manual ? "manual" : "run", null, by, result, end.Reason, took, cost, run.Recording]));
        }

        // Stable, so a run and the line it led to keep the order they were written in.
        return rows.OrderBy(row => row.At).Select(row => row.Cells);
    }

    static string Kind(JournalKind kind) => kind.ToString().ToLowerInvariant();

    static string When(DateTimeOffset at) => at.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    /// <summary>"11m 3s", "1h 2m", "40s": what the Journey reads back into a duration.</summary>
    static string Took(TimeSpan took)
    {
        var seconds = (long)Math.Max(0, Math.Round(took.TotalSeconds));
        var parts = new List<string>();
        if (seconds >= 3600)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{seconds / 3600}h"));
        }

        if (seconds % 3600 >= 60)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{seconds % 3600 / 60}m"));
        }

        if (seconds % 60 > 0 || parts.Count == 0)
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"{seconds % 60}s"));
        }

        return string.Join(' ', parts);
    }

    /// <summary>
    /// One line, with the pipe escaped: a cell is what the table reads, and a newline would end the
    /// row. A terminal's colour codes are left out, because an escape comes back from GitHub as the two
    /// characters <c>^[</c> and the comment would never read back as written (#710).
    /// </summary>
    static string Cell(string? value) =>
        string.IsNullOrEmpty(value) ? "" : ScriptLine.Plain(value).Replace("|", "\\|", StringComparison.Ordinal).Trim();

    /// <summary>The name of a script of the harness, then a colon.</summary>
    [GeneratedRegex("^([A-Za-z0-9][A-Za-z0-9._-]*):")]
    private static partial Regex ScriptName();
}
