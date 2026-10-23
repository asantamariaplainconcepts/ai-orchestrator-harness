using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #734. The Metrics screen of a folder at /folders/{id}/metrics, linked from the Loop screen: the four
// blocks, the period and the epic in the address and in the request, and a number that lists the PBIs
// it counts, each opening its Journey.
//
// The API is stubbed at the browser, like LoopTests: what is under test is the screen's reading of the
// contract. MetricsTests in the Loop functional tests check the numbers against a journal, runs and a
// vendor; the running product was checked by hand against aio-dogfood for the pull request.
[Collection("review-frontend")]
public sealed class MetricsScreenTests(ReviewFrontendFixture frontend)
{
    const string FolderId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c6";
    const string Repository = "owner/sandbox";

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task The_four_blocks_follow_the_period_and_the_epic_and_a_number_lists_its_PBIs(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1280, 900);
        var asked = await Arrange(page);
        await page.GotoAsync("/loop");

        await page.GetByRole(AriaRole.Link, new() { Name = "Metrics of sandbox" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex($"/folders/{FolderId}/metrics$"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Metrics · sandbox", Level = 1 })).ToBeVisibleAsync();

        var agents = page.GetByRole(AriaRole.Region, new() { Name = "Agents, by step of the loop" });
        await Assertions.Expect(agents).ToContainTextAsync("/aio:refine");
        await Assertions.Expect(agents).ToContainTextAsync("$0.80");
        await Assertions.Expect(agents.GetByRole(AriaRole.Img, new() { Name = "Cost by step" })).ToBeVisibleAsync();
        await Assertions.Expect(agents.GetByRole(AriaRole.Img, new() { Name = "Duration by step, median and p90" })).ToBeVisibleAsync();
        await Assertions.Expect(agents.GetByRole(AriaRole.Link, new() { Name = "Open the Journey of #2" }).First).ToHaveAttributeAsync("href", $"/pbis/{FolderId}/2/journey");

        var people = page.GetByRole(AriaRole.Region, new() { Name = "People, at each hold" });
        await Assertions.Expect(people).ToContainTextAsync("El gate está en rojo");
        await Assertions.Expect(people).ToContainTextAsync("Released unchanged 2 of 2: this hold caught nothing");
        await Assertions.Expect(people.GetByRole(AriaRole.Img, new() { Name = "El gate está en rojo: Released unchanged 2" })).ToBeVisibleAsync();

        var flow = page.GetByRole(AriaRole.Region, new() { Name = "Flow" });
        await Assertions.Expect(flow).ToContainTextAsync("3h 40m");
        await Assertions.Expect(flow).ToContainTextAsync("The epic");

        var notes = page.GetByRole(AriaRole.Region, new() { Name = "Notes" });
        await Assertions.Expect(notes).ToContainTextAsync("2 runs without a cost: the CLI never said what they cost, so the cost totals are a floor.");

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"metrics-{theme}"), FullPage = true });

