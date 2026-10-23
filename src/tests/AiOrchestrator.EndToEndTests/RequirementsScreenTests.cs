using System.Text.Json;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #680. The requirements screen against the SPA with the API stubbed at the network: the first start
// opens it while a required CLI is missing, says how to install it with a button that copies the
// command, and does not mark it seen; with everything required present it is marked seen and leads
// to adding the first folder. A console whose preferences say it was seen opens where it always did.
[Collection("review-frontend")]
public sealed class RequirementsScreenTests(ReviewFrontendFixture frontend)
{
    [Fact]
    public async Task The_first_start_with_node_missing_opens_the_requirements_and_copies_the_command()
    {
        await using var context = await frontend.Open("light");
        await context.GrantPermissionsAsync(["clipboard-read", "clipboard-write"]);
        var page = await context.NewPageAsync();
        var calls = await Arrange(page, seen: false, nodeMissing: true);

        await page.GotoAsync("/");

        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/requirements$"));
        await Assertions.Expect(page.GetByText("1 required missing. Run the commands below, then check again.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("brew install node")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("gh auth login")).ToHaveCountAsync(0);

        await page.GetByRole(AriaRole.Button, new() { Name = "Copy brew install node" }).ClickAsync();
        await Assertions.Expect(page.GetByText("Copied")).ToBeVisibleAsync();
        (await page.EvaluateAsync<string>("() => navigator.clipboard.readText()")).ShouldBe("brew install node");
        calls.ShouldNotContain("PUT /api/preferences");
    }

    [Fact]
    public async Task The_first_start_with_everything_required_is_marked_seen_and_offers_the_first_folder()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        var calls = await Arrange(page, seen: false, nodeMissing: false);

        await page.GotoAsync("/");

        await Assertions.Expect(page.GetByText("Everything required is on this machine.")).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Dialog)).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByLabel("Path")).ToBeVisibleAsync();
        for (var waited = 0; waited < 50 && !Called(calls, "PUT /api/preferences"); waited++)
        {
            await page.WaitForTimeoutAsync(100);
        }

        Called(calls, "PUT /api/preferences").ShouldBeTrue();
    }

    static bool Called(List<string> calls, string call)
    {
        lock (calls)
        {
            return calls.Contains(call);
        }
    }

    [Fact]
    public async Task A_console_that_has_seen_them_opens_on_its_pbis_and_reaches_them_from_settings()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await Arrange(page, seen: true, nodeMissing: true);

        await page.GotoAsync("/");
        await Assertions.Expect(page).ToHaveURLAsync(new System.Text.RegularExpressions.Regex("/pbis$"));

        await page.GotoAsync("/settings");
        await page.GetByRole(AriaRole.Link, new() { Name = "Requirements" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Heading, new() { Name = "Requirements", Level = 1 })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("brew install node")).ToBeVisibleAsync();
    }

    /// <summary>A machine with gh signed in, Claude Code, Podman, and node present or missing.</summary>
    static object Machine(bool nodeMissing) => new
    {
        vendors = Array.Empty<object>(),
        agents = Array.Empty<object>(),
        ready = !nodeMissing,
        requirements = Rows(nodeMissing),
    };

    public static object[] Rows(bool nodeMissing) =>
        [
            Requirement("git", "present", required: true, version: "2.51.0"),
            Requirement("gh", "present", required: true, version: "2.99.0", account: "octocat"),
            nodeMissing
                ? Requirement("node", "missing", required: true, command: "brew install node", link: "https://nodejs.org/en/download")
                : Requirement("node", "present", required: true, version: "24.9.0"),
            Requirement("claude", "present", required: true, agent: true, version: "2.1.288"),
            Requirement("opencode", "missing", required: true, agent: true, command: "npm install -g opencode-ai", link: "https://opencode.ai/docs"),
            Requirement("sbx", "signed-out", required: false, problem: "sbx holds no Claude credential. Sign in once inside a sandbox with /login.", command: "sbx run claude"),
            Requirement("podman", "present", required: false, version: "6.0.2"),
        ];

    static object Requirement(
        string name,
        string state,
        bool required,
        bool agent = false,
        string? version = null,
        string? account = null,
        string? problem = null,
        string? command = null,
        string? link = null
    ) => new { name, state, required, agent, version, account, problem, command, link };

    static async Task<List<string>> Arrange(IPage page, bool seen, bool nodeMissing)
    {
        var calls = new List<string>();
        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var path = new Uri(route.Request.Url).AbsolutePath;
                if (route.Request.Method != "GET")
                {
                    lock (calls)
                    {
                        calls.Add($"{route.Request.Method} {path}");
                    }
                }

                object response = path switch
                {
                    "/api/folders" => Array.Empty<object>(),
                    "/api/sessions" => Array.Empty<object>(),
                    "/api/pbis" or "/api/pbis/done" => new { pbis = Array.Empty<object>(), unread = Array.Empty<object>() },
                    "/api/machine" => new { agents = new[] { "claude-code" }, environments = new[] { "worktree" }, defaultEnvironment = "worktree", notes = Array.Empty<object>() },
                    "/api/machine/tools" => Machine(nodeMissing),
                    "/api/directories" => new { path = "/", parent = (string?)null, directories = Array.Empty<object>() },
                    "/api/preferences" => new { editor = "code", requirementsSeen = seen || route.Request.Method == "PUT" },
                    _ => new { },
                };
                await route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(response) });
            }
        );

        return calls;
    }
}
