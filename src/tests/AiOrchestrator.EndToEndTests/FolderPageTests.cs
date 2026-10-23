using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #565. The folder page, opened from its name in the rail: the engine's switch, the harness version
// against the one the app ships and what is missing; the loop validated again when the file changes
// on disk, without a reload; the harness file by file; the checkout. In both themes, at 390 px, and
// from the keyboard.
//
// The API is stubbed at the browser, like the rest of this collection. What the server reads from
// the checkout is covered by Workspace's FolderPageTests, and the running product was looked at by
// hand for the pull request with this repository's own loop.yaml broken on disk.
[Collection("review-frontend")]
public sealed class FolderPageTests(ReviewFrontendFixture frontend)
{
    const string FolderId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c6";
    const string FolderPath = "/checkouts/sandbox";
    const string Repository = "owner/sandbox";
    const string Remedy = $"cd '{FolderPath}' && claude";

    const string Loop = """
        name: sandbox
        states:
          ready-for-proposal: { command: /aio:refine, next: verifying }
          verifying: { script: [gate], on: { green: merging } }
          merging: { script: [merge], on: { merged: done, conflict: rebasing } }
          rebasing:
            script: [gate]
            next: merging
            max_loops: 1
        """;

    /// <summary>ValidateLoop's own answer to this loop without its <c>max_loops</c>, as Loop's ValidateLoopTests pins it.</summary>
    const string Problem = "states.rebasing: goes back to merging without max_loops; a state that can go back says how many times";

