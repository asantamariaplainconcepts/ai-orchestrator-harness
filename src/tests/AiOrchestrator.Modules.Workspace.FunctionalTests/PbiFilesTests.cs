using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AiOrchestrator.Modules.Workspace.FunctionalTests;

/// <summary>
/// The Files tab's four routes (#514) against a real repository on disk: the tree of the PBI's
/// branch read with git, a save that lands in the checkout holding the branch and changes the count
/// of modified files, the refusals when nothing holds it, and Open in editor with the editor that
/// Settings names. Only the desktop is a stub, because opening an editor is not something a test
/// can watch.
/// </summary>
[Collection(WorkspaceCollection.Name)]
public sealed class PbiFilesTests(WorkspaceFixture fixture) : IAsyncLifetime
{
    readonly string _root = Path.Combine(Path.GetTempPath(), "aio-files-" + Guid.NewGuid().ToString("N")[..8]);

    string Checkout => Path.Combine(_root, "checkout");

    string Worktree => Path.Combine(_root, "wt-7");

    public async Task InitializeAsync()
    {
        await fixture.ResetDatabase();

        Directory.CreateDirectory(Path.Combine(Checkout, "src"));
        await Git(Checkout, "init", "--quiet", "--initial-branch=master");
        await Git(Checkout, "config", "user.name", "Files tests");
        await Git(Checkout, "config", "user.email", "files-tests@localhost");
        await Git(Checkout, "config", "commit.gpgSign", "false");
        await File.WriteAllTextAsync(Path.Combine(Checkout, "README.md"), "# widgets\n");
        await File.WriteAllTextAsync(Path.Combine(Checkout, "src", "Loop.cs"), "namespace Loop;\n");
        await Git(Checkout, "add", ".");
        await Git(Checkout, "commit", "--quiet", "-m", "base");

        // change/7 adds a file master does not have, and is checked out in a worktree beside the folder.
        await Git(Checkout, "branch", "change/7");
        await Git(Checkout, "worktree", "add", "--quiet", Worktree, "change/7");
        await File.WriteAllTextAsync(Path.Combine(Worktree, "src", "Engine.cs"), "class Engine {}\n");
        await Git(Worktree, "add", ".");
        await Git(Worktree, "commit", "--quiet", "-m", "engine");

        // change/9 exists and nothing holds it.
        await Git(Checkout, "branch", "change/9", "change/7");
    }

