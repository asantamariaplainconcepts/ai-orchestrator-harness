using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #692. The add folder dialog's browser against the SPA with the API stubbed at the network: two
// clicks down from home, a click on a repository fills the field, and Add folder adds that path. A
// path typed that exists moves the browser to it, and a directory the server may not read shows the
// server's sentence instead of an empty list. What the server answers is DirectoriesTests'.
[Collection("review-frontend")]
public sealed class AddFolderBrowserTests(ReviewFrontendFixture frontend)
{
    const string Home = "/home/ada";
    const string Portal = "/home/ada/projects/clients/portal";
    const string AddedId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4d1";
    const string Denied = "This machine does not let the console read /home/ada/Documents. Allow it in System Settings → Privacy & Security → Files and Folders, for the app aio was started from, then try again.";

    [Fact]
    public async Task Two_clicks_down_and_a_repository_fill_the_field_and_Add_folder_adds_it()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        var writes = await Arrange(page);

        await page.GotoAsync("/pbis");
        await page.GetByRole(AriaRole.Navigation, new() { Name = "Main" }).GetByRole(AriaRole.Button, new() { Name = "Add folder" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Add folder" });
        var list = dialog.GetByRole(AriaRole.List, new() { Name = "Folders on this machine" });

        await list.GetByRole(AriaRole.Button, new() { Name = "projects" }).ClickAsync();
        await list.GetByRole(AriaRole.Button, new() { Name = "clients" }).ClickAsync();
        await Assertions.Expect(dialog.GetByRole(AriaRole.Navigation, new() { Name = "Current folder" }).GetByRole(AriaRole.Button, new() { Name = "clients" }))
            .ToHaveAttributeAsync("aria-current", "location");
        await list.GetByRole(AriaRole.Button, new() { Name = "portal git" }).ClickAsync();

        await Assertions.Expect(dialog.GetByLabel("Path")).ToHaveValueAsync(Portal);
        await dialog.GetByRole(AriaRole.Button, new() { Name = "Add folder" }).ClickAsync();

        await Assertions.Expect(page).ToHaveURLAsync(new Regex($"/folders/{AddedId}$"));
        writes.ShouldContain($"POST /api/folders {{\"path\":\"{Portal}\"}}");
    }

    [Fact]
    public async Task A_typed_path_moves_the_browser_and_a_denied_directory_says_where_to_allow_it()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 900);
        await Arrange(page);

        await page.GotoAsync("/pbis");
        await page.GetByRole(AriaRole.Navigation, new() { Name = "Main" }).GetByRole(AriaRole.Button, new() { Name = "Add folder" }).ClickAsync();
        var dialog = page.GetByRole(AriaRole.Dialog, new() { Name = "Add folder" });
        var list = dialog.GetByRole(AriaRole.List, new() { Name = "Folders on this machine" });

        await dialog.GetByLabel("Path").FillAsync("~/projects/clients");
        await Assertions.Expect(list.GetByRole(AriaRole.Button, new() { Name = "portal git" })).ToBeVisibleAsync();

        await dialog.GetByRole(AriaRole.Navigation, new() { Name = "Current folder" }).GetByRole(AriaRole.Button, new() { Name = "ada" }).ClickAsync();
        await list.GetByRole(AriaRole.Button, new() { Name = "Documents" }).ClickAsync();
        await Assertions.Expect(list.GetByRole(AriaRole.Alert)).ToHaveTextAsync(Denied);
    }

    static object Listing(string path, string? parent, params (string Name, bool Repository)[] directories) => new
    {
        path,
        parent,
        directories = directories.Select(directory => new { name = directory.Name, path = $"{path}/{directory.Name}", isGitRepository = directory.Repository }),
    };

    static async Task<ConcurrentQueue<string>> Arrange(IPage page)
    {
        var writes = new ConcurrentQueue<string>();
        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var method = route.Request.Method;
                var uri = new Uri(route.Request.Url);
                if (method != "GET") writes.Enqueue($"{method} {uri.AbsolutePath} {route.Request.PostData}");

                var asked = System.Web.HttpUtility.ParseQueryString(uri.Query)["path"];
                (int Status, object Body) answer = (method, uri.AbsolutePath, asked) switch
                {
                    ("GET", "/api/directories", null or Home) => (200, Listing(Home, "/home", ("Documents", false), ("projects", false))),
                    ("GET", "/api/directories", "/home/ada/projects") => (200, Listing("/home/ada/projects", Home, ("clients", false), ("notes", true))),
                    ("GET", "/api/directories", "/home/ada/projects/clients" or "~/projects/clients") => (200, Listing("/home/ada/projects/clients", "/home/ada/projects", ("portal", true))),
                    ("GET", "/api/directories", "/home/ada/Documents") => (403, new { status = 403, detail = Denied, code = "Directory.Denied" }),
                    ("GET", "/api/directories", _) => (404, (object)new { status = 404, detail = $"There is no directory at {asked} on this machine." }),
                    ("GET", "/api/folders", _) => (200, Array.Empty<object>()),
                    ("GET", "/api/sessions", _) => (200, Array.Empty<object>()),
                    ("GET", "/api/pbis", _) => (200, new { pbis = Array.Empty<object>(), unread = Array.Empty<object>() }),
                    ("GET", "/api/machine", _) => (200, new { agents = new[] { "claude-code" }, environments = new[] { "worktree" }, defaultEnvironment = "worktree" }),
                    ("GET", "/api/preferences", _) => (200, new { editor = "code", requirementsSeen = true }),
                    ("POST", "/api/folders", _) => (201, new { id = AddedId, name = "portal", path = Portal, branch = "main" }),
                    _ => (200, (object)new { }),
                };
                await route.FulfillAsync(new() { Status = answer.Status, ContentType = "application/json", Body = JsonSerializer.Serialize(answer.Body) });
            }
        );
        return writes;
    }
}