        // The period and the epic go in the address and in the next read.
        await page.GetByLabel("Period").SelectOptionAsync("7");
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("days=7"));
        await page.GetByLabel("Epic").SelectOptionAsync("9");
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("days=7&epic=9"));
        for (var waited = 0; waited < 50 && !asked.Any(query => query.Contains("days=7") && query.Contains("epic=9")); waited++)
        {
            await page.WaitForTimeoutAsync(100);
        }
        asked.ShouldContain(query => query.Contains("days=7") && query.Contains("epic=9"), "the next read asks for the period and the epic");

        // A number lists what it counts, and each PBI opens its Journey.
        await flow.GetByRole(AriaRole.Button, new() { Name = "PBIs that reached done: 2, list them" }).ClickAsync();
        var listed = page.GetByRole(AriaRole.Dialog, new() { Name = "PBIs that reached done" });
        await Assertions.Expect(listed.GetByRole(AriaRole.Link)).ToHaveCountAsync(2);
        await listed.GetByRole(AriaRole.Link, new() { Name = "#1 First" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex($"/pbis/{FolderId}/1/journey$"));
    }

    [Fact]
    public async Task At_390_wide_nothing_scrolls_the_page_sideways()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await Arrange(page);
        await page.GotoAsync($"/folders/{FolderId}/metrics");

        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Notes" })).ToBeVisibleAsync();
        var wide = await page.EvaluateAsync<bool>("() => document.documentElement.scrollWidth > document.documentElement.clientWidth + 1");
        wide.ShouldBeFalse("the tables scroll inside their own box, never the page");
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath("metrics-390"), FullPage = true });
    }

    static async Task<ConcurrentQueue<string>> Arrange(IPage page)
    {
        var asked = new ConcurrentQueue<string>();
        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var url = new Uri(route.Request.Url);
                if (url.AbsolutePath == "/api/loop/metrics") asked.Enqueue(url.Query);
                object response = url.AbsolutePath switch
                {
                    "/api/folders" => Folders,
                    "/api/loop/metrics" => Report,
                    "/api/pbis" => new { pbis = Array.Empty<object>(), unread = Array.Empty<object>() },
                    "/api/sessions" => Array.Empty<object>(),
                    "/api/loop/journal" => Array.Empty<object>(),
                    "/api/loop/present" => Present,
                    _ => new { },
                };
                await route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(response) });
            }
        );
        return asked;
    }

    static readonly object[] Folders =
    [
        new { id = FolderId, name = "sandbox", path = "/checkouts/sandbox", vendor = "github", repository = Repository, loopYaml = "name: test-loop\nstates: {}\n" },
    ];

    static readonly object Present = new
    {
        enabled = false,
        intervalSeconds = 60,
        lastPass = (object?)null,
        nextPassAt = (DateTimeOffset?)null,
        liveRuns = Array.Empty<object>(),
        liveScripts = Array.Empty<object>(),
        folders = Array.Empty<object>(),
        lastDecision = (object?)null,
    };

    static readonly DateTimeOffset T0 = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);

    static object Run(string id, string state, string pbi, double minutes, double? seconds, string result, decimal? cost) => new
    {
        runId = id,
        state,
        pbi,
        startedAt = T0.AddMinutes(minutes),
        seconds,
        result,
        succeeded = result is not ("live" or "notLoggedIn"),
        costUsd = cost,
        tokens = cost is null ? (long?)null : 1000L,
    };

    /// <summary>A report of two PBIs of one epic; <see cref="ScreenCaptureTests"/> captures the screen with it too.</summary>
    internal static readonly object Report = new
    {
        repository = Repository,
        from = (DateTimeOffset?)null,
        to = T0.AddDays(1),
        epic = (string?)null,
        epics = new[] { new { id = "9", title = "The epic" } },
        pbis = new[] { new { id = "1", title = "First" }, new { id = "2", title = "Second" }, new { id = "9", title = "The epic" } },
        agents = new
        {
            steps = new object[]
            {
                new
                {
                    state = "ready-for-proposal",
                    command = "/aio:refine",
                    runs = new[] { "00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002" },
                    succeeded = new[] { "00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-000000000002" },
                    medianSeconds = 600.0,
                    p90Seconds = 1080.0,
                    costUsd = 0.80m,
                    tokens = 2000L,
                    costPerRun = 0.40m,
                    tokensPerRun = 1000L,
                },
                new
                {
                    state = "in-progress",
                    command = "/aio:implement",
                    runs = new[] { "00000000-0000-0000-0000-000000000003" },
                    succeeded = Array.Empty<string>(),
                    medianSeconds = (double?)null,
                    p90Seconds = (double?)null,
                    costUsd = 0m,
                    tokens = 0L,
                    costPerRun = (decimal?)null,
                    tokensPerRun = (long?)null,
                },
            },
            runs = new[]
            {
                Run("00000000-0000-0000-0000-000000000003", "in-progress", "2", 90, null, "live", null),
                Run("00000000-0000-0000-0000-000000000002", "ready-for-proposal", "2", 60, 1080, "ready", 0.50m),
                Run("00000000-0000-0000-0000-000000000001", "ready-for-proposal", "1", 0, 600, "ready", 0.30m),
            },
        },
        people = new
        {
            holds = new[]
            {
                new
                {
                    reason = "El gate está en rojo",
                    unchanged = new[] { "1", "2" },
                    afterCommit = Array.Empty<string>(),
                    returned = Array.Empty<string>(),
                    closed = Array.Empty<string>(),
                    open = Array.Empty<string>(),
                    medianSeconds = (double?)1500,
                    maxSeconds = (double?)2280,
                    alwaysUnchanged = true,
                },
            },
            decisions = new[]
            {
                new { state = "proposal-review", tab = "proposal", asIs = new[] { "1" }, afterChanges = new[] { "2" }, withoutPerson = Array.Empty<string>() },
            },
            mergedWithPerson = new[] { "1" },
            mergedAgentOnly = new[] { "2" },
        },
        flow = new
        {
            done = new[] { "1", "2" },
            p50Seconds = (double?)13200,
            p75Seconds = (double?)13800,
            p90Seconds = (double?)14160,
            epics = new[]
            {
                new { id = "9", title = "The epic", pbis = new[] { "1", "2" }, started = T0, closed = (DateTimeOffset?)T0.AddMinutes(301), seconds = (double?)18060 },
            },
            rework = new[] { new { pbi = "1", rounds = 1 } },
            weeks = new[] { new { week = new DateTimeOffset(2026, 10, 12, 0, 0, 0, TimeSpan.Zero), pbis = new[] { "1", "2" } } },
        },
        notes = new object[]
        {
            new
            {
                kind = "unpricedRuns",
                count = 2,
                pbis = new[] { "2", "2" },
                runs = new[] { "00000000-0000-0000-0000-000000000003", "00000000-0000-0000-0000-000000000002" },
                at = (DateTimeOffset?)null,
                detail = (string?)null,
            },
            new { kind = "firstRun", count = 0, pbis = Array.Empty<string>(), runs = Array.Empty<string>(), at = (DateTimeOffset?)T0, detail = (string?)null },
        },
    };
}
