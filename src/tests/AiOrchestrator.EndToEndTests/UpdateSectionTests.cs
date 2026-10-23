using System.Text.Json;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #679. The update section, in settings, against the SPA with `/api/updates` stubbed at the
// network: the states a person can read (up to date, a newer version and the command that installs
// it, the dnx line, nothing published yet, a failed check's sentence), and that opening settings
// reads the last answer and asks nuget.org nothing; Update and restart and Start at login (#703).
// Beside it, the console section's Stop dialog,
// and the PBIs screen of a console with no folder, which this stub already is.
//
// Same collection as the other route-stubbing classes: they share ReviewFrontendFixture's one
// Vite, and two at once kill each other.
[Collection("review-frontend")]
public sealed class UpdateSectionTests(ReviewFrontendFixture frontend)
{
    [Fact]
    public async Task Opening_settings_reads_the_last_answer_and_checks_nothing()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        var calls = await Arrange(page, new Replies { Status = Answer("up-to-date", latest: "0.3.0") });
        await page.GotoAsync("/settings");

        await Assertions.Expect(page.GetByText("Updates", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Installed version 0.3.0")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("Up to date: 0.3.0 is the newest on nuget.org.")).ToBeVisibleAsync();
        Assert.Equal(["GET /api/updates"], calls);
    }

    [Fact]
    public async Task A_newer_version_gives_the_command_and_copies_it()
    {
        await using var context = await frontend.Open("light");
        await context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        var page = await context.NewPageAsync();
        await Arrange(page, new Replies { Check = Answer("available", latest: "0.3.1") });
        await page.GotoAsync("/settings");

        await page.GetByRole(AriaRole.Button, new() { Name = "Check for updates" }).ClickAsync();

        await Assertions.Expect(page.GetByText("Version 0.3.1 is on nuget.org.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("dotnet tool update -g aio", new() { Exact = true })).ToBeVisibleAsync();
        await page.GetByRole(AriaRole.Button, new() { Name = "Copy" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Copied" })).ToBeVisibleAsync();
        Assert.Equal("dotnet tool update -g aio", await page.EvaluateAsync<string>("navigator.clipboard.readText()"));
    }

    [Fact]
    public async Task Started_with_dnx_the_next_run_takes_the_new_version()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await Arrange(page, new Replies { Status = Answer("available", latest: "0.3.1", how: "dnx") });
        await page.GotoAsync("/settings");

        await Assertions.Expect(page.GetByText("Started with dnx: the next dnx aio runs the new version.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("dotnet tool update -g aio", new() { Exact = true })).ToHaveCountAsync(0);
    }

    [Fact]
    public async Task Nothing_published_yet_is_said_as_such()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await Arrange(page, new Replies { Status = Answer("unpublished") });
        await page.GotoAsync("/settings");

        await Assertions.Expect(page.GetByText("No version of aio is published on nuget.org yet.")).ToBeVisibleAsync();
    }

    [Fact]
    public async Task A_failed_check_says_its_sentence()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await Arrange(page, new Replies { Check = Answer("error", detail: "nuget.org did not answer") });
        await page.GotoAsync("/settings");

        await page.GetByRole(AriaRole.Button, new() { Name = "Check for updates" }).ClickAsync();
        await Assertions.Expect(page.GetByText("The check failed: nuget.org did not answer")).ToBeVisibleAsync();
    }

    // #703. Update and restart is offered to an installed tool with a newer version, and to nothing
    // else: up to date there is no button, and dnx has its own sentence.
    [Fact]
    public async Task Update_and_restart_is_offered_only_when_a_newer_version_is_there()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        var calls = await Arrange(
            page,
            new Replies
            {
                Status = Answer("up-to-date", latest: "0.3.0"),
                Check = Answer("available", latest: "0.3.1"),
                Install = JsonSerializer.Serialize(new { succeeded = false, output = "$ dotnet tool update -g aio\nerror NU1101: Unable to find package aio." }),
            }
        );
        await page.GotoAsync("/settings");

        await Assertions.Expect(page.GetByText("Up to date: 0.3.0 is the newest on nuget.org.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Update and restart" })).ToHaveCountAsync(0);

        await page.GetByRole(AriaRole.Button, new() { Name = "Check for updates" }).ClickAsync();
        var update = page.GetByRole(AriaRole.Button, new() { Name = "Update and restart" });
        await Assertions.Expect(update).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("dotnet tool update -g aio", new() { Exact = true })).ToBeVisibleAsync();

        await update.ClickAsync();
        await Assertions.Expect(page.GetByText("error NU1101: Unable to find package aio.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("The update failed, so nothing restarts.", new() { Exact = false })).ToBeVisibleAsync();
        lock (calls)
        {
            calls.ShouldContain("POST /api/updates/install");
        }
    }

    [Fact]
    public async Task Started_with_dnx_there_is_no_update_and_restart()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await Arrange(page, new Replies { Status = Answer("available", latest: "0.3.1", how: "dnx") });
        await page.GotoAsync("/settings");

        await Assertions.Expect(page.GetByText("Version 0.3.1 is on nuget.org.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Update and restart" })).ToHaveCountAsync(0);
    }

    // #703. Start at login is the installed tool's: from a checkout or dnx it is blocked with why.
    [Theory]
    [InlineData("checkout", "This console runs from a checkout. Only the installed aio tool starts at login.")]
    [InlineData("dnx", "Started with dnx. Install the tool with dotnet tool install -g aio to start it at login.")]
    public async Task Start_at_login_is_blocked_outside_the_tool_and_says_why(string how, string why)
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        var calls = await Arrange(page, new Replies { Service = Service(how, registered: false) });
        await page.GotoAsync("/settings");

        var toggle = page.GetByRole(AriaRole.Switch, new() { Name = "Start at login" });
        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-disabled", "true");
        await Assertions.Expect(page.GetByText(why)).ToBeVisibleAsync();

        // Forced: Playwright waits for an aria-disabled control forever, and the point is that pressing does nothing.
        await toggle.ClickAsync(new() { Force = true });
        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-checked", "false");
        lock (calls)
        {
            calls.ShouldNotContain("PUT /api/app/service");
        }
    }

