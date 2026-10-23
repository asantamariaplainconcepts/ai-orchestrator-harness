using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Playwright;

namespace AiOrchestrator.EndToEndTests;

// #514. The Files tab: the tree of the PBI's branch with react-arborist, filtered by name and
// marking what is modified; the chosen file in CodeMirror 6, edited and saved; the header that
// counts the modified files; Open in editor; a branch that is gone; and a tree of 2,000 files that
// stays responsive.
//
// The API is stubbed at the browser, like PbiScreenTests: what is under test is the screen's
// reading of the contract and the writes it sends. The writes themselves (a save landing on disk in
// the worktree and counting as modified, Open in editor with the editor Settings names) are covered
// against a real repository by PbiFilesTests.
[Collection("review-frontend")]
public sealed class FilesTabTests(ReviewFrontendFixture frontend)
{
    const string FolderId = "01a11dcb-cb89-70d1-8bba-b1eb145eb4c6";
    const string Repository = "owner/sandbox";
    const string Chosen = "src/modules/Loop/LoopDefinition.cs";

    static readonly string[] Paths =
    [
        ".aio/loop.yaml",
        "AGENTS.md",
        "README.md",
        "docs/process/retro-log.md",
        "src/modules/Loop/Decisions.cs",
        "src/modules/Loop/LoopDefinition.cs",
        "src/modules/Loop/LoopEngine.cs",
        "src/modules/Pbis/PbisModule.cs",
    ];

    [Theory]
    [InlineData("light")]
    [InlineData("dark")]
    public async Task A_file_is_edited_and_saved_and_the_header_counts_it_modified(string theme)
    {
        await using var context = await frontend.Open(theme);
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var server = new Server(Paths);
        await Arrange(page, server);

        await page.GotoAsync($"/pbis/{FolderId}/495/files");
        await Assertions.Expect(page.GetByText("change/495 · 0 modified", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("8 files", new() { Exact = true })).ToBeVisibleAsync();

        var tree = page.GetByRole(AriaRole.Tree, new() { Name = "Files" });
        await Assertions.Expect(tree.GetByRole(AriaRole.Treeitem)).ToHaveCountAsync(5);
        await Assertions.Expect(tree.GetByRole(AriaRole.Treeitem).First).ToContainTextAsync(".aio");

        await tree.GetByRole(AriaRole.Treeitem).Filter(new() { HasText = "src" }).ClickAsync();
        await tree.GetByRole(AriaRole.Treeitem).Filter(new() { HasText = "modules" }).ClickAsync();
        await tree.GetByRole(AriaRole.Treeitem).Filter(new() { HasText = "Loop" }).First.ClickAsync();
        await tree.GetByRole(AriaRole.Treeitem).Filter(new() { HasText = "LoopDefinition.cs" }).ClickAsync();

        var editor = page.GetByRole(AriaRole.Textbox, new() { Name = $"Contents of {Chosen}" });
        await Assertions.Expect(editor).ToContainTextAsync("public sealed record LoopState(");
        await Assertions.Expect(page.GetByText("C#", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save" })).ToBeDisabledAsync();

        // Edit at the end of the file, and the editor says it is not saved.
        await editor.ClickAsync();
        await page.Keyboard.PressAsync("ControlOrMeta+End");
        await page.Keyboard.TypeAsync("// edited");
        await Assertions.Expect(page.GetByText("modified, not saved", new() { Exact = true })).ToBeVisibleAsync();
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"files-editing-{theme}") });

        await page.GetByRole(AriaRole.Button, new() { Name = "Save" }).ClickAsync();
        await Assertions.Expect(page.GetByText("change/495 · 1 modified", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync($"Saved {Chosen}");
        await Assertions.Expect(page.GetByText("modified, not saved", new() { Exact = true })).ToHaveCountAsync(0);
        await Assertions.Expect(tree.GetByRole(AriaRole.Treeitem).Filter(new() { HasText = "LoopDefinition.cs" }))
            .ToContainTextAsync("M");

        var saved = server.Writes.Single();
        saved.ShouldStartWith("POST /api/files/content ");
        var body = JsonDocument.Parse(saved["POST /api/files/content ".Length..]).RootElement;
        body.GetProperty("folderId").GetString().ShouldBe(FolderId);
        body.GetProperty("pbi").GetString().ShouldBe("495");
        body.GetProperty("asIs").GetBoolean().ShouldBeFalse();
        body.GetProperty("path").GetString().ShouldBe(Chosen);
        body.GetProperty("text").GetString().ShouldBe(Text + "// edited");

        // Open in editor hands the chosen file over, and says which editor took it.
        await page.GetByRole(AriaRole.Button, new() { Name = "Open in editor" }).ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Status)).ToHaveTextAsync("Opened in zed");
        server.Writes.Last().ShouldBe(
            $$"""POST /api/folders/{{FolderId}}/open-in-editor {"file":"{{Chosen}}","pbi":"495","asIs":false}"""
        );

        await page.ScreenshotAsync(new() { Path = frontend.CapturePath($"files-saved-{theme}") });
    }

