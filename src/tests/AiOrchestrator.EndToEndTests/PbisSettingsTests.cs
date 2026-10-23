using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #515. The PBIs list and Settings, with the rail's `+`: the list groups by the loop's
// states in the loop's order and filters by source, folder, state and text, with the closed behind
// Done; a folder added from the rail opens its page with what it lacks (#565), and Settings lists no
// folder; a CLI the server says is not on the PATH shows as missing.
//
// The API is stubbed at the browser, like the rest of this collection. What the server answers is
// covered by the Workspace, Pbis and Infrastructure tests, and the running product was looked at
// by hand for the pull request.
[Collection("review-frontend")]
public sealed class PbisSettingsTests(ReviewFrontendFixture frontend)
{
    const string SandboxId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c6";
    const string AddedId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c7";
    const string SandboxPath = "/checkouts/sandbox";
    const string AddedPath = "/checkouts/notes";
    const string Repository = "owner/sandbox";

    const string Loop = """
        name: sandbox
        states:
          ready-for-proposal: { script: [gate], on: { green: verifying } }
          verifying: { script: [gate], on: { green: done } }
        """;

    [Theory]
    [InlineData("light")]
    public async Task The_list_groups_by_the_loop_and_filters(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var api = await Arrange(page, added: false);

        await page.GotoAsync("/pbis");
        var main = page.GetByRole(AriaRole.Main);
        await Assertions.Expect(main.GetByRole(AriaRole.Heading, new() { Name = "PBIs", Level = 1 })).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByText("4 open in 1 folder, grouped by loop state.")).ToBeVisibleAsync();

        // The loop's order, then a state the loop does not declare, then the PBIs with none.
        var groups = main.GetByRole(AriaRole.Heading, new() { Level = 2 });
        await Assertions.Expect(groups).ToHaveCountAsync(4);
        await Assertions.Expect(groups.Nth(0)).ToContainTextAsync("ready-for-proposal");
        await Assertions.Expect(groups.Nth(1)).ToContainTextAsync("verifying");
        await Assertions.Expect(groups.Nth(2)).ToContainTextAsync("needs-refinement");
        await Assertions.Expect(groups.Nth(2)).ToContainTextAsync("The loop does not declare this state.");
        await Assertions.Expect(groups.Nth(3)).ToContainTextAsync("No state");

        var held = main.GetByRole(AriaRole.Link).Filter(new() { HasText = "Break the sum" });
        await Assertions.Expect(held.GetByRole(AriaRole.Img, new() { Name = "In hold" })).ToBeVisibleAsync();
        await Assertions.Expect(held).ToHaveAttributeAsync("href", $"/pbis/{SandboxId}/3/journey");
        await Assertions.Expect(main.GetByText("Shipped last week")).ToHaveCountAsync(0);

