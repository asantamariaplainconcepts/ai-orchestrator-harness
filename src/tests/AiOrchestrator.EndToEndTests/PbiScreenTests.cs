using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #511. The PBI screen: its header with the state, the hold and its reason and the pull request;
// the one button whose words depend on the state; and the Journey with the loop's states top to
// bottom, a score with its band and why, a run that unfolds into its end record and replays its
// recording with asciinema-player, and a live run that says since when it runs and when it wrote.
//
// The API is stubbed at the browser, like ShellTests: what is under test is the screen's reading
// of the contract and the writes it sends. The writes themselves — a release going on by `next`, a
// stop ending the run as Stopped — are covered against the real host by PersonDecisionTests and
// PbiRunTests. The stub answers a release the way the engine does, so the Journey shows the line.
[Collection("review-frontend")]
public sealed class PbiScreenTests(ReviewFrontendFixture frontend)
{
    const string FolderId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c6";
    const string Repository = "owner/sandbox";
    const string ReviewRun = "01a11dcb-dec7-78dd-8f4c-70a254ded201";

    /// <summary>Where another instance copied its implement run's recording on aio/evidence (#697).</summary>
    const string EvidencePath = "495/01a11dcbdec778dd8f4c70a254ded2aa/session.cast";
    const string ImplementRun = "01a11dcb-dec7-78dd-8f4c-70a254ded202";
    const string LiveRun = "01a11dcb-dec7-78dd-8f4c-70a254ded203";