    [Theory]
    [InlineData("light")]
    public async Task The_page_opens_from_the_rail_and_says_what_the_folder_lacks(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var api = await Arrange(page);

        await page.GotoAsync("/pbis");
        var rail = page.GetByRole(AriaRole.Navigation, new() { Name = "Main" });
        // The chevron still shows and hides the folder's PBIs; the name is a link now.
        await Assertions.Expect(rail.GetByRole(AriaRole.Button, new() { Name = "Show or hide the PBIs of sandbox" })).ToBeVisibleAsync();
        await rail.GetByRole(AriaRole.Link, new() { Name = "sandbox", Exact = true }).ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(new Regex($"/folders/{FolderId}$"));
        var main = page.GetByRole(AriaRole.Main);
        await Assertions.Expect(main.GetByRole(AriaRole.Heading, new() { Name = "sandbox", Level = 1 })).ToBeVisibleAsync();
        await page.EvaluateAsync("window.__sameDocument = true");

        // The strip: the switch, the version against the shipped one, and what is missing.
        var strip = main.GetByRole(AriaRole.Region, new() { Name = "How this folder stands for the loop" });
        var engine = strip.GetByRole(AriaRole.Switch, new() { Name = "Engine for sandbox" });
        await Assertions.Expect(engine).ToHaveAttributeAsync("aria-checked", "false");
        await Assertions.Expect(strip.GetByText("Off · nothing new starts here; live runs finish")).ToBeVisibleAsync();
        await Assertions.Expect(strip.GetByText("1 installed · the app ships 2")).ToBeVisibleAsync();
        // #566. Reinstall names the version it proposes, and the repository and branch it opens the PR from.
        var reinstall = strip.GetByRole(AriaRole.Button, new() { Name = "Reinstall 2" });
        await Assertions.Expect(reinstall).ToBeEnabledAsync();
        await Assertions.Expect(strip.GetByText($"Opens a pull request on {Repository} from harness/install", new() { Exact = false })).ToBeVisibleAsync();
        await Assertions.Expect(strip.GetByText("3 things missing")).ToBeVisibleAsync();
        await Assertions.Expect(strip.GetByText("The gate is the installed stub.", new() { Exact = false })).ToBeVisibleAsync();
        await Assertions.Expect(strip.GetByText("1 file of the harness package is missing. The harness block names it.")).ToBeVisibleAsync();
        // #606. The default agent asks to trust a folder and does not trust this one: the strip says so
        // and gives the command, and the page answers nothing for the person.
        await Assertions.Expect(strip.GetByText("claude-code does not trust this folder, so the engine starts none of its runs here.")).ToBeVisibleAsync();
        await Assertions.Expect(strip.GetByText("Trust · claude-code")).ToBeVisibleAsync();
        await Assertions.Expect(strip.GetByText("Does not trust this folder. Its runs wait here until it does.")).ToBeVisibleAsync();
        await Assertions.Expect(strip.GetByText(Remedy, new() { Exact = true })).ToBeVisibleAsync();

        // The blocks: the loop as the file has it, the harness file by file, the checkout.
        await Assertions.Expect(main.GetByText("Valid · 4 states")).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByLabel(".aio/loop.yaml")).ToContainTextAsync("max_loops: 1");
        await Assertions.Expect(main.GetByText("Missing: merge")).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByText("Still the installed stub, which fails every PBI")).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByText("Not yet on github: rebasing.", new() { Exact = false })).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByText(FolderPath, new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByLabel("Default agent")).ToHaveValueAsync("claude-code");
        await Assertions.Expect(main.GetByText("PBIs", new() { Exact = true })).ToHaveCountAsync(0);
        // #612. One row per agent state, with the one environment this machine has, chosen by default.
        var placed = main.GetByLabel("Environment for ready-for-proposal");
        await Assertions.Expect(placed).ToHaveValueAsync("worktree");
        await Assertions.Expect(placed.Locator("option")).ToHaveCountAsync(1);
        await Assertions.Expect(main.GetByLabel("Environment for verifying")).ToHaveCountAsync(0);
        await Assertions.Expect(main.GetByText("Scripts and scores run in worktree.", new() { Exact = false })).ToBeVisibleAsync();

        // The keyboard: Space turns the engine on, Tab reaches Reinstall and then Open in editor.
        await engine.FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await Until(() => api.Writes.Contains($"PUT /api/loop/engine {{\"repository\":\"{Repository}\",\"on\":true}}"));
        await Assertions.Expect(engine).ToHaveAttributeAsync("aria-checked", "true");
        await Assertions.Expect(strip.GetByText("On · looks every 60 s", new() { Exact = false })).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(reinstall).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        var open = main.GetByRole(AriaRole.Button, new() { Name = "Open in editor" });
        await Assertions.Expect(open).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(main.GetByText("Opened in code.")).ToBeVisibleAsync();
        api.Writes.ShouldContain($"POST /api/folders/{FolderId}/open-in-editor {{\"file\":\".aio/loop.yaml\"}}");

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"folder-{theme}"), FullPage = true });

        // Broken on disk: the next read has it, and the page says what ValidateLoop says, unreloaded.
        api.Yaml = Loop.Replace("\n    max_loops: 1", "", StringComparison.Ordinal);
        await Assertions.Expect(main.GetByText("Does not validate").First).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByRole(AriaRole.List, new() { Name = "Why the loop does not validate" })).ToContainTextAsync(Problem);
        await Assertions.Expect(strip.GetByText($"The loop does not validate: {Problem}")).ToBeVisibleAsync();
        (await page.EvaluateAsync<bool?>("window.__sameDocument")).ShouldBe(true, "the page read the file again without a reload");

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"folder-broken-{theme}"), FullPage = true });

        // Reinstall opens the pull request, and the cell links it in place of the button.
        await reinstall.ClickAsync();
        await Assertions.Expect(strip.GetByRole(AriaRole.Link, new() { Name = "PR #7" })).ToHaveAttributeAsync("href", PullUrl);
        await Assertions.Expect(strip.GetByText("2 proposed in", new() { Exact = false })).ToBeVisibleAsync();
        await Assertions.Expect(reinstall).ToHaveCountAsync(0);

        await Narrow(page, $"folder-390-{theme}");

        api.Writes.Where(write => !write.Contains("/api/loop/validate", StringComparison.Ordinal))
            .ShouldBe(
                [
                    $"PUT /api/loop/engine {{\"repository\":\"{Repository}\",\"on\":true}}",
                    $"POST /api/folders/{FolderId}/open-in-editor {{\"file\":\".aio/loop.yaml\"}}",
                    $"POST /api/folders/{FolderId}/harness/install {{}}",
                ],
                "the page writes the switch, opens the editor and asks for the install pull request, and never writes the loop"
            );
    }

    /// <summary>At a phone's width the page keeps its heading and scrolls down, never sideways.</summary>
    async Task Narrow(IPage page, string capture)
    {
        await page.SetViewportSizeAsync(AppFixture.Phone.Width, AppFixture.Phone.Height);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Show the rail" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Main).GetByRole(AriaRole.Heading, new() { Level = 1 })).ToBeVisibleAsync();
        var sideways = await page.EvaluateAsync<int>(
            "() => { const pane = document.querySelector('main > div'); return pane.scrollWidth - pane.clientWidth; }"
        );
        sideways.ShouldBe(0, "nothing on the page is wider than a phone");
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath(capture), FullPage = true });
    }

    static async Task Until(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            DateTime.UtcNow.ShouldBeLessThan(deadline, "the condition never held");
            await Task.Delay(100);
        }
    }

    sealed class Stub
    {
        public ConcurrentQueue<string> Writes { get; } = new();

        /// <summary>The checkout's loop.yaml as the next read finds it.</summary>
        public volatile string Yaml = Loop;

        public volatile bool On;

        public volatile bool Installed;

        /// <summary>#612. Each state's environment as the page stored it.</summary>
        public ConcurrentDictionary<string, string> Placed { get; } = new();
    }

    const string PullUrl = "https://github.com/owner/sandbox/pull/7";

    const string Caution =
        "This environment pushes with a token that reaches all your repositories. A fine-grained token for this repository alone, with Contents, Pull requests and Issues, is the recommended one: store it for the PBI's sandbox.";

    const string NarrowerToken = "sbx secret set github --sandbox aio-<repository>-change-<n>";

    // #612. Only the environments the server offers are listed, and a choice is stored at once.
    // #614. sbx with only the global GitHub token is offered with a caution, shown beside the selector
    // once a state runs there, with the command that stores a narrower token.
    [Theory]
    [InlineData("light")]
    public async Task A_state_placed_in_sbx_shows_the_caution_about_the_global_token_beside_the_selector(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        object[] notes = [new { environment = "sbx", offered = true, note = Caution, remedy = NarrowerToken }];
        var api = await Arrange(page, ["sbx", "worktree"], notes);

        await page.GotoAsync($"/folders/{FolderId}");
        var block = page.GetByRole(AriaRole.Main).GetByRole(AriaRole.Region, new() { Name = "Environments · where each agent state runs" });
        var placed = block.GetByLabel("Environment for ready-for-proposal");
        await Assertions.Expect(placed).ToHaveValueAsync("worktree");
        await Assertions.Expect(block.GetByText(Caution)).ToHaveCountAsync(0);

        await placed.SelectOptionAsync("sbx");
        await Until(() => api.Writes.Contains(
            $"PUT /api/runs/environments {{\"repository\":\"{Repository}\",\"state\":\"ready-for-proposal\",\"environment\":\"sbx\"}}"
        ));
        await Assertions.Expect(placed).ToHaveValueAsync("sbx");
        await Assertions.Expect(block.GetByText("sbx · read before it runs")).ToBeVisibleAsync();
        await Assertions.Expect(block.GetByText(Caution)).ToBeVisibleAsync();
        await Assertions.Expect(block.GetByText(NarrowerToken)).ToBeVisibleAsync();
        await block.ScreenshotAsync(new() { Path = frontend.CapturePath($"folder-environments-sbx-{theme}") });

        await page.SetViewportSizeAsync(AppFixture.Phone.Width, AppFixture.Phone.Height);
        await block.ScrollIntoViewIfNeededAsync();
        var sideways = await page.EvaluateAsync<int>(
            "() => { const pane = document.querySelector('main > div'); return pane.scrollWidth - pane.clientWidth; }"
        );
        sideways.ShouldBe(0, "the caution and its command fit a phone");
        await block.ScreenshotAsync(new() { Path = frontend.CapturePath($"folder-environments-sbx-390-{theme}") });
    }

    static async Task<Stub> Arrange(IPage page, string[]? environments = null, object[]? notes = null)
    {
        var stub = new Stub();
        var now = DateTimeOffset.UtcNow;
        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var method = route.Request.Method;
                var path = new Uri(route.Request.Url).AbsolutePath;
                if (method != "GET") stub.Writes.Enqueue($"{method} {path} {route.Request.PostData}");
                if (method == "PUT" && path == "/api/loop/engine") stub.On = true;
                if (method == "POST" && path == $"/api/folders/{FolderId}/harness/install") stub.Installed = true;
                if (method == "PUT" && path == "/api/runs/environments")
                {
                    using var body = JsonDocument.Parse(route.Request.PostData ?? "{}");
                    stub.Placed[body.RootElement.GetProperty("state").GetString()!] = body.RootElement.GetProperty("environment").GetString()!;
                }
                var placed = stub.Placed.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new { state = pair.Key, environment = pair.Value })
                    .ToArray();
                var pull = stub.Installed ? new { id = "7", url = PullUrl } : null;

                object response = (method, path) switch
                {
                    ("GET", "/api/folders") => new[] { Row },
                    ("GET", $"/api/folders/{FolderId}") => Page(stub.Yaml),
                    ("GET", $"/api/folders/{FolderId}/states") => new { vendor = "github", states = new[] { "merging", "ready-for-proposal", "verifying" }, problem = (string?)null },
                    ("GET", $"/api/folders/{FolderId}/harness/install") => new { branch = "harness/install", pullRequest = pull, problem = (string?)null },
                    ("POST", $"/api/folders/{FolderId}/harness/install") => new
                    {
                        version = "2",
                        branch = "harness/install",
                        @base = "main",
                        pullRequest = pull,
                        alreadyOpen = false,
                        states = Array.Empty<string>(),
                        kept = new[] { ".aio/gate" },
                        problems = Array.Empty<string>(),
                    },
                    ("GET", "/api/pbis") => new { pbis = Array.Empty<object>(), unread = Array.Empty<object>() },
                    ("GET", "/api/sessions") => Array.Empty<object>(),
                    ("GET", "/api/machine") => new { agents = new[] { "claude-code", "opencode" }, environments = environments ?? ["worktree"], defaultEnvironment = "worktree", notes = notes ?? [] },
                    ("GET", "/api/runs/environments") or ("PUT", "/api/runs/environments") => new { repository = Repository, states = placed },
                    ("GET", "/api/loop/settings") => new { intervalSeconds = 60, folders = new[] { new { path = FolderPath, repository = Repository, on = stub.On } } },
                    ("GET", "/api/loop/present") => Present(now, stub.On),
                    ("PUT", "/api/loop/engine") => new { repository = Repository, on = true, written = (object?)null },
                    ("POST", "/api/loop/validate") => Validate(route.Request.PostData ?? ""),
                    ("POST", _) when path.EndsWith("/open-in-editor", StringComparison.Ordinal) => new { opened = $"{FolderPath}/.aio/loop.yaml", editor = "code" },
                    _ => new { },
                };
                await route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(response) });
            }
        );
        return stub;
    }

    static readonly object Row = new
    {
        id = FolderId,
        name = "sandbox",
        path = FolderPath,
        vendor = "github",
        repository = Repository,
        loopYaml = Loop,
        defaultAgent = "claude-code",
        defaultModel = (string?)null,
        hasGate = true,
    };

    static object Page(string yaml) => new
    {
        id = FolderId,
        name = "sandbox",
        path = FolderPath,
        vendor = "github",
        repository = Repository,
        branch = "main",
        defaultAgent = "claude-code",
        defaultModel = (string?)null,
        loopYaml = yaml,
        harnessVersion = "1",
        shippedVersion = "2",
        harness = new[]
        {
            Harness(".ai/commands/aio/implement.md", present: true),
            Harness(".ai/commands/aio/refine.md", present: true),
            Harness(".claude/settings.json", present: true),
            Harness(".aio/loop.yaml", present: true),
            Harness(".aio/steps/claim", present: true),
            Harness(".aio/gate", present: true, stub: true),
            Harness(".aio/steps/merge", present: false),
            Harness(".aio/version", present: true),
        },
        trust = new[]
        {
            new
            {
                provider = "claude-code",
                trusted = false,
                why = $"Claude Code does not trust {FolderPath}, so a run there would wait on its trust question. Run claude there once and accept it.",
                remedy = (string?)Remedy,
            },
        },
    };

    static object Harness(string path, bool present, bool stub = false) => new { path, present, stub };

    /// <summary>What ValidateLoop answers for the two texts this test serves.</summary>
    static object Validate(string body) =>
        body.Contains("max_loops", StringComparison.Ordinal)
            ? (object)new
            {
                valid = true,
                states = new[] { "ready-for-proposal", "verifying", "merging", "rebasing" }
                    .Select(name => new { name, transitions = Array.Empty<object>(), agent = name == "ready-for-proposal" }),
                undeclared = new[] { "done" },
                problems = Array.Empty<string>(),
            }
            : new
            {
                valid = false,
                states = Array.Empty<object>(),
                undeclared = Array.Empty<string>(),
                problems = new[] { Problem },
            };

    static object Present(DateTimeOffset now, bool on) => new
    {
        enabled = on,
        intervalSeconds = 60,
        lastPass = new { at = now.AddSeconds(-12), folders = 1, pbis = 0, skipped = Array.Empty<string>(), written = 0, failure = (string?)null },
        nextPassAt = (DateTimeOffset?)null,
        liveRuns = Array.Empty<object>(),
        folders = Array.Empty<object>(),
        lastDecision = (object?)null,
    };
}