        var filters = main.GetByRole(AriaRole.Search, new() { Name = "Filters" });
        await filters.GetByLabel("State").SelectOptionAsync("held");
        await Assertions.Expect(main.GetByRole(AriaRole.Link)).ToHaveCountAsync(1);
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("state=held"));

        await filters.GetByLabel("State").SelectOptionAsync("done");
        await Assertions.Expect(main.GetByRole(AriaRole.Heading, new() { Name = "Closed" })).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByText("Shipped last week")).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByRole(AriaRole.Link)).ToHaveCountAsync(1);

        // The search is reached and typed from the keyboard; a number matches the id.
        await filters.GetByLabel("State").SelectOptionAsync("open");
        await filters.GetByLabel("State").FocusAsync();
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(filters.GetByRole(AriaRole.Searchbox, new() { Name = "Search" })).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync("7");
        await Assertions.Expect(main.GetByRole(AriaRole.Link)).ToHaveCountAsync(1);
        await Assertions.Expect(main.GetByRole(AriaRole.Link)).ToContainTextAsync("A live session for the tray");
        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(main.GetByRole(AriaRole.Link)).ToBeFocusedAsync();

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"pbis-{theme}") });
        await Narrow(page, $"pbis-390-{theme}");
        api.Writes.Where(write => !write.Contains("/api/loop/validate", StringComparison.Ordinal)).ShouldBeEmpty(
            "the list reads, and checking a loop's text writes nothing"
        );
    }

    [Theory]
    [InlineData("light")]
    public async Task A_folder_added_from_the_rail_opens_its_page_and_Settings_lists_no_folders(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var api = await Arrange(page, added: false);

        await page.GotoAsync("/pbis");
        var rail = page.GetByRole(AriaRole.Navigation, new() { Name = "Main" });
        await rail.GetByRole(AriaRole.Button, new() { Name = "Add folder" }).ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Add folder" });
        await Assertions.Expect(dialog.GetByLabel("Path")).ToBeFocusedAsync();
        await page.Keyboard.TypeAsync(AddedPath);
        api.Added = true;
        await page.Keyboard.PressAsync("Enter");

        // #565. The new folder's page, with what it lacks, rather than a row in Settings.
        await Assertions.Expect(page).ToHaveURLAsync(new Regex($"/folders/{AddedId}$"));
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        api.Writes.ShouldContain($"POST /api/folders {{\"path\":\"{AddedPath}\"}}");

        var main = page.GetByRole(AriaRole.Main);
        await Assertions.Expect(main.GetByRole(AriaRole.Heading, new() { Name = "notes", Level = 1 })).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByText("No loop declared. Add .aio/loop.yaml to the repository.").First).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByText("No gate script. Add .aio/gate, which the verifying state runs.")).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByText("Not installed · the app ships 1")).ToBeVisibleAsync();

        await rail.GetByRole(AriaRole.Link, new() { Name = "Settings" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings$"));

        // Settings keeps the instance, the machine and the person: no folder, no loop.
        await Assertions.Expect(main.GetByText("Folders · ", new() { Exact = false })).ToHaveCountAsync(0);
        await Assertions.Expect(main.GetByText(SandboxPath)).ToHaveCountAsync(0);
        await Assertions.Expect(main.GetByText(AddedPath)).ToHaveCountAsync(0);
        await Assertions.Expect(main.GetByText(".aio/loop.yaml", new() { Exact = false })).ToHaveCountAsync(0);

        // A CLI the server found nowhere on the PATH is missing, with what that costs.
        await Assertions.Expect(main.GetByText("2.99.0 · logged in as octocat")).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByText("Not on PATH. A state that names it will not start.")).ToBeVisibleAsync();
        await Assertions.Expect(main.GetByText("Missing")).ToHaveCountAsync(1);
        await Assertions.Expect(main.GetByText("Not yet used")).ToBeVisibleAsync();

        await main.GetByRole(AriaRole.Button, new() { Name = "Change" }).ClickAsync();
        await page.Keyboard.PressAsync("ControlOrMeta+a");
        await page.Keyboard.TypeAsync("cursor");
        await page.Keyboard.PressAsync("Enter");
        await Until(() => api.Writes.Contains("PUT /api/preferences {\"editor\":\"cursor\"}"));

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"settings-{theme}"), FullPage = true });
        await Narrow(page, $"settings-390-{theme}");
    }

    /// <summary>At a phone's width the screen keeps its heading and scrolls down, never sideways.</summary>
    async Task Narrow(IPage page, string capture)
    {
        await page.SetViewportSizeAsync(AppFixture.Phone.Width, AppFixture.Phone.Height);
        // The rail leaves the row a render after the width changes; measured before, it is 86 px of it.
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Show the rail" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Main).GetByRole(AriaRole.Heading, new() { Level = 1 })).ToBeVisibleAsync();
        var sideways = await page.EvaluateAsync<int>(
            "() => { const pane = document.querySelector('main > div'); return pane.scrollWidth - pane.clientWidth; }"
        );
        sideways.ShouldBe(0, "nothing on the screen is wider than a phone");
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

        /// <summary>Whether the folder the test adds is on the list yet.</summary>
        public volatile bool Added;
    }

    static async Task<Stub> Arrange(IPage page, bool added)
    {
        var stub = new Stub { Added = added };
        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var method = route.Request.Method;
                var path = new Uri(route.Request.Url).AbsolutePath;
                if (method != "GET") stub.Writes.Enqueue($"{method} {path} {route.Request.PostData}");

                object response = (method, path) switch
                {
                    ("GET", "/api/folders") => Folders(stub.Added),
                    ("GET", $"/api/folders/{AddedId}") => NotesPage,
                    ("GET", $"/api/folders/{AddedId}/states") => new { vendor = (string?)null, states = (string[]?)null, problem = "Its origin is not on github.com, so there is no vendor to ask." },
                    ("GET", "/api/loop/settings") => new { intervalSeconds = 60, folders = Array.Empty<object>() },
                    ("GET", "/api/pbis") => OpenPbis,
                    ("GET", "/api/pbis/done") => DonePbis,
                    ("GET", "/api/sessions") => Array.Empty<object>(),
                    ("GET", "/api/machine") => new { agents = new[] { "claude-code" }, environments = new[] { "worktree" }, defaultEnvironment = "worktree" },
                    ("GET", "/api/machine/tools") => Tools,
                    ("GET", "/api/directories") => new { path = "/", parent = (string?)null, directories = Array.Empty<object>() },
                    ("GET", "/api/preferences") => new { editor = "code", worktreeDefault = true },
                    ("POST", "/api/loop/validate") => Validation,
                    ("POST", "/api/folders") => new { id = AddedId, name = "notes", path = AddedPath, branch = "main", guardFailure = (string?)null },
                    ("POST", _) when path.EndsWith("/open-in-editor", StringComparison.Ordinal) => new { opened = $"{SandboxPath}/.aio/loop.yaml", editor = "code" },
                    ("PUT", "/api/preferences") => new { editor = "cursor", worktreeDefault = true },
                    _ => new { },
                };
                await route.FulfillAsync(new()
                {
                    Status = method == "POST" && path == "/api/folders" ? 201 : method == "DELETE" ? 204 : 200,
                    ContentType = "application/json",
                    Body = method == "DELETE" ? "" : JsonSerializer.Serialize(response),
                });
            }
        );
        return stub;
    }

    static object[] Folders(bool added) => added ? [Sandbox, Notes] : [Sandbox];

    static readonly object Sandbox = new
    {
        id = SandboxId,
        name = "sandbox",
        path = SandboxPath,
        vendor = "github",
        repository = Repository,
        loopYaml = Loop,
        defaultAgent = "claude-code",
        defaultModel = (string?)null,
        hasGate = true,
    };

    static readonly object Notes = new
    {
        id = AddedId,
        name = "notes",
        path = AddedPath,
        vendor = (string?)null,
        repository = (string?)null,
        loopYaml = (string?)null,
        defaultAgent = "claude-code",
        defaultModel = (string?)null,
        hasGate = false,
    };

    static readonly object NotesPage = new
    {
        id = AddedId,
        name = "notes",
        path = AddedPath,
        vendor = (string?)null,
        repository = (string?)null,
        branch = (string?)null,
        defaultAgent = "claude-code",
        defaultModel = (string?)null,
        loopYaml = (string?)null,
        harnessVersion = (string?)null,
        shippedVersion = "1",
        harness = new[]
        {
            new { path = ".aio/loop.yaml", present = false, stub = false },
            new { path = ".aio/gate", present = false, stub = false },
            new { path = ".aio/version", present = false, stub = false },
        },
        trust = new[] { new { provider = "claude-code", trusted = true, why = "Claude Code trusts the folder.", remedy = (string?)null } },
    };

    static readonly object Validation = new
    {
        valid = true,
        states = new object[]
        {
            new { name = "ready-for-proposal", transitions = Array.Empty<object>() },
            new { name = "verifying", transitions = Array.Empty<object>() },
        },
        undeclared = new[] { "done" },
        problems = Array.Empty<string>(),
    };

    static readonly object OpenPbis = new
    {
        pbis = new[]
        {
            Pbi("3", "Break the sum so the gate goes red", "verifying", held: true, closed: false),
            Pbi("7", "A live session for the tray", "ready-for-proposal", held: false, closed: false),
            Pbi("8", "Not in the loop yet", null, held: false, closed: false),
            Pbi("9", "Ask what the user meant", "needs-refinement", held: false, closed: false),
        },
        unread = Array.Empty<object>(),
    };

    static readonly object DonePbis = new
    {
        pbis = new[] { Pbi("2", "Shipped last week", "done", held: false, closed: true) },
        unread = Array.Empty<object>(),
    };

    static object Pbi(string id, string title, string? state, bool held, bool closed) => new
    {
        vendor = "github",
        repository = Repository,
        id,
        title,
        state,
        held,
        assignee = (string?)null,
        closed,
        folderPath = SandboxPath,
    };

    static readonly object Tools = new
    {
        vendors = new[]
        {
            new { adapter = "github", cli = "gh", path = (string?)"/opt/homebrew/bin/gh", version = (string?)"2.99.0", account = (string?)"octocat", problem = (string?)null },
        },
        agents = new[]
        {
            new { adapter = "claude-code", cli = "claude", path = (string?)null, version = (string?)null, account = (string?)null, problem = (string?)null },
        },
    };
}
