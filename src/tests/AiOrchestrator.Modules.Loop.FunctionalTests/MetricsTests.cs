using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Features;
using AiOrchestrator.Modules.Loop.Journal;
using AiOrchestrator.Modules.Loop.Persistence;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Loop.FunctionalTests;

/// <summary>
/// #734. The host with a journal, runs and a vendor history laid out by hand, as two dogfood epics left
/// them: what <c>GET /api/loop/metrics</c> answers is checked against what a person counts by eye.
/// </summary>
public sealed class MetricsFixture : ApiServiceFixtureBase, IAsyncLifetime
{
    internal FakeVendor Vendor { get; } = new();

    internal FakeRuns Runs { get; } = new(new FakeScripts());

    internal FakeFolders Folders { get; } = new();

    /// <summary>Three days ago, so everything but #5 falls in the last 7 days.</summary>
    public static readonly DateTimeOffset T0 = new(DateTimeOffset.UtcNow.AddDays(-3).UtcDateTime.Date.AddHours(9), TimeSpan.Zero);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IPbiProvider>();
            services.AddSingleton<IPbiProvider>(Vendor);
            services.RemoveAll<IRunRequests>();
            services.AddSingleton<IRunRequests>(Runs);
            services.RemoveAll<IFolderDirectory>();
            services.AddSingleton<IFolderDirectory>(Folders);
        });
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await using var scope = Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<LoopDbContext>();
        foreach (var (minutes, pbi, from, kind, to, why) in Journal)
        {
            database.Journal.Add(new JournalEntry(T0.AddMinutes(minutes), FakeFolders.Repository, pbi, from, kind, to, why));
        }

        await database.SaveChangesAsync();
        Runs.Repository.AddRange(RunsOf());
        Vendor.History = History();
    }

    static readonly (double Minutes, string Pbi, string From, JournalKind Kind, string? To, string Why)[] Journal =
    [
        // #5 went to done twenty days ago: in all of it, not in the last 7 days.
        (-20 * 1440, "5", "ready-for-proposal", JournalKind.Started, "ready-for-proposal", "asked for /aio:refine 5"),
        (-20 * 1440 + 100, "5", "merging", JournalKind.Moved, "done", "merged: Pull request 15"),

        // #1: a score hold released with no commit, a red gate released after a person's commit, done.
        (0, "1", "ready-for-proposal", JournalKind.Started, "ready-for-proposal", "asked for /aio:refine 1"),
        (11, "1", "ready-for-proposal", JournalKind.Moved, "spec-ready", "ready: the agent gave no reason"),
        (12, "1", "spec-ready", JournalKind.Started, "spec-ready", "running claim"),
        (13, "1", "spec-ready", JournalKind.Ran, null, "claim: change/1 created at origin."),
        (14, "1", "spec-ready", JournalKind.Started, "spec-ready", "asked for /aio:propose 1"),
        (21, "1", "spec-ready", JournalKind.Moved, "proposal-review", "next: the agent gave no reason"),
        (22, "1", "proposal-review", JournalKind.Held, "in-progress", "El score de riesgo salió alto: touches a port"),
        (60, "1", "proposal-review", JournalKind.Released, "in-progress", "a person released the hold"),
        (91, "1", "in-progress", JournalKind.Moved, "verifying", "next: the agent gave no reason"),
        (100, "1", "verifying", JournalKind.Held, null, "El gate está en rojo: aspire do ci failed"),
        (120, "1", "verifying", JournalKind.Released, "verifying", "a person released the hold"),
        (130, "1", "verifying", JournalKind.Moved, "code-review", "green: aspire do ci passed"),
        (200, "1", "merging", JournalKind.Moved, "done", "merged: Pull request 11"),

        // #2: changes requested on the proposal, then let through by a low score; done.
        (60, "2", "ready-for-proposal", JournalKind.Started, "ready-for-proposal", "asked for /aio:refine 2"),
        (120, "2", "spec-ready", JournalKind.Moved, "proposal-review", "next: the agent gave no reason"),
        (121, "2", "proposal-review", JournalKind.Held, "in-progress", "El score de riesgo salió alto: a new table"),
        (150, "2", "proposal-review", JournalKind.Released, "spec-ready", "a person requested changes on the proposal"),
        (170, "2", "spec-ready", JournalKind.Moved, "proposal-review", "next: the agent gave no reason"),
        (180, "2", "proposal-review", JournalKind.Moved, "in-progress", "low: one file"),
        (300, "2", "merging", JournalKind.Moved, "done", "merged: Pull request 12"),

        // The epic closes with its last sub-issue.
        (301, "9", "no state", JournalKind.Moved, "done", "all 2 sub-issues are closed, the last one owner/repo#2"),

        // #3, outside the epic: a login hold nobody released yet.
        (360, "3", "ready-for-proposal", JournalKind.Held, null, "the run failed (NotLoggedIn): Claude Code is not logged in."),

        // #4: stopped and released unchanged, then sent back by the reviewer.
        (400, "4", "ready-for-proposal", JournalKind.Held, null, "the run failed (Stopped): Stopped by a person."),
        (410, "4", "ready-for-proposal", JournalKind.Released, "ready-for-proposal", "a person released the hold"),
        (500, "4", "code-review", JournalKind.Moved, "in-progress", "rejected: the agent gave no reason"),
    ];

    static RunStatus Run(string id, string pbi, string state, double start, double? end, string? outcome, RunFailure? failure = null, decimal? cost = null, long? tokens = null) =>
        new(Guid.Parse(id), new PbiRef(FakeFolders.Repository, pbi), state, end is { } ended
            ? new RunEnd(outcome, null, failure, []) { EndedAt = T0.AddMinutes(ended), CostUsd = cost, InputTokens = tokens, OutputTokens = tokens is null ? null : 0 }
            : null)
        {
            StartedAt = T0.AddMinutes(start),
            Command = $"/aio:{(state == "ready-for-proposal" ? "refine" : state == "spec-ready" ? "propose" : "implement")} {pbi}",
        };

    public const string Unpriced = "00000000-0000-0000-0000-000000000002";

    public const string Live = "00000000-0000-0000-0000-000000000007";

    static RunStatus[] RunsOf() =>
    [
        Run("00000000-0000-0000-0000-000000000001", "1", "ready-for-proposal", 0, 10, "ready", cost: 0.30m, tokens: 1000),
        Run(Unpriced, "1", "spec-ready", 14, 20, "next"),
        Run("00000000-0000-0000-0000-000000000003", "1", "in-progress", 61, 90, "next", cost: 0.50m, tokens: 3000),
        Run("00000000-0000-0000-0000-000000000004", "2", "ready-for-proposal", 60, 80, "ready", cost: 0.50m, tokens: 2000),
        Run("00000000-0000-0000-0000-000000000005", "2", "in-progress", 181, 240, "next", cost: 0.40m, tokens: 2000),
        Run("00000000-0000-0000-0000-000000000006", "3", "ready-for-proposal", 359, 360, null, RunFailure.NotLoggedIn),
        Run(Live, "4", "in-progress", 510, null, null),
        // A terminal a person opened: never counted as an agent's run.
        Run("00000000-0000-0000-0000-000000000008", "1", "verifying", 105, 115, null) with { Manual = true },
    ];

    static VendorHistory History() =>
        new(
            [
                Lineage("1", "First", "9"),
                Lineage("2", "Second", "9"),
                Lineage("3", "Third", null),
                Lineage("4", "Fourth", null),
                Lineage("5", "Fifth", null),
                Lineage("9", "The epic", null),
            ],
            [
                // The claim's commit and the run's are the loop's; the one at 110 is a person's, while #1 was held.
                Pull("11", "change/1", 199, (12.5, Claim), (80, ImplementOne), (110, Person)),
                // #2's one commit is its implement run's.
                Pull("12", "change/2", 299, (200, ImplementTwo)),
                // #6, #7 and #8 were worked by another instance: this journal and these runs know none of them.
                Pull("13", "change/6", 310, (305, Archive)),
                Pull("14", "change/7", 320, (315, Person)),
                Pull("16", "change/8", 330, (325, RebasedRun)),
                // #10's commit is older than every marked one, the claim at 12.5 the earliest.
                Pull("17", "change/10", 340, (-60, Person)),
            ],
            false
        );

    static PbiLineage Lineage(string id, string title, string? parent) => new(new PbiRef(FakeFolders.Repository, id), title, parent, null);

    static readonly BranchCommit Person = Commit("Andoni Santamaria");

    static readonly BranchCommit Claim = Commit("aio script claim");

    static readonly BranchCommit Archive = Commit("aio script archive");

    static readonly BranchCommit ImplementOne = Commit("aio run 00000000-0000-0000-0000-000000000003");

    static readonly BranchCommit ImplementTwo = Commit("aio run 00000000-0000-0000-0000-000000000005");

    /// <summary>A run's commit whose committer a rewrite took, its mark kept in a trailer.</summary>
    static readonly BranchCommit RebasedRun = Commit("Andoni Santamaria", "Aio-Run: 00000000-0000-0000-0000-0000000000aa");

    /// <summary>Every commit is authored by the person, as on one machine: only the committer tells them apart.</summary>
    static BranchCommit Commit(string committer, params string[] trailers) => new("sha", default, ["Andoni Santamaria"], committer, trailers);

    static BranchPullRequest Pull(string id, string branch, double merged, params (double At, BranchCommit Commit)[] commits) =>
        new(id, branch, T0.AddMinutes(merged), [.. commits.Select(each => each.Commit with { Sha = $"sha{each.At}", At = T0.AddMinutes(each.At) })]);
}

