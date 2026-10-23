using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Engine;
using AiOrchestrator.Modules.Loop.Journal;
using static AiOrchestrator.Modules.Loop.Features.ReadMetrics;

namespace AiOrchestrator.Modules.Loop.Features;

/// <summary>
/// #734. The Metrics screen's numbers, from what the engine wrote, what Runs kept and what the vendor
/// says. Pure, like <see cref="Planner"/>: no port, no clock, no store, so each rule is a test over a
/// journal, some runs and a history written by hand.
/// <para>
/// <b>Whose commit it is.</b> The engine, its runs and the person commit with the same git identity on
/// one machine: on aio-dogfood#47 the claim, the agent's commit and a person's would all read "Andoni
/// Santamaria". Since #741 every commit the loop makes carries a mark (<see cref="CommitMark"/>), so a
/// commit on <c>change/&lt;n&gt;</c> without one is a person's, whichever instance worked the PBI. The
/// commits from before the loop marked any are neither: the earliest marked commit the vendor shows
/// is where the marks begin, and the notes say so.
/// </para>
/// </summary>
static class MetricsReading
{
    /// <summary>Where a PBI ends: the target README.md#the-loop names for a merged change.</summary>
    public const string Done = "done";

    /// <summary>Everything the screen reads, for one repository.</summary>
    /// <param name="Journal">The repository's journal, in the order it was written.</param>
    /// <param name="Runs">Every run of the repository, the loop's and a person's.</param>
    /// <param name="History">What the vendor says, or null when it could not be read.</param>
    /// <param name="Unread">Why the vendor could not be read, or null.</param>
    /// <param name="From">The start of the period, or null for all of it.</param>
    /// <param name="Epic">The epic whose sub-issues alone are counted, or null for every PBI.</param>
    public sealed record Input(
        string Repository,
        LoopDefinition Loop,
        IReadOnlyList<JournalEntry> Journal,
        IReadOnlyList<RunStatus> Runs,
        VendorHistory? History,
        string? Unread,
        DateTimeOffset Now,
        DateTimeOffset? From,
        string? Epic
    );

    public static MetricsReport Read(Input input)
    {
        var lineage = (input.History?.Pbis ?? []).ToDictionary(pbi => pbi.Ref.Id, StringComparer.Ordinal);
        var epics = lineage.Values.Select(pbi => pbi.Parent).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var inScope = (string pbi) => input.Epic is null || lineage.GetValueOrDefault(pbi)?.Parent == input.Epic;
        var inPeriod = (DateTimeOffset at) => input.From is not { } from || at >= from;

        var lines = input.Journal.Where(line => line.Kind != JournalKind.Switched && line.Pbi.Length > 0).ToList();
        var byPbi = lines.GroupBy(line => line.Pbi, StringComparer.Ordinal).ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var loopRuns = input.Runs.Where(run => !run.Manual).ToList();
        var commits = Commits(input);

        var agents = Agents(input, [.. loopRuns.Where(run => inScope(run.Pbi.Id) && inPeriod(Started(run)))]);
        var holds = Holds(input, byPbi, lineage, commits, inScope, inPeriod);
        var people = new MetricsPeople(
            Reasons(holds),
            Decisions(input.Loop, byPbi, inScope, inPeriod),
            Merged(input, inScope, inPeriod, commits, person: true),
            Merged(input, inScope, inPeriod, commits, person: false)
        );
        var flow = Flow(input, byPbi, lineage, epics, inScope, inPeriod);

        var named = lines
            .Select(line => line.Pbi)
            .Concat(epics)
            .Concat(input.Runs.Select(run => run.Pbi.Id))
            .Concat((input.History?.PullRequests ?? []).Select(pull => PbiOf(pull.Branch)).OfType<string>())
            .Distinct(StringComparer.Ordinal);
        return new MetricsReport(
            input.Repository,
            input.From,
            input.Now,
            input.Epic,
            [.. epics.Order(Numerically).Select(id => new MetricsPbi(id, lineage.GetValueOrDefault(id)?.Title ?? string.Empty))],
            [.. named.Order(Numerically).Select(id => new MetricsPbi(id, lineage.GetValueOrDefault(id)?.Title ?? string.Empty))],
            agents,
            people,
            flow,
            Notes(input, holds, loopRuns, commits, inScope, inPeriod)
        );
    }