    public Task DisposeAsync()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException) { }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task The_tree_is_the_branch_and_a_save_lands_in_its_worktree_and_counts_as_modified()
    {
        var folder = await fixture.Folder(Checkout);
        using var client = fixture.CreateClient();

        var tree = await Tree(client, folder, "7");
        tree.GetProperty("branch").GetString().ShouldBe("change/7");
        tree.GetProperty("exists").GetBoolean().ShouldBeTrue();
        tree.GetProperty("inWorktree").GetBoolean().ShouldBeTrue();
        tree.GetProperty("checkout").GetString()!.ShouldEndWith("wt-7");
        Strings(tree, "files").ShouldBe(["README.md", "src/Engine.cs", "src/Loop.cs"]);
        Strings(tree, "modified").ShouldBeEmpty();

        var saved = await client.PostAsJsonAsync(
            "/api/files/content",
            new { folderId = folder, pbi = "7", asIs = false, path = "src/Engine.cs", text = "class Engine { }\r\n" }
        );
        saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        Strings(await saved.Content.ReadFromJsonAsync<JsonElement>(), "modified").ShouldBe(["src/Engine.cs"]);

        (await File.ReadAllTextAsync(Path.Combine(Worktree, "src", "Engine.cs"))).ShouldBe("class Engine { }\r\n");
        File.Exists(Path.Combine(Checkout, "src", "Engine.cs")).ShouldBeFalse("the folder stays on master");

        var read = await client.GetFromJsonAsync<JsonElement>(
            $"/api/files/content?folderId={folder}&pbi=7&path=src%2FEngine.cs"
        );
        read.GetProperty("text").GetString().ShouldBe("class Engine { }\r\n");
        read.GetProperty("isBinary").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task A_folder_on_the_branch_is_where_the_save_lands()
    {
        await Git(Checkout, "switch", "--quiet", "-c", "change/8");
        var folder = await fixture.Folder(Checkout);
        using var client = fixture.CreateClient();

        var saved = await client.PostAsJsonAsync(
            "/api/files/content",
            new { folderId = folder, pbi = "8", asIs = false, path = "README.md", text = "# widgets, edited\n" }
        );

        saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync());
        var tree = await saved.Content.ReadFromJsonAsync<JsonElement>();
        tree.GetProperty("inWorktree").GetBoolean().ShouldBeFalse();
        Strings(tree, "modified").ShouldBe(["README.md"]);
        (await File.ReadAllTextAsync(Path.Combine(Checkout, "README.md"))).ShouldBe("# widgets, edited\n");
    }

    [Fact]
    public async Task A_branch_nothing_holds_is_read_from_git_and_saving_says_there_is_nowhere_to_save()
    {
        var folder = await fixture.Folder(Checkout);
        using var client = fixture.CreateClient();

        var tree = await Tree(client, folder, "9");
        tree.GetProperty("exists").GetBoolean().ShouldBeTrue();
        tree.GetProperty("checkout").ValueKind.ShouldBe(JsonValueKind.Null);
        Strings(tree, "files").ShouldContain("src/Engine.cs");

        var read = await client.GetFromJsonAsync<JsonElement>(
            $"/api/files/content?folderId={folder}&pbi=9&path=src%2FEngine.cs"
        );
        read.GetProperty("text").GetString().ShouldBe("class Engine {}\n");

        var saved = await client.PostAsJsonAsync(
            "/api/files/content",
            new { folderId = folder, pbi = "9", asIs = false, path = "src/Engine.cs", text = "x" }
        );
        saved.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await saved.Content.ReadAsStringAsync()).ShouldContain("No checkout on this machine is on change/9");

        var opened = await client.PostAsJsonAsync(
            $"/api/folders/{folder}/open-in-editor",
            new { file = (string?)null, pbi = "9" }
        );
        opened.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        fixture.Desktop.Edited.ShouldBeEmpty();
    }

    [Fact]
    public async Task A_branch_that_is_gone_says_so_and_the_checkout_as_it_is_is_offered()
    {
        var folder = await fixture.Folder(Checkout);
        using var client = fixture.CreateClient();

        var gone = await Tree(client, folder, "404");
        gone.GetProperty("exists").GetBoolean().ShouldBeFalse();
        gone.GetProperty("branch").GetString().ShouldBe("change/404");
        Strings(gone, "files").ShouldBeEmpty();

        var asIs = await client.GetFromJsonAsync<JsonElement>($"/api/files?folderId={folder}&pbi=404&asIs=true");
        asIs.GetProperty("branch").GetString().ShouldBe("master");
        Strings(asIs, "files").ShouldBe(["README.md", "src/Loop.cs"]);
    }

    [Theory]
    [InlineData("../outside.txt")]
    [InlineData("/etc/hosts")]
    [InlineData(".git/config")]
    public async Task A_path_the_branch_does_not_list_is_neither_read_nor_written(string path)
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "outside.txt"), "secret\n");
        var folder = await fixture.Folder(Checkout);
        using var client = fixture.CreateClient();

        var read = await client.GetAsync(
            $"/api/files/content?folderId={folder}&pbi=7&path={Uri.EscapeDataString(path)}"
        );
        read.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var saved = await client.PostAsJsonAsync(
            "/api/files/content",
            new { folderId = folder, pbi = "7", asIs = false, path, text = "overwritten" }
        );
        saved.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await File.ReadAllTextAsync(Path.Combine(_root, "outside.txt"))).ShouldBe("secret\n");
    }

    [Fact]
    public async Task Open_in_editor_opens_the_file_in_the_checkout_with_the_editor_settings_names()
    {
        var folder = await fixture.Folder(Checkout);
        using var client = fixture.CreateClient();

        var opened = await client.PostAsJsonAsync(
            $"/api/folders/{folder}/open-in-editor",
            new { file = "src/Engine.cs", pbi = "7" }
        );
        opened.StatusCode.ShouldBe(HttpStatusCode.OK, await opened.Content.ReadAsStringAsync());

        (await client.PutAsJsonAsync("/api/preferences", new { editor = "zed" })).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/api/folders/{folder}/open-in-editor", new { file = (string?)null, pbi = "7" }))
            .EnsureSuccessStatusCode();

        // A file the branch does not list is not opened, even inside the checkout.
        (await client.PostAsJsonAsync($"/api/folders/{folder}/open-in-editor", new { file = "src/Missing.cs", pbi = "7" }))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);

        fixture.Desktop.Edited.Count.ShouldBe(2);
        fixture.Desktop.Edited[0].Path.ShouldEndWith(Path.Combine("wt-7", "src", "Engine.cs"));
        fixture.Desktop.Edited[0].Editor.ShouldBe("code");
        fixture.Desktop.Edited[1].Path.ShouldEndWith("wt-7");
        fixture.Desktop.Edited[1].Editor.ShouldBe("zed");
    }

    [Fact]
    public async Task A_tree_of_two_thousand_files_is_listed_whole()
    {
        for (var index = 0; index < 2_000; index++)
        {
            var directory = Path.Combine(Worktree, "gen", $"d{index / 50:D2}");
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, $"f{index:D4}.txt"), $"{index}\n");
        }

        await Git(Worktree, "add", ".");
        await Git(Worktree, "commit", "--quiet", "-m", "two thousand files");
        var folder = await fixture.Folder(Checkout);
        using var client = fixture.CreateClient();

        var clock = Stopwatch.StartNew();
        var tree = await Tree(client, folder, "7");
        clock.Stop();

        Strings(tree, "files").Length.ShouldBe(2_003);
        tree.GetProperty("truncated").GetBoolean().ShouldBeFalse();
        clock.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    static Task<JsonElement> Tree(HttpClient client, Guid folder, string pbi) =>
        client.GetFromJsonAsync<JsonElement>($"/api/files?folderId={folder}&pbi={pbi}");

    static string[] Strings(JsonElement body, string name) =>
        [.. body.GetProperty(name).EnumerateArray().Select(entry => entry.GetString()!)];

    static async Task Git(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };

        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var git = Process.Start(start)!;
        var error = await git.StandardError.ReadToEndAsync();
        await git.WaitForExitAsync();
        git.ExitCode.ShouldBe(0, error);
    }
}