public sealed class MetricsTests(MetricsFixture fixture) : IClassFixture<MetricsFixture>
{
    [Fact]
    public async Task A_hold_released_with_no_commit_is_unchanged_and_one_after_a_persons_commit_is_not()
    {
        var report = await Read("days=7");
        var holds = report.GetProperty("people").GetProperty("holds");

        var score = Reason(holds, "El score de riesgo salió alto");
        Ids(score, "unchanged").ShouldBe(["1"]);
        Ids(score, "returned").ShouldBe(["2"], "a request for changes sends it back");
        score.GetProperty("alwaysUnchanged").GetBoolean().ShouldBeFalse();
        // Held 38 and 29 minutes.
        score.GetProperty("medianSeconds").GetDouble().ShouldBe(33.5 * 60);
        score.GetProperty("maxSeconds").GetDouble().ShouldBe(38 * 60);

        var gate = Reason(holds, "El gate está en rojo");
        Ids(gate, "afterCommit").ShouldBe(["1"], "the commit at 110 carries no mark");
        Ids(gate, "unchanged").ShouldBeEmpty();

        var stopped = Reason(holds, "the run failed (Stopped)");
        Ids(stopped, "unchanged").ShouldBe(["4"]);
        stopped.GetProperty("alwaysUnchanged").GetBoolean().ShouldBeTrue();

        Ids(Reason(holds, "the run failed (NotLoggedIn)"), "open").ShouldBe(["3"]);
    }

