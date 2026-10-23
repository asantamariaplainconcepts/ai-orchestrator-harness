using System.Buffers.Binary;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Folders;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #547. Every screen of the route table, in both themes, at 1440 and at 390, as a PNG with the
// sidecar `.aio/steps/evidence` stages: what the screen is, the viewport and theme measured off
// the page, and when. `evidence` runs this class when a change touched src/frontend/ and publishes
// the screens the change touched.
//
// The set is read from the routes, not listed here. The page imports `app/screens.tsx` and the PBI's
// tabs and this class visits every route that loads a screen, with each `:param` filled from the
// stub below; a new screen or tab is captured without touching this file, and a new parameter fails
// here by name. Redirects (`index`, the PBI without a tab) load no screen of their own and are
// skipped.
//
// The API is stubbed at the browser, like the other classes of this collection, so every screen has
// something to draw: one folder, a handful of PBIs and one PBI with a pull request under review.
[Collection("review-frontend")]
public sealed class ScreenCaptureTests(ReviewFrontendFixture frontend)
{
    const string FolderId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c6";
    const string Repository = "owner/sandbox";
    const string FolderPath = "/checkouts/sandbox";
    const string PbiId = "495";
    const string ReviewRun = "01a11dcb-dec7-78dd-8f4c-70a254ded202";
    const string Head = "9b01a3c4d5e6f708192a3b4c5d6e7f8091a2b3c4";
    const string Root = "openspec/changes/loop-states/";

    /// <summary>Where the captures land: ignored, and the directory `evidence` stages from.</summary>
    public static string Output => Path.Combine(
        SelfCheckout.Root() ?? throw new DirectoryNotFoundException("No checkout to write the captures into."),
        "src", "artifacts", "design", "screens"
    );

