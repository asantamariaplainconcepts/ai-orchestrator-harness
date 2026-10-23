using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #509. The shell: the rail reads /api and moves between the four screens and the
// PBI's five tabs in both themes, and a live session in the tray opens its terminal over the
// WebSocket and closes it again without asking the server for anything that would stop it.
//
// The API and the socket are stubbed at the browser, like the other classes of this collection:
// what is under test is the screen's reading of the contract, and the server side of both is
// covered by the Runs, Pbis and Server functional tests. The running product was checked by hand
// against a live session for the pull request.
[Collection("review-frontend")]
public sealed class ShellTests(ReviewFrontendFixture frontend)
{
    const string FolderId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c6";
    const string RunId = "01a11dcb-dec7-78dd-8f4c-70a254ded118";

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task The_rail_reads_the_api_and_moves_between_the_routes(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var writes = await Arrange(page);
        await page.GotoAsync("/");

        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/pbis$"));
        await Assertions.Expect(page.Locator(".dark")).ToHaveCountAsync(theme == "dark" ? 1 : 0);

        var rail = page.GetByRole(AriaRole.Navigation, new() { Name = "Main" });
        await Assertions.Expect(rail.GetByRole(AriaRole.Link, new() { Name = "PBIs" })).ToContainTextAsync("3");
        await Assertions.Expect(rail.GetByText("1 need you")).ToBeVisibleAsync();
        await Assertions.Expect(rail.GetByText("Folders · 1")).ToBeVisibleAsync();

        // #8 has no state: it is open, and not in the loop, so the rail leaves it to the PBIs screen.
        await Assertions.Expect(rail.GetByText("Not in the loop yet")).ToHaveCountAsync(0);
        var held = rail.GetByRole(AriaRole.Link).Filter(new() { HasText = "Break the sum" });
        await Assertions.Expect(held.GetByRole(AriaRole.Img, new() { Name = "In hold" })).ToBeVisibleAsync();
        var live = rail.GetByRole(AriaRole.Link).Filter(new() { HasText = "A live session" });
        await Assertions.Expect(live.GetByRole(AriaRole.Img, new() { Name = "A run is live" })).ToBeVisibleAsync();

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "PBIs", Level = 1 })).ToBeVisibleAsync();

        await rail.GetByRole(AriaRole.Link, new() { Name = "Loop" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/loop$"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Loop", Level = 1 })).ToBeVisibleAsync();

        await live.ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex($"/pbis/{FolderId}/7/journey$"));
        // #511 filled the PBI's header and its Journey; PbiScreenTests covers them. The tabs are filled
        // too: Issue and Proposal (#512, IssueProposalTests), Review (#513, ReviewTabTests) and Files
        // (#514, FilesTabTests).
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "A live session for the tray" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.List, new() { Name = "Journey" })).ToBeVisibleAsync();

        await rail.GetByRole(AriaRole.Link, new() { Name = "Settings" }).ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex("/settings$"));
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Settings", Level = 1 })).ToBeVisibleAsync();

        await rail.GetByRole(AriaRole.Button, new() { Name = "Actions for sandbox" }).ClickAsync();
        await page.GetByRole(AriaRole.Menuitem, new() { Name = "Explore with agent" }).ClickAsync();
        // #516 put the dialog behind it; SessionsTests covers what it opens.
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog, new() { Name = "Explore with agent" })).ToBeVisibleAsync();

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"shell-{theme}") });
        writes.ShouldBeEmpty("moving around the shell reads and never writes");
    }

    [Fact]
    public async Task A_live_session_opens_from_the_tray_and_closes_without_stopping()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var writes = await Arrange(page);

        var typed = new ConcurrentQueue<string>();
        // Counted rather than flagged: React's development StrictMode mounts the terminal twice, so a
        // first socket opens and closes before the one that stays.
        var attached = 0;
        var detached = 0;
        await page.RouteWebSocketAsync(
            new Regex($"/api/sessions/{RunId}/attach$"),
            socket =>
            {
                Interlocked.Increment(ref attached);
                socket.Send(Encoding.UTF8.GetBytes("stand-in output, replayed on attach\r\n"));
                socket.OnMessage(frame =>
                {
                    if (frame.Binary is { } bytes) typed.Enqueue(Encoding.UTF8.GetString(bytes));
                });
                socket.OnClose((_, _) => Interlocked.Increment(ref detached));
            }
        );

        await page.GotoAsync("/loop");

        var tray = page.GetByRole(AriaRole.Region, new() { Name = "Live sessions" });
        await Assertions.Expect(tray.GetByText("Sessions · 1")).ToBeVisibleAsync();
        var chip = tray.GetByRole(AriaRole.Button).Filter(new() { HasText = "claude-code · #7 /aio:refine 7" });

        await chip.ClickAsync();
        await Assertions.Expect(page.Locator(".xterm-rows")).ToContainTextAsync("stand-in output, replayed on attach");
        await Assertions.Expect(chip).ToHaveAttributeAsync("aria-pressed", "true");

        await page.Locator(".xterm").ClickAsync();
        await page.Keyboard.TypeAsync("hi");
        await Until(() => string.Concat(typed).Contains("hi", StringComparison.Ordinal));

        Volatile.Read(ref detached).ShouldBeLessThan(Volatile.Read(ref attached), "the open terminal holds one socket");
        await chip.ClickAsync();
        await Until(() => Volatile.Read(ref detached) == Volatile.Read(ref attached));
        await Assertions.Expect(page.Locator(".xterm")).ToHaveCountAsync(0);
        await Assertions.Expect(chip).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Loop", Level = 1 })).ToBeVisibleAsync();

        writes.ShouldBeEmpty("closing the terminal detaches; nothing asks the server to stop the run");
    }

    // On a touch phone the terminal grows the row of keys the glass left out. Ctrl latches, the next
    // letter typed becomes its control byte, and the latch clears.
    [Fact]
    public async Task On_a_phone_Ctrl_in_the_row_and_c_interrupt_the_agent_and_the_latch_clears()
    {
        await using var context = await frontend.Open("light", phone: true);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var writes = await Arrange(page);
        var typed = new ConcurrentQueue<string>();
        await page.RouteWebSocketAsync(
            new Regex($"/api/sessions/{RunId}/attach$"),
            socket =>
            {
                socket.Send(Encoding.UTF8.GetBytes("? Allow this edit? (y/n)\r\n"));
                socket.OnMessage(frame =>
                {
                    if (frame.Binary is { } bytes) typed.Enqueue(Encoding.UTF8.GetString(bytes));
                });
            }
        );
        await page.GotoAsync("/loop");
        (await page.EvaluateAsync<bool>("() => matchMedia('(pointer: coarse)').matches")).ShouldBeTrue("the context is a touch phone");

        var tray = page.GetByRole(AriaRole.Region, new() { Name = "Live sessions" });
        await tray.GetByRole(AriaRole.Button).Filter(new() { HasText = "claude-code · #7 /aio:refine 7" }).ClickAsync();
        await Assertions.Expect(page.Locator(".xterm-rows")).ToContainTextAsync("Allow this edit?");

        var keys = page.GetByRole(AriaRole.Toolbar, new() { Name = "Terminal keys" });
        var ctrl = keys.GetByRole(AriaRole.Button, new() { Name = "Ctrl" });
        await ctrl.ClickAsync();
        await Assertions.Expect(ctrl).ToHaveAttributeAsync("aria-pressed", "true");
        await page.Locator(".xterm").ClickAsync();
        await page.Keyboard.TypeAsync("c");

        // Frame by frame: the socket also carries the terminal's size as a frame of its own.
        await Until(() => typed.Contains("\x03"));
        typed.ShouldNotContain("c", "the letter went out as its control byte, not as itself");
        await Assertions.Expect(ctrl).ToHaveAttributeAsync("aria-pressed", "false");
        writes.ShouldBeEmpty("the interrupt goes over the session's socket and nowhere else");
    }

    // The theme control in the rail: light to dark, on every screen, and still dark on the next visit.
    [Fact]
    public async Task Pressing_the_theme_control_in_light_redraws_dark_and_stays_dark_on_the_next_visit()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await Arrange(page);
        await page.GotoAsync("/pbis");
        await Assertions.Expect(page.Locator(".dark")).ToHaveCountAsync(0);
        const string Background = "() => getComputedStyle(document.querySelector('main')).getPropertyValue('--background').trim()";
        var light = await page.EvaluateAsync<string>(Background);

        var theme = page.GetByRole(AriaRole.Button, new() { Name = "Switch between light and dark" });
        await Assertions.Expect(theme).ToHaveTextAsync("Dark");
        await theme.ClickAsync();

        await Assertions.Expect(page.Locator(".dark")).ToHaveCountAsync(1);
        await Assertions.Expect(theme).ToHaveTextAsync("Light");
        var dark = await page.EvaluateAsync<string>(Background);
        dark.ShouldNotBe(light, "the dark tokens are not the light ones");

        await page.GetByRole(AriaRole.Navigation, new() { Name = "Main" }).GetByRole(AriaRole.Link, new() { Name = "Loop" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Loop", Level = 1 })).ToBeVisibleAsync();
        (await page.EvaluateAsync<string>(Background)).ShouldBe(dark);

        // The next visit, in a light system: the choice was stored.
        var next = await context.NewPageAsync();
        await next.SetViewportSizeAsync(1440, 900);
        await Arrange(next);
        await next.GotoAsync("/settings");
        await Assertions.Expect(next.GetByRole(AriaRole.Heading, new() { Name = "Settings", Level = 1 })).ToBeVisibleAsync();
        await Assertions.Expect(next.Locator(".dark")).ToHaveCountAsync(1);
        (await next.EvaluateAsync<string>(Background)).ShouldBe(dark);
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

    /// <summary>Stubs `/api` and answers with every request that was not a read.</summary>
    static async Task<ConcurrentQueue<string>> Arrange(IPage page)
    {
        var writes = new ConcurrentQueue<string>();
        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                if (route.Request.Method != "GET") writes.Enqueue($"{route.Request.Method} {route.Request.Url}");
                object response = new Uri(route.Request.Url).AbsolutePath switch
                {
                    "/api/folders" => Folders,
                    "/api/pbis" => Pbis,
                    "/api/sessions" => Sessions,
                    "/api/loop/present" => Present,
                    "/api/loop/journal" => Array.Empty<object>(),
                    "/api/pbi" => new { pbi = Pbi("7", "A live session for the tray", "ready-for-proposal", held: false), pullRequest = (object?)null },
                    "/api/runs" => Array.Empty<object>(),
                    "/api/loop/definition" => new { name = "loop", agents = Array.Empty<object>(), states = Array.Empty<object>(), undeclared = Array.Empty<string>(), problem = (string?)null },
                    "/api/machine/tools" => new { vendors = Array.Empty<object>(), agents = Array.Empty<object>() },
                    _ => new { },
                };
                await route.FulfillAsync(new()
                {
                    ContentType = "application/json",
                    Body = JsonSerializer.Serialize(response),
                });
            }
        );
        return writes;
    }

    const string FolderPath = "/checkouts/sandbox";
    const string Repository = "owner/sandbox";

    static readonly object[] Folders =
    [
        new { id = FolderId, name = "sandbox", path = FolderPath, vendor = "github", repository = Repository, loopYaml = "" },
    ];

    static readonly object Pbis = new
    {
        pbis = new object[]
        {
            Pbi("7", "A live session for the tray", "ready-for-proposal", held: false),
            Pbi("3", "Break the sum so the gate goes red", "verifying", held: true),
            Pbi("8", "Not in the loop yet", null, held: false),
        },
        unread = Array.Empty<object>(),
    };

    static object Pbi(string id, string title, string? state, bool held) => new
    {
        vendor = "github",
        repository = Repository,
        id,
        title,
        state,
        held,
        assignee = (string?)null,
        closed = false,
        folderPath = FolderPath,
    };

    // The engine off and silent: the Loop screen (#510) has its own tests in LoopTests.
    static readonly object Present = new
    {
        enabled = false,
        intervalSeconds = 60,
        lastPass = (object?)null,
        nextPassAt = (DateTimeOffset?)null,
        liveRuns = Array.Empty<object>(),
        folders = Array.Empty<object>(),
        lastDecision = (object?)null,
    };

    static readonly object[] Sessions =
    [
        new
        {
            runId = RunId,
            repository = Repository,
            pbi = "7",
            state = "ready-for-proposal",
            manual = false,
            provider = "claude-code",
            command = "/aio:refine 7",
            startedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            lastOutputAt = DateTimeOffset.UtcNow,
            stale = false,
        },
    ];
}
