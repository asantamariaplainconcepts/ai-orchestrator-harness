using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #516. The sessions a person opens by hand: Explore with agent and Terminal, on a PBI from its
// header and on a folder from the rail's menu. The dialog offers the agents the machine has, one
// not on the PATH shown and not choosable, and the chosen agent's models; the folder's agent and
// model come selected. A session opened goes to the tray and opens over the screen, and on a PBI it
// is a manual run in the Journey. #577: any session in the tray is ended from its chip or from the
// terminal over the screen, after one line saying what dies; closing still ends nothing.
//
// The API is stubbed at the browser, like PbiScreenTests: what is under test is the screen's
// reading of the contract and the writes it sends. That a manual run stores its recording and
// never moves the loop is PbiRunTests', against the real host.
[Collection("review-frontend")]
public sealed class SessionsTests(ReviewFrontendFixture frontend)
{
    const string FolderId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c6";
    const string Repository = "owner/sandbox";
    const string OpenedRun = "01a11dcb-dec7-78dd-8f4c-70a254ded301";

    [Theory]
    [InlineData("light")]
    public async Task Explore_with_agent_on_a_pbi_changes_the_models_with_the_agent_and_lands_in_the_journey_as_a_manual_run(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var stub = new Stub();
        await Arrange(page, stub);

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");
        await page.GetByRole(AriaRole.Button, new() { Name = "Explore with agent" }).ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Explore with agent" });
        await Assertions.Expect(dialog).ToContainTextAsync("sandbox · #495 · change/495");
        var agent = dialog.GetByLabel(new Regex("^Agent"));
        var model = dialog.GetByLabel(new Regex("^Model"));

        // The folder's agent and model, selected and not labelled.
        await Assertions.Expect(agent).ToHaveValueAsync("claude-code");
        await Assertions.Expect(model).ToHaveValueAsync("claude-sonnet-5-5");
        await Assertions.Expect(model.Locator("option")).ToHaveTextAsync(["claude-opus-5-5", "claude-fable-5-1", "claude-sonnet-5-5", "claude-haiku-4-5"]);
        // One not on the PATH is shown and cannot be chosen.
        await Assertions.Expect(agent.Locator("option[disabled]")).ToHaveTextAsync("copilot · not on PATH");

        // Another agent, another list.
        await agent.SelectOptionAsync("opencode");
        await Assertions.Expect(model.Locator("option")).ToHaveTextAsync(["gpt-6-sol", "claude-opus-5-5"]);
        await Assertions.Expect(model).ToHaveValueAsync("gpt-6-sol");
        await model.SelectOptionAsync("claude-opus-5-5");

        // The current state's command is the suggestion, with the PBI's number.
        await dialog.GetByRole(AriaRole.Button, new() { Name = "/aio:review-diff 495" }).ClickAsync();
        await Assertions.Expect(dialog.GetByLabel(new Regex("^Command"))).ToHaveValueAsync("/aio:review-diff 495");
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"explore-pbi-{theme}") });

        stub.Writes.ShouldBeEmpty("choosing reads and never writes");
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Open" }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);

        // One session opened, and nothing moved.
        stub.Writes.ShouldBe(
            [$$"""POST /api/pbi/sessions {"repository":"{{Repository}}","id":"495","provider":"opencode","model":"claude-opus-5-5","command":"/aio:review-diff 495"}"""]
        );

        // The tray lists it, and its terminal is open over the screen.
        var tray = page.GetByRole(AriaRole.Region, new() { Name = "Live sessions" });
        var chip = tray.GetByRole(AriaRole.Button, new() { Name = "Close the terminal of opencode · #495 /aio:review-diff 495. The run keeps running." });
        await Assertions.Expect(chip).ToHaveAttributeAsync("aria-pressed", "true");
        await Assertions.Expect(page.GetByText("explore output")).ToBeVisibleAsync();
        await chip.ClickAsync();

        // In the Journey, a manual run of the state the PBI is in; the PBI is where it was.
        var row = page.GetByRole(AriaRole.Button, new() { Name = "End record of /aio:review-diff 495" });
        await Assertions.Expect(row).ToContainTextAsync("manual");
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Journey" }).Locator("li[aria-current=step]")).ToContainTextAsync("code-review");
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"explore-journey-{theme}"), FullPage = true });
    }

    [Theory]
    [InlineData("light")]
    public async Task A_folder_terminal_is_ended_from_the_tray_by_keyboard_at_375px_after_its_confirmation(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var stub = new Stub();
        await Arrange(page, stub);

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");
        var rail = page.GetByRole(AriaRole.Navigation, new() { Name = "Main" });
        await rail.GetByRole(AriaRole.Button, new() { Name = "Actions for sandbox" }).ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new() { Name = "Terminal" }).ClickAsync();
        var tray = page.GetByRole(AriaRole.Region, new() { Name = "Live sessions" });
        var chip = tray.GetByRole(AriaRole.Button, new() { Name = "Close the terminal of terminal · sandbox. The run keeps running." });
        // No dialog: the shell opens in the checkout, and over the screen.
        await Assertions.Expect(chip).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToHaveCountAsync(0);
        stub.Writes.ShouldBe(["""POST /api/folders/sessions {"path":"/checkouts/sandbox"}"""]);
        stub.Writes.Clear();

        // The terminal takes focus when it mounts, which a loaded machine does late; the chip is
        // focused once it has, or the Tab below starts from the terminal.
        await Assertions.Expect(page.GetByText("explore output")).ToBeVisibleAsync();
        // #515 saw the tray's text run off the right edge at 375px. The shell lays out again for the
        // narrow width a frame or two after the resize, so the width is read once the tray settles.
        await page.SetViewportSizeAsync(375, 812);
        try
        {
            await page.WaitForFunctionAsync(
                "() => { const tray = document.querySelector('[aria-label=\"Live sessions\"]'); return tray && tray.scrollWidth <= tray.clientWidth; }",
                null,
                new() { Timeout = 5_000 }
            );
        }
        catch (TimeoutException)
        {
            // Measured below, where the assertion says what overflowed.
        }
        (await tray.EvaluateAsync<int>("tray => tray.scrollWidth - tray.clientWidth")).ShouldBeLessThanOrEqualTo(0, "nothing in the tray is wider than a phone");
        await chip.FocusAsync();
        await Assertions.Expect(chip).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Tab");
        var end = tray.GetByRole(AriaRole.Button, new() { Name = "End terminal · sandbox" });
        await Assertions.Expect(end).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");

        // One line, and focus on Cancel, so a second Enter does not kill anything by accident.
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "End session" });
        await Assertions.Expect(dialog).ToContainTextAsync("The process of terminal · sandbox is killed, and nothing of the session is kept.");
        await Assertions.Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "Cancel" })).ToBeFocusedAsync();
        var overflow = await page.EvaluateAsync<int>("document.documentElement.scrollWidth - window.innerWidth");
        overflow.ShouldBeLessThanOrEqualTo(0, "the confirmation fits a phone");
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"end-folder-terminal-375-{theme}") });
        stub.Writes.ShouldBeEmpty("asking sends nothing");

        await page.Keyboard.PressAsync("Tab");
        await Assertions.Expect(dialog.GetByRole(AriaRole.Button, new() { Name = "End session" })).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");

        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        stub.Writes.ShouldBe([$"POST /api/runs/{OpenedRun}/stop {{}}"]);
        await Assertions.Expect(tray).ToContainTextAsync("No live sessions.");
        await Assertions.Expect(page.GetByText("explore output")).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Closing_the_terminal_ends_nothing_and_ending_it_over_the_screen_asks_first()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var stub = new Stub { Session = FolderTerminal() };
        await Arrange(page, stub);

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");
        var tray = page.GetByRole(AriaRole.Region, new() { Name = "Live sessions" });
        var opener = tray.GetByRole(AriaRole.Button, new() { Name = "Open the terminal of terminal · sandbox" });
        await opener.ClickAsync();
        await Assertions.Expect(page.GetByText("explore output")).ToBeVisibleAsync();

        // Close, from the terminal's own button: the terminal goes, the chip and the session stay.
        var main = page.GetByRole(AriaRole.Main);
        await main.GetByRole(AriaRole.Button, new() { Name = "Close the terminal of terminal · sandbox. The run keeps running." }).ClickAsync();
        await Assertions.Expect(page.GetByText("explore output")).ToHaveCountAsync(0);
        await Assertions.Expect(opener).ToBeVisibleAsync();
        stub.Writes.ShouldBeEmpty("closing ends nothing");

        // End session over the terminal asks; Escape answers no.
        await opener.ClickAsync();
        await main.GetByRole(AriaRole.Button, new() { Name = "End terminal · sandbox" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "End session" });
        await Assertions.Expect(dialog).ToBeVisibleAsync();
        await page.Keyboard.PressAsync("Escape");
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByText("explore output")).ToBeVisibleAsync();
        stub.Writes.ShouldBeEmpty("Escape ends nothing");
    }

    [Fact]
    public async Task Ending_a_loop_run_from_the_tray_says_the_pbi_holds_on_the_next_pass_and_stops_it()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var now = DateTimeOffset.UtcNow;
        var stub = new Stub
        {
            Session = new
            {
                runId = OpenedRun,
                repository = Repository,
                pbi = "495",
                state = "in-progress",
                manual = false,
                provider = "claude-code",
                command = "/aio:implement 495",
                startedAt = now,
                lastOutputAt = now,
                stale = false,
                folderPath = "/checkouts/sandbox",
            },
        };
        await Arrange(page, stub);

        await page.GotoAsync($"/pbis/{FolderId}/495/journey");
        var tray = page.GetByRole(AriaRole.Region, new() { Name = "Live sessions" });
        await tray.GetByRole(AriaRole.Button, new() { Name = "End claude-code · #495 /aio:implement 495" }).ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "End session" });
        await Assertions.Expect(dialog).ToContainTextAsync(
            "The session of /aio:implement 495 is killed. The run ends as failed, and on its next pass the loop holds #495 with \"Stopped by a person\"."
        );
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath("end-loop-run") });
        stub.Writes.ShouldBeEmpty("the warning is read before anything dies");

        await dialog.GetByRole(AriaRole.Button, new() { Name = "End session" }).ClickAsync();
        await Assertions.Expect(dialog).ToHaveCountAsync(0);
        stub.Writes.ShouldBe([$"POST /api/runs/{OpenedRun}/stop {{}}"]);
        await Assertions.Expect(tray).ToContainTextAsync("No live sessions.");
    }

    static object FolderTerminal()
    {
        var now = DateTimeOffset.UtcNow;
        return new
        {
            runId = OpenedRun,
            repository = (string?)null,
            pbi = (string?)null,
            state = (string?)null,
            manual = true,
            provider = (string?)null,
            command = "",
            startedAt = now,
            lastOutputAt = now,
            stale = false,
            folderPath = "/checkouts/sandbox",
        };
    }

    /// <summary>What the stub serves, the session once one is opened, and every write the screens sent.</summary>
    sealed class Stub
    {
        public ConcurrentQueue<string> Writes { get; } = new();

        public object? Session { get; set; }

        public object? ManualRun { get; set; }
    }

    static async Task Arrange(IPage page, Stub stub)
    {
        await page.RouteWebSocketAsync(
            new Regex($"/api/sessions/{OpenedRun}/attach$"),
            socket => socket.Send(Encoding.UTF8.GetBytes("explore output\r\n"))
        );

        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var request = route.Request;
                var path = new Uri(request.Url).AbsolutePath;
                var now = DateTimeOffset.UtcNow;

                // #577. Ending a session: it dies, and the tray's next read lists none.
                if (request.Method == "POST" && path == $"/api/runs/{OpenedRun}/stop")
                {
                    stub.Writes.Enqueue($"{request.Method} {path} {request.PostData}");
                    stub.Session = null;
                    await route.FulfillAsync(new() { Status = 204 });
                    return;
                }

                if (request.Method != "GET")
                {
                    stub.Writes.Enqueue($"{request.Method} {path} {request.PostData}");
                    var body = JsonDocument.Parse(request.PostData ?? "{}").RootElement;
                    var provider = body.TryGetProperty("provider", out var named) ? named.GetString() : null;
                    var command = body.TryGetProperty("command", out var asked) ? asked.GetString() ?? "" : "";
                    var onPbi = path == "/api/pbi/sessions";

                    stub.Session = new
                    {
                        runId = OpenedRun,
                        repository = onPbi ? Repository : null,
                        pbi = onPbi ? "495" : null,
                        state = onPbi ? "code-review" : null,
                        manual = true,
                        provider,
                        command,
                        startedAt = now,
                        lastOutputAt = now,
                        stale = false,
                        folderPath = "/checkouts/sandbox",
                    };
                    if (onPbi)
                    {
                        stub.ManualRun = new
                        {
                            runId = OpenedRun,
                            state = "code-review",
                            manual = true,
                            provider,
                            model = "claude-opus-5-5",
                            command,
                            startedAt = now,
                            endedAt = (DateTimeOffset?)null,
                            lastOutputAt = (DateTimeOffset?)now,
                            stale = false,
                            outcome = (string?)null,
                            reason = (string?)null,
                            failure = (string?)null,
                            problems = Array.Empty<string>(),
                            endRecord = (string?)null,
                            inputTokens = (long?)null,
                            outputTokens = (long?)null,
                            costUsd = (decimal?)null,
                            recordingPath = $"/runs/{OpenedRun}/session.cast",
                            recorded = false,
                        };
                    }
                    await Json(route, new { runId = OpenedRun });
                    return;
                }

                object response = path switch
                {
                    "/api/folders" => Folders,
                    "/api/pbis" => new { pbis = new[] { Pbi }, unread = Array.Empty<object>() },
                    "/api/sessions" => Listed(stub.Session),
                    "/api/pbi" => new { pbi = Pbi, pullRequest = (object?)null },
                    "/api/runs" => Listed(stub.ManualRun),
                    "/api/loop/journal" => Array.Empty<object>(),
                    "/api/loop/definition" => Loop,
                    "/api/machine/tools" => Tools,
                    _ => new { },
                };
                await Json(route, response);
            }
        );
    }

    static object[] Listed(object? row) => row is null ? [] : [row];

    static Task Json(IRoute route, object body) =>
        route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(body) });

    static readonly object Pbi = new
    {
        vendor = "github",
        repository = Repository,
        id = "495",
        title = "Declare the loop as a state machine",
        state = "code-review",
        held = false,
        assignee = (string?)null,
        closed = false,
        folderPath = "/checkouts/sandbox",
    };

    static readonly object[] Folders =
    [
        new
        {
            id = FolderId,
            name = "sandbox",
            path = "/checkouts/sandbox",
            vendor = "github",
            repository = Repository,
            loopYaml = "",
            defaultAgent = "claude-code",
            defaultModel = "claude-sonnet-5-5",
            hasGate = true,
        },
    ];

    static readonly object Tools = new
    {
        vendors = Array.Empty<object>(),
        agents = new object[]
        {
            Tool("claude-code", "claude", "/usr/local/bin/claude", "2.1.4", ["claude-opus-5-5", "claude-fable-5-1", "claude-sonnet-5-5", "claude-haiku-4-5"]),
            Tool("copilot", "copilot", null, null, []),
            Tool("opencode", "opencode", "/usr/local/bin/opencode", "0.9.2", ["gpt-6-sol", "claude-opus-5-5"]),
        },
    };

    static object Tool(string adapter, string cli, string? path, string? version, string[] models) =>
        new { adapter, cli, path, version, account = (string?)null, problem = (string?)null, models };

    static readonly object Loop = new
    {
        name = "spec-driven-issue-loop",
        agents = new[] { new { role = "reviewer", provider = "copilot", model = (string?)null } },
        states = new object[]
        {
            State("in-progress", "/aio:implement", "verifying"),
            State("code-review", "/aio:review-diff", "merge-review"),
            State("merge-review", null, "done"),
        },
        undeclared = new[] { "done" },
        problem = (string?)null,
    };

    static object State(string name, string? command, string next) => new
    {
        name,
        scripts = Array.Empty<string>(),
        agent = command is null ? null : "reviewer",
        command,
        score = (string?)null,
        provider = (string?)null,
        model = (string?)null,
        maxLoops = (int?)null,
        transitions = new[] { new { outcome = (string?)null, target = next, hold = (string?)null } },
    };
}