    [Theory]
    [InlineData("light", 1440, 900)]
    [InlineData("dark", 1440, 900)]
    [InlineData("light", 390, 844)]
    [InlineData("dark", 390, 844)]
    public async Task Every_route_is_captured(string theme, int width, int height)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, height);
        await Arrange(page);
        await page.GotoAsync("/");
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToBeVisibleAsync();

        var table = await page.EvaluateAsync<JsonElement>(
            """
            async () => {
              const { screens } = await import("/app/screens.tsx");
              const { pbiTabs } = await import("/features/pbi/tabs.tsx");
              return {
                routes: screens
                  .filter((route) => typeof route.lazy === "function")
                  .map((route) => ({
                    path: route.path,
                    module: (String(route.lazy).match(/\/(features\/[^"'?]+?)\.tsx?/) || [])[1] || null,
                  })),
                tabs: pbiTabs.map((tab) => tab.path),
              };
            }
            """
        );
        var values = new Dictionary<string, string[]>
        {
            ["folderId"] = [FolderId],
            ["pbiId"] = [PbiId],
            ["tab"] = [.. table.GetProperty("tabs").EnumerateArray().Select(tab => tab.GetString()!)],
        };
        var screens = table.GetProperty("routes").EnumerateArray()
            .SelectMany(route => Expand(route.GetProperty("path").GetString()!, values)
                .Select(url => (Url: url.Path, Name: url.Name, Module: route.GetProperty("module").GetString())))
            .ToList();
        screens.ShouldNotBeEmpty("the route table loads no screen");

        Directory.CreateDirectory(Output);
        foreach (var screen in screens)
        {
            await page.GotoAsync(screen.Url);
            // Every screen titles itself with one h1, and the route's error boundary has none: a
            // screen whose module died is a failure here, not a picture of the error.
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToBeVisibleAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            // Still one once the screen has drawn what it fetched: markdown's `#` is an h2 (#586).
            await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Level = 1 })).ToHaveCountAsync(1);

            var measured = await page.EvaluateAsync<JsonElement>(
                "() => ({ width: innerWidth, height: innerHeight, dark: document.querySelector('.dark') !== null })"
            );
            measured.GetProperty("width").GetInt32().ShouldBe(width, $"{screen.Url} laid out at another width");
            var drawn = measured.GetProperty("dark").GetBoolean() ? "dark" : "light";
            drawn.ShouldBe(theme, $"{screen.Url} came out in the other theme");

            var name = $"{screen.Name}.{width}.{theme}";
            var png = Path.Combine(Output, $"{name}.png");
            var bytes = await page.ScreenshotAsync(new() { Path = png });
            await File.WriteAllTextAsync(
                Path.Combine(Output, $"{name}.json"),
                JsonSerializer.Serialize(new
                {
                    screen = screen.Name,
                    route = screen.Url,
                    module = screen.Module,
                    // Off the PNG itself: width and height are the IHDR's first two fields.
                    viewport = new
                    {
                        width = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(16, 4)),
                        height = BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(20, 4)),
                    },
                    theme = drawn,
                    capturedAt = DateTimeOffset.UtcNow,
                })
            );
        }
    }

    /// <summary>
    /// The concrete URLs of one route pattern, and the name each capture is filed under: the literal
    /// segments and the value of the last parameter, so `pbis/:folderId/:pbiId/:tab` files the Review
    /// tab as `pbis-review`.
    /// </summary>
    static IEnumerable<(string Path, string Name)> Expand(string pattern, Dictionary<string, string[]> values)
    {
        IEnumerable<(string Path, string Name)> partial = [("", "")];
        foreach (var segment in pattern.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!segment.StartsWith(':'))
            {
                partial = partial.Select(p => ($"{p.Path}/{segment}", Join(p.Name, segment)));
                continue;
            }

            var parameter = segment[1..];
            values.TryGetValue(parameter, out var options)
                .ShouldBeTrue($"the route {pattern} has a :{parameter} this class has no value for");
            partial = partial.SelectMany(p => options!.Select(value =>
                ($"{p.Path}/{Uri.EscapeDataString(value)}", parameter == "tab" ? Join(p.Name, value) : p.Name)));
        }

        return partial;

        static string Join(string name, string part) => name.Length == 0 ? part : $"{name}-{part}";
    }

    static async Task Arrange(IPage page)
    {
        var now = DateTimeOffset.UtcNow;
        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var path = new Uri(route.Request.Url).AbsolutePath;
                if (path == $"/api/runs/{ReviewRun}/recording")
                {
                    await route.FulfillAsync(new()
                    {
                        ContentType = "application/x-asciicast",
                        Body = "{\"version\": 2, \"width\": 80, \"height\": 6}\n[0.1, \"o\", \"reviewing the diff\\r\\n\"]\n",
                    });
                    return;
                }

                object response = path switch
                {
                    "/api/folders" => Folders,
                    // #565. The folder page: the checkout as read, the vendor's states and the switches.
                    $"/api/folders/{FolderId}" => FolderPage,
                    $"/api/folders/{FolderId}/states" => new { vendor = "github", states = new[] { "code-review", "in-progress" }, problem = (string?)null },
                    "/api/loop/settings" => new { intervalSeconds = 60, folders = new[] { new { path = FolderPath, repository = Repository, on = true } } },
                    "/api/pbis" => new { pbis = OpenPbis, unread = Array.Empty<object>() },
                    "/api/pbis/done" => new { pbis = new[] { Pbi("2", "Shipped last week", "done", held: false, closed: true) }, unread = Array.Empty<object>() },
                    "/api/sessions" => Array.Empty<object>(),
                    "/api/loop/present" => Present(now),
                    "/api/loop/journal" => Journal(now),
                    "/api/loop/definition" => Loop,
                    // #734. The Metrics screen of the folder.
                    "/api/loop/metrics" => MetricsScreenTests.Report,
                    "/api/pbi" => new { pbi = Reviewed, pullRequest = PullRequest },
                    "/api/runs" => Runs(now),
                    "/api/pbi/issue" => Issue(now),
                    "/api/pbi/children" => Array.Empty<object>(),
                    "/api/pbi/proposal" => Proposal(now),
                    "/api/pbi/review" => Review(now),
                    "/api/files" => Tree,
                    "/api/files/content" => new { path = "README.md", text = "# sandbox\n\nThe loop's test repository.\n", isBinary = false, truncated = false, bytes = 40 },
                    // A POST, but a read: the PBIs screen and the folder page ask whether a loop.yaml is valid.
                    "/api/loop/validate" => new
                    {
                        valid = true,
                        states = Array.Empty<object>(),
                        undeclared = new[] { "done" },
                        problems = Array.Empty<string>(),
                    },
                    "/api/machine" => new { agents = new[] { "claude-code" }, environments = new[] { "worktree" }, defaultEnvironment = "worktree" },
                    "/api/machine/tools" => Tools,
                    "/api/preferences" => new { editor = "code", worktreeDefault = true },
                    "/api/updates" => new { state = "up-to-date", installed = "0.2.4", latest = "0.2.4", how = "tool", detail = (string?)null },
                    _ => new { },
                };
                await route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(response) });
            }
        );
    }

    const string LoopYaml = """
        name: spec-driven-issue-loop
        wip: 4
        states:
          ready-for-proposal:
            agent: refiner
            command: /aio:refine
            on:
              ready: spec-ready
          spec-ready:
            agent: coder
            command: /aio:propose
            next: in-progress
          in-progress:
            agent: coder
            command: /aio:implement
            next: verifying
          verifying:
            script:
              - gate
            on:
              green: code-review
              red:
                hold: El gate está en rojo
          code-review:
            agent: reviewer
            command: /aio:review-diff
            on:
              approved: done
              rejected: in-progress
        """;

    static readonly object[] Folders =
    [
        new
        {
            id = FolderId,
            name = "sandbox",
            path = FolderPath,
            vendor = "github",
            repository = Repository,
            loopYaml = LoopYaml,
            defaultAgent = "claude-code",
            defaultModel = (string?)null,
            hasGate = true,
        },
    ];

    static readonly object FolderPage = new
    {
        id = FolderId,
        name = "sandbox",
        path = FolderPath,
        vendor = "github",
        repository = Repository,
        branch = "main",
        defaultAgent = "claude-code",
        defaultModel = (string?)null,
        loopYaml = LoopYaml,
        harnessVersion = "1",
        shippedVersion = "1",
        harness = new[]
        {
            new { path = ".aio/loop.yaml", present = true, stub = false },
            new { path = ".aio/gate", present = true, stub = false },
            new { path = ".aio/version", present = true, stub = false },
        },
        trust = new[] { new { provider = "claude-code", trusted = true, why = $"Claude Code trusts {FolderPath}.", remedy = (string?)null } },
    };

    static object Pbi(string id, string title, string? state, bool held, bool closed = false) => new
    {
        vendor = "github",
        repository = Repository,
        id,
        title,
        state,
        held,
        assignee = (string?)null,
        closed,
        folderPath = FolderPath,
    };

    static readonly object Reviewed = Pbi(PbiId, "Declare the loop as a state machine", "code-review", held: false);

    static readonly object[] OpenPbis =
    [
        Reviewed,
        Pbi("3", "Break the sum so the gate goes red", "verifying", held: true),
        Pbi("7", "Refine what the user asked for", "ready-for-proposal", held: false),
        Pbi("9", "Wait for a slot under the WIP", "spec-ready", held: false),
        Pbi("8", "Not in the loop yet", null, held: false),
    ];

    static readonly object PullRequest = new
    {
        id = "512",
        url = "https://github.com/owner/sandbox/pull/512",
        isDraft = false,
        isMerged = false,
        isApproved = false,
    };

    static object Line(DateTimeOffset at, string pbi, string from, string kind, string? to, string why) =>
        new { at, repository = Repository, pbi, from, kind, to, why };

    static object[] Journal(DateTimeOffset now) =>
    [
        Line(now.AddMinutes(-5), "3", "verifying", "held", null, "gate failed: 2 tests red"),
        Line(now.AddMinutes(-9), "9", "spec-ready", "waited", null, "WIP 4 of 4"),
        Line(now.AddMinutes(-34), PbiId, "verifying", "moved", "code-review", "green"),
        Line(now.AddMinutes(-80), PbiId, "in-progress", "moved", "verifying", "next"),
    ];

    static object Present(DateTimeOffset now) => new
    {
        enabled = true,
        intervalSeconds = 60,
        lastPass = new { at = now.AddSeconds(-12), folders = 1, pbis = 5, skipped = Array.Empty<string>(), written = 1, failure = (string?)null },
        nextPassAt = now.AddSeconds(48),
        liveRuns = Array.Empty<object>(),
        folders = new[] { new { path = FolderPath, vendor = "github", repository = Repository, wip = 4, problem = (string?)null } },
        lastDecision = Journal(now)[0],
    };

    static object State(string name, string[]? scripts = null, string? agent = null, string? command = null, object[]? transitions = null) => new
    {
        name,
        scripts = scripts ?? [],
        agent,
        command,
        score = (string?)null,
        provider = (string?)null,
        model = (string?)null,
        maxLoops = (int?)null,
        transitions = transitions ?? [],
    };

    static object To(string? outcome, string? target, string? hold = null) => new { outcome, target, hold };

    static readonly object Loop = new
    {
        name = "spec-driven-issue-loop",
        agents = new[] { new { role = "coder", provider = "claude-code", model = "claude-opus-5-5" } },
        states = new object[]
        {
            State("ready-for-proposal", agent: "refiner", command: "/aio:refine", transitions: [To("ready", "spec-ready")]),
            State("spec-ready", agent: "coder", command: "/aio:propose", transitions: [To(null, "in-progress")]),
            State("in-progress", agent: "coder", command: "/aio:implement", transitions: [To(null, "verifying")]),
            State("verifying", scripts: ["gate"], transitions: [To("green", "code-review"), To("red", null, "El gate está en rojo")]),
            State("code-review", agent: "reviewer", command: "/aio:review-diff", transitions: [To("approved", "done"), To("rejected", "in-progress")]),
        },
        undeclared = new[] { "done" },
        problem = (string?)null,
    };

    static object Run(string runId, string state, string provider, string command, DateTimeOffset startedAt, DateTimeOffset endedAt, string outcome, decimal cost, bool recorded) => new
    {
        runId,
        state,
        manual = false,
        provider,
        model = "claude-opus-5-5",
        command,
        startedAt,
        endedAt = (DateTimeOffset?)endedAt,
        lastOutputAt = (DateTimeOffset?)null,
        stale = false,
        outcome,
        reason = "the diff does what the issue asks",
        failure = (string?)null,
        problems = Array.Empty<string>(),
        endRecord = (string?)null,
        inputTokens = (long?)38210,
        outputTokens = (long?)2904,
        costUsd = (decimal?)cost,
        recordingPath = $"/runs/{runId}/session.cast",
        recorded,
    };

    static object[] Runs(DateTimeOffset now) =>
    [
        Run("01a11dcb-dec7-78dd-8f4c-70a254ded201", "in-progress", "claude-code", "/aio:implement 495", now.AddHours(-3), now.AddHours(-2), "next", 3.34m, recorded: false),
        Run(ReviewRun, "code-review", "claude-code", "/aio:review-diff 495", now.AddMinutes(-30), now.AddMinutes(-22), "approved", 0.41m, recorded: true),
    ];

    static object Issue(DateTimeOffset now) => new
    {
        url = "https://github.com/owner/sandbox/issues/495",
        author = "dev",
        createdAt = now.AddDays(-1),
        body = "# Loop as states\n\nDeclare the loop as states, top to bottom, so the engine reads one file.",
        labels = new[] { "status:code-review" },
        assignees = new[] { "dev" },
        parent = new { repository = Repository, id = "477", title = "Harden the loop", url = "https://github.com/owner/sandbox/issues/477", closed = false },
        blockedBy = Array.Empty<object>(),
        comments = new[]
        {
            new { id = "101", author = "dev", createdAt = now.AddHours(-3), body = "Confirm done needs no declaring.", url = (string?)null, harness = (string?)null },
        },
        images = Array.Empty<object>(),
    };

    static object ProposalFile(string name, string kind, string text, string? capability = null, string? master = null, object? delta = null, object? tasks = null) =>
        new { path = Root + name, name, kind, text, capability, master, delta, tasks };

    static object Proposal(DateTimeOffset now) => new
    {
        pullRequestId = "512",
        pullRequestUrl = "https://github.com/owner/sandbox/pull/512",
        head = Head,
        change = "loop-states",
        root = Root,
        missing = (string?)null,
        files = new object[]
        {
            ProposalFile("proposal.md", "proposal", "# Loop as states\n\n## Why\n\nThe DAG has to be followed by hand.\n"),
            ProposalFile("design.md", "design", "# Design\n\nOne state, one label.\n"),
            ProposalFile("specs/loop/spec.md", "spec", "## ADDED Requirements\n\n### Requirement: A state per label\n",
                capability: "loop", master: "# loop\n", delta: new { added = 1, modified = 0, removed = 0, renamed = 0 }),
            ProposalFile("tasks.md", "tasks", "## 1. Do\n\n- [x] 1.1 One\n- [ ] 1.2 Two\n", tasks: new { done = 1, total = 2 }),
        },
        comments = new[]
        {
            new { id = "9", path = Root + "proposal.md", line = (int?)3, author = "dev", createdAt = now.AddHours(-1), body = "Why only this?" },
        },
    };

    static object Review(DateTimeOffset now) => new
    {
        pullRequest = PullRequest,
        change = new
        {
            baseRef = "main",
            baseSha = "4f9c2e1aaaabbbbccccddddeeeeffff000011112",
            headRef = "change/495",
            headSha = Head,
            commits = 4,
            additions = 2,
            deletions = 0,
            changedFiles = 1,
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
                    new { id = "901", author = (string?)"dev", at = now.AddHours(-3), body = "Does state stay a string on the port?" },
                },
            },
        },
        checks = new
        {
            rows = new[] { new { name = "aspire do ci", state = "passing", url = (string?)null } },
            problem = (string?)null,
        },
        quality = new
        {
            headSha = Head,
            stale = false,
            author = "aio-bot",
            at = now.AddMinutes(-45),
            rows = new[]
            {
                new { check = "Coverage of changed lines", measured = "80.0%: 4 of 5 changed lines covered", line = "70%", result = "ok" },
                new { check = "CRAP of touched methods", measured = "highest 12.0 of 2 methods, 0 over 30", line = "30", result = "ok" },
                new { check = "Mutation in touched files", measured = "no summary.json at src/artifacts/mutation/summary.json", line = "reported only", result = "not measured" },
                new { check = "New duplicated lines", measured = "12 lines in 1 file: src/Ports/IPbiProvider.cs 12", line = "0", result = "over" },
                new { check = "Tests lost or skipped", measured = "none lost or skipped", line = "0", result = "ok" },
                new { check = "Tests touched", measured = "no test that stood at the base touched", line = "0", result = "ok" },
                new { check = "Files outside the issue's paths", measured = "1 file: .aio/README.md", line = "0", result = "over" },
                new { check = "New code on old tests only", measured = "no old test fails against the new code", line = "0", result = "ok" },
            },
            problem = (string?)null,
        },
        evidence = new
        {
            batchId = (string?)null,
            headSha = (string?)null,
            author = (string?)null,
            at = (DateTimeOffset?)null,
            captures = Array.Empty<object>(),
            problem = (string?)null,
        },
    };

    static readonly object Tree = new
    {
        branch = "change/495",
        exists = true,
        checkout = (string?)"/checkouts/sandbox/.worktrees/change-495",
        inWorktree = true,
        files = new[] { ".aio/loop.yaml", "AGENTS.md", "README.md", "src/Ports/IPbiProvider.cs" },
        modified = new[] { "src/Ports/IPbiProvider.cs" },
        truncated = false,
    };

    static readonly object Tools = new
    {
        vendors = new[]
        {
            new { adapter = "github", cli = "gh", path = (string?)"/opt/homebrew/bin/gh", version = (string?)"2.99.0", account = (string?)"octocat", problem = (string?)null },
        },
        agents = new[]
        {
            new { adapter = "claude-code", cli = "claude", path = (string?)"/usr/local/bin/claude", version = (string?)"2.1.0", account = (string?)null, problem = (string?)null },
        },
        // #680. The requirements screen is captured with node missing, sbx signed out and opencode not installed.
        requirements = RequirementsScreenTests.Rows(nodeMissing: true),
        ready = false,
    };
}
