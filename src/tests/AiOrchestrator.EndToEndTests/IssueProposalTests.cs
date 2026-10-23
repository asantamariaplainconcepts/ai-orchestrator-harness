using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #512. The Issue and Proposal tabs of the PBI. Issue: the body with its image, the comments in
// order with the harness's marked and its captures drawn, an image enlarged with a click, and a
// comment written with an image dropped on it sent as the person. Proposal: the change's files with
// line numbers, a comment anchored to a line sent as a review comment on that file and line, and
// Request changes sending the review and the PBI back to spec-ready, which the Journey shows.
//
// The API is stubbed at the browser, like PbiScreenTests: what is under test is the screens'
// reading of the contract and the writes they send. The writes themselves — the comment at the
// vendor, the review comment on the line, the review and the move to spec-ready — are covered
// against the real host by IssueAndProposalTests and RequestChangesTests.
[Collection("review-frontend")]
public sealed partial class IssueProposalTests(ReviewFrontendFixture frontend)
{
    const string FolderId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c6";
    const string Repository = "owner/sandbox";
    const string BodyImage = "https://github.com/user-attachments/assets/0f8a3c1e-1111-4222-8333-944455556666";
    const string Capture = "https://github.com/user-attachments/assets/7b2d9e4f-aaaa-4bbb-8ccc-dddd00001111";
    const string Root = "openspec/changes/loop-states/";