    // ---- 1. Agents, by step of the loop.

    static MetricsAgents Agents(Input input, List<RunStatus> runs)
    {
        var order = input.Loop.States.Select(state => state.Name).ToList();
        var steps = runs
            .GroupBy(run => run.State, StringComparer.Ordinal)
            .OrderBy(group => order.IndexOf(group.Key) is >= 0 and var at ? at : int.MaxValue)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                var ended = group.Where(run => run.End is not null).ToList();
                var priced = ended.Where(run => run.End!.CostUsd is not null).ToList();
                var counted = ended.Where(run => Tokens(run) is not null).ToList();
                var durations = ended.Select(Seconds).OfType<double>().ToList();
                var cost = priced.Sum(run => run.End!.CostUsd!.Value);
                var tokens = counted.Sum(run => Tokens(run)!.Value);
                return new MetricsStep(
                    group.Key,
                    group.Select(run => Verb(run.Command)).OfType<string>().FirstOrDefault(),
                    [.. group.Select(run => run.RunId)],
                    [.. group.Where(run => run.End?.Outcome is not null).Select(run => run.RunId)],
                    Percentile(durations, 50),
                    Percentile(durations, 90),
                    cost,
                    tokens,
                    priced.Count == 0 ? null : decimal.Round(cost / priced.Count, 4),
                    counted.Count == 0 ? null : tokens / counted.Count
                );
            });

        return new MetricsAgents(
            [.. steps],
            [
                .. runs.OrderByDescending(Started).Select(run => new MetricsRun(
                    run.RunId,
                    run.State,
                    run.Pbi.Id,
                    Started(run),
                    Seconds(run),
                    Result(run),
                    run.End?.Outcome is not null,
                    run.End?.CostUsd,
                    Tokens(run)
                )),
            ]
        );
    }

    /// <summary>The command's own name, such as <c>/aio:implement</c>, without the PBI it was about.</summary>
    static string? Verb(string? command) => command?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

    static string Result(RunStatus run) =>
        run.End switch
        {
            null => "live",
            { Outcome: { } outcome } => outcome,
            { Failure: { } failure } => JsonNamingPolicy.CamelCase.ConvertName(failure.ToString()),
            _ => "ended",
        };

    static DateTimeOffset Started(RunStatus run) => run.StartedAt ?? default;

    static double? Seconds(RunStatus run) => run.End?.EndedAt is { } ended && run.StartedAt is { } started ? (ended - started).TotalSeconds : null;

    static long? Tokens(RunStatus run) =>
        run.End is { InputTokens: null, OutputTokens: null } or null ? null : (run.End.InputTokens ?? 0) + (run.End.OutputTokens ?? 0);

    // ---- 2. People: what they did at each hold.

    enum Ending
    {
        Unchanged,
        AfterCommit,
        Returned,
        Closed,
        Open,
    }

    sealed record HoldSpan(string Pbi, string Reason, DateTimeOffset At, Ending Ending, double? Seconds);

    static List<HoldSpan> Holds(
        Input input,
        Dictionary<string, List<JournalEntry>> byPbi,
        Dictionary<string, PbiLineage> lineage,
        CommitReading commits,
        Func<string, bool> inScope,
        Func<DateTimeOffset, bool> inPeriod
    )
    {
        var people = commits.People;
        var holds = new List<HoldSpan>();
        foreach (var (pbi, lines) in byPbi.Where(each => inScope(each.Key)))
        {
            for (var index = 0; index < lines.Count; index++)
            {
                var held = lines[index];
                if (held.Kind != JournalKind.Held || !inPeriod(held.At))
                {
                    continue;
                }

                var reason = Reason(input.Loop, held.Why);
                var end = lines.Skip(index + 1).FirstOrDefault(line => line.Kind is JournalKind.Released or JournalKind.Moved);
                if (end is null)
                {
                    var closed = lineage.GetValueOrDefault(pbi)?.ClosedAt is { } at && at > held.At ? at : (DateTimeOffset?)null;
                    holds.Add(new HoldSpan(pbi, reason, held.At, closed is null ? Ending.Open : Ending.Closed, closed is { } when ? (when - held.At).TotalSeconds : null));
                    continue;
                }

                var ending = SentBack(input.Loop, end)
                    ? Ending.Returned
                    : people.GetValueOrDefault(pbi)?.Any(at => at > held.At && at <= end.At) == true
                        ? Ending.AfterCommit
                        : Ending.Unchanged;
                holds.Add(new HoldSpan(pbi, reason, held.At, ending, (end.At - held.At).TotalSeconds));
            }
        }

        return holds;
    }

    static List<MetricsHoldReason> Reasons(List<HoldSpan> holds) =>
    [
        .. holds
            .GroupBy(hold => hold.Reason, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group =>
            {
                List<string> Of(Ending ending) => [.. group.Where(hold => hold.Ending == ending).Select(hold => hold.Pbi)];
                var timed = group.Where(hold => hold.Ending != Ending.Open).Select(hold => hold.Seconds).OfType<double>().ToList();
                var unchanged = Of(Ending.Unchanged);
                var ended = group.Count(hold => hold.Ending != Ending.Open);
                return new MetricsHoldReason(
                    group.Key,
                    unchanged,
                    Of(Ending.AfterCommit),
                    Of(Ending.Returned),
                    Of(Ending.Closed),
                    Of(Ending.Open),
                    Percentile(timed, 50),
                    timed.Count == 0 ? null : timed.Max(),
                    ended > 0 && unchanged.Count == ended
                );
            }),
    ];

    /// <summary>
    /// What a hold is about, grouped as a person would: the reason the loop declares for it, a run's
    /// failure by its cause, a script by its name, or <c>max_loops</c> used up.
    /// </summary>
    public static string Reason(LoopDefinition loop, string why)
    {
        var declared = loop.States.SelectMany(state => state.Transitions).Select(to => to.Hold).OfType<string>();
        if (declared.FirstOrDefault(hold => why.StartsWith(hold + ":", StringComparison.Ordinal) || why == hold) is { } reason)
        {
            return reason;
        }

        if (why.StartsWith("the run failed (", StringComparison.Ordinal) && why.IndexOf(')', StringComparison.Ordinal) is > 0 and var close)
        {
            return why[..(close + 1)];
        }

        if (why.Contains("(max_loops)", StringComparison.Ordinal))
        {
            return "max_loops used up";
        }

        var colon = why.IndexOf(": ", StringComparison.Ordinal);
        return colon > 0 ? why[..colon] : why;
    }

    /// <summary>A release or move that sends the PBI back: a request for changes, or a state above the one it left.</summary>
    static bool SentBack(LoopDefinition loop, JournalEntry line) =>
        line.Why is Planner.ChangesRequestedByPerson or Planner.ProposalChangesRequestedByPerson
        || (line.To is { } to && to != line.From && Planner.Back(loop, line.From, to));

    /// <summary>A move or release out of a state forward: to a state below it, or to one the loop does not declare.</summary>
    static bool Forward(LoopDefinition loop, JournalEntry line) =>
        line.Kind is JournalKind.Moved or JournalKind.Released && line.To is { } to && to != line.From && !Planner.Back(loop, line.From, to);

    /// <summary>
    /// The states where a person decides: those with a <c>back</c> and a hold in their <c>on</c>, the
    /// plan's and the merge's in this repository. Each pass through one ends approved as it was, after
    /// changes were asked for, or without a person, when the score let it through.
    /// </summary>
    static List<MetricsDecisionPoint> Decisions(
        LoopDefinition loop,
        Dictionary<string, List<JournalEntry>> byPbi,
        Func<string, bool> inScope,
        Func<DateTimeOffset, bool> inPeriod
    )
    {
        var points = new List<MetricsDecisionPoint>();
        foreach (var state in loop.States.Where(state => state.Back is not null && state.Transitions.Any(to => to.Hold is not null)))
        {
            List<string> asIs = [], afterChanges = [], withoutPerson = [];
            foreach (var (pbi, lines) in byPbi.Where(each => inScope(each.Key)).OrderBy(each => each.Key, Numerically))
            {
                var (held, asked) = (false, false);
                foreach (var line in lines.Where(line => line.From == state.Name))
                {
                    if (line.Kind == JournalKind.Held)
                    {
                        held = true;
                    }
                    else if (SentBack(loop, line))
                    {
                        asked = true;
                    }
                    else if (Forward(loop, line))
                    {
                        if (inPeriod(line.At))
                        {
                            (asked ? afterChanges : held ? asIs : withoutPerson).Add(pbi);
                        }
                        (held, asked) = (false, false);
                    }
                }
            }

            points.Add(new MetricsDecisionPoint(state.Name, state.Tab, asIs, afterChanges, withoutPerson));
        }

        return points;
    }

    /// <summary>
    /// The merged pull requests of <c>change/&lt;n&gt;</c> branches, whichever instance worked the PBI:
    /// with a person's commit, or all the loop's. One with no person's commit and some from before the
    /// marks is neither, since those cannot be told apart.
    /// </summary>
    static List<string> Merged(
        Input input,
        Func<string, bool> inScope,
        Func<DateTimeOffset, bool> inPeriod,
        CommitReading commits,
        bool person
    ) =>
    [
        .. (input.History?.PullRequests ?? [])
            .Where(pull => pull.MergedAt is { } merged && inPeriod(merged))
            .Select(pull => PbiOf(pull.Branch))
            .OfType<string>()
            .Where(inScope)
            .Where(pbi => person ? commits.People.ContainsKey(pbi) : !commits.People.ContainsKey(pbi) && !commits.Unmarked.ContainsKey(pbi))
            .Distinct(StringComparer.Ordinal)
            .Order(Numerically),
    ];

    /// <summary>The PBI a branch of the loop is for: <c>change/44</c> is 44's (ADR-0005).</summary>
    static string? PbiOf(string branch) => branch.StartsWith("change/", StringComparison.Ordinal) && branch.Length > 7 ? branch[7..] : null;

    /// <summary>The commits on each PBI's branch that are a person's, and those from before the marks.</summary>
    /// <param name="People">When a person committed on each PBI's branch.</param>
    /// <param name="Unmarked">When each PBI's commits from before <paramref name="Since"/> were made.</param>
    /// <param name="Since">The earliest commit with a mark, or now when none has one.</param>
    sealed record CommitReading(Dictionary<string, List<DateTimeOffset>> People, Dictionary<string, List<DateTimeOffset>> Unmarked, DateTimeOffset Since);

    /// <summary>
    /// A commit with a mark is the loop's; one without is a person's, unless it is older than every
    /// marked commit, when nothing marked commits yet and it cannot be told apart.
    /// </summary>
    static CommitReading Commits(Input input)
    {
        var pulls = (input.History?.PullRequests ?? []).Select(pull => (Pbi: PbiOf(pull.Branch), pull.Commits)).Where(pull => pull.Pbi is not null).ToList();
        var since = pulls.SelectMany(pull => pull.Commits).Where(commit => commit.Marked).Select(commit => commit.At).DefaultIfEmpty(input.Now).Min();
        var people = new Dictionary<string, List<DateTimeOffset>>(StringComparer.Ordinal);
        var unmarked = new Dictionary<string, List<DateTimeOffset>>(StringComparer.Ordinal);
        foreach (var (pbi, commits) in pulls)
        {
            foreach (var commit in commits.Where(commit => !commit.Marked))
            {
                var into = commit.At < since ? unmarked : people;
                (into.TryGetValue(pbi!, out var kept) ? kept : into[pbi!] = []).Add(commit.At);
            }
        }

        return new CommitReading(people, unmarked, since);
    }

    // ---- 3. Flow.

    static MetricsFlow Flow(
        Input input,
        Dictionary<string, List<JournalEntry>> byPbi,
        Dictionary<string, PbiLineage> lineage,
        HashSet<string> epics,
        Func<string, bool> inScope,
        Func<DateTimeOffset, bool> inPeriod
    )
    {
        var done = byPbi
            .Where(each => inScope(each.Key) && !epics.Contains(each.Key))
            .Select(each => (Pbi: each.Key, Start: each.Value[0].At, End: each.Value.FirstOrDefault(line => line.Kind == JournalKind.Moved && line.To == Done)?.At))
            .Where(each => each.End is { } end && inPeriod(end))
            .OrderBy(each => each.End)
            .ToList();
        var durations = done.Select(each => (each.End!.Value - each.Start).TotalSeconds).ToList();

        var epicFlows = new List<MetricsEpicFlow>();
        foreach (var epic in epics.Where(epic => input.Epic is null || epic == input.Epic).Order(Numerically))
        {
            var children = lineage.Values.Where(pbi => pbi.Parent == epic).Select(pbi => pbi.Ref.Id).Where(byPbi.ContainsKey).Order(Numerically).ToList();
            if (children.Count == 0)
            {
                continue;
            }

            var started = children.Min(child => byPbi[child][0].At);
            var closed = byPbi.GetValueOrDefault(epic)?.FirstOrDefault(line => line.Kind == JournalKind.Moved && line.To == Done)?.At ?? lineage.GetValueOrDefault(epic)?.ClosedAt;
            if (inPeriod(started) || closed is { } end && inPeriod(end))
            {
                epicFlows.Add(new MetricsEpicFlow(epic, lineage.GetValueOrDefault(epic)?.Title ?? string.Empty, children, started, closed, closed is { } at ? (at - started).TotalSeconds : null));
            }
        }

        return new MetricsFlow(
            [.. done.Select(each => each.Pbi)],
            Percentile(durations, 50),
            Percentile(durations, 75),
            Percentile(durations, 90),
            epicFlows,
            Rework(input.Loop, byPbi, inScope, inPeriod),
            Weeks([.. done.Select(each => (each.Pbi, each.End!.Value))], input.From, input.Now)
        );
    }

    /// <summary>
    /// Each time a PBI went back in the loop: a move or release to a state above the one it left (a
    /// reviewer's <c>rejected</c>, a request for changes), or a hold that led nowhere in a state that
    /// runs scripts, such as a red gate, which the PBI later left forward.
    /// </summary>
    static List<MetricsRework> Rework(LoopDefinition loop, Dictionary<string, List<JournalEntry>> byPbi, Func<string, bool> inScope, Func<DateTimeOffset, bool> inPeriod)
    {
        var scripted = loop.States.Where(state => state.Agent is null && state.Command is null).Select(state => state.Name).ToHashSet(StringComparer.Ordinal);
        var rework = new List<MetricsRework>();
        foreach (var (pbi, lines) in byPbi.Where(each => inScope(each.Key)).OrderBy(each => each.Key, Numerically))
        {
            var rounds = 0;
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                if (!inPeriod(line.At))
                {
                    continue;
                }

                if (line.Kind is JournalKind.Moved or JournalKind.Released && SentBack(loop, line))
                {
                    rounds++;
                }
                else if (
                    line is { Kind: JournalKind.Held, To: null }
                    && scripted.Contains(line.From)
                    && lines.Skip(index + 1).Any(later => later.From == line.From && Forward(loop, later))
                )
                {
                    rounds++;
                }
            }

            if (rounds > 0)
            {
                rework.Add(new MetricsRework(pbi, rounds));
            }
        }

        return rework;
    }

    /// <summary>The PBIs that reached done, by the week they did, Monday first, with the empty weeks between and after.</summary>
    static List<MetricsWeek> Weeks(List<(string Pbi, DateTimeOffset At)> done, DateTimeOffset? from, DateTimeOffset now)
    {
        if (done.Count == 0)
        {
            return [];
        }

        // From the period's start, or from the first week a PBI reached done when that is later: weeks
        // before the repository had a loop would read as weeks it did nothing.
        var earliest = done.Min(each => each.At);
        var first = Monday(from is { } start && start > earliest ? start : earliest);
        var weeks = new List<MetricsWeek>();
        for (var week = first; week <= now; week = week.AddDays(7))
        {
            var next = week.AddDays(7);
            weeks.Add(new MetricsWeek(week, [.. done.Where(each => each.At >= week && each.At < next).Select(each => each.Pbi)]));
        }

        return weeks;
    }

    static DateTimeOffset Monday(DateTimeOffset at)
    {
        var day = new DateTimeOffset(at.UtcDateTime.Date, TimeSpan.Zero);
        return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }

    // ---- 4. Notes.

    static List<MetricsNote> Notes(
        Input input,
        List<HoldSpan> holds,
        List<RunStatus> loopRuns,
        CommitReading commits,
        Func<string, bool> inScope,
        Func<DateTimeOffset, bool> inPeriod
    )
    {
        var notes = new List<MetricsNote>();
        var counted = loopRuns.Where(run => inScope(run.Pbi.Id) && inPeriod(Started(run))).ToList();

        if (input.Unread is { } unread)
        {
            notes.Add(new MetricsNote("vendorUnread", 0, [], [], null, unread));
        }
        else if (input.History is { MorePullRequests: true })
        {
            notes.Add(new MetricsNote("olderPullRequests", input.History.PullRequests.Count, [], [], null, null));
        }

        var unmarked = commits.Unmarked.Where(each => inScope(each.Key)).SelectMany(each => each.Value.Where(inPeriod).Select(_ => each.Key)).ToList();
        if (unmarked.Count > 0)
        {
            notes.Add(new MetricsNote("unmarkedCommits", unmarked.Count, [.. unmarked.Distinct(StringComparer.Ordinal).Order(Numerically)], [], commits.Since, null));
        }

        var unpriced = counted.Where(run => run.End is { CostUsd: null }).ToList();
        if (unpriced.Count > 0)
        {
            notes.Add(new MetricsNote("unpricedRuns", unpriced.Count, [.. unpriced.Select(run => run.Pbi.Id)], [.. unpriced.Select(run => run.RunId)], null, null));
        }

        var live = counted.Where(run => run.End is null).ToList();
        if (live.Count > 0)
        {
            notes.Add(new MetricsNote("liveRuns", live.Count, [.. live.Select(run => run.Pbi.Id)], [.. live.Select(run => run.RunId)], null, null));
        }

        var open = holds.Where(hold => hold.Ending == Ending.Open).ToList();
        if (open.Count > 0)
        {
            notes.Add(new MetricsNote("openHolds", open.Count, [.. open.Select(hold => hold.Pbi)], [], null, null));
        }

        var manual = input.Runs.Count(run => run.Manual && inScope(run.Pbi.Id) && inPeriod(Started(run)));
        if (manual > 0)
        {
            notes.Add(new MetricsNote("manualRuns", manual, [], [], null, null));
        }

        if (input.Runs.Count == 0)
        {
            notes.Add(new MetricsNote("noRuns", 0, [], [], null, null));
        }
        else if (input.Runs.Min(Started) is var first && (input.From is not { } from || from < first))
        {
            notes.Add(new MetricsNote("firstRun", 0, [], [], first, null));
        }

        return notes;
    }

    /// <summary>
    /// The value below which <paramref name="percent"/> of them fall, interpolated linearly between the
    /// two nearest ranks: the definition of the Platform's Project Health collector
    /// (<c>collector/src/stats.ts</c>), so a p90 here reads as one there. Null when there are none.
    /// </summary>
    public static double? Percentile(IReadOnlyCollection<double> values, int percent)
    {
        if (values.Count == 0)
        {
            return null;
        }

        var sorted = values.Order().ToList();
        var rank = percent / 100.0 * (sorted.Count - 1);
        var (low, high) = ((int)Math.Floor(rank), (int)Math.Ceiling(rank));
        return sorted[low] + (sorted[high] - sorted[low]) * (rank - low);
    }

    /// <summary>PBI ids as numbers where they are numbers, so #9 comes before #10.</summary>
    static readonly IComparer<string> Numerically = Comparer<string>.Create((left, right) =>
        long.TryParse(left, out var a) && long.TryParse(right, out var b) ? a.CompareTo(b) : string.CompareOrdinal(left, right)
    );
}
