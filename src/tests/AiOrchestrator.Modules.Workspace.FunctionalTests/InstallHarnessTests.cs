using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Modules.Workspace.Features.Harness;
using AiOrchestrator.SharedFunctionalTests;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AiOrchestrator.Modules.Workspace.FunctionalTests;

/// <summary>
/// #566. <i>Install harness</i> and <i>Reinstall</i> through the route: the package committed on
/// <c>harness/install</c> of the folder's origin by real git, a bare repository standing in for
/// GitHub's copy, and the pull request and the states asked of a vendor that answers from memory.
/// The default branch and the folder's checkout are what must not move.
/// </summary>
[Collection(WorkspaceCollection.Name)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class InstallHarnessTests(WorkspaceFixture fixture) : IAsyncLifetime
{
    readonly string _root = Directory.CreateTempSubdirectory("install-harness-").FullName;
    /// <summary>One for the class, because its host is built once; emptied before every test.</summary>
    static readonly InstallVendor _vendor = new();

    string Origin => Path.Combine(_root, "origin.git");

    string Work => Path.Combine(_root, "work");

    public Task InitializeAsync()
    {
        _vendor.Reset();
        Git(_root, "init", "--quiet", "--bare", "origin.git");
        Git(Origin, "symbolic-ref", "HEAD", "refs/heads/main");
        Git(_root, "clone", "--quiet", "origin.git", "work");
        Git(Work, "config", "user.name", "Test");
        Git(Work, "config", "user.email", "test@example.com");
        Git(Work, "switch", "--quiet", "-c", "main");
        File.WriteAllText(Path.Combine(Work, "README.md"), "A repository with no harness.\n");
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", "Initial revision");
        Git(Work, "push", "--quiet", "origin", "main");
        return fixture.ResetDatabase();
    }

    public Task DisposeAsync()
    {
        Directory.Delete(_root, recursive: true);
        return Task.CompletedTask;
    }

    HttpClient Client() =>
        fixture
            .ClassHost<InstallHarnessTests>(services =>
            {
                services.RemoveAll<IPbiProvider>();
                services.AddSingleton<IPbiProvider>(_vendor);
            })
            .CreateClient();

    [Fact]
    public async Task Install_opens_a_pull_request_with_the_package_and_creates_the_states()
    {
        var id = await fixture.Folder(Work);
        var main = Git(Origin, "rev-parse", "refs/heads/main");
        using var client = Client();

        (await client.GetFromJsonAsync<JsonElement>($"/api/folders/{id}/harness/install")).GetProperty("pullRequest").ValueKind.ShouldBe(JsonValueKind.Null);

        var response = await client.PostAsync($"/api/folders/{id}/harness/install", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var installed = await response.Content.ReadFromJsonAsync<JsonElement>();

        installed.GetProperty("version").GetString().ShouldBe(HarnessPackage.Version);
        installed.GetProperty("branch").GetString().ShouldBe("harness/install");
        installed.GetProperty("base").GetString().ShouldBe("main");
        installed.GetProperty("alreadyOpen").GetBoolean().ShouldBeFalse();
        installed.GetProperty("problems").EnumerateArray().ShouldBeEmpty();
        installed.GetProperty("pullRequest").GetProperty("url").GetString().ShouldBe("https://github.com/example/portal/pull/1");

        // Every file of the package on the branch, and nothing on main.
        Git(Origin, "rev-parse", "refs/heads/main").ShouldBe(main);
        Git(Origin, "rev-parse", "refs/heads/harness/install^").ShouldBe(main);
        var tree = Git(Origin, "ls-tree", "-r", "--name-only", "harness/install").Split('\n');
        tree.ShouldBe([.. HarnessPackage.Files.Select(file => file.Path).Append("README.md").Order(StringComparer.Ordinal)], ignoreOrder: true);
        Git(Origin, "show", "harness/install:.aio/version").ShouldBe(HarnessPackage.Version);

        // The pull request and the states, as the vendor was asked.
        _vendor.Opened.ShouldBe([("example/portal", "harness/install", $"Install harness {HarnessPackage.Version}")]);
        _vendor.Bodies.Single().ShouldContain("nothing was written to `main`");
        _vendor.States.ShouldBe(HarnessPackage.States, ignoreOrder: true);
        HarnessPackage.States.ShouldBe(
            ["ready-for-proposal", "spec-ready", "in-progress", "verifying", "code-review", "retro", "merging", "needs-refinement", "done"]
        );

        // The checkout is untouched: it is still on main, without the package.
        Git(Work, "symbolic-ref", "--short", "HEAD").ShouldBe("main");
        File.Exists(Path.Combine(Work, ".aio", "version")).ShouldBeFalse();

        // While it is open, the page reads it, and a second press answers it with nothing written.
        var state = await client.GetFromJsonAsync<JsonElement>($"/api/folders/{id}/harness/install");
        state.GetProperty("pullRequest").GetProperty("id").GetString().ShouldBe("1");

        var second = await (await client.PostAsync($"/api/folders/{id}/harness/install", null)).Content.ReadFromJsonAsync<JsonElement>();
        second.GetProperty("alreadyOpen").GetBoolean().ShouldBeTrue();
        second.GetProperty("pullRequest").GetProperty("id").GetString().ShouldBe("1");
        _vendor.Opened.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Reinstall_over_an_older_version_opens_a_new_pull_request_and_keeps_the_repositorys_gate()
    {
        // Version 1 merged, with a gate the repository made its own.
        Directory.CreateDirectory(Path.Combine(Work, ".aio"));
        File.WriteAllText(Path.Combine(Work, ".aio", "version"), "1\n");
        File.WriteAllText(Path.Combine(Work, ".aio", "gate"), "#!/usr/bin/env bash\ndotnet test\n");
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", "Install harness 1");
        Git(Work, "push", "--quiet", "origin", "main");
        Git(Work, "push", "--quiet", "origin", "main:refs/heads/harness/install");
        _vendor.Merged("harness/install");
        var main = Git(Origin, "rev-parse", "refs/heads/main");

        var id = await fixture.Folder(Work);
        using var client = Client();

        var page = await client.GetFromJsonAsync<JsonElement>($"/api/folders/{id}");
        page.GetProperty("harnessVersion").GetString().ShouldBe("1");
        page.GetProperty("shippedVersion").GetString().ShouldBe(HarnessPackage.Version);

        var installed = await (await client.PostAsync($"/api/folders/{id}/harness/install", null)).Content.ReadFromJsonAsync<JsonElement>();

        installed.GetProperty("alreadyOpen").GetBoolean().ShouldBeFalse();
        installed.GetProperty("pullRequest").GetProperty("id").GetString().ShouldBe("2", "a new pull request, not the merged one");
        installed.GetProperty("kept").EnumerateArray().Select(path => path.GetString()).ShouldBe([".aio/gate"]);
        Git(Origin, "rev-parse", "refs/heads/main").ShouldBe(main, "the default branch is not written");
        Git(Origin, "rev-parse", "refs/heads/harness/install^").ShouldBe(main, "the branch starts again from the default one");
        Git(Origin, "show", "harness/install:.aio/version").ShouldBe(HarnessPackage.Version);
        Git(Origin, "show", "harness/install:.aio/gate").ShouldContain("dotnet test");
        Git(Origin, "diff", "--name-only", "main", "harness/install").ShouldNotContain(".aio/gate");
    }

    /// <summary>
    /// #729, #779. Over a repository installed before harness 24, with every script in <c>.harness/scripts/</c>
    /// and lib/, schemas/ and laya's requirements at the root, the update moves: the package lands in
    /// <c>.aio/</c>, the repository's gate becomes <c>.aio/gate</c> as it wrote it, its evidence and its
    /// protected paths are gathered into <c>.aio/config.json</c>, and nothing is left where the old layout
    /// had it, not even a script the package no longer carries. Except laya's requirements (#759): their
    /// former path is harness 34's, beside the steps, so harness 23's stays, a file nothing reads.
    /// </summary>
    [Fact]
    public async Task Reinstall_over_the_layout_before_harness_24_moves_the_package_to_aio_and_the_gate_to_the_root()
    {
        Write(".harness/version", "23\n");
        Write(".harness/scripts/gate", "#!/usr/bin/env bash\ndotnet test\n");
        Write(".harness/scripts/claim", "#!/usr/bin/env bash\necho old claim\n");
        Write(".harness/scripts/aio", "#!/usr/bin/env node\n");
        // An older package carried release; this one does not, and the folder goes whole.
        Write(".harness/scripts/release", "#!/usr/bin/env bash\n");
        Write(".harness/lib/issue-state.mjs", "// old\n");
        Write(".harness/schemas/done.v1.json", "{}\n");
        Write(".harness/laya-requirements.txt", "laya\n");
        Write(".harness/evidence.json", "{\"paths\": [\"web/**\"]}\n");
        Write(".harness/protected-paths", "web/payments/*\n");
        // #742. A README of the repository's own at the root of .harness/ is the one file there the package overwrites.
        Write(".harness/README.md", "Our notes on the harness.\n");
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", "Install harness 23");
        Git(Work, "push", "--quiet", "origin", "main");

        var id = await fixture.Folder(Work);
        using var client = Client();

        var response = await client.PostAsync($"/api/folders/{id}/harness/install", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var installed = await response.Content.ReadFromJsonAsync<JsonElement>();

        installed.GetProperty("kept").EnumerateArray().Select(path => path.GetString()).ShouldBe([".aio/gate"]);
        var tree = Git(Origin, "ls-tree", "-r", "--name-only", "harness/install").Split('\n');
        tree.ShouldBe([.. HarnessPackage.Files.Select(file => file.Path).Append("README.md").Append(".harness/laya-requirements.txt").Order(StringComparer.Ordinal)], ignoreOrder: true);
        tree.ShouldContain(".aio/steps/claim");
        tree.ShouldContain(".aio/lib/issue-state.mjs");
        Git(Origin, "show", "harness/install:.aio/gate").ShouldContain("dotnet test");
        Git(Origin, "show", "harness/install:.aio/steps/claim").ShouldNotContain("old claim");
        Git(Origin, "show", "harness/install:.aio/README.md").ShouldBe(
            System.Text.Encoding.UTF8.GetString(HarnessPackage.Files.Single(file => file.Path == ".aio/README.md").Content).Trim()
        );
        Git(Origin, "ls-tree", "harness/install", ".aio/gate").ShouldStartWith("100755", Case.Sensitive, "the gate keeps its execute bit");
        // git sees the gate move, not a deletion and a new file.
        Git(Origin, "diff", "-M", "--name-status", "main", "harness/install", "--", ".aio/gate", ".harness/scripts/gate").ShouldStartWith("R100");

        var config = Config(Git(Origin, "show", "harness/install:.aio/config.json"));
        config.GetProperty("evidence").GetProperty("paths")[0].GetString().ShouldBe("web/**");
        config.GetProperty("protected").EnumerateArray().Select(path => path.GetString()).ShouldBe(["web/payments/*"]);
    }

    /// <summary>
    /// #779. Over harness 36, with the package in <c>.harness/aio/</c> and the repository's own files at the
    /// root of <c>.harness/</c>, the update moves everything to <c>.aio/</c>: git sees the repository's
    /// own files, the loop, the gate, the templates and the context, as renames of the same bytes; the
    /// package's files go where they now live; <c>evidence.json</c>, <c>quality.json</c>,
    /// <c>protected-paths</c> and the <c>config.json</c> of <c>feedback</c> are gathered into
    /// <c>.aio/config.json</c> with what each had; and nothing of <c>.harness/</c> is left.
    /// </summary>
    [Fact]
    public async Task Reinstall_over_harness_36_moves_everything_to_aio_and_gathers_the_configuration()
    {
        var own = new Dictionary<string, string>
        {
            [".harness/loop.yaml"] = "name: ours\nstates: {}\n",
            [".harness/gate"] = "#!/usr/bin/env bash\ndotnet test\n",
            [".harness/context.md"] = "Our own notes for the commands.\n",
            [".harness/templates/issue.md"] = "## Qué pasa hoy\n",
            [".harness/templates/epic.md"] = "## Objetivo\n",
        };
        foreach (var (path, text) in own)
        {
            Write(path, text);
        }

        // The package of harness 36, byte for byte what this one carries at the former place, so git's rename is whole.
        foreach (var file in HarnessPackage.Files.Where(file => file.Target is null && !HarnessPackage.Owned.Contains(file.Path) && HarnessPackage.Former(file.Path) is [_, ..]))
        {
            Write(HarnessPackage.Former(file.Path)[0], System.Text.Encoding.UTF8.GetString(file.Content));
        }

        Write(".harness/version", "36\n");
        Write(".harness/evidence.json", "// What evidence captures.\n{\n  // \"paths\": [],\n  \"paths\": [\"web/**\"],\n  \"routes\": [\"/\"]\n}\n");
        Write(".harness/quality.json", "{\n  \"coverage\": {\"report\": \"cov.xml\", \"min\": 80}\n}\n");
        Write(".harness/protected-paths", "# The gate.\n.harness/gate\nweb/payments/*\n\n# Secrets.\nsrc/secrets/*   # keys\n");
        Write(".harness/config.json", "{\"feedback\": {\"repository\": \"example/harness\"}}\n");
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", "Install harness 36");
        Git(Work, "push", "--quiet", "origin", "main");

        var id = await fixture.Folder(Work);
        using var client = Client();

        var response = await client.PostAsync($"/api/folders/{id}/harness/install", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var installed = await response.Content.ReadFromJsonAsync<JsonElement>();

        // Nothing of the old layout is left, and the tree is the package, this README and the repository's context.
        var tree = Git(Origin, "ls-tree", "-r", "--name-only", "harness/install").Split('\n');
        tree.ShouldNotContain(path => path.StartsWith(".harness/", StringComparison.Ordinal));
        tree.ShouldBe([.. HarnessPackage.Files.Select(file => file.Path).Append("README.md").Append(".aio/context.md").Order(StringComparer.Ordinal)], ignoreOrder: true);

        // The repository's own files are the same bytes at their new place, and git calls them renames.
        installed.GetProperty("kept").EnumerateArray().Select(path => path.GetString())
            .ShouldBe([.. own.Keys.Where(path => path != ".harness/context.md").Select(path => path.Replace(".harness/", ".aio/", StringComparison.Ordinal))], ignoreOrder: true);
        var renames = Git(Origin, "diff", "-M", "--name-status", "main", "harness/install").Split('\n');
        foreach (var (path, text) in own)
        {
            var now = path.Replace(".harness/", ".aio/", StringComparison.Ordinal);
            Git(Origin, "show", $"harness/install:{now}").ShouldBe(text.Trim());
            renames.ShouldContain($"R100\t{path}\t{now}");
        }

        renames.ShouldContain("R100\t.harness/aio/claim\t.aio/steps/claim");
        renames.ShouldContain("R100\t.harness/aio/scorers/laya/laya\t.aio/scorers/laya/laya");
        renames.ShouldContain("R100\t.harness/aio/lib/issue-state.mjs\t.aio/lib/issue-state.mjs");
        renames.ShouldContain("R100\t.harness/aio/aio\t.aio/aio");
        Git(Origin, "ls-tree", "harness/install", ".aio/gate", ".aio/steps/claim", ".aio/aio").Split('\n').ShouldAllBe(line => line.StartsWith("100755", StringComparison.Ordinal));
        Git(Origin, "show", "harness/install:.aio/version").ShouldBe(HarnessPackage.Version);

        // The three files and feedback's config are one config.json, each keeping what it had.
        var written = Git(Origin, "show", "harness/install:.aio/config.json");
        written.ShouldContain("// What evidence captures.");
        written.ShouldContain("// \"paths\": [],");
        written.ShouldContain("// The gate.");
        var config = Config(written);
        config.GetProperty("evidence").GetProperty("paths")[0].GetString().ShouldBe("web/**");
        config.GetProperty("evidence").GetProperty("routes")[0].GetString().ShouldBe("/");
        config.GetProperty("quality").GetProperty("coverage").GetProperty("min").GetInt32().ShouldBe(80);
        config.GetProperty("protected").EnumerateArray().Select(path => path.GetString()).ShouldBe([".harness/gate", "web/payments/*", "src/secrets/*"]);
        config.GetProperty("feedback").GetProperty("repository").GetString().ShouldBe("example/harness");
    }

    /// <summary>
    /// #779. A repository that already has its <c>.aio/config.json</c> keeps it: the update writes the
    /// example only where there is none, and still takes the old files out of <c>.harness/</c>.
    /// </summary>
    [Fact]
    public async Task Reinstall_keeps_an_aio_config_json_the_repository_already_has()
    {
        Write(".aio/version", "37\n");
        Write(".aio/config.json", "{\"protected\": [\"mine/*\"]}\n");
        Write(".harness/evidence.json", "{\"paths\": [\"old/**\"]}\n");
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", "Half moved");
        Git(Work, "push", "--quiet", "origin", "main");

        var id = await fixture.Folder(Work);
        using var client = Client();

        var response = await client.PostAsync($"/api/folders/{id}/harness/install", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var installed = await response.Content.ReadFromJsonAsync<JsonElement>();

        installed.GetProperty("kept").EnumerateArray().Select(path => path.GetString()).ShouldContain(".aio/config.json");
        Config(Git(Origin, "show", "harness/install:.aio/config.json")).GetProperty("protected")[0].GetString().ShouldBe("mine/*");
        Git(Origin, "ls-tree", "-r", "--name-only", "harness/install").Split('\n').ShouldNotContain(".harness/evidence.json");
    }

    /// <summary>A <c>config.json</c> as the steps read it: without the whole lines that start with <c>//</c>.</summary>
    static JsonElement Config(string text) =>
        JsonDocument.Parse(string.Join('\n', text.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)))).RootElement.Clone();

    /// <summary>
    /// #759. Over harness 34, with <c>laya</c> and its requirements straight in <c>.harness/aio/</c>,
    /// the update moves both to <c>.aio/scorers/laya/</c>: git sees renames, laya keeps its
    /// execute bit, and nothing of laya is left beside the steps.
    /// </summary>
    [Fact]
    public async Task Reinstall_over_laya_beside_the_steps_moves_it_to_scorers_laya()
    {
        Write(".harness/version", "34\n");
        Write(".harness/gate", "#!/usr/bin/env bash\ndotnet test\n");
        var laya = HarnessPackage.Files.Single(file => file.Path == ".aio/scorers/laya/laya");
        var requirements = HarnessPackage.Files.Single(file => file.Path == ".aio/scorers/laya/laya-requirements.txt");
        // The same content as the package's, so git's rename is whole: harness 34 had laya beside the steps.
        Write(".harness/aio/laya", System.Text.Encoding.UTF8.GetString(laya.Content));
        Write(".harness/aio/laya-requirements.txt", System.Text.Encoding.UTF8.GetString(requirements.Content));
        File.SetUnixFileMode(Path.Combine(Work, ".harness/aio/laya-requirements.txt"), UnixFileMode.UserRead | UnixFileMode.UserWrite);
        Git(Work, "add", "-A");
        Git(Work, "commit", "--quiet", "-m", "Install harness 34");
        Git(Work, "push", "--quiet", "origin", "main");

        var id = await fixture.Folder(Work);
        using var client = Client();

        var response = await client.PostAsync($"/api/folders/{id}/harness/install", null);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());

        var tree = Git(Origin, "ls-tree", "-r", "--name-only", "harness/install").Split('\n');
        tree.ShouldNotContain(".harness/aio/laya");
        tree.ShouldNotContain(".harness/aio/laya-requirements.txt");
        tree.ShouldContain(".aio/scorers/laya/laya");
        tree.ShouldContain(".aio/scorers/laya/laya-requirements.txt");
        Git(Origin, "ls-tree", "harness/install", ".aio/scorers/laya/laya").ShouldStartWith("100755", Case.Sensitive, "laya keeps its execute bit");
        var moved = Git(Origin, "diff", "-M", "--name-status", "main", "harness/install", "--", ".harness/aio/laya", ".harness/aio/laya-requirements.txt", ".aio/scorers/laya/").Split('\n');
        moved.ShouldBe(
            [
                "R100\t.harness/aio/laya\t.aio/scorers/laya/laya",
                "R100\t.harness/aio/laya-requirements.txt\t.aio/scorers/laya/laya-requirements.txt",
            ],
            ignoreOrder: true
        );
    }

    void Write(string path, string text)
    {
        var file = Path.Combine(Work, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, text);
        File.SetUnixFileMode(file, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    [Fact]
    public async Task A_folder_with_no_github_origin_is_refused_with_why()
    {
        fixture.RemoteUrl = null;
        var id = await fixture.Folder(Work);
        using var client = Client();

        var response = await client.PostAsync($"/api/folders/{id}/harness/install", null);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Its origin is not on github.com");
        _vendor.Opened.ShouldBeEmpty();
        Git(Origin, "branch", "--list", "harness/install").ShouldBeEmpty();
    }

    static string Git(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = directory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        process.ExitCode.ShouldBe(0, $"git {string.Join(' ', arguments)}: {error}");
        return output.Trim();
    }

    /// <summary>A vendor that holds pull requests and states in memory, and answers only what installing asks.</summary>
    sealed class InstallVendor : VendorStub, IPbiProvider
    {
        readonly List<(string Branch, PullRequestInfo Pull, bool Open)> _pulls = [];

        public List<(string Repository, string Branch, string Title)> Opened { get; } = [];

        public List<string> Bodies { get; } = [];

        public List<string> States { get; } = [];

        public void Reset()
        {
            _pulls.Clear();
            Opened.Clear();
            Bodies.Clear();
            States.Clear();
        }

        /// <summary>A pull request from the branch, merged before the test: there and no longer open.</summary>
        public void Merged(string branch) =>
            _pulls.Add((branch, new PullRequestInfo("1", new Uri("https://github.com/example/portal/pull/1"), false, true, false), false));

        public Task<PullRequestInfo?> GetBranchPullRequestAsync(string repository, string branch, CancellationToken ct) =>
            Task.FromResult(_pulls.Where(pull => pull.Branch == branch && pull.Open).Select(pull => pull.Pull).FirstOrDefault());

        public override Task<Written<PullRequestInfo>> OpenPullRequestAsync(string repository, PullRequestDraft draft, CancellationToken ct)
        {
            var branch = draft.Branch;
            Opened.Add((repository, branch, draft.Title));
            Bodies.Add(draft.Body);
            var number = (_pulls.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            var pull = new PullRequestInfo(number, new Uri($"https://github.com/{repository}/pull/{number}"), false, false, false);
            _pulls.Add((branch, pull, true));
            return Task.FromResult(new Written<PullRequestInfo>(pull, null));
        }

        public Task<Written<IReadOnlyList<string>>> CreateStatesAsync(string repository, IReadOnlyList<string> states, CancellationToken ct)
        {
            States.AddRange(states.Except(States));
            return Task.FromResult(new Written<IReadOnlyList<string>>([.. States.Order(StringComparer.Ordinal)], null));
        }

        public Task<IReadOnlyList<string>> ListStatesAsync(string repository, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<string>>([.. States.Order(StringComparer.Ordinal)]);
    }
}
