using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #513. The Review tab: the pull request with its base and head, the files, the diff of the file
// chosen with its threads under their lines, the checks of the head, the quality (#746) and the
// evidence captures, in that order; a thread written on a line, a reply and a resolve; Approve, which the loop answers by
// going on to retro; and Request changes behind a dialog that asks what has to change.
//
// The API is stubbed at the browser, like PbiScreenTests: what is under test is the screen's
// reading of the contract and the writes it sends. The writes themselves are covered against the
// host by ReviewVerdictTests and ReviewTabTests, and against GitHub's own answers by
// GitHubPbiProviderReviewTests.
[Collection("review-frontend")]
public sealed class ReviewTabTests(ReviewFrontendFixture frontend)
{
    const string FolderId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c6";
    const string Repository = "owner/sandbox";
    const string Head = "9b01a3c4d5e6f708192a3b4c5d6e7f8091a2b3c4";
    const string Image = "https://github.com/user-attachments/assets/8efe4e72-07ae-4422-aff0-54cb3a7b5dec";

    /// <summary>A 1×1 PNG, the answer of the attachment route.</summary>
    static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChwGA60e6kgAAAABJRU5ErkJggg=="
    );

    [Theory]
    [InlineData("light")]
    public async Task The_review_reads_in_order_and_a_thread_a_reply_a_resolve_and_an_approval_are_written(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var vendor = new Vendor();
        await Arrange(page, vendor);

        await page.GotoAsync($"/pbis/{FolderId}/495/review");

        // The pull request: base, head, commits, lines, and the two verdicts.
        await Assertions.Expect(page.GetByText("base: main@4f9c2e1")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("head: change/495@9b01a3c")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("41 commits")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Approve writes the approval on PR #512")).ToBeVisibleAsync();

        // In order: files, diff, gate, quality, evidence.
        var headings = await page.Locator("main h2").AllInnerTextsAsync();
        headings.ShouldBe([
            "Files · 2",
            "Diff · src/Ports/IPbiProvider.cs",
            "Gate · on 9b01a3c",
            "Quality · on 9b01a3c",
            "Evidence · 1 capture on issue #495 against 9b01a3c",
        ]);

        // The thread sits under its line, with its comments in order.
        var thread = page.GetByRole(AriaRole.Region, new() { Name = "Thread on line 3" });
        await Assertions.Expect(thread).ToContainTextAsync("Does state stay a string on the port?");
        await Assertions.Expect(thread).ToContainTextAsync("the provider maps it");

        // The gate's checks and the evidence image, read through the server.
        var gate = page.GetByRole(AriaRole.List, new() { Name = "Gate · on 9b01a3c" });
        await Assertions.Expect(gate.Locator("li")).ToHaveCountAsync(2);
        await Assertions.Expect(gate.Locator("li").First).ToContainTextAsync("PASS");
        await Assertions.Expect(gate.Locator("li").Last).ToContainTextAsync("FAIL");
        var capture = page.GetByRole(AriaRole.Img, new() { Name = "settings at 1440×900, light theme" });
        await Assertions.Expect(capture).ToBeVisibleAsync();
        (await capture.EvaluateAsync<int>("image => image.naturalWidth")).ShouldBe(1, "the image is the bytes the attachment route answered");
        vendor.Images.ShouldContain(Image);

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"review-{theme}"), FullPage = true });

        // A new thread on line 4: the gutter number opens the composer.
        await page.GetByRole(AriaRole.Button, new() { Name = "Comment on line 4" }).ClickAsync();
        var composer = page.GetByRole(AriaRole.Form, new() { Name = "New thread on src/Ports/IPbiProvider.cs:4" });
        await composer.GetByRole(AriaRole.Textbox).FillAsync("Name the comment's author too.");
        await composer.GetByRole(AriaRole.Button, new() { Name = "Comment" }).ClickAsync();
        await Assertions.Expect(composer).ToHaveCountAsync(0);

        // A reply and a resolve on the thread of line 3.
        await thread.GetByRole(AriaRole.Textbox, new() { Name = "Reply to the thread on line 3" }).FillAsync("Agreed.");
        await thread.GetByRole(AriaRole.Button, new() { Name = "Reply" }).ClickAsync();
        await Assertions.Expect(thread.GetByRole(AriaRole.Textbox)).ToHaveValueAsync("");
        await thread.GetByRole(AriaRole.Button, new() { Name = "Resolve" }).ClickAsync();

        // Approve: written at the vendor, and the loop goes on to retro.
        await page.GetByRole(AriaRole.Button, new() { Name = "Approve" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync(
            "Approved on PR #512. The loop moved #495 from merge-review to retro."
        );

        vendor.Writes.ShouldBe([
            $$"""POST /api/pbi/review/comment {"repository":"{{Repository}}","id":"495","path":"src/Ports/IPbiProvider.cs","line":4,"body":"Name the comment's author too."}""",
            $$"""POST /api/pbi/review/reply {"repository":"{{Repository}}","id":"495","commentId":"901","body":"Agreed."}""",
            $$"""POST /api/pbi/review/resolve {"repository":"{{Repository}}","id":"495","threadId":"PRRT_1"}""",
            $$"""POST /api/pbi/review/approve {"repository":"{{Repository}}","id":"495","body":null}""",
        ]);
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"review-approved-{theme}"), FullPage = true });
    }

    [Fact]
    public async Task Request_changes_asks_what_has_to_change_and_a_refusal_is_said_in_the_vendors_words()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var vendor = new Vendor { RefuseApproval = "The approval of pull request 512 was not written: Can not approve your own pull request" };
        await Arrange(page, vendor);

        await page.GotoAsync($"/pbis/{FolderId}/495/review");

        await page.GetByRole(AriaRole.Button, new() { Name = "Approve" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Alert)).ToContainTextAsync(
            "Nothing was written. The approval of pull request 512 was not written: Can not approve your own pull request"
        );

        await page.GetByRole(AriaRole.Button, new() { Name = "Request changes" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Request changes" });
        var send = dialog.GetByRole(AriaRole.Button, new() { Name = "Request changes" });
        await Assertions.Expect(send).ToBeDisabledAsync();
        await dialog.GetByRole(AriaRole.Textbox, new() { Name = "What has to change" }).FillAsync("The port leaks a label.");
        await send.ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync(
            "Changes requested on PR #512. The loop moved #495 from merge-review to in-progress."
        );

        vendor.Writes.Last().ShouldBe(
            $$"""POST /api/pbi/review/request-changes {"repository":"{{Repository}}","id":"495","body":"The port leaks a label."}"""
        );
    }

    [Theory]
    [InlineData("light")]
    public async Task An_approval_GitHub_took_only_as_a_comment_says_so_and_why(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var vendor = new Vendor { AsComment = "Review Can not approve your own pull request" };
        await Arrange(page, vendor);

        await page.GotoAsync($"/pbis/{FolderId}/495/review");
        await page.GetByRole(AriaRole.Button, new() { Name = "Approve" }).ClickAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToContainTextAsync(
            "Approved on PR #512 as a comment, because GitHub refused the approval: Review Can not approve your own pull request. "
                + "The loop moved #495 from merge-review to retro."
        );
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"review-approved-as-comment-{theme}"), FullPage = true });
    }

    /// <summary>#595. A state without <c>back</c> in the loop offers no Request changes, and says why.</summary>
    [Fact]
    public async Task A_state_without_back_offers_no_request_for_changes()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await Arrange(page, new Vendor { State = "verifying" });

        await page.GotoAsync($"/pbis/{FolderId}/495/review");

        await Assertions.Expect(page.GetByText("verifying declares no back in the loop")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Approve" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Request changes" })).ToHaveCountAsync(0);
    }

    /// <summary>#719. A merged pull request leaves nothing to decide: no verdicts, and one line that says so.</summary>
    [Fact]
    public async Task A_merged_pull_request_offers_no_verdict_and_says_nothing_is_left_to_decide()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await Arrange(page, new Vendor { State = "done", Merged = true });

        await page.GotoAsync($"/pbis/{FolderId}/495/review");

        await Assertions.Expect(page.GetByText("PR #512 is merged. Nothing is left to decide here.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Approve writes the approval")).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Approve" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Request changes" })).ToHaveCountAsync(0);
    }

    /// <summary>
    /// #719. GitHub reports no checks, and this machine's journal has the gate's ran line for the PBI: the
    /// Gate section says what it said, with its log. Without the line it says where the gate runs.
    /// </summary>
    [Fact]
    public async Task With_no_checks_the_gate_says_what_this_machine_ran()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        await Arrange(page, new Vendor { NoChecks = true, GateRan = "gate: aspire do ci passed: 10/10 steps succeeded" });

        await page.GotoAsync($"/pbis/{FolderId}/495/review");

        var gate = page.GetByRole(AriaRole.Region, new() { Name = "Gate · on 9b01a3c" });
        await Assertions.Expect(gate).ToContainTextAsync("The gate ran on this machine at");
        await Assertions.Expect(gate).ToContainTextAsync(": aspire do ci passed: 10/10 steps succeeded");
        await Assertions.Expect(gate.GetByRole(AriaRole.Link, new() { Name = "Log" })).ToHaveAttributeAsync("href", "/api/loop/journal/77/output");
        await Assertions.Expect(gate).Not.ToContainTextAsync("The gate runs on this machine in verifying");

        var other = await context.NewPageAsync();
        await other.SetViewportSizeAsync(1440, 1000);
        await Arrange(other, new Vendor { NoChecks = true });
        await other.GotoAsync($"/pbis/{FolderId}/495/review");
        await Assertions.Expect(other.GetByRole(AriaRole.Region, new() { Name = "Gate · on 9b01a3c" })).ToContainTextAsync(
            "GitHub reports no checks on 9b01a3c. The gate runs on this machine in verifying, and the Journey shows what it said."
        );
    }

    /// <summary>
    /// #746. The quality between the gate and the evidence, at 390 in both themes: a dot and a row per
    /// check, the reason beside one not measured, and a measure of an earlier commit said as such.
    /// Without a comment the block says the change has no measure yet.
    /// </summary>
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task The_quality_shows_each_check_with_its_result_in_both_themes_at_390(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(390, 844);
        await Arrange(page, new Vendor { QualityOn = "5d6e7f8091a2b3c4d5e6f708192a3b4c5d6e7f80" });

        await page.GotoAsync($"/pbis/{FolderId}/495/review");

        var block = page.GetByRole(AriaRole.Region, new() { Name = "Quality · on 5d6e7f8" });
        await Assertions.Expect(block).ToContainTextAsync("Measured on 5d6e7f8, the head is 9b01a3c.");
        var rows = block.GetByRole(AriaRole.List, new() { Name = "Quality of the change" }).Locator("li");
        await Assertions.Expect(rows).ToHaveCountAsync(5);
        await Assertions.Expect(rows.Nth(0)).ToContainTextAsync("Coverage of changed lines25.0%: 1 of 4 changed lines coveredline 70% · over");
        await Assertions.Expect(rows.Nth(2)).ToContainTextAsync("not measured: no summary.json at src/artifacts/mutation/summary.json");
        await Assertions.Expect(rows.Nth(4)).ToContainTextAsync("none lost or skipped");
        var dots = await rows.EvaluateAllAsync<string[]>("rows => rows.map(row => getComputedStyle(row.firstElementChild).backgroundColor)");
        dots[0].ShouldBe(dots[1], "both over are the same red");
        dots[0].ShouldNotBe(dots[4], "over and ok differ");
        dots[2].ShouldNotBe(dots[0], "not measured is neither red");
        dots[2].ShouldNotBe(dots[4], "nor green");

        var overflow = await page.EvaluateAsync<int>(
            "(() => { const pane = document.querySelector('main > div'); return pane.scrollWidth - pane.clientWidth; })()"
        );
        overflow.ShouldBeLessThanOrEqualTo(0, "nothing on the tab is wider than the pane at 390px");
        await block.ScrollIntoViewIfNeededAsync();
        await block.ScreenshotAsync(new() { Path = frontend.CapturePath($"review-quality-390-{theme}") });

        var none = await context.NewPageAsync();
        await none.SetViewportSizeAsync(390, 844);
        await Arrange(none, new Vendor { NoQuality = true });
        await none.GotoAsync($"/pbis/{FolderId}/495/review");
        await Assertions.Expect(none.GetByRole(AriaRole.Region, new() { Name = "Quality" })).ToContainTextAsync(
            "#495 has no quality measure yet. The loop's quality step publishes one on the issue in verifying, after the gate."
        );
    }

    [Fact]
    public async Task At_a_narrow_width_the_tab_fits_and_a_file_and_a_line_are_reached_from_the_keyboard()
    {
        await using var context = await frontend.Open("dark");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(600, 900);
        var vendor = new Vendor();
        await Arrange(page, vendor);

        await page.GotoAsync($"/pbis/{FolderId}/495/review");
        var files = page.GetByRole(AriaRole.List, new() { Name = "Files of the pull request" });
        var second = files.GetByRole(AriaRole.Button).Last;
        await second.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(second).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Diff · .aio/loop.yaml" })).ToBeVisibleAsync();

        var line = page.GetByRole(AriaRole.Button, new() { Name = "Comment on line 2" });
        await line.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByRole(AriaRole.Form, new() { Name = "New thread on .aio/loop.yaml:2" })).ToBeVisibleAsync();

        var overflow = await page.EvaluateAsync<int>(
            "(() => { const pane = document.querySelector('main > div'); return pane.scrollWidth - pane.clientWidth; })()"
        );
        overflow.ShouldBeLessThanOrEqualTo(0, "nothing on the tab is wider than the pane at 600px; a long line scrolls inside the diff");

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath("review-narrow-dark"), FullPage = true });
    }

    /// <summary>What the stub serves, and every write the tab sent.</summary>
    sealed class Vendor
    {
        public string State { get; set; } = "merge-review";

        public string? RefuseApproval { get; init; }

        /// <summary>GitHub's sentence when the approval went on as a comment, or null when it did not.</summary>
        public string? AsComment { get; init; }

        public bool Merged { get; init; }

        /// <summary>GitHub reports no checks on the head.</summary>
        public bool NoChecks { get; init; }

        /// <summary>The why of the gate's ran line in this machine's journal, or null when it has none.</summary>
        public string? GateRan { get; init; }

        /// <summary>The commit the quality comment measured, the head unless said.</summary>
        public string QualityOn { get; init; } = Head;

        /// <summary>The issue carries no quality comment.</summary>
        public bool NoQuality { get; init; }

        public ConcurrentQueue<string> Writes { get; } = new();

        public ConcurrentBag<string> Images { get; } = [];
    }

    static async Task Arrange(IPage page, Vendor vendor)
    {
        var now = DateTimeOffset.UtcNow;
        var hold = Line(now.AddMinutes(-30), "merge-review", "held", "retro", "El riesgo del diff salió alto: touches the IPbiProvider port");

        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var request = route.Request;
                var uri = new Uri(request.Url);
                var path = uri.AbsolutePath;
                if (request.Method != "GET")
                {
                    vendor.Writes.Enqueue($"{request.Method} {path} {request.PostData}");
                    switch (path)
                    {
                        case "/api/pbi/review/approve" when vendor.RefuseApproval is { } refusal:
                            await route.FulfillAsync(new()
                            {
                                Status = 409,
                                ContentType = "application/problem+json",
                                Body = JsonSerializer.Serialize(new { title = "Conflict", status = 409, detail = refusal }),
                            });
                            return;
                        case "/api/pbi/review/approve":
                            vendor.State = "retro";
                            await Json(route, new { review = "1", line = Line(DateTimeOffset.UtcNow, "merge-review", "released", "retro", "a person approved the pull request"), unmoved = (string?)null, note = (string?)null, asComment = vendor.AsComment });
                            return;
                        case "/api/pbi/review/request-changes":
                            vendor.State = "in-progress";
                            await Json(route, new { review = "2", line = Line(DateTimeOffset.UtcNow, "merge-review", "released", "in-progress", "a person requested changes on the pull request"), unmoved = (string?)null, note = (string?)null });
                            return;
                        default:
                            await Json(route, new { });
                            return;
                    }
                }

                if (path == "/api/pbi/attachment")
                {
                    vendor.Images.Add(System.Web.HttpUtility.ParseQueryString(uri.Query)["url"] ?? "");
                    await route.FulfillAsync(new() { ContentType = "image/png", BodyBytes = Png });
                    return;
                }

                object response = path switch
                {
                    "/api/folders" => new[]
                    {
                        new { id = FolderId, name = "sandbox", path = "/checkouts/sandbox", vendor = "github", repository = Repository, loopYaml = "" },
                    },
                    "/api/pbis" => new { pbis = new[] { Pbi(vendor) }, unread = Array.Empty<object>() },
                    "/api/sessions" => Array.Empty<object>(),
                    "/api/pbi" => new
                    {
                        pbi = Pbi(vendor),
                        pullRequest = PullRequest(vendor),
                    },
                    "/api/pbi/review" => Review(now, vendor),
                    "/api/runs" => Array.Empty<object>(),
                    "/api/loop/journal" => (object[])[
                        .. vendor.State == "merge-review" ? [hold] : Array.Empty<object>(),
                        .. vendor.GateRan is { } why
                            ? [new { id = 77, at = now.AddHours(-1), repository = Repository, pbi = "495", from = "verifying", kind = "ran", to = (string?)null, why, output = "/data/scripts/owner/sandbox/495/gate-20261014-095000-000.log" }]
                            : Array.Empty<object>(),
                    ],
                    "/api/loop/definition" => Loop,
                    _ => new { },
                };
                await Json(route, response);
            }
        );
    }

    static object PullRequest(Vendor vendor) =>
        new { id = "512", url = "https://github.com/owner/sandbox/pull/512", isDraft = false, isMerged = vendor.Merged, isApproved = false, isClosed = false };

    static object Review(DateTimeOffset now, Vendor vendor) => new
    {
        pullRequest = PullRequest(vendor),
        change = new
        {
            baseRef = "main",
            baseSha = "4f9c2e1aaaabbbbccccddddeeeeffff000011112",
            headRef = "change/495",
            headSha = Head,
            commits = 41,
            additions = 10,
            deletions = 0,
            changedFiles = 2,
            files = new object[]
            {
                new
                {
                    path = "src/Ports/IPbiProvider.cs",
                    previousPath = (string?)null,
                    status = "modified",
                    additions = 2,
                    deletions = 0,
                    patch = "@@ -1,3 +1,5 @@\n public interface IPbiProvider\n {\n+    Task MoveAsync(PbiKey key, string state, CancellationToken ct);\n+    Task CommentAsync(PbiKey key, string body, CancellationToken ct);\n }",
                },
                new
                {
                    path = ".aio/loop.yaml",
                    previousPath = (string?)null,
                    status = "modified",
                    additions = 1,
                    deletions = 0,
                    patch = "@@ -1 +1,2 @@\n name: spec-driven-issue-loop\n+wip: 4",
                },
            },
        },
        threads = new[]
        {
            new
            {
                id = "PRRT_1",
                path = "src/Ports/IPbiProvider.cs",
                line = (int?)3,
                isResolved = false,
                isOutdated = false,
                comments = new[]
                {
                    new { id = "901", author = (string?)"asantamariaplainconcepts", at = now.AddHours(-3), body = "Azure DevOps has a field, not a label. Does state stay a string on the port?" },
                    new { id = "902", author = (string?)"reviewer", at = now.AddMinutes(-40), body = "Yes: the provider maps it." },
                },
            },
        },
        checks = new
        {
            rows = vendor.NoChecks
                ? []
                : new[]
                {
                    new { name = "aspire do ci", state = "passing", url = (string?)null },
                    new { name = "pnpm build", state = "failing", url = (string?)"https://github.com/owner/sandbox/runs/2" },
                },
            problem = (string?)null,
        },
        quality = new
        {
            headSha = vendor.NoQuality ? null : vendor.QualityOn,
            stale = !vendor.NoQuality && vendor.QualityOn != Head,
            author = vendor.NoQuality ? null : "aio-bot",
            at = vendor.NoQuality ? (DateTimeOffset?)null : now.AddMinutes(-45),
            rows = vendor.NoQuality
                ? []
                : new[]
                {
                    new { check = "Coverage of changed lines", measured = "25.0%: 1 of 4 changed lines covered", line = "70%", result = "over" },
                    new { check = "CRAP of touched methods", measured = "highest 42.0 of 1 method, 1 over 30", line = "30", result = "over" },
                    new { check = "Mutation in touched files", measured = "no summary.json at src/artifacts/mutation/summary.json", line = "reported only", result = "not measured" },
                    new { check = "New duplicated lines", measured = "no new duplicated line", line = "0", result = "ok" },
                    new { check = "Tests lost or skipped", measured = "none lost or skipped", line = "0", result = "ok" },
                },
            problem = (string?)null,
        },
        evidence = new
        {
            headSha = Head,
            author = "aio-bot",
            at = now.AddMinutes(-50),
            captures = new[]
            {
                new { name = "settings 1440x900 light", url = Image, screen = "settings", width = 1440, height = 900, theme = "light" },
            },
            problem = (string?)null,
        },
    };

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
        held = vendor.State == "merge-review",
        assignee = (string?)null,
        closed = false,
        folderPath = "/checkouts/sandbox",
    };

    static readonly object Loop = new
    {
        name = "spec-driven-issue-loop",
        agents = new[] { new { role = "coder", provider = "claude-code", model = "claude-opus-5-5" } },
        states = new object[]
        {
            State("in-progress", agent: "coder", command: "/aio:implement", transitions: [To(null, "verifying")]),
            State("verifying", scripts: ["gate", "evidence"], transitions: [To("green", "code-review"), To("red", null, "El gate está en rojo")]),
            State("code-review", agent: "reviewer", command: "/aio:review-diff", transitions: [To("rejected", "in-progress"), To("approved", "merge-review")], back: "in-progress", tab: "review"),
            State("merge-review", score: "diff-risk", transitions: [To("low", "retro"), To("high", "retro", "El riesgo del diff salió alto")], back: "in-progress", tab: "review"),
            State("retro", agent: "coder", command: "/aio:review", transitions: [To(null, "merging")]),
            State("merging", scripts: ["archive", "merge"], transitions: [To("merged", "done")]),
        },
        undeclared = new[] { "done" },
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
