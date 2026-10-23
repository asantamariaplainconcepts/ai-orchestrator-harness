using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #510. The Loop screen at /loop: the engine's strip, the three bands with each row's reason,
// and the journal's latest decisions, read from /api and polled. A hold is on top with the reason
// the engine wrote, and its row opens the PBI's Journey by mouse and by keyboard. A stale run
// goes back to motion on the next poll, without anybody reloading. Each folder has
// its engine switch in the strip (#564), and a folder without a loop cannot be switched on.
//
// The API is stubbed at the browser, like ShellTests: what is under test is the screen's reading
// of the contract. The engine itself is covered by the Loop functional tests (EngineSwitchPresentTests
// for the switch against the real host), and the running product was checked by hand against a live
// engine for the pull request.
[Collection("review-frontend")]
public sealed class LoopTests(ReviewFrontendFixture frontend)
{
    const string FolderId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c6";
    const string RunId = "01a11dcb-dec7-78dd-8f4c-70a254ded118";
    const string FolderPath = "/checkouts/sandbox";
    const string Repository = "owner/sandbox";
    const string BarePath = "/checkouts/bare";
    const string BareRepository = "owner/bare";
    const string NoLoop = "No loop declared. Add .aio/loop.yaml to the repository.";

    [Theory]
    [InlineData("light")]
    public async Task A_hold_is_on_top_with_its_reason_and_opens_its_Journey(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var engine = new Engine();
        var writes = await Arrange(page, engine);
        await page.GotoAsync("/loop");

        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Loop", Level = 1 })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("4 open PBIs.", new() { Exact = false })).ToBeVisibleAsync();

        var strip = page.GetByRole(AriaRole.Region, new() { Name = "The engine now" });
        await Assertions.Expect(strip).ToContainTextAsync("Engine · 1 of 2 folders on");
        await Assertions.Expect(strip).ToContainTextAsync("every 60 s · last");
        await Assertions.Expect(strip).ToContainTextAsync("1 run · in-progress #7");
        await Assertions.Expect(strip).ToContainTextAsync("WIP 1 of 4");
        await Assertions.Expect(strip).ToContainTextAsync("#3 verifying → hold");

        var needsYou = page.GetByRole(AriaRole.Region, new() { Name = "Needs you · 1" });
        var held = needsYou.GetByRole(AriaRole.Link);
        await Assertions.Expect(held).ToHaveCountAsync(1);
        await Assertions.Expect(held).ToContainTextAsync("Break the sum so the gate goes red");
        await Assertions.Expect(held).ToContainTextAsync("Hold · gate failed: 2 tests red");
        await Assertions.Expect(held.GetByRole(AriaRole.Img, new() { Name = "In hold" })).ToBeVisibleAsync();

        var inMotion = page.GetByRole(AriaRole.Region, new() { Name = "In motion · 1" });
        await Assertions.Expect(inMotion).ToContainTextAsync("claude-code · /aio:implement 7");
        await Assertions.Expect(inMotion).ToContainTextAsync("last output");

        var rest = page.GetByRole(AriaRole.Region, new() { Name = "The rest · 2" });
        await Assertions.Expect(rest).ToContainTextAsync("Next: /aio:propose · waiting, WIP 4 of 4");
        await Assertions.Expect(rest).ToContainTextAsync("Not in the loop yet");

        var decisions = page.GetByRole(AriaRole.Region, new() { Name = "Last decisions · what the engine did and why" });
        await Assertions.Expect(decisions.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(3);
        await Assertions.Expect(decisions).ToContainTextAsync("gate failed: 2 tests red");

        // Above the bands so the capture is of what the dev sees first.
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"loop-{theme}") });

        await held.ClickAsync();
        await Assertions.Expect(page).ToHaveURLAsync(new Regex($"/pbis/{FolderId}/3/journey$"));

        writes.ShouldBeEmpty("the Loop screen writes only when a switch is pressed");
    }

    // #655. A ran line links to everything its script printed, as the Journey does (#641), beside the
    // row that opens the Journey and not inside it.
    [Theory]
    [InlineData("light")]
    public async Task A_ran_line_links_to_its_script_log(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await Arrange(page, new Engine { Ran = true });
        await page.GotoAsync("/loop");

        var decisions = page.GetByRole(AriaRole.Region, new() { Name = "Last decisions · what the engine did and why" });
        await Assertions.Expect(decisions.GetByRole(AriaRole.Listitem)).ToHaveCountAsync(4);
        var log = decisions.GetByRole(AriaRole.Link, new() { Name = "Everything gate printed" });
        await Assertions.Expect(log).ToHaveCountAsync(1);
        await Assertions.Expect(log).ToHaveAttributeAsync("href", "/api/loop/journal/41/output");
        await Assertions.Expect(log).ToHaveAttributeAsync("target", "_blank");
        await Assertions.Expect(log).ToHaveTextAsync("log");
        (await page.Locator("a a").CountAsync()).ShouldBe(0, "the log link is not nested in the row's link");

        var ran = decisions.GetByRole(AriaRole.Listitem).Nth(1);
        await Assertions.Expect(ran).ToContainTextAsync("verifying · script ran");
        await Assertions.Expect(ran.GetByRole(AriaRole.Link, new() { Name = "Everything gate printed" })).ToBeVisibleAsync();

        await decisions.ScreenshotAsync(new() { Path = frontend.CapturePath($"loop-log-{theme}") });
    }

    // #564. Every folder off: the strip says nothing moves, and a folder's switch turns its engine on
    // and off from the keyboard and the mouse. Each press is one PUT, and what the switch shows after
    // it is the next read of the present, not the press. The folder without a loop offers nothing
    // and says why.
    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task A_switch_turns_a_folder_on_and_off_and_the_next_read_shows_it(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var engine = new Engine { Enabled = false };
        var writes = await Arrange(page, engine);
        await page.GotoAsync("/loop");

        var strip = page.GetByRole(AriaRole.Region, new() { Name = "The engine now" });
        await Assertions.Expect(strip).ToContainTextAsync("Engine · 0 of 2 folders on");
        await Assertions.Expect(strip).ToContainTextAsync("Off. Nothing moves on its own.");
        await Assertions.Expect(strip).Not.ToContainTextAsync("Loop:Enabled");

        var sandbox = strip.GetByRole(AriaRole.Switch, new() { Name = "Engine for sandbox" });
        await Assertions.Expect(sandbox).ToHaveAttributeAsync("aria-checked", "false");

        // The folder without a loop: off, focusable, refusing, and saying why.
        var bare = strip.GetByRole(AriaRole.Switch, new() { Name = $"Engine for {BareRepository}" });
        await Assertions.Expect(bare).ToHaveAttributeAsync("aria-checked", "false");
        await Assertions.Expect(bare).ToHaveAttributeAsync("aria-disabled", "true");
        await Assertions.Expect(bare).ToHaveAccessibleDescriptionAsync(NoLoop);
        // Forced, because Playwright reads aria-disabled as "do not click"; a person can still try.
        await bare.ClickAsync(new() { Force = true });
        await bare.FocusAsync();
        await page.Keyboard.PressAsync("Space");
        await page.WaitForTimeoutAsync(300);
        writes.ShouldBeEmpty("a folder without a loop offers no switch to press");

        // On by keyboard.
        await sandbox.FocusAsync();
        await Assertions.Expect(sandbox).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Space");
        await Assertions.Expect(sandbox).ToHaveAttributeAsync("aria-checked", "true");
        await Assertions.Expect(strip).ToContainTextAsync("Engine · 1 of 2 folders on");
        await Assertions.Expect(strip).ToContainTextAsync("every 60 s");
        await Assertions.Expect(strip).ToContainTextAsync("sandbox engine on");
        var decisions = page.GetByRole(AriaRole.Region, new() { Name = "Last decisions · what the engine did and why" });
        await Assertions.Expect(decisions.GetByRole(AriaRole.Listitem).First).ToContainTextAsync("engine on");

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"loop-switch-{theme}") });

        // Off by mouse.
        await sandbox.ClickAsync();
        await Assertions.Expect(sandbox).ToHaveAttributeAsync("aria-checked", "false");
        await Assertions.Expect(strip).ToContainTextAsync("Engine · 0 of 2 folders on");
        await Assertions.Expect(strip).ToContainTextAsync("sandbox engine off");

        writes.ToArray().ShouldBe([$"PUT {Repository} on", $"PUT {Repository} off"]);
    }

    [Fact]
    public async Task At_a_narrow_width_by_keyboard_a_row_opens_its_Journey()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(600, 900);
        await Arrange(page, new Engine());
        await page.GotoAsync("/loop");

        var held = page.GetByRole(AriaRole.Region, new() { Name = "Needs you · 1" }).GetByRole(AriaRole.Link);
        await Assertions.Expect(held).ToBeVisibleAsync();

        // Nothing wider than the window: the rows and the strip fold instead of scrolling sideways.
        var overflow = await page.EvaluateAsync<bool>(
            "() => [...document.querySelectorAll('main *')].some(e => e.scrollWidth > e.clientWidth + 1 && getComputedStyle(e).overflowX === 'auto')"
        );
        overflow.ShouldBeFalse("the Loop screen fits a narrow window");
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath("loop-narrow") });

        await held.FocusAsync();
        await Assertions.Expect(held).ToBeFocusedAsync();
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page).ToHaveURLAsync(new Regex($"/pbis/{FolderId}/3/journey$"));
    }

    [Theory]
    [InlineData("light")]
    public async Task A_stale_run_needs_you_until_it_writes_again(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var engine = new Engine { Stale = true };
        var writes = await Arrange(page, engine);
        await page.GotoAsync("/loop");

        // #546. Silent past Runs:StaleAfter: the run's PBI joins the hold in Needs you, with how
        // long it has said nothing, and the rail counts it. It is not a hold, so it carries no pause.
        var needsYou = page.GetByRole(AriaRole.Region, new() { Name = "Needs you · 2" });
        await Assertions.Expect(needsYou).ToBeVisibleAsync();
        var stale = needsYou.GetByRole(AriaRole.Link).Filter(new() { HasText = "A live run in progress" });
        await Assertions.Expect(stale).ToContainTextAsync("claude-code · /aio:implement 7");
        await Assertions.Expect(stale).ToContainTextAsync("stale, silent for 14m");
        await Assertions.Expect(stale.GetByRole(AriaRole.Img, new() { Name = "In hold" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "In motion · 0" })).ToBeVisibleAsync();
        var rail = page.GetByRole(AriaRole.Navigation, new() { Name = "Main" });
        await Assertions.Expect(rail).ToContainTextAsync("2 need you");

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"loop-stale-{theme}") });

        // It writes: back in motion on the next poll, and nobody reloads.
        engine.Stale = false;

        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "In motion · 1" })).ToBeVisibleAsync(new() { Timeout = 15_000 });
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Needs you · 1" })).ToBeVisibleAsync();
        await Assertions.Expect(rail).ToContainTextAsync("1 need you", new() { Timeout = 15_000 });

        writes.ShouldBeEmpty("a stale run is not a hold, and nothing is written for it");
    }

    [Theory]
    [InlineData("light", 1440)]
    [InlineData("light", 390)]
    public async Task A_running_gate_is_in_motion_with_its_script_and_how_long(string theme, int width)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(width, 900);
        var engine = new Engine { Gating = true };
        var writes = await Arrange(page, engine);
        await page.GotoAsync("/loop");

        // #597. The gate runs inside the engine's pass: no session, no run, but the present names it.
        var inMotion = page.GetByRole(AriaRole.Region, new() { Name = "In motion · 2" });
        var gating = inMotion.GetByRole(AriaRole.Link).Filter(new() { HasText = "Break the sum so the gate goes red" });
        await Assertions.Expect(gating).ToContainTextAsync("Running gate · 3m");
        await Assertions.Expect(gating.GetByRole(AriaRole.Img, new() { Name = "A script is running" })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Needs you · 0" })).ToBeVisibleAsync();

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"loop-script-{theme}-{width}"), FullPage = true });

        // Past the stale threshold it needs a person, as a silent run does.
        engine.GateStale = true;
        var needsYou = page.GetByRole(AriaRole.Region, new() { Name = "Needs you · 1" });
        await Assertions.Expect(needsYou).ToContainTextAsync("Running gate · 3m · longer than a run may stay silent", new() { Timeout = 15_000 });

        writes.ShouldBeEmpty("a running script is not a hold, and nothing is written for it");
    }

    // The host has just started: the present's last pass is null, and the strip says so.
    [Fact]
    public async Task After_a_restart_the_strip_says_no_pass_has_run()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await Arrange(page, new Engine { NoPass = true });
        await page.GotoAsync("/loop");

        var strip = page.GetByRole(AriaRole.Region, new() { Name = "The engine now" });
        await Assertions.Expect(strip).ToContainTextAsync("every 60 s · no pass since the server started");
        await Assertions.Expect(strip).Not.ToContainTextAsync("· last");
    }

    // A person released #3's hold a minute ago: the line reads released, from the held state to
    // where it went, with the reason the engine writes for a person's release.
    [Fact]
    public async Task A_release_by_a_person_reads_as_released_from_the_held_state_to_its_target()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await Arrange(page, new Engine { Released = true });
        await page.GotoAsync("/loop");

        var decisions = page.GetByRole(AriaRole.Region, new() { Name = "Last decisions · what the engine did and why" });
        var released = decisions.GetByRole(AriaRole.Listitem).First;
        await Assertions.Expect(released).ToContainTextAsync("verifying → done, released");
        await Assertions.Expect(released).ToContainTextAsync("a person released the hold");
    }

    /// <summary>What the stubbed engine answers.</summary>
    sealed class Engine
    {
        /// <summary>The host has just started and no pass has run.</summary>
        public bool NoPass { get; init; }

        /// <summary>A person released #3's hold a minute ago.</summary>
        public bool Released { get; init; }

        readonly ConcurrentQueue<object> _switched = new();
        volatile bool _stale;
        volatile bool _on = true;

        /// <summary>The sandbox's switch; the folder without a loop is always off.</summary>
        public bool Enabled
        {
            get => _on;
            init => _on = value;
        }

        /// <summary>What <c>PUT /api/loop/engine</c> does: the switch, and a line when it changed.</summary>
        public (int Status, object Body) Switch(string repository, bool on)
        {
            if (repository == BareRepository && on)
            {
                return (409, new { title = "Conflict", status = 409, detail = $"The engine cannot be turned on for {BareRepository}: {NoLoop}" });
            }

            object? line = null;
            if (repository == Repository && on != _on)
            {
                line = new
                {
                    at = DateTimeOffset.UtcNow,
                    repository,
                    pbi = "",
                    from = _on ? "on" : "off",
                    kind = "switched",
                    to = on ? "on" : "off",
                    why = on ? "A person turned the engine on for this folder." : "A person turned the engine off for this folder.",
                };
                _switched.Enqueue(line);
                _on = on;
            }

            return (200, new { repository, on, written = line });
        }

        /// <summary>Whether #7's run has been silent past the threshold, fourteen minutes.</summary>
        public bool Stale
        {
            get => _stale;
            set => _stale = value;
        }

        DateTimeOffset LastOutput => DateTimeOffset.UtcNow.AddSeconds(_stale ? -14 * 60 : -4);

        /// <summary>The journal carries the ran line of #3's red gate, with the file its output went to (#655).</summary>
        public bool Ran { get; init; }

        /// <summary>#3 is not held but in verifying with its gate running for three minutes (#597).</summary>
        public bool Gating { get; init; }

        volatile bool _gateStale;

        /// <summary>Whether that gate has run past the stale threshold.</summary>
        public bool GateStale
        {
            get => _gateStale;
            set => _gateStale = value;
        }

        readonly DateTimeOffset _gateSince = DateTimeOffset.UtcNow.AddMinutes(-3).AddSeconds(-5);

        public object Pbis() => !Gating
            ? LoopTests.Pbis
            : new
            {
                pbis = new object[]
                {
                    Pbi("7", "A live run in progress", "in-progress", held: false),
                    Pbi("3", "Break the sum so the gate goes red", "verifying", held: false),
                    Pbi("9", "Wait for a slot under the WIP", "spec-ready", held: false),
                    Pbi("8", "Not in the loop yet", null, held: false),
                },
                unread = Array.Empty<object>(),
            };

        public object Present()
        {
            var now = DateTimeOffset.UtcNow;
            var runs = new List<object> { Run("7", "in-progress", LastOutput, _stale) };
            return new
            {
                enabled = _on,
                intervalSeconds = 60,
                lastPass = NoPass ? null : new { at = now.AddSeconds(-12), folders = 1, pbis = 4, skipped = Array.Empty<string>(), written = 1, failure = (string?)null },
                nextPassAt = now.AddSeconds(48),
                liveRuns = runs,
                liveScripts = Gating
                    ? new object[] { new { repository = Repository, pbi = "3", state = "verifying", script = "gate", since = _gateSince, stale = _gateStale } }
                    : [],
                folders = new[]
                {
                    new { path = FolderPath, vendor = "github", repository = Repository, on = _on, wip = (int?)4, problem = (string?)null },
                    new { path = BarePath, vendor = "github", repository = BareRepository, on = false, wip = (int?)null, problem = (string?)NoLoop },
                },
                lastDecision = Journal()[0],
            };
        }

        public object[] Journal()
        {
            var now = DateTimeOffset.UtcNow;
            var lines = new List<object>
            {
                Line(now.AddMinutes(-5), "3", "verifying", "held", null, "gate failed: 2 tests red"),
                Line(now.AddMinutes(-9), "9", "spec-ready", "waited", null, "WIP 4 of 4"),
                Line(now.AddMinutes(-20), "7", "spec-ready", "moved", "in-progress", "next"),
            };
            if (Ran)
            {
                lines.Insert(1, new
                {
                    id = 41L,
                    at = now.AddMinutes(-6),
                    repository = Repository,
                    pbi = "3",
                    from = "verifying",
                    kind = "ran",
                    to = (string?)null,
                    why = "gate: red, 2 tests red",
                    output = "/data/scripts/owner/sandbox/3/gate-20261011T101500.log",
                });
            }
            if (Released)
            {
                lines.Insert(0, Line(now.AddMinutes(-1), "3", "verifying", "released", "done", "a person released the hold"));
            }
            lines.InsertRange(0, _switched.Reverse());
            return [.. lines];
        }

        public object[] Sessions() =>
        [
            new
            {
                runId = RunId,
                repository = Repository,
                pbi = "7",
                state = "in-progress",
                manual = false,
                provider = "claude-code",
                command = "/aio:implement 7",
                startedAt = DateTimeOffset.UtcNow.AddMinutes(-12),
                lastOutputAt = LastOutput,
                stale = _stale,
            },
        ];

        static object Run(string pbi, string state, DateTimeOffset lastOutputAt, bool stale) => new
        {
            runId = Guid.NewGuid(),
            repository = Repository,
            pbi,
            state,
            lastOutputAt,
            stale,
        };

        static object Line(DateTimeOffset at, string pbi, string from, string kind, string? to, string why) =>
            new { at, repository = Repository, pbi, from, kind, to, why };
    }

    static async Task<ConcurrentQueue<string>> Arrange(IPage page, Engine engine)
    {
        var writes = new ConcurrentQueue<string>();
        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var path = new Uri(route.Request.Url).AbsolutePath;
                if (route.Request.Method == "PUT" && path == "/api/loop/engine")
                {
                    using var body = JsonDocument.Parse(route.Request.PostData ?? "{}");
                    var repository = body.RootElement.GetProperty("repository").GetString() ?? "";
                    var on = body.RootElement.GetProperty("on").GetBoolean();
                    writes.Enqueue($"PUT {repository} {(on ? "on" : "off")}");
                    var (status, switched) = engine.Switch(repository, on);
                    await route.FulfillAsync(new()
                    {
                        Status = status,
                        ContentType = status == 200 ? "application/json" : "application/problem+json",
                        Body = JsonSerializer.Serialize(switched),
                    });
                    return;
                }

                if (route.Request.Method != "GET") writes.Enqueue($"{route.Request.Method} {route.Request.Url}");
                object response = path switch
                {
                    "/api/folders" => Folders,
                    "/api/pbis" => engine.Pbis(),
                    "/api/sessions" => engine.Sessions(),
                    "/api/loop/present" => engine.Present(),
                    "/api/loop/journal" => engine.Journal(),
                    "/api/loop/definition" => Definition,
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

    // The loop of this repository, cut to the states the rows are in.
    const string LoopYaml = """
        name: test-loop
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
              green: done
              red:
                hold: El gate está en rojo
        """;

    // #669. LoopYaml as /api/loop/definition answers it: the rows read what each state runs from here.
    static readonly object Definition = new
    {
        name = "test-loop",
        agents = Array.Empty<object>(),
        states = new[]
        {
            State("ready-for-proposal", "/aio:refine"),
            State("spec-ready", "/aio:propose"),
            State("in-progress", "/aio:implement"),
            State("verifying", null, "gate"),
        },
        undeclared = new[] { "done" },
        problem = (string?)null,
    };

    static object State(string name, string? command, params string[] scripts) => new
    {
        name,
        scripts,
        agent = (string?)null,
        command,
        score = (string?)null,
        provider = (string?)null,
        model = (string?)null,
        maxLoops = (int?)null,
        transitions = Array.Empty<object>(),
        back = (string?)null,
        tab = (string?)null,
    };

    static readonly object[] Folders =
    [
        new { id = FolderId, name = "sandbox", path = FolderPath, vendor = "github", repository = Repository, loopYaml = LoopYaml },
    ];

    static readonly object Pbis = new
    {
        pbis = new object[]
        {
            Pbi("7", "A live run in progress", "in-progress", held: false),
            Pbi("3", "Break the sum so the gate goes red", "verifying", held: true),
            Pbi("9", "Wait for a slot under the WIP", "spec-ready", held: false),
            Pbi("8", "Not in the loop yet", null, held: false),
            Pbi("2", "Closed and gone from the loop", "verifying", held: false, closed: true),
        },
        unread = Array.Empty<object>(),
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
}