    [Fact]
    public async Task The_loops_commits_are_not_a_persons_and_a_request_for_changes_is_counted()
    {
        var people = (await Read("days=7&epic=9")).GetProperty("people");

        Ids(people, "mergedWithPerson").ShouldBe(["1"]);
        Ids(people, "mergedAgentOnly").ShouldBe(["2"], "#2's one commit carries its implement run's mark");

        var points = people.GetProperty("decisions").EnumerateArray().ToList();
        points.Select(point => point.GetProperty("state").GetString()).ShouldBe(["proposal-review", "merge-review"]);
        Ids(points[0], "asIs").ShouldBe(["1"]);
        Ids(points[0], "afterChanges").ShouldBe(["2"]);

        var rework = (await Read("days=7")).GetProperty("flow").GetProperty("rework").EnumerateArray()
            .Select(each => $"{each.GetProperty("pbi").GetString()}×{each.GetProperty("rounds").GetInt32()}");
        rework.ShouldBe(["1×1", "2×1", "4×1"], "a red gate that went green, a request for changes, a rejected review");
    }

    /// <summary>
    /// #741. Whose a commit is, by its mark alone: a run's or a script's committer, or a trailer, is the
    /// loop's; no mark is a person's, on a PBI this instance never worked as on one it did.
    /// </summary>
    [Fact]
    public async Task A_commit_is_the_loops_by_its_mark_and_a_persons_without_one_whichever_instance_worked_it()
    {
        var people = (await Read("days=7")).GetProperty("people");

        Ids(people, "mergedAgentOnly").ShouldBe(["2", "6", "8"], "a run's committer, a script's committer and a run's trailer");
        Ids(people, "mergedWithPerson").ShouldBe(["1", "7"], "#7 is in no journal here, and its commit carries no mark");
        Ids(Reason(people.GetProperty("holds"), "El gate está en rojo"), "afterCommit").ShouldBe(["1"]);
    }

    /// <summary>#741. Before the loop marked anything its commits read as a person's, so they count as nobody's.</summary>
    [Fact]
    public async Task A_commit_from_before_the_marks_is_in_the_note_and_nowhere_else()
    {
        var report = await Read("days=7");
        var people = report.GetProperty("people");

        Ids(people, "mergedWithPerson").ShouldNotContain("10");
        Ids(people, "mergedAgentOnly").ShouldNotContain("10");
        people.GetProperty("holds").EnumerateArray().SelectMany(reason => reason.EnumerateObject())
            .Where(field => field.Value.ValueKind == JsonValueKind.Array)
            .SelectMany(field => field.Value.EnumerateArray().Select(id => id.GetString()))
            .ShouldNotContain("10");

        var note = report.GetProperty("notes").EnumerateArray().Single(note => note.GetProperty("kind").GetString() == "unmarkedCommits");
        note.GetProperty("count").GetInt32().ShouldBe(1);
        Ids(note, "pbis").ShouldBe(["10"]);
        note.GetProperty("at").GetDateTimeOffset().ShouldBe(MetricsFixture.T0.AddMinutes(12.5), "the claim's is the earliest marked commit");

        (await Read("days=7&epic=9")).GetProperty("notes").EnumerateArray()
            .ShouldNotContain(each => each.GetProperty("kind").GetString() == "unmarkedCommits", "#10 is not one of #9's");
    }