    [Theory]
    [InlineData("light")]
    public async Task A_hold_is_released_from_the_header_and_the_journey_shows_it_went_on_by_next(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var vendor = new Vendor("merge-review", held: true, live: false);
        await Arrange(page, vendor);

        await page.GotoAsync($"/pbis/{FolderId}/495");
        await Assertions.Expect(page).ToHaveURLAsync(new Regex($"/pbis/{FolderId}/495/journey$"));

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Declare the loop as a state machine" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Hold · El riesgo del diff salió alto: touches the IPbiProvider port")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link).Filter(new() { HasText = "PR #512" })).ToContainTextAsync("approved");
        var tabs = page.GetByRole(AriaRole.Navigation, new() { Name = "PBI tabs" });
        await Assertions.Expect(tabs.GetByRole(AriaRole.Link, new() { Name = "Review" })).ToContainTextAsync("needs you");

        // The loop's states, top to bottom, ending in the ones it does not declare.
        var journey = page.GetByRole(AriaRole.List, new() { Name = "Journey" });
        var steps = journey.Locator(":scope > li");
        await Assertions.Expect(steps).ToHaveCountAsync(12);
        await Assertions.Expect(steps.First).ToContainTextAsync("ready-for-proposal");
        await Assertions.Expect(steps.Last).ToContainTextAsync("done");
        var current = journey.Locator("li[aria-current=step]");
        await Assertions.Expect(current).ToContainTextAsync("merge-review");

        // A score is a state with its band and why.
        await Assertions.Expect(current.GetByText("diff-risk", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(current.GetByText("high", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(current.GetByText("touches the IPbiProvider port", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(current).ToContainTextAsync("moves this to retro");

        // A run unfolds into its end record as written, and its recording replays from its row.
        var row = page.GetByRole(AriaRole.Button, new() { Name = "End record of /aio:review-diff 495" });
        await Assertions.Expect(row).ToContainTextAsync("$0.41");
        await row.ClickAsync();
        await Assertions.Expect(row).ToHaveAttributeAsync("aria-expanded", "true");
        var record = row.Locator("xpath=..");
        await Assertions.Expect(record).ToContainTextAsync("\"outcome\": \"approved\"");
        await Assertions.Expect(record).ToContainTextAsync("2 comments posted on PR #512");
        await Assertions.Expect(record).ToContainTextAsync("38,210 in · 2,904 out · $0.41");
        await Assertions.Expect(record.GetByText("merge-review", new() { Exact = true })).ToBeVisibleAsync();
        await record.GetByRole(AriaRole.Button, new() { Name = "Play 11m" }).ClickAsync();
        var player = page.GetByRole(AriaRole.Region, new() { Name = "Recording of /aio:review-diff 495" });
        await Assertions.Expect(player.Locator(".ap-player")).ToBeVisibleAsync();
        await Assertions.Expect(player).ToContainTextAsync("reviewing the diff", new() { Timeout = 10_000 });

        // A manual terminal is in the Journey, marked, and moved nothing.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "End record of shell" })).ToContainTextAsync("manual");

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"pbi-hold-{theme}"), FullPage = true });

        // Release hold: one press, and the loop goes on to the hold's next.
        await page.GetByRole(AriaRole.Button, new() { Name = "Release hold" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Release hold" })).ToHaveCountAsync(0);
        await Assertions.Expect(current).ToContainTextAsync("retro");
        var mergeReview = steps.Nth(6);
        await Assertions.Expect(mergeReview).ToContainTextAsync("score · diff-risk");
        await Assertions.Expect(mergeReview).ToContainTextAsync("released → retro");
        await Assertions.Expect(mergeReview).ToContainTextAsync("a person released the hold");
        await Assertions.Expect(page.GetByText("Hold ·")).ToHaveCountAsync(0);

        vendor.Writes.ShouldBe([$$"""POST /api/pbi/release {"repository":"{{Repository}}","id":"495","to":null}"""]);
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"pbi-released-{theme}"), FullPage = true });
    }

    /// <summary>#595. The marked tab is the one the state names with <c>tab:</c> in the loop, and no other.</summary>
    [Theory]
    [InlineData("proposal-review", "Proposal", "light")]
    [InlineData("code-review", "Review", "light")]
    [InlineData("in-progress", null, "light")]
    public async Task The_tab_the_state_names_is_the_one_marked(string state, string? marked, string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await Arrange(page, new Vendor(state, held: false, live: false));

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");
        var tabs = page.GetByRole(AriaRole.Navigation, new() { Name = "PBI tabs" });
        await Assertions.Expect(tabs.GetByRole(AriaRole.Link, new() { Name = "Journey" })).ToBeVisibleAsync();
        // The badge waits for the loop, which is read after the PBI.
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Journey" }).Locator("li[aria-current=step]")).ToContainTextAsync(state);

        foreach (var name in new[] { "Journey", "Issue", "Proposal", "Review", "Files" })
        {
            var tab = tabs.GetByRole(AriaRole.Link, new() { Name = name, Exact = false });
            if (name == marked)
            {
                await Assertions.Expect(tab).ToContainTextAsync("now");
            }
            else
            {
                await Assertions.Expect(tab).Not.ToContainTextAsync("now");
            }
        }

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"pbi-tab-{state}-{theme}") });
    }

    /// <summary>
    /// The header's one button by state: a PBI the loop is working on, not held and with no live run,
    /// has none; an open PBI in <c>done</c> or <c>needs-refinement</c>, states the loop does not run,
    /// has <i>Move to…</i>; a PBI closed in <c>done</c> has none, because it is reopened in the vendor;
    /// and an epic has none even open and with no state, because the engine closes it (#720).
    /// </summary>
    [Theory]
    [InlineData("in-progress", false, 0, null)]
    [InlineData("done", false, 0, "Move to…")]
    [InlineData("needs-refinement", false, 0, "Move to…")]
    [InlineData("done", true, 0, null)]
    [InlineData(null, false, 2, null)]
    public async Task The_header_shows_the_one_button_the_state_asks_for(string? state, bool closed, int children, string? button)
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var vendor = new Vendor(state, held: false, live: false) { Closed = closed, Children = children };
        await Arrange(page, vendor);

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");
        // The header's badge says the state, or the epic's count once the sub-issues are read.
        var header = page.Locator("h1").Locator("xpath=..");
        await Assertions.Expect(header).ToContainTextAsync(children > 0 ? $"epic · {children} of {children} open" : state!);
        // The button waits for every read it depends on; an absence asserted before them proves nothing.
        await Until(() => new[] { "/api/runs", "/api/loop/journal", "/api/loop/definition", "/api/pbi/children" }.All(vendor.Asked.Contains));
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Explore with agent" })).ToBeVisibleAsync();

        foreach (var name in new[] { "Release hold", "Stop run", "Move to…" })
        {
            await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true }))
                .ToHaveCountAsync(name == button ? 1 : 0);
        }

        vendor.Writes.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_live_run_says_since_when_and_when_it_wrote_and_is_stopped_after_asking()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var vendor = new Vendor("in-progress", held: false, live: true);
        await Arrange(page, vendor);

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");

        var row = page.GetByRole(AriaRole.Button, new() { Name = "End record of /aio:implement 495" }).Last;
        await Assertions.Expect(row).ToContainTextAsync(new Regex(@"running for 1[23]m · wrote \d+s ago"));
        await Assertions.Expect(row).ToContainTextAsync("running");

        await page.GetByRole(AriaRole.Button, new() { Name = "Stop run" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Stop run" });
        await Assertions.Expect(dialog).ToContainTextAsync("Stopped by a person");
        vendor.Writes.ShouldBeEmpty("the dialog asks before anything is stopped");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Stop run" }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);

        vendor.Writes.ShouldBe([$"POST /api/runs/{LiveRun}/stop {{}}"]);
    }

    [Fact]
    public async Task At_a_narrow_width_the_screen_fits_and_a_run_unfolds_from_the_keyboard()
    {
        await using var context = await frontend.Open("dark");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(600, 900);
        await Arrange(page, new Vendor("merge-review", held: true, live: false));

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");
        var row = page.GetByRole(AriaRole.Button, new() { Name = "End record of /aio:review-diff 495" });
        await Assertions.Expect(row).ToBeVisibleAsync();

        await row.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(row).ToHaveAttributeAsync("aria-expanded", "true");
        // The next stop is the row's own recording, and Enter plays it.
        await page.Keyboard.PressAsync("Tab");
        var play = page.GetByRole(AriaRole.Button, new() { Name = "Play 11m" });
        await Assertions.Expect(play).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Recording of /aio:review-diff 495" })).ToBeVisibleAsync();

        var overflow = await page.EvaluateAsync<int>(
            "(() => { const pane = document.querySelector('main > div'); return pane.scrollWidth - pane.clientWidth; })()"
        );
        overflow.ShouldBeLessThanOrEqualTo(0, "nothing on the screen is wider than the pane at 600px");

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath("pbi-narrow-dark"), FullPage = true });
    }

    [Theory]
    [InlineData("light", 1440)]
    [InlineData("light", 390)]
    public async Task A_running_gate_is_a_row_under_its_state_without_a_recording(string theme, int width)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, 1000);
        await Arrange(page, new Vendor("verifying", held: false, live: false) { Gating = true });

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");

        // #597. Shaped as a live run's row, under verifying, and it does not unfold: there is nothing to replay.
        var gate = page.GetByRole(AriaRole.Group, new() { Name = "Script gate, running" });
        await Assertions.Expect(gate).ToContainTextAsync("running for 3m");
        await Assertions.Expect(gate.GetByRole(AriaRole.Button)).ToHaveCountAsync(0);
        var verifying = page.GetByRole(AriaRole.Listitem).Filter(new() { Has = page.GetByText("verifying", new() { Exact = true }) });
        await Assertions.Expect(verifying.GetByRole(AriaRole.Group, new() { Name = "Script gate, running" })).ToBeVisibleAsync();

        await gate.ScrollIntoViewIfNeededAsync();
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"journey-script-{theme}-{width}") });
    }

    /// <summary>
    /// #617. With <c>/api/pbi</c> held unanswered, the open tab's read, its chunk, and the runs,
    /// journal and loop are all asked for: none of them waits for the PBI. Released, the tab paints.
    /// </summary>
    [Fact]
    public async Task The_tab_asks_for_its_read_without_waiting_for_the_PBI()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await Arrange(page, new Vendor("proposal-review", held: false, live: false));

        var asked = new ConcurrentQueue<string>();
        page.Request += (_, request) => asked.Enqueue(new Uri(request.Url).AbsolutePath);
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pbiPending = true;
        bool? proposalWhilePending = null;
        // Routes added later are asked first; these two answer or hold, and hand the rest back.
        await page.RouteAsync(
            new Regex(@"/api/pbi\?"),
            async route =>
            {
                await held.Task;
                await route.FallbackAsync();
            }
        );
        await page.RouteAsync(
            new Regex(@"/api/pbi/proposal\?"),
            route =>
            {
                proposalWhilePending ??= pbiPending;
                return Json(route, new
                {
                    pullRequestId = (string?)null,
                    pullRequestUrl = (string?)null,
                    head = (string?)null,
                    change = (string?)null,
                    root = (string?)null,
                    missing = "#495 has no pull request yet.",
                    files = Array.Empty<object>(),
                    comments = Array.Empty<object>(),
                });
            }
        );

        await page.GotoAsync($"/pbis/{FolderId}/495/proposal");

        string[] expected = ["/api/pbi/proposal", "/api/runs", "/api/loop/journal", "/api/loop/definition", "/features/pbi/tabs/ProposalTab.tsx"];
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (expected.Except(asked).Any() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
        expected.Except(asked).ShouldBeEmpty("each is asked for while /api/pbi has not answered");
        proposalWhilePending.ShouldBe(true);
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Declare the loop as a state machine" })).ToHaveCountAsync(0);

        pbiPending = false;
        held.SetResult();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Declare the loop as a state machine" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("#495 has no pull request yet.")).ToBeVisibleAsync();
    }

    /// <summary>
    /// #627. Once the Journey has painted, the Issue, Proposal and Review reads and their chunks are
    /// asked for in the background, and a click on Proposal paints from the cache without asking again.
    /// </summary>
    [Fact]
    public async Task The_other_tabs_are_read_while_the_Journey_is_open_and_Proposal_paints_from_the_cache()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await Arrange(page, new Vendor("proposal-review", held: false, live: false));

        var asked = new ConcurrentQueue<string>();
        page.Request += (_, request) => asked.Enqueue(new Uri(request.Url).AbsolutePath);
        await page.RouteAsync(
            new Regex(@"/api/pbi/proposal\?"),
            route => Json(route, new
            {
                pullRequestId = (string?)null,
                pullRequestUrl = (string?)null,
                head = (string?)null,
                change = (string?)null,
                root = (string?)null,
                missing = "#495 has no pull request yet.",
                files = Array.Empty<object>(),
                comments = Array.Empty<object>(),
            })
        );

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Journey" }).Locator("li[aria-current=step]")).ToContainTextAsync("proposal-review");

        string[] expected =
        [
            "/api/pbi/issue",
            "/api/pbi/children",
            "/api/pbi/proposal",
            "/api/pbi/review",
            "/features/pbi/tabs/IssueTab.tsx",
            "/features/pbi/tabs/ProposalTab.tsx",
            "/features/pbi/tabs/ReviewTab.tsx",
        ];
        await Until(() => !expected.Except(asked).Any(), seconds: 15);
        expected.Except(asked).ShouldBeEmpty("each is asked for while the Journey is open");
        asked.ShouldNotContain("/api/files", "Files reads the local checkout and waits for its click");
        var proposals = asked.Count(path => path == "/api/pbi/proposal");

        var tabs = page.GetByRole(AriaRole.Navigation, new() { Name = "PBI tabs" });
        await tabs.GetByRole(AriaRole.Link, new() { Name = "Proposal", Exact = false }).ClickAsync();
        await Assertions.Expect(page.GetByText("#495 has no pull request yet.")).ToBeVisibleAsync();
        asked.Count(path => path == "/api/pbi/proposal").ShouldBe(proposals, "the click reads the cache, not the vendor");
    }

    /// <summary>
    /// #726. A repository that plans in the issue: with no OpenSpec change, the tab shows the latest
    /// <c>## Plan</c> comment rendered, with its author, the earlier one linked, and the decisions.
    /// </summary>
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task Without_an_OpenSpec_change_the_tab_shows_the_latest_plan_comment(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await Arrange(page, new Vendor("proposal-review", held: true, live: false));
        await page.RouteAsync(
            new Regex(@"/api/pbi/proposal\?"),
            route => Json(route, new
            {
                pullRequestId = "49",
                pullRequestUrl = "https://github.com/owner/sandbox/pull/49",
                head = "8e08fdf44ee6cd5fa505c5c6df85a7fcea12fc76",
                change = (string?)null,
                root = (string?)null,
                missing = (string?)null,
                files = Array.Empty<object>(),
                comments = Array.Empty<object>(),
                plan = new
                {
                    id = "203",
                    author = "coder",
                    createdAt = DateTimeOffset.UtcNow.AddHours(-1),
                    body = "## Plan\n\n**What changes.** The card of the warmest day stands out.\n\n**Files.**\n\n- `src/App.tsx`\n- `src/Card.tsx`",
                    url = "https://github.com/owner/sandbox/issues/495#issuecomment-203",
                    images = Array.Empty<string>(),
                },
                earlierPlans = new[]
                {
                    new
                    {
                        id = "201",
                        author = "coder",
                        createdAt = DateTimeOffset.UtcNow.AddHours(-3),
                        body = "## Plan\n\nFirst pass.",
                        url = "https://github.com/owner/sandbox/issues/495#issuecomment-201",
                        images = Array.Empty<string>(),
                    },
                },
            })
        );

        await page.GotoAsync($"/pbis/{FolderId}/495/proposal");

        var plan = page.GetByRole(AriaRole.Article, new() { Name = "Plan" });
        await Assertions.Expect(plan.GetByRole(AriaRole.Heading, new() { Name = "Plan" })).ToBeVisibleAsync();
        await Assertions.Expect(plan).ToContainTextAsync("The card of the warmest day stands out.");
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "## Plan comment by coder", Exact = false }))
            .ToHaveAttributeAsync("href", "https://github.com/owner/sandbox/issues/495#issuecomment-203");
        await Assertions.Expect(page.GetByText("1 earlier:")).ToBeVisibleAsync();
        await Assertions.Expect(page.Locator("a[href$='#issuecomment-201']")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Approve" })).ToBeEnabledAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Request changes" })).ToBeVisibleAsync();
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"proposal-plan-{theme}"), FullPage = true });
    }

    /// <summary>
    /// #627. Entering the PBI again shows what was read at once, with <c>/api/pbi</c> held unanswered,
    /// and asks for it again behind the screen. A change of tab asks for nothing.
    /// </summary>
    [Fact]
    public async Task Entering_the_PBI_again_shows_what_was_read_and_reads_it_again_behind_it()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await Arrange(page, new Vendor("proposal-review", held: false, live: false));

        var asked = new ConcurrentQueue<string>();
        page.Request += (_, request) => asked.Enqueue(new Uri(request.Url).AbsolutePath);
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var hold = false;
        await page.RouteAsync(
            new Regex(@"/api/pbi\?"),
            async route =>
            {
                if (hold)
                {
                    await held.Task;
                }
                await route.FallbackAsync();
            }
        );

        // The clock is the test's, as in the journal test below: the polls of runs and journal fire
        // when it says, so every `/api/pbi` counted here was asked for by an entry.
        await page.Clock.InstallAsync();

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");
        var title = page.GetByRole(AriaRole.Heading, new() { Name = "Declare the loop as a state machine" });
        await Assertions.Expect(title).ToBeVisibleAsync();
        var tabs = page.GetByRole(AriaRole.Navigation, new() { Name = "PBI tabs" });
        await tabs.GetByRole(AriaRole.Link, new() { Name = "Issue", Exact = false }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/495/issue$"));
        await tabs.GetByRole(AriaRole.Link, new() { Name = "Journey", Exact = false }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/495/journey$"));
        asked.Count(path => path == "/api/pbi").ShouldBe(1, "a change of tab is not an entry");

        // Away to another PBI and back, inside the app as the rail does it. The URL changes with the
        // pushState; the screen changes when the router has run its navigation, so the signal that
        // 496 was entered is its read. A back sent before that found 495 still on screen, and
        // neither read happened: 1 of 10 runs on 11 October (#640).
        await page.EvaluateAsync($"() => {{ history.pushState(null, '', '/pbis/{FolderId}/496/journey'); dispatchEvent(new PopStateEvent('popstate')); }}");
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/496/journey$"));
        await Until(() => asked.Count(path => path == "/api/pbi") >= 2);
        asked.Count(path => path == "/api/pbi").ShouldBe(2, "496 is read on entering it");
        hold = true;
        await page.GoBackAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/495/journey$"));

        await Assertions.Expect(title).ToBeVisibleAsync();
        await Until(() => asked.Count(path => path == "/api/pbi") >= 3);
        asked.Count(path => path == "/api/pbi").ShouldBe(3, "495, 496, and 495 again behind the kept copy");
        await Assertions.Expect(page.GetByText("Loading")).ToHaveCountAsync(0);

        held.SetResult();
        await Assertions.Expect(title).ToBeVisibleAsync();
    }

    /// <summary>
    /// #627. The header follows the loop with no revisit and no refresh: a new journal line about the
    /// PBI, read on the journal's own poll, asks <c>/api/pbi</c> again and the header shows the new state.
    /// </summary>
    [Fact]
    public async Task A_new_journal_line_makes_the_header_show_the_new_state()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var vendor = new Vendor("proposal-review", held: false, live: false);
        await Arrange(page, vendor);
        var asked = new ConcurrentQueue<string>();
        page.Request += (_, request) => asked.Enqueue(new Uri(request.Url).AbsolutePath);
        await page.Clock.InstallAsync();

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");
        var header = page.Locator("h1").Locator("xpath=..");
        await Assertions.Expect(header).ToContainTextAsync("proposal-review");
        var reads = asked.Count(path => path == "/api/pbi");

        // The engine moves it: GitHub says in-progress, and the journal has the line.
        vendor.State = "in-progress";
        vendor.Journal.Insert(0, Line(DateTimeOffset.UtcNow, "proposal-review", "moved", "in-progress", "low: the change is small"));
        await page.Clock.FastForwardAsync("00:16");

        await Assertions.Expect(header).ToContainTextAsync("in-progress");
        await Assertions.Expect(header).Not.ToContainTextAsync("proposal-review");
        asked.Count(path => path == "/api/pbi").ShouldBe(reads + 1, "one read of the PBI, asked by the journal line");
    }

    /// <summary>
    /// #627. The header's refresh button reads the PBI and every tab's vendor read again, each sent
    /// with <c>Cache-Control: no-cache</c> so the server skips its kept copy.
    /// </summary>
    [Theory]
    [InlineData("light", 1440)]
    [InlineData("light", 390)]
    public async Task The_refresh_button_reads_every_vendor_read_again_past_the_server_copy(string theme, int width)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, 900);
        await Arrange(page, new Vendor("proposal-review", held: false, live: false));

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Journey" }).Locator("li[aria-current=step]")).ToContainTextAsync("proposal-review");
        var refresh = page.GetByRole(AriaRole.Button, new() { Name = "Read #495 again from GitHub" });
        await Assertions.Expect(refresh).ToBeVisibleAsync();

        var fresh = new ConcurrentQueue<string>();
        page.Request += (_, request) =>
        {
            if (request.Headers.TryGetValue("cache-control", out var control) && control.Contains("no-cache"))
            {
                fresh.Enqueue(new Uri(request.Url).AbsolutePath);
            }
        };
        await refresh.FocusAsync();
        await page.Keyboard.PressAsync("Enter");

        string[] expected = ["/api/pbi", "/api/pbi/issue", "/api/pbi/children", "/api/pbi/proposal", "/api/pbi/review"];
        await Until(() => !expected.Except(fresh).Any());
        expected.Except(fresh).ShouldBeEmpty("the button reads each again with Cache-Control: no-cache");
        fresh.ShouldNotContain("/api/runs", "the live reads keep their own polling");
        await Assertions.Expect(refresh).ToHaveAttributeAsync("aria-busy", "false");

        await page.Locator("h1").Locator("xpath=../..").ScreenshotAsync(new() { Path = frontend.CapturePath($"pbi-refresh-{theme}-{width}") });
    }

    /// <summary>
    /// #696. An instance whose database has no run and no line of the PBI, the installed one or a
    /// second laptop, draws the journey the engine of another instance wrote on the issue: the states
    /// visited, the run with its cost and duration, and the lines, under a line that says where they
    /// came from. #697: the run's recording plays from its copy on <c>aio/evidence</c>, at the path
    /// the comment names, through the server.
    /// </summary>
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task With_nothing_of_its_own_the_Journey_reads_the_journey_comment_of_the_issue(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var at = DateTimeOffset.UtcNow.AddHours(-4);
        string When(int minutes) => at.AddMinutes(minutes).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", System.Globalization.CultureInfo.InvariantCulture);
        var comment = $$"""
            <!-- aio:journey -->
            ### Journey

            What the loop did with this PBI: one row per transition and per finished run, oldest first.

            | When | State | Kind | To | Agent or script | Result | Why | Took | Cost | Recording |
            | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
            | {{When(0)}} | in-progress | run |  | /aio:implement 495 · claude-code · claude-opus-5-5 | next | implemented, with a \| in it | 1h 2m | $3.34 | {{EvidencePath}} |
            | {{When(63)}} | in-progress | moved | verifying |  |  | next: implemented |  |  |
            | {{When(80)}} | verifying | moved | code-review | gate, evidence |  | green: 412 passed |  |  |
            | {{When(130)}} | code-review | moved | merge-review |  |  | approved: the diff does what the issue asks |  |  |
            | {{When(131)}} | merge-review | moved | retro | diff-risk |  | low: two files, no port |  |  |
            """;
        await Arrange(page, new Vendor("retro", held: false, live: false) { JourneyComment = comment });

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");

        await Assertions.Expect(page.GetByText("From the issue, as another instance wrote it")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Open the comment" }))
            .ToHaveAttributeAsync("href", "https://github.com/owner/sandbox/issues/495#issuecomment-901");
        var journey = page.GetByRole(AriaRole.List, new() { Name = "Journey" });
        await Assertions.Expect(journey.Locator("li[aria-current=step]")).ToContainTextAsync("retro");
        await Assertions.Expect(journey.GetByRole(AriaRole.Img, new() { Name = "Visited" })).ToHaveCountAsync(4);
        foreach (var (state, line) in new[]
        {
            ("in-progress", "moved → verifying"),
            ("verifying", "moved → code-review"),
            ("code-review", "moved → merge-review"),
            ("merge-review", "moved → retro"),
        })
        {
            var step = journey.Locator(":scope > li").Filter(new() { Has = page.GetByText(state, new() { Exact = true }) });
            await Assertions.Expect(step.GetByRole(AriaRole.Img, new() { Name = "Visited" })).ToBeVisibleAsync();
            await Assertions.Expect(step.GetByRole(AriaRole.List, new() { Name = "What the engine decided here" })).ToContainTextAsync(line);
        }

        var run = page.GetByRole(AriaRole.Button, new() { Name = "End record of /aio:implement 495" });
        await Assertions.Expect(run).ToContainTextAsync("1h 2m");
        await Assertions.Expect(run).ToContainTextAsync("$3.34");
        await Assertions.Expect(run).ToContainTextAsync("next");
        await run.ClickAsync();
        await Assertions.Expect(run.Locator("xpath=..")).ToContainTextAsync("implemented, with a | in it");
        await Assertions.Expect(run.Locator("xpath=..")).ToContainTextAsync("verifying");

        await run.Locator("xpath=..").GetByRole(AriaRole.Button, new() { Name = "Play" }).ClickAsync();
        var player = page.GetByRole(AriaRole.Region, new() { Name = "Recording of /aio:implement 495" });
        await Assertions.Expect(player.Locator(".ap-player")).ToBeVisibleAsync();

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"journey-from-issue-{theme}"), FullPage = true });
    }

    /// <summary>What the stub serves for one PBI, and every write the screen sent.</summary>
    sealed class Vendor(string? state, bool held, bool live)
    {
        public string? State { get; set; } = state;

        /// <summary>Whether the issue is closed in the vendor (#720).</summary>
        public bool Closed { get; init; }

        /// <summary>How many open sub-issues the PBI has; any makes it an epic (#720).</summary>
        public int Children { get; init; }

        /// <summary>Every GET path the stub has answered.</summary>
        public ConcurrentBag<string> Asked { get; } = [];

        public bool Held { get; set; } = held;

        public bool Live { get; } = live;

        /// <summary>The engine is running <c>gate</c> on the PBI, in verifying, for three minutes (#597).</summary>
        public bool Gating { get; init; }

        public List<object> Journal { get; } = [];

        public ConcurrentQueue<string> Writes { get; } = new();

        /// <summary>The issue's <c>aio:journey</c> comment, with no runs or lines of this instance (#696).</summary>
        public string? JourneyComment { get; init; }
    }

    static async Task Arrange(IPage page, Vendor vendor)
    {
        var now = DateTimeOffset.UtcNow;
        if (vendor.JourneyComment is null)
        {
            vendor.Journal.AddRange(
            [
                Line(now.AddMinutes(-30), "merge-review", "held", "retro", "El riesgo del diff salió alto: touches the IPbiProvider port"),
                Line(now.AddMinutes(-31), "code-review", "moved", "merge-review", "approved: the diff does what the issue asks"),
                Line(now.AddMinutes(-45), "code-review", "started", "code-review", "asked for /aio:review-diff 495 with copilot"),
                Line(now.AddMinutes(-46), "verifying", "moved", "code-review", "green: 412 passed"),
                Line(now.AddHours(-2), "in-progress", "moved", "verifying", "next: implemented"),
            ]);
        }

        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var request = route.Request;
                var path = new Uri(request.Url).AbsolutePath;
                if (request.Method != "GET")
                {
                    vendor.Writes.Enqueue($"{request.Method} {path} {request.PostData}");
                    if (path == "/api/pbi/release")
                    {
                        var released = Line(DateTimeOffset.UtcNow, vendor.State!, "released", "retro", "a person released the hold");
                        vendor.Journal.Insert(0, released);
                        vendor.State = "retro";
                        vendor.Held = false;
                        await Json(route, released);
                        return;
                    }
                    await route.FulfillAsync(new() { Status = 204 });
                    return;
                }

                // #697. The copy on aio/evidence, asked for by the repository and the path the comment names.
                if (path == "/api/pbi/recording"
                    && System.Web.HttpUtility.ParseQueryString(new Uri(request.Url).Query) is var asked
                    && asked["repository"] == Repository
                    && asked["path"] == EvidencePath)
                {
                    await route.FulfillAsync(new()
                    {
                        ContentType = "application/x-asciicast",
                        Body = "{\"version\": 2, \"width\": 80, \"height\": 6}\n[0.1, \"o\", \"implementing\\r\\n\"]\n",
                    });
                    return;
                }

                if (path == $"/api/runs/{ReviewRun}/recording")
                {
                    await route.FulfillAsync(new()
                    {
                        ContentType = "application/x-asciicast",
                        Body = "{\"version\": 2, \"width\": 80, \"height\": 6}\n[0.1, \"o\", \"reviewing the diff\\r\\n\"]\n[0.3, \"o\", \"approved\\r\\n\"]\n",
                    });
                    return;
                }

                object response = path switch
                {
                    "/api/folders" => Folders,
                    "/api/pbis" => new { pbis = new[] { Pbi(vendor) }, unread = Array.Empty<object>() },
                    "/api/sessions" => Array.Empty<object>(),
                    "/api/pbi" => new
                    {
                        pbi = Pbi(vendor),
                        pullRequest = new { id = "512", url = "https://github.com/owner/sandbox/pull/512", isDraft = false, isMerged = false, isApproved = true },
                    },
                    "/api/runs" => vendor.JourneyComment is null ? Runs(now, vendor.Live) : (object[])[],
                    "/api/loop/journal" => vendor.Journal,
                    "/api/loop/definition" => Loop,
                    // The Issue tab's two reads in the shape of the contract: it maps the children and the
                    // issue's lists, and `{}` threw inside the tab whenever an answer landed while it was
                    // mounted, which it did 5 times in 10 on 11 October (#640).
                    "/api/pbi/issue" => new
                    {
                        url = "https://github.com/owner/sandbox/issues/495",
                        author = "dev",
                        createdAt = now.AddDays(-1),
                        body = "Declare the loop as a state machine.",
                        labels = Array.Empty<string>(),
                        assignees = Array.Empty<string>(),
                        parent = (object?)null,
                        blockedBy = Array.Empty<object>(),
                        comments = vendor.JourneyComment is { } journey
                            ? new object[]
                            {
                                new
                                {
                                    id = "901",
                                    author = "aio-bot",
                                    createdAt = now,
                                    body = journey,
                                    url = "https://github.com/owner/sandbox/issues/495#issuecomment-901",
                                    harness = "journey",
                                },
                            }
                            : [],
                        images = Array.Empty<object>(),
                    },
                    "/api/pbi/children" => Enumerable.Range(1, vendor.Children)
                        .Select(n => new { repository = Repository, id = $"{600 + n}", title = $"Part {n}", state = (string?)null, held = false, closed = false })
                        .ToArray(),
                    "/api/loop/present" => new
                    {
                        liveScripts = vendor.Gating
                            ? new object[] { new { repository = Repository, pbi = "495", state = "verifying", script = "gate", since = now.AddMinutes(-3).AddSeconds(-5), stale = false } }
                            : [],
                    },
                    _ => new { },
                };
                await Json(route, response);
                vendor.Asked.Add(path);
            }
        );
    }

    /// <summary>
    /// Waits for the signal a test counts, up to <paramref name="seconds"/>; the assertion after it
    /// says what was expected. The requests of a screen have no locator for <c>Expect</c> to wait on.
    /// </summary>
    static async Task Until(Func<bool> done, int seconds = 10)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (!done() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    static Task Json(IRoute route, object body) =>
        route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(body) });

    static object Line(DateTimeOffset at, string from, string kind, string? to, string why) =>
        new { at, repository = Repository, pbi = "495", from, kind, to, why };

    static object Pbi(Vendor vendor) => new
    {
        vendor = "github",
        repository = Repository,
        id = "495",
        title = "Declare the loop as a state machine",
        state = vendor.State,
        held = vendor.Held,
        assignee = (string?)null,
        closed = vendor.Closed,
        folderPath = "/checkouts/sandbox",
    };

    static object[] Runs(DateTimeOffset now, bool live)
    {
        var runs = new List<object>
        {
            Run(ImplementRun, "in-progress", false, "claude-code", "/aio:implement 495", now.AddHours(-3), now.AddHours(-2), "next", null, 3.34m),
            Run(ReviewRun, "code-review", false, "copilot", "/aio:review-diff 495", now.AddMinutes(-45), now.AddMinutes(-34), "approved",
                """{"outcome": "approved", "reason": "the diff does what the issue asks", "problems": ["2 comments posted on PR #512"]}""", 0.41m),
            Run("01a11dcb-dec7-78dd-8f4c-70a254ded204", "merge-review", true, null, "", now.AddMinutes(-20), now.AddMinutes(-18), null, null, null),
        };
        if (live)
        {
            runs.Add(new
            {
                runId = LiveRun,
                state = "in-progress",
                manual = false,
                provider = "claude-code",
                model = "claude-opus-5-5",
                command = "/aio:implement 495",
                startedAt = now.AddMinutes(-12).AddSeconds(-5),
                endedAt = (DateTimeOffset?)null,
                lastOutputAt = DateTimeOffset.UtcNow.AddSeconds(-4),
                stale = false,
                outcome = (string?)null,
                reason = (string?)null,
                failure = (string?)null,
                problems = Array.Empty<string>(),
                endRecord = (string?)null,
                inputTokens = (long?)null,
                outputTokens = (long?)null,
                costUsd = (decimal?)null,
                recordingPath = "/runs/live/session.cast",
                recorded = true,
            });
        }
        return [.. runs];
    }

    static object Run(
        string runId,
        string state,
        bool manual,
        string? provider,
        string command,
        DateTimeOffset startedAt,
        DateTimeOffset endedAt,
        string? outcome,
        string? record,
        decimal? cost
    ) => new
    {
        runId,
        state,
        manual,
        provider,
        model = provider is null ? null : "opus-5-5",
        command,
        startedAt,
        endedAt = (DateTimeOffset?)endedAt,
        lastOutputAt = (DateTimeOffset?)null,
        stale = false,
        outcome,
        reason = outcome is null ? null : "the diff does what the issue asks",
        failure = (string?)null,
        problems = record is null ? Array.Empty<string>() : ["2 comments posted on PR #512"],
        endRecord = record,
        inputTokens = cost is null ? null : (long?)38210,
        outputTokens = cost is null ? null : (long?)2904,
        costUsd = cost,
        recordingPath = $"/runs/{runId}/session.cast",
        recorded = runId == ReviewRun,
    };

    static readonly object[] Folders =
    [
        new { id = FolderId, name = "sandbox", path = "/checkouts/sandbox", vendor = "github", repository = Repository, loopYaml = "" },
    ];

    static readonly object Loop = new
    {
        name = "spec-driven-issue-loop",
        agents = new[] { new { role = "coder", provider = "claude-code", model = "claude-opus-5-5" } },
        states = new object[]
        {
            State("ready-for-proposal", agent: "refiner", command: "/aio:refine", transitions: [To("ready", "spec-ready"), To("gaps", "needs-refinement")]),
            State("spec-ready", scripts: ["file-overlap", "claim"], agent: "coder", command: "/aio:propose", transitions: [To(null, "proposal-review")]),
            State("proposal-review", score: "risk-score", transitions: [To("low", "in-progress"), To("high", "in-progress", "El score de riesgo salió alto")], back: "spec-ready", tab: "proposal"),
            State("in-progress", agent: "coder", command: "/aio:implement", transitions: [To(null, "verifying")]),
            State("verifying", scripts: ["gate", "evidence"], transitions: [To("green", "code-review"), To("red", null, "El gate está en rojo")]),
            State("code-review", agent: "reviewer", command: "/aio:review-diff", transitions: [To("rejected", "in-progress"), To("approved", "merge-review")], back: "in-progress", tab: "review"),
            State("merge-review", score: "diff-risk", transitions: [To("low", "retro"), To("high", "retro", "El riesgo del diff salió alto")], back: "in-progress", tab: "review"),
            State("retro", agent: "coder", command: "/aio:review", transitions: [To(null, "merging")]),
            State("merging", scripts: ["archive", "merge"], transitions: [To("merged", "done"), To("conflict", "rebasing")]),
            State("rebasing", agent: "coder", command: "/rebase-safely", transitions: [To(null, "merging")]),
        },
        undeclared = new[] { "needs-refinement", "done" },
        problem = (string?)null,
    };

    static object State(
        string name,
        string[]? scripts = null,
        string? agent = null,
        string? command = null,
        string? score = null,
        object[]? transitions = null,
        string? back = null,
        string? tab = null
    ) => new
    {
        name,
        scripts = scripts ?? [],
        agent,
        command,
        score,
        provider = (string?)null,
        model = (string?)null,
        maxLoops = (int?)null,
        transitions = transitions ?? [],
        back,
        tab,
    };

    static object To(string? outcome, string? target, string? hold = null) => new { outcome, target, hold };
}