    [Fact]
    public async Task Start_at_login_in_the_tool_registers_the_service()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        var calls = await Arrange(page, new Replies { Service = Service("tool", registered: false) });
        await page.GotoAsync("/settings");

        var toggle = page.GetByRole(AriaRole.Switch, new() { Name = "Start at login" });
        await Assertions.Expect(page.GetByText("The console starts when you run aio.")).ToBeVisibleAsync();
        await toggle.ClickAsync();

        await Assertions.Expect(toggle).ToHaveAttributeAsync("aria-checked", "true");
        await Assertions.Expect(page.GetByText("The console starts when you log in, with no terminal.")).ToBeVisibleAsync();
        lock (calls)
        {
            calls.ShouldContain("PUT /api/app/service");
        }
    }

    static string Service(string how, bool registered) => JsonSerializer.Serialize(new { how, supported = true, registered });

    // Two sessions in the tray: Stop console says both end before anything is sent, and only the
    // confirmation sends the stop.
    [Fact]
    public async Task Stopping_with_two_live_sessions_says_both_end_before_the_person_confirms()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        var calls = await Arrange(page, new Replies { Sessions = [Session("7"), Session("9")] });
        await page.GotoAsync("/settings");
        await Assertions.Expect(page.GetByRole(AriaRole.Region, new() { Name = "Live sessions" }).GetByText("Sessions · 2")).ToBeVisibleAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Stop console" }).ClickAsync();

        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Stop console" });
        await Assertions.Expect(dialog).ToContainTextAsync("Stopping it ends 2 live sessions.");
        lock (calls)
        {
            calls.ShouldNotContain("POST /api/console/stop", "nothing is sent before the person confirms");
        }

        await dialog.GetByRole(AriaRole.Button, new() { Name = "Stop", Exact = true }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Stop console" })).ToHaveCountAsync(0);
        lock (calls)
        {
            calls.ShouldContain("POST /api/console/stop");
        }
    }

    // The console with no folder: the PBIs screen says how to add one.
    [Fact]
    public async Task The_PBIs_screen_of_a_console_with_no_folder_says_how_to_add_one()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await Arrange(page, new Replies());
        await page.GotoAsync("/pbis");

        await Assertions.Expect(page.GetByText("No PBIs yet. Add a folder with the + in the rail.", new() { Exact = true })).ToBeVisibleAsync();
    }

    static object Session(string pbi) => new
    {
        runId = Guid.NewGuid(),
        repository = "owner/sandbox",
        pbi,
        state = "in-progress",
        manual = false,
        provider = "claude-code",
        command = $"/aio:implement {pbi}",
        startedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
        lastOutputAt = DateTimeOffset.UtcNow,
        stale = false,
    };

    sealed class Replies
    {
        public object[] Sessions { get; init; } = [];

        public string Status { get; init; } = Answer("unchecked");
        public string Check { get; init; } = Answer("up-to-date", latest: "0.3.0");
        public string Install { get; init; } = JsonSerializer.Serialize(new { succeeded = true, output = "" });
        public string Service { get; init; } = JsonSerializer.Serialize(new { how = "checkout", supported = true, registered = false });
    }

    /// <summary>An <c>/api/updates</c> answer for a tool running 0.3.0.</summary>
    static string Answer(string state, string? latest = null, string how = "tool", string? detail = null) =>
        JsonSerializer.Serialize(new { state, installed = "0.3.0", latest, how, detail });

    /// <summary>Stubs the API, answering the update routes from <paramref name="replies"/>, and returns every update call made.</summary>
    static async Task<List<string>> Arrange(IPage page, Replies replies)
    {
        var calls = new List<string>();

        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var path = new Uri(route.Request.Url).AbsolutePath;

                if (path.StartsWith("/api/updates", StringComparison.Ordinal))
                {
                    lock (calls)
                    {
                        calls.Add($"{route.Request.Method} {path}");
                    }

                    await route.FulfillAsync(
                        new()
                        {
                            ContentType = "application/json",
                            Body = path switch
                            {
                                "/api/updates/check" => replies.Check,
                                "/api/updates/install" => replies.Install,
                                _ => replies.Status,
                            },
                        }
                    );
                    return;
                }

                if (route.Request.Method != "GET")
                {
                    lock (calls)
                    {
                        calls.Add($"{route.Request.Method} {path}");
                    }
                }

                // #703. Start at login: what it reads, and a change answered as stored.
                if (path == "/api/app/service")
                {
                    var body = route.Request.Method == "PUT"
                        ? JsonSerializer.Serialize(new { how = "tool", supported = true, registered = JsonDocument.Parse(route.Request.PostData!).RootElement.GetProperty("registered").GetBoolean() })
                        : replies.Service;
                    await route.FulfillAsync(new() { ContentType = "application/json", Body = body });
                    return;
                }

                // What the shell and the rest of Settings read, empty: no folders, no PBIs, nothing
                // live unless a test says so, and a machine with nothing on it.
                object response = path switch
                {
                    "/api/folders" => Array.Empty<object>(),
                    "/api/sessions" => replies.Sessions,
                    "/api/console/stop" => new { stopping = true },
                    "/api/pbis" or "/api/pbis/done" => new { pbis = Array.Empty<object>(), unread = Array.Empty<object>() },
                    "/api/machine" => new { agents = Array.Empty<string>(), environments = new[] { "worktree" }, defaultEnvironment = "worktree" },
                    "/api/machine/tools" => new { vendors = Array.Empty<object>(), agents = Array.Empty<object>() },
                    "/api/preferences" => new { editor = "code" },
                    _ => new { },
                };
                await route.FulfillAsync(
                    new() { ContentType = "application/json", Body = JsonSerializer.Serialize(response) }
                );
            }
        );

        return calls;
    }
}