    [Fact]
    public async Task Percentiles_come_from_the_data_and_a_run_without_a_cost_is_a_note_not_a_zero()
    {
        var report = await Read("days=7");

        var refine = report.GetProperty("agents").GetProperty("steps").EnumerateArray().First(step => step.GetProperty("state").GetString() == "ready-for-proposal");
        refine.GetProperty("command").GetString().ShouldBe("/aio:refine");
        refine.GetProperty("runs").GetArrayLength().ShouldBe(3);
        refine.GetProperty("succeeded").GetArrayLength().ShouldBe(2);
        // 1, 10 and 20 minutes: the median is 10, the p90 is 10 + 0.8 × 10.
        refine.GetProperty("medianSeconds").GetDouble().ShouldBe(600, 0.001);
        refine.GetProperty("p90Seconds").GetDouble().ShouldBe(18 * 60, 0.001);
        refine.GetProperty("costUsd").GetDecimal().ShouldBe(0.80m);
        refine.GetProperty("costPerRun").GetDecimal().ShouldBe(0.40m, "over the two runs that said a cost, not the three");

        var propose = report.GetProperty("agents").GetProperty("steps").EnumerateArray().First(step => step.GetProperty("state").GetString() == "spec-ready");
        propose.GetProperty("costUsd").GetDecimal().ShouldBe(0m);
        propose.GetProperty("costPerRun").ValueKind.ShouldBe(JsonValueKind.Null, "a run without a cost is not a run that cost nothing");

        var notes = report.GetProperty("notes").EnumerateArray().ToDictionary(note => note.GetProperty("kind").GetString()!);
        notes["unpricedRuns"].GetProperty("count").GetInt32().ShouldBe(2);
        notes["unpricedRuns"].GetProperty("runs").EnumerateArray().Select(run => run.GetString()).ShouldContain(MetricsFixture.Unpriced);
        notes["liveRuns"].GetProperty("runs").EnumerateArray().Single().GetString().ShouldBe(MetricsFixture.Live);
        notes["openHolds"].GetProperty("pbis").EnumerateArray().Single().GetString().ShouldBe("3");
        notes["manualRuns"].GetProperty("count").GetInt32().ShouldBe(1);

        // #1 took 200 minutes from its first line to done and #2 took 240.
        var flow = report.GetProperty("flow");
        Ids(flow, "done").ShouldBe(["1", "2"]);
        flow.GetProperty("p50Seconds").GetDouble().ShouldBe(220 * 60, 0.001);
        flow.GetProperty("p75Seconds").GetDouble().ShouldBe(230 * 60, 0.001);
        flow.GetProperty("p90Seconds").GetDouble().ShouldBe(236 * 60, 0.001);
        var epic = flow.GetProperty("epics").EnumerateArray().Single();
        epic.GetProperty("id").GetString().ShouldBe("9");
        epic.GetProperty("seconds").GetDouble().ShouldBe(301 * 60, 0.001);
    }

    [Fact]
    public async Task The_period_and_the_epic_narrow_every_block()
    {
        var all = await Read(null);
        Ids(all.GetProperty("flow"), "done").ShouldBe(["5", "1", "2"]);
        all.GetProperty("epics").EnumerateArray().Select(epic => epic.GetProperty("title").GetString()).ShouldBe(["The epic"]);

        var epic = await Read("days=30&epic=9");
        Ids(epic.GetProperty("flow"), "done").ShouldBe(["1", "2"]);
        epic.GetProperty("agents").GetProperty("runs").EnumerateArray().Select(run => run.GetProperty("pbi").GetString()).Distinct().Order().ShouldBe(["1", "2"]);
        epic.GetProperty("people").GetProperty("holds").EnumerateArray().Select(reason => reason.GetProperty("reason").GetString())
            .ShouldBe(["El score de riesgo salió alto", "El gate está en rojo"]);

        using var client = fixture.CreateClient();
        using var refused = await client.GetAsync($"/api/loop/metrics?repository={FakeFolders.Repository}&days=12");
        refused.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    async Task<JsonElement> Read(string? query)
    {
        using var client = fixture.CreateClient();
        using var response = await client.GetAsync($"/api/loop/metrics?repository={FakeFolders.Repository}{(query is null ? "" : "&" + query)}");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    static JsonElement Reason(JsonElement holds, string reason) =>
        holds.EnumerateArray().Single(each => each.GetProperty("reason").GetString() == reason);

    static List<string?> Ids(JsonElement element, string name) => [.. element.GetProperty(name).EnumerateArray().Select(id => id.GetString())];
}