    [Fact]
    public async Task A_PNG_chosen_in_the_tree_says_it_is_binary_and_Save_stays_disabled()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var server = new Server(["README.md", "logo.png"]);
        await Arrange(page, server);

        await page.GotoAsync($"/pbis/{FolderId}/495/files");
        var tree = page.GetByRole(AriaRole.Tree, new() { Name = "Files" });
        await tree.GetByRole(AriaRole.Treeitem).Filter(new() { HasText = "logo.png" }).ClickAsync();

        await Assertions.Expect(page.GetByText("logo.png is binary, 2,048 bytes. Open in editor to see it.", new() { Exact = true })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Textbox, new() { Name = "Contents of logo.png" })).ToHaveCountAsync(0);
        await Assertions.Expect(page.GetByRole(AriaRole.Button, new() { Name = "Save" })).ToBeDisabledAsync();
        server.Writes.ShouldBeEmpty();
    }

    [Fact]
    public async Task Two_thousand_files_are_filtered_and_opened_without_blocking()
    {
        await using var context = await frontend.Open("light");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(1440, 1000);
        var paths = Enumerable.Range(0, 2_000).Select(index => $"gen/d{index / 50:D2}/f{index:D4}.txt").ToArray();
        await Arrange(page, new Server(paths));

        await page.GotoAsync($"/pbis/{FolderId}/495/files");
        await Assertions.Expect(page.GetByText("2,000 files", new() { Exact = true })).ToBeVisibleAsync();
        var tree = page.GetByRole(AriaRole.Tree, new() { Name = "Files" });

        // The page answers while it filters, and the longest task in the browser is measured to say so.
        await page.EvaluateAsync("""
            () => {
              window.__longest = 0;
              new PerformanceObserver((list) => {
                for (const entry of list.getEntries()) window.__longest = Math.max(window.__longest, entry.duration);
              }).observe({ type: "longtask" });
            }
            """);
        var filter = page.GetByRole(AriaRole.Searchbox, new() { Name = "Filter files" });
        var clock = Stopwatch.StartNew();

        // "f" matches every file, so all 2,041 rows are open: only those on screen are in the
        // document, and End reaches the last file.
        await filter.PressSequentiallyAsync("f");
        await Assertions.Expect(tree.GetByRole(AriaRole.Treeitem).Filter(new() { HasText = "f0000.txt" })).ToBeVisibleAsync();
        (await tree.GetByRole(AriaRole.Treeitem).CountAsync()).ShouldBeLessThan(80, "the tree draws only the rows on screen");
        await tree.GetByRole(AriaRole.Treeitem).Filter(new() { HasText = "f0000.txt" }).FocusAsync();
        await page.Keyboard.PressAsync("End");
        await Assertions.Expect(tree.GetByRole(AriaRole.Treeitem).Filter(new() { HasText = "f1999.txt" })).ToBeVisibleAsync();

        // Focusing the filter again puts the caret first, so the rest is typed after a click at its end.
        await filter.ClickAsync();
        await page.Keyboard.PressAsync("End");
        await page.Keyboard.TypeAsync("1999");
        var match = tree.GetByRole(AriaRole.Treeitem).Filter(new() { HasText = "f1999.txt" });
        await Assertions.Expect(tree.GetByRole(AriaRole.Treeitem)).ToHaveCountAsync(3);
        clock.Stop();
        var longest = await page.EvaluateAsync<double>("() => window.__longest");
        Console.WriteLine($"2,000 files: filtered and scrolled in {clock.ElapsedMilliseconds} ms, longest task {longest:0} ms");
        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
        longest.ShouldBeLessThan(500, "no step of the filter blocks the page for half a second");

        await match.ClickAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Textbox, new() { Name = "Contents of gen/d39/f1999.txt" }))
            .ToContainTextAsync("public sealed record LoopState(");
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath("files-2000") });
    }

    [Fact]
    public async Task A_branch_that_is_gone_offers_the_checkout_and_the_keyboard_opens_a_file_at_a_narrow_width()
    {
        await using var context = await frontend.Open("dark");
        var page = await context.NewPageAsync();
        await page.SetViewportSizeAsync(600, 900);
        var server = new Server(Paths) { Gone = true };
        await Arrange(page, server);

        await page.GotoAsync($"/pbis/{FolderId}/495/files");
        await Assertions.Expect(page.GetByText("This repository no longer has change/495", new() { Exact = false }))
            .ToBeVisibleAsync();
        await Assertions.Expect(page.GetByRole(AriaRole.Tree)).ToHaveCountAsync(0);

        await page.GetByRole(AriaRole.Button, new() { Name = "Show the checkout" }).ClickAsync();
        await Assertions.Expect(page.GetByText("on master. Save writes here.", new() { Exact = false })).ToBeVisibleAsync();
        await Assertions.Expect(page.GetByText("master · 0 modified", new() { Exact = true })).ToBeVisibleAsync();
        server.Reads.ShouldContain(read => read.Contains("asIs=true"));

        // Filter, then Tab into the tree, Down to the one match, Enter to open it.
        var filter = page.GetByRole(AriaRole.Searchbox, new() { Name = "Filter files" });
        await filter.FillAsync("README");
        await filter.FocusAsync();
        await page.Keyboard.PressAsync("Tab");
        await page.Keyboard.PressAsync("ArrowDown");
        await page.Keyboard.PressAsync("Enter");
        await Assertions.Expect(page.GetByRole(AriaRole.Textbox, new() { Name = "Contents of README.md" })).ToBeVisibleAsync();
        server.Reads.ShouldContain(read => read.Contains("path=README.md") && read.Contains("asIs=true"));

        var overflow = await page.EvaluateAsync<int>(
            "(() => { const pane = document.querySelector('main > div'); return pane.scrollWidth - pane.clientWidth; })()"
        );
        overflow.ShouldBeLessThanOrEqualTo(0, "nothing on the tab is wider than the pane at 600px");
        await page.ScreenshotAsync(new() { Path = frontend.CapturePath("files-narrow-dark"), FullPage = true });
    }

    const string Text = """
        namespace AiOrchestrator.Modules.Loop;

        public sealed record LoopState(
            string Name,
            IReadOnlyList<string> Scripts)
        {
            public bool DoesOneThing => true;
        }

        """;

    /// <summary>What the stub serves, and every read and write the tab sent.</summary>
    sealed class Server(string[] paths)
    {
        public string[] Paths { get; } = paths;

        public bool Gone { get; init; }

        public List<string> Modified { get; } = [];

        public ConcurrentQueue<string> Reads { get; } = new();

        public ConcurrentQueue<string> Writes { get; } = new();
    }

    static async Task Arrange(IPage page, Server server)
    {
        await page.RouteAsync(
            "**/api/**",
            async route =>
            {
                var request = route.Request;
                var url = new Uri(request.Url);
                var path = url.AbsolutePath;
                var asIs = url.Query.Contains("asIs=true", StringComparison.Ordinal);

                if (request.Method != "GET")
                {
                    server.Writes.Enqueue($"{request.Method} {path} {request.PostData}");
                    if (path == "/api/files/content")
                    {
                        var saved = JsonDocument.Parse(request.PostData!).RootElement.GetProperty("path").GetString()!;
                        server.Modified.Add(saved);
                        await Json(route, Tree(server, asIs: false));
                        return;
                    }
                    if (path == $"/api/folders/{FolderId}/open-in-editor")
                    {
                        await Json(route, new { opened = "/checkouts/sandbox/" + Chosen, editor = "zed" });
                        return;
                    }
                    await route.FulfillAsync(new() { Status = 204 });
                    return;
                }

                if (path.StartsWith("/api/files", StringComparison.Ordinal))
                {
                    server.Reads.Enqueue(Uri.UnescapeDataString(url.PathAndQuery));
                }

                object response = path switch
                {
                    "/api/files" => Tree(server, asIs),
                    // A PNG is answered as the server reads one: binary, its size, and no text.
                    "/api/files/content" when url.Query.Contains(".png", StringComparison.Ordinal) => new
                    {
                        path = Uri.UnescapeDataString(url.Query.Split("path=")[1].Split('&')[0]),
                        text = "",
                        isBinary = true,
                        truncated = false,
                        bytes = 2048,
                    },
                    "/api/files/content" => new
                    {
                        path = Uri.UnescapeDataString(url.Query.Split("path=")[1].Split('&')[0]),
                        text = Text,
                        isBinary = false,
                        truncated = false,
                        bytes = Text.Length,
                    },
                    "/api/folders" => Folders,
                    "/api/pbis" => new { pbis = new[] { Pbi }, unread = Array.Empty<object>() },
                    "/api/sessions" => Array.Empty<object>(),
                    "/api/pbi" => new { pbi = Pbi, pullRequest = (object?)null },
                    "/api/runs" => Array.Empty<object>(),
                    "/api/loop/journal" => Array.Empty<object>(),
                    "/api/loop/definition" => new
                    {
                        name = "loop",
                        agents = Array.Empty<object>(),
                        states = Array.Empty<object>(),
                        undeclared = Array.Empty<string>(),
                        problem = (string?)null,
                    },
                    _ => new { },
                };
                await Json(route, response);
            }
        );
    }

    static object Tree(Server server, bool asIs) =>
        asIs
            ? new
            {
                branch = "master",
                exists = true,
                checkout = (string?)"/checkouts/sandbox",
                inWorktree = false,
                files = server.Paths,
                modified = Array.Empty<string>(),
                truncated = false,
            }
            : new
            {
                branch = "change/495",
                exists = !server.Gone,
                checkout = server.Gone ? null : "/checkouts/sandbox/.worktrees/change-495",
                inWorktree = !server.Gone,
                files = server.Gone ? [] : server.Paths,
                modified = server.Modified.ToArray(),
                truncated = false,
            };

    static Task Json(IRoute route, object body) =>
        route.FulfillAsync(new() { ContentType = "application/json", Body = JsonSerializer.Serialize(body) });

    static readonly object Pbi = new
    {
        vendor = "github",
        repository = Repository,
        id = "495",
        title = "Declare the loop as a state machine",
        state = "in-progress",
        held = false,
        assignee = (string?)null,
        closed = false,
        folderPath = "/checkouts/sandbox",
    };

    static readonly object[] Folders =
    [
        new { id = FolderId, name = "sandbox", path = "/checkouts/sandbox", vendor = "github", repository = Repository, loopYaml = "" },
    ];
}