    /// <summary>A 1×1 PNG, so every image the stub serves decodes.</summary>
    static readonly byte[] Png = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="
    );

    [Theory]
    [InlineData("light")]
    public async Task The_issue_shows_the_vendor_s_thread_and_a_comment_with_an_image_is_sent_as_the_person(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var stub = new Stub();
        await Arrange(page, stub);

        await page.GotoAsync($"/pbis/{FolderId}/495/issue");

        await Assertions.Expect(page.GetByText("Declare the loop as states, top to bottom.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Link, new() { Name = "Open in GitHub" })).ToHaveAttributeAsync("href", "https://github.com/owner/sandbox/issues/495");

        var comments = page.GetByRole(AriaRole.List, new() { Name = "Comments" }).Locator(":scope > li");
        await Assertions.Expect(comments).ToHaveCountAsync(2);
        await Assertions.Expect(comments.First).ToContainTextAsync("Confirm done needs no declaring.");
        await Assertions.Expect(comments.Last).ToContainTextAsync("harness · evidence");
        await Assertions.Expect(comments.Last.GetByRole(AriaRole.Button, new() { Name = "Enlarge wide-dark.png" })).ToBeVisibleAsync();

        var facts = page.GetByRole(AriaRole.Complementary, new() { Name = "About this issue" });
        await Assertions.Expect(facts).ToContainTextAsync("status:proposal-review");
        await Assertions.Expect(facts).ToContainTextAsync("#477 Harden the loop");
        await Assertions.Expect(facts).ToContainTextAsync("#482 Before (closed)");

        // An image is served from this origin and enlarges with a click.
        var image = page.GetByRole(AriaRole.Button, new() { Name = "Enlarge the loop today" }).First;
        await Assertions.Expect(image.Locator("img")).ToHaveAttributeAsync("src", $"/api/pbi/attachment?repository=owner%2Fsandbox&id=495&url={Uri.EscapeDataString(BodyImage)}");
        await image.ClickAsync();
        var viewer = page.GetByRole(AriaRole.Dialog, new() { Name = "the loop today" });
        await Assertions.Expect(viewer).ToBeVisibleAsync();
        await viewer.GetByRole(AriaRole.Button, new() { Name = "Close" }).Last.ClickAsync();
        await Assertions.Expect(viewer).ToHaveCountAsync(0);

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"issue-{theme}"), FullPage = true });

        // A comment with an image dropped on it.
        var composer = page.GetByRole(AriaRole.Form, new() { Name = "Comment on the issue" });
        await composer.GetByRole(AriaRole.Textbox).FillAsync("The header overlaps at 390.");
        await composer.Locator("input[type=file]").SetInputFilesAsync(new FilePayload { Name = "narrow.png", MimeType = "image/png", Buffer = Png });
        await Assertions.Expect(composer.GetByRole(AriaRole.List, new() { Name = "Images to attach" })).ToContainTextAsync("narrow.png");
        await composer.GetByRole(AriaRole.Button, new() { Name = "Comment", Exact = true }).ClickAsync();

        await Assertions.Expect(comments).ToHaveCountAsync(3);
        await Assertions.Expect(comments.Last).ToContainTextAsync("The header overlaps at 390.");
        var sent = JsonDocument.Parse(stub.Writes.Single(write => write.Path == "/api/pbi/issue/comment").Body).RootElement;
        sent.GetProperty("body").GetString().ShouldBe("The header overlaps at 390.");
        sent.GetProperty("attachments")[0].GetProperty("name").GetString().ShouldBe("narrow.png");
        Convert.FromBase64String(sent.GetProperty("attachments")[0].GetProperty("base64").GetString()!).ShouldBe(Png);
        await Assertions.Expect(composer.GetByRole(AriaRole.Textbox)).ToHaveValueAsync("");
    }

    [Theory]
    [InlineData("light")]
    public async Task A_line_of_the_proposal_takes_a_review_comment_and_request_changes_sends_the_PBI_back_to_spec_ready(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var stub = new Stub();
        await Arrange(page, stub);

        await page.GotoAsync($"/pbis/{FolderId}/495/proposal");

        var files = page.GetByRole(AriaRole.Navigation, new() { Name = "Files of the change" });
        await Assertions.Expect(files.GetByRole(AriaRole.Button)).ToHaveTextAsync(["proposal.md1", "design.md", "specs/loop/spec.md+2 ~1", "tasks.md1 / 3", "states.png"]);
        await Assertions.Expect(page.GetByText("tasks 1 of 3")).ToBeVisibleAsync();

        var lines = page.GetByRole(AriaRole.List, new() { Name = "Lines of proposal.md" }).Locator(":scope > li");
        await Assertions.Expect(lines.Nth(0)).ToContainTextAsync("1");
        await Assertions.Expect(lines.Nth(2)).ToContainTextAsync("5");
        await Assertions.Expect(lines.Nth(2)).ToContainTextAsync("Why only this?", new() { Timeout = 5_000 });
        // The change's image is drawn inside the text, served from the branch through this origin.
        await Assertions.Expect(lines.Nth(4).Locator("img")).ToHaveAttributeAsync("src", $"/api/pbi/proposal/file?repository=owner%2Fsandbox&id=495&path={Uri.EscapeDataString(Root + "states.png")}");

        // Hover a line, anchor a comment to it.
        var line7 = lines.Nth(3);
        await line7.HoverAsync();
        await line7.GetByRole(AriaRole.Button, new() { Name = "Comment on line 7" }).ClickAsync();
        await page.GetByRole(AriaRole.Textbox, new() { Name = "Comment, anchored to line 7" }).FillAsync("Link the ADR here.");
        await page.GetByRole(AriaRole.Form, new() { Name = "Comment on line 7" }).GetByRole(AriaRole.Button, new() { Name = "Comment", Exact = true }).ClickAsync();
        await Assertions.Expect(line7).ToContainTextAsync("Link the ADR here.");
        stub.Writes.Single(write => write.Path == "/api/pbi/proposal/comment").Body.ShouldBe(
            $$"""{"repository":"{{Repository}}","id":"495","path":"{{Root}}proposal.md","line":7,"body":"Link the ADR here."}"""
        );

        // A spec delta reads against master.
        await files.GetByRole(AriaRole.Button, new() { Name = "specs/loop/spec.md" }).ClickAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Show loop on master" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "loop on master" })).ToContainTextAsync("Requirement: Old");

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"proposal-{theme}"), FullPage = true });

        // Request changes: the review, then the Journey shows the PBI back at spec-ready.
        await page.GetByRole(AriaRole.Button, new() { Name = "Request changes" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Request changes" });
        await dialog.GetByRole(AriaRole.Textbox).FillAsync("Split the port change into its own ADR.");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Send and request changes" }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        stub.Writes.Single(write => write.Path == "/api/pbi/proposal/request-changes").Body.ShouldBe(
            $$"""{"repository":"{{Repository}}","id":"495","body":"Split the port change into its own ADR."}"""
        );

        await page.GetByRole(AriaRole.Navigation, new() { Name = "PBI tabs" }).GetByRole(AriaRole.Link, new() { Name = "Journey" }).ClickAsync();
        var journey = page.GetByRole(AriaRole.List, new() { Name = "Journey" });
        await Assertions.Expect(journey.Locator("li[aria-current=step]")).ToContainTextAsync("spec-ready");
        var review = journey.Locator(":scope > li").Nth(2);
        await Assertions.Expect(review).ToContainTextAsync("released → spec-ready");
        await Assertions.Expect(review).ToContainTextAsync("a person requested changes on the proposal");
    }

    [Fact]
    public async Task Approve_releases_the_hold_and_a_narrow_proposal_fits()
    {
        await using var context = await frontend.Open("dark");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(600, 900);
        var stub = new Stub();
        await Arrange(page, stub);

        await page.GotoAsync($"/pbis/{FolderId}/495/proposal");
        await page.GetByRole(AriaRole.Button, new() { Name = "Approve" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Approve" })).ToHaveCountAsync(0);
        stub.Writes.Single(write => write.Path == "/api/pbi/release").Body.ShouldBe(
            $$"""{"repository":"{{Repository}}","id":"495","to":null}"""
        );

        var overflow = await page.EvaluateAsync<int>(
            "(() => { const pane = document.querySelector('main > div'); return pane.scrollWidth - pane.clientWidth; })()"
        );
        overflow.ShouldBeLessThanOrEqualTo(0, "nothing on the Proposal tab is wider than the pane at 600px");
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath("proposal-narrow-dark"), FullPage = true });

        await page.GotoAsync($"/pbis/{FolderId}/495/issue");
        await Assertions.Expect(page.GetByText("Declare the loop as states, top to bottom.")).ToBeVisibleAsync();
        overflow = await page.EvaluateAsync<int>(
            "(() => { const pane = document.querySelector('main > div'); return pane.scrollWidth - pane.clientWidth; })()"
        );
        overflow.ShouldBeLessThanOrEqualTo(0, "nothing on the Issue tab is wider than the pane at 600px");
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath("issue-narrow-dark"), FullPage = true });
    }

    [Fact]
    public async Task Markdown_that_starts_with_a_heading_leaves_the_page_one_h1()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var stub = new Stub { IssueBody = "# Loop as states\n\n## Why\n\nDeclare the loop as states, top to bottom." };
        await Arrange(page, stub);
        var h1 = page.GetByRole(AriaRole.Heading, new() { Level = 1 });

        // proposal.md opens with `# Loop as states`, the issue body with the same line.
        foreach (var tab in new[] { "proposal", "issue" })
        {
            await page.GotoAsync($"/pbis/{FolderId}/495/{tab}");
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 2, Name = "Loop as states" }).First).ToBeVisibleAsync();
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 3, Name = "Why" }).First).ToBeVisibleAsync();
            await Assertions.Expect(h1).ToHaveCountAsync(1);
            await Assertions.Expect(h1).ToHaveTextAsync("Declare the loop as a state machine");
        }
    }

    sealed record Write(string Path, string Body);

    /// <summary>What the stub serves for one PBI, and every write the screen sent.</summary>
    sealed class Stub
    {
        public string State { get; set; } = "proposal-review";

        public bool Held { get; set; } = true;

        public string IssueBody { get; init; } = $"Declare the loop as states, top to bottom.\n\n<img width=\"600\" alt=\"the loop today\" src=\"{BodyImage}\">";

        public List<object> Comments { get; } = [];

        public List<object> ReviewComments { get; } = [];

        public List<object> Journal { get; } = [];

        /// <summary>#611. The issue's sub-issues, as <c>/api/pbi/children</c> answers them.</summary>
        public List<object> Children { get; } = [];

        /// <summary>#611. The engine now, whose live runs and scripts paint a sub-issue as running.</summary>
        public object Present { get; set; } = new { liveRuns = Array.Empty<object>(), liveScripts = Array.Empty<object>() };

        public ConcurrentQueue<Write> Writes { get; } = new();
    }

    static async Task Arrange(IPage page, Stub stub)
    {
        var now = DateTimeOffset.UtcNow;
        stub.Comments.AddRange(
        [
            new { id = "101", author = "dev", createdAt = now.AddHours(-3), body = "Confirm done needs no declaring.", url = (string?)null, harness = (string?)null },
            new
            {
                id = "102",
                author = "aio-bot",
                createdAt = now.AddHours(-2),
                body = $"Evidence for #495.\n\n![wide-dark.png]({Capture})\n\n<!-- aio:evidence {{\"schemaVersion\": 1}} -->",
                url = (string?)null,
                harness = (string?)"evidence",
            },
        ]);
        stub.ReviewComments.Add(new { id = "9", path = Root + "proposal.md", line = (int?)5, author = "dev", createdAt = now.AddHours(-1), body = "Why only this?" });
        stub.Journal.Add(Line(now.AddMinutes(-30), "proposal-review", "held", "in-progress", "El score de riesgo salió alto: touches a port"));

        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var request = route.Request;
                var path = new Uri(request.Url).AbsolutePath;
                if (request.Method != "GET")
                {
                    var body = request.PostData ?? "";
                    stub.Writes.Enqueue(new Write(path, body));
                    var sent = body.Length == 0 ? default : JsonDocument.Parse(body).RootElement;
                    switch (path)
                    {
                        case "/api/pbi/issue/comment":
                            {
                                var comment = new
                                {
                                    id = "103",
                                    author = "dev",
                                    createdAt = DateTimeOffset.UtcNow,
                                    body = sent.GetProperty("body").GetString() + "\n\n![narrow.png](https://github.com/user-attachments/assets/1d6c1d3a-0000-4000-8000-000000000001)",
                                    url = (string?)null,
                                    harness = (string?)null,
                                };
                                stub.Comments.Add(comment);
                                await Json(route, comment);
                                return;
                            }
                        case "/api/pbi/proposal/comment":
                            {
                                var comment = new
                                {
                                    id = "10",
                                    path = sent.GetProperty("path").GetString(),
                                    line = (int?)sent.GetProperty("line").GetInt32(),
                                    author = "dev",
                                    createdAt = DateTimeOffset.UtcNow,
                                    body = sent.GetProperty("body").GetString(),
                                };
                                stub.ReviewComments.Add(comment);
                                await Json(route, comment);
                                return;
                            }
                        case "/api/pbi/proposal/request-changes":
                        case "/api/pbi/release":
                            {
                                var released = path.EndsWith("release", StringComparison.Ordinal);
                                var to = released ? "in-progress" : "spec-ready";
                                var why = released ? "a person released the hold" : "a person requested changes on the proposal";
                                var line = Line(DateTimeOffset.UtcNow, stub.State, "released", to, why);
                                stub.Journal.Insert(0, line);
                                stub.State = to;
                                stub.Held = false;
                                await Json(route, released ? line : new { review = "77", line, unmoved = (string?)null, note = (string?)null });
                                return;
                            }
                    }
                    await route.FulfillAsync(new() { Status = 204 });
                    return;
                }

                if (path is "/api/pbi/attachment" or "/api/pbi/proposal/file")
                {
                    await route.FulfillAsync(new() { ContentType = "image/png", BodyBytes = Png });
                    return;
                }

                object response = path switch
                {
                    "/api/folders" => Folders,
                    "/api/pbis" => new { pbis = new[] { Pbi(stub) }, unread = Array.Empty<object>() },
                    "/api/sessions" => Array.Empty<object>(),
                    "/api/pbi" => new
                    {
                        pbi = Pbi(stub),
                        pullRequest = new { id = "512", url = "https://github.com/owner/sandbox/pull/512", isDraft = true, isMerged = false, isApproved = false },
                    },
                    "/api/runs" => Array.Empty<object>(),
                    "/api/loop/journal" => stub.Journal,
                    "/api/loop/definition" => Loop,
                    "/api/pbi/issue" => Issue(stub, now),
                    "/api/pbi/proposal" => Proposal(stub),
                    "/api/pbi/children" => stub.Children,
                    "/api/loop/present" => stub.Present,
                    _ => new { },
                };
                await Json(route, response);
            }
        );
    }

    static object Issue(Stub stub, DateTimeOffset now) => new
    {
        url = "https://github.com/owner/sandbox/issues/495",
        author = "dev",
        createdAt = now.AddDays(-1),
        body = stub.IssueBody,
        labels = new[] { $"status:{stub.State}", "status:holding" },
        assignees = new[] { "dev" },
        parent = new { repository = Repository, id = "477", title = "Harden the loop", url = "https://github.com/owner/sandbox/issues/477", closed = false },
        blockedBy = new[] { new { repository = Repository, id = "482", title = "Before", url = "https://github.com/owner/sandbox/issues/482", closed = true } },
        comments = stub.Comments,
        images = new object[]
        {
            new { url = BodyImage, name = "the loop today", commentId = (string?)null },
            new { url = Capture, name = "wide-dark.png", commentId = (string?)"102" },
        },
    };

    static object Proposal(Stub stub) => new
    {
        pullRequestId = "512",
        pullRequestUrl = "https://github.com/owner/sandbox/pull/512",
        head = "4f1c2b7e9a0d3c5b8e6f7a1b2c3d4e5f6a7b8c9d",
        change = "loop-states",
        root = Root,
        missing = (string?)null,
        files = new object[]
        {
            File("proposal.md", "proposal", "# Loop as states\n\n## Why\n\nThe DAG has to be followed by hand.\n\n`IPbiProvider` gains `MoveAsync`.\n\n![states](./states.png)\n"),
            File("design.md", "design", "# Design\n\nOne state, one label.\n"),
            File("specs/loop/spec.md", "spec", "## ADDED Requirements\n\n### Requirement: A\n\n### Requirement: B\n\n## MODIFIED Requirements\n\n### Requirement: Old\n",
                capability: "loop", master: "# loop\n\n### Requirement: Old\n\nThe loop is a DAG.\n", delta: new { added = 2, modified = 1, removed = 0, renamed = 0 }),
            File("tasks.md", "tasks", "## 1. Do\n\n- [x] 1.1 One\n- [ ] 1.2 Two\n- [ ] 1.3 Three\n", tasks: new { done = 1, total = 3 }),
            File("states.png", "image", null),
        },
        comments = stub.ReviewComments,
    };

    static object File(string name, string kind, string? text, string? capability = null, string? master = null, object? delta = null, object? tasks = null) =>
        new { path = Root + name, name, kind, text, capability, master, delta, tasks };

    static Task Json(IRoute route, object body) =>
        route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(body) });

    static object Line(DateTimeOffset at, string from, string kind, string? to, string why) =>
        new { at, repository = Repository, pbi = "495", from, kind, to, why };

    static object Pbi(Stub stub) => new
    {
        vendor = "github",
        repository = Repository,
        id = "495",
        title = "Declare the loop as a state machine",
        state = stub.State,
        held = stub.Held,
        assignee = (string?)null,
        closed = false,
        folderPath = "/checkouts/sandbox",
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
