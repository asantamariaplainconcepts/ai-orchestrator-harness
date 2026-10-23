using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AiOrchestrator.Modules.Workspace.FunctionalTests;

/// <summary>
/// #692. <c>GET /api/directories</c> over a real temporary directory: subdirectories by name, no
/// dot directories and no files, a repository marked whether its <c>.git</c> is a directory or the
/// file a linked worktree has, the parent, a 404 for a path that is not there, and a 403 that says
/// so for one this process may not read.
/// </summary>
[Collection(WorkspaceCollection.Name)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class DirectoriesTests(WorkspaceFixture fixture) : IDisposable
{
    readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("directories-");

    public void Dispose()
    {
        foreach (var directory in _directory.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
        {
            File.SetUnixFileMode(directory.FullName, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        _directory.Delete(recursive: true);
    }

    [Fact]
    public async Task A_directory_lists_its_subdirectories_by_name_and_marks_the_repositories()
    {
        Directory.CreateDirectory(Path.Combine(_directory.FullName, "zeta", ".git"));
        Directory.CreateDirectory(Path.Combine(_directory.FullName, "Alpha"));
        Directory.CreateDirectory(Path.Combine(_directory.FullName, "middle"));
        await File.WriteAllTextAsync(Path.Combine(_directory.FullName, "middle", ".git"), "gitdir: /elsewhere/.git/worktrees/middle\n");
        Directory.CreateDirectory(Path.Combine(_directory.FullName, ".hidden"));
        await File.WriteAllTextAsync(Path.Combine(_directory.FullName, "notes.txt"), "not a directory\n");
        using var client = fixture.CreateClient();

        var listing = await client.GetFromJsonAsync<JsonElement>($"/api/directories?path={Uri.EscapeDataString(_directory.FullName + "/")}");

        listing.GetProperty("path").GetString().ShouldBe(_directory.FullName);
        listing.GetProperty("parent").GetString().ShouldBe(_directory.Parent!.FullName);
        var directories = listing.GetProperty("directories").EnumerateArray().ToArray();
        directories.Select(entry => entry.GetProperty("name").GetString()).ShouldBe(["Alpha", "middle", "zeta"]);
        directories.Select(entry => entry.GetProperty("isGitRepository").GetBoolean()).ShouldBe([false, true, true]);
        directories[0].GetProperty("path").GetString().ShouldBe(Path.Combine(_directory.FullName, "Alpha"));
    }

    [Fact]
    public async Task No_path_starts_at_home_and_the_root_has_no_parent()
    {
        using var client = fixture.CreateClient();

        var home = await client.GetFromJsonAsync<JsonElement>("/api/directories");
        var root = await client.GetFromJsonAsync<JsonElement>("/api/directories?path=/");

        home.GetProperty("path").GetString().ShouldBe(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        root.GetProperty("path").GetString().ShouldBe("/");
        root.GetProperty("parent").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task A_path_that_is_not_there_is_a_404_that_names_it()
    {
        var missing = Path.Combine(_directory.FullName, "gone");
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"/api/directories?path={Uri.EscapeDataString(missing)}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("detail").GetString()
            .ShouldBe($"There is no directory at {missing} on this machine.");
    }

    [Fact]
    public async Task A_directory_this_process_may_not_read_is_a_403_and_not_an_empty_list()
    {
        var locked = Directory.CreateDirectory(Path.Combine(_directory.FullName, "locked")).FullName;
        File.SetUnixFileMode(locked, UnixFileMode.None);
        using var client = fixture.CreateClient();

        var response = await client.GetAsync($"/api/directories?path={Uri.EscapeDataString(locked)}");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        problem.GetProperty("code").GetString().ShouldBe("Directory.Denied");
        problem.GetProperty("detail").GetString()!.ShouldStartWith($"This machine does not let the console read {locked}.");
    }
}
