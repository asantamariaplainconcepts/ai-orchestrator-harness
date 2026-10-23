using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using AiOrchestrator.Modules.Workspace.Features.Harness;
using YamlDotNet.RepresentationModel;

namespace AiOrchestrator.Modules.Workspace.FunctionalTests;

/// <summary>
/// #563. The harness package this build carries: what it lists through its route, that its loop
/// template is a loop the engine would follow, that its gate stub refuses in the gate's own shape, and
/// that every other file is this repository's own. #566: the package's commands need no OpenSpec and
/// no skill, so the package runs in a repository that has neither. #572: none of them names this
/// repository's ports or files, and the package carries no archive. #618: the commands are this
/// repository's own <c>.ai/commands/aio/</c>, embedded and not copied, and what this repository adds
/// to them is <c>.aio/context.md</c>, which the package does not carry. #633: the package carries
/// the issue and epic templates, and the issue template is the Definition of Ready.
/// </summary>
[Collection(WorkspaceCollection.Name)]
public sealed class HarnessPackageTests(WorkspaceFixture fixture)
{
    static readonly string Repository = Directory.GetParent(SourceRoot())!.FullName;

    const string Context = ".aio/context.md";

    /// <summary>
    /// The package's own files, which this repository has its own version of: the three an installed
    /// repository makes its own with the config, and the version.
    /// </summary>
    static readonly string[] PackagesOwn =
    [
        ".aio/config.json",
        ".aio/loop.yaml",
        ".aio/gate",
        ".aio/version",
    ];

    /// <summary>
    /// What is this repository's and not every repository's: its five ports, its own documents and
    /// folders, and OpenSpec's change files. A package command that names one does not fit a repository
    /// that received the package (#572); since #618 they live in the context file instead.
    /// </summary>
    static readonly string[] ThisRepositorys =
    [
        "IPbiProvider",
        "IAgentProvider",
        "IEnvironment",
        "ISession",
        "IScriptRunner",
        "five ports",
        "AiOrchestrator",
        "Modules.",
        "ARCHITECTURE.md",
        "DESIGN.md",
        "docs/",
        "src/",
        "retro-log",
        "proposal.md",
        "tasks.md",
        "aspire",
    ];

    /// <summary>
    /// What a package command must not need: OpenSpec or a skill. What the engine does is
    /// AgentCommandTests' list, over the same files (#668).
    /// </summary>
    static readonly string[] Forbidden = ["openspec", "OpenSpec", "skill"];

    [Fact]
    public async Task The_route_lists_the_package_files_and_its_version()
    {
        using var client = fixture.CreateClient();

        var package = await client.GetFromJsonAsync<JsonElement>("/api/harness");

        package.GetProperty("version").GetString().ShouldBe(File.ReadAllText(Path.Combine(Repository, "src", "modules", "Workspace", "AiOrchestrator.Modules.Workspace", "Features", "Harness", "Package", "version")).Trim());
        package.GetProperty("version").GetString().ShouldBe("37");
        // This repository has installed 1: its loop and its gate are its own, not the package's.
        File.ReadAllText(Path.Combine(Repository, ".aio", "version")).Trim().ShouldBe("1");

        var files = package.GetProperty("files").EnumerateArray().ToList();
        var paths = files.Select(file => file.GetProperty("path").GetString()!).ToList();
        var scripts = Scripts();

        paths.ShouldBe(
            [
                .. new[]
                {
                    ".ai/commands/aio/explore.md",
                    ".ai/commands/aio/implement.md",
                    ".ai/commands/aio/propose.md",
                    ".ai/commands/aio/refine.md",
                    ".ai/commands/aio/review-diff.md",
                    ".ai/commands/aio/review.md",
                    ".claude/commands",
                    ".claude/hooks/pre-tool-use",
                    ".claude/settings.json",
                    ".aio/README.md",
                    ".aio/checks/_lib.sh",
                    ".aio/checks/crap.sh",
                    ".aio/checks/old-tests-new-code.sh",
                    ".aio/checks/out-of-seam.sh",
                    ".aio/checks/tests-lost-or-skipped.sh",
                    ".aio/checks/tests-touched.sh",
                    ".aio/config.json",
                    ".aio/gate",
                    ".aio/lib/issue-state.mjs",
                    ".aio/lib/where-it-lands.js",
                    ".aio/loop.yaml",
                    ".aio/schemas/done.v1.json",
                    ".aio/scorers/laya/laya",
                    ".aio/scorers/laya/laya-requirements.txt",
                    ".aio/templates/epic.md",
                    ".aio/templates/issue.md",
                    ".aio/version",
                    ".opencode/command",
                }
                    .Concat(scripts)
                    .Order(StringComparer.Ordinal),
            ]
        );

        files.Single(file => file.GetProperty("path").GetString() == ".opencode/command").GetProperty("target").GetString().ShouldBe("../.ai/commands");
        files.Single(file => file.GetProperty("path").GetString() == ".aio/gate").GetProperty("executable").GetBoolean().ShouldBeTrue();
        files.Single(file => file.GetProperty("path").GetString() == ".aio/steps/claim").GetProperty("executable").GetBoolean().ShouldBeTrue();
        files.Single(file => file.GetProperty("path").GetString() == ".aio/lib/issue-state.mjs").GetProperty("executable").GetBoolean().ShouldBeFalse();
        // #759. laya is not a step: it is in its own folder, still executable, and not beside the steps.
        files.Single(file => file.GetProperty("path").GetString() == ".aio/scorers/laya/laya").GetProperty("executable").GetBoolean().ShouldBeTrue();
        files.Single(file => file.GetProperty("path").GetString() == ".aio/scorers/laya/laya-requirements.txt").GetProperty("executable").GetBoolean().ShouldBeFalse();
        paths.ShouldNotContain(".aio/steps/laya");
        paths.ShouldNotContain(".aio/laya");
        paths.ShouldAllBe(path => !path.StartsWith(".harness/", StringComparison.Ordinal));
        files.Single(file => file.GetProperty("path").GetString() == ".aio/aio").GetProperty("executable").GetBoolean().ShouldBeTrue();
        files.Single(file => file.GetProperty("path").GetString() == ".claude/settings.json").GetProperty("executable").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task The_loop_template_validates_with_seven_states_no_score_and_claude_code_in_every_role()
    {
        using var client = fixture.CreateClient();
        var yaml = Text(".aio/loop.yaml");

        var check = await (await client.PostAsJsonAsync("/api/loop/validate", new { yaml })).Content.ReadFromJsonAsync<JsonElement>();

        check.GetProperty("problems").EnumerateArray().Select(problem => problem.GetString()).ShouldBeEmpty();
        check.GetProperty("valid").GetBoolean().ShouldBeTrue();
        check.GetProperty("states").EnumerateArray().Select(state => state.GetProperty("name").GetString()).ShouldBe(
            ["ready-for-proposal", "spec-ready", "in-progress", "verifying", "code-review", "retro", "merging"]
        );
        yaml.ShouldNotContain("score:");
        // No OpenSpec archive before the merge, and no rebasing: a conflict holds the PBI.
        var declared = yaml.Split('\n').Where(line => !line.TrimStart().StartsWith('#')).ToList();
        declared.ShouldNotContain(line => line.Contains("archive", StringComparison.Ordinal) || line.Contains("rebasing", StringComparison.Ordinal));
        yaml.Split('\n').Where(line => line.Contains("provider:", StringComparison.Ordinal)).ShouldAllBe(line => line.Trim() == "provider: claude-code");
        // #709. The captures follow the gate, configured by the evidence in the repository's own
        // config.json; #746, then quality reports what the change measures, as its quality says.
        yaml.ShouldContain("  verifying:\n    script:\n      - gate\n      - evidence\n      - quality\n");
        // #733. Evidence that was needed and could not be taken, or whose capture failed, holds the PBI.
        yaml.ShouldContain("      blocked:\n        hold: The evidence was needed and could not be taken\n");
        yaml.ShouldContain("      failed:\n        hold: The evidence's captures failed\n");
        HarnessPackage.Owned.ShouldContain(".aio/config.json");
        // quality's checks are files the package carries, run with bash, so none is executable.
        HarnessPackage.Files.Where(file => file.Path.StartsWith(".aio/checks/", StringComparison.Ordinal)).ShouldAllBe(file => !file.Executable);
    }

    [Fact]
    public async Task The_gate_stub_answers_red_with_define_your_gate_here()
    {
        var gate = Path.Combine(Path.GetTempPath(), $"aio-gate-stub-{Guid.NewGuid():N}");
        await File.WriteAllBytesAsync(gate, Entry(".aio/gate").Content);

        try
        {
            using var process = Process.Start(new ProcessStartInfo("bash", [gate]) { RedirectStandardOutput = true })!;
            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            process.ExitCode.ShouldBe(1);
            using var answer = JsonDocument.Parse(output.Trim().Split('\n')[^1]);
            answer.RootElement.GetProperty("outcome").GetString().ShouldBe("red");
            answer.RootElement.GetProperty("why").GetString().ShouldBe("define your gate here");
        }
        finally
        {
            File.Delete(gate);
        }

        // One commented line per stack, each through `run` so its output lands in a log (#638), and no detection.
        var stub = Text(".aio/gate");
        stub.ShouldContain("# run aspire do ci");
        stub.ShouldContain("# run dotnet test");
        stub.ShouldContain("# run pnpm test");
        stub.ShouldContain("aio-gate-");
    }

    /// <summary>
    /// #633. The issue template is the Definition of Ready, so a repository makes it its own like its
    /// loop: a reinstall writes it only where the repository has none. Its sections are the five, and
    /// its Dónde cae is a section <c>file-overlap</c> reads, while the help line under it names no path.
    /// </summary>
    [Fact]
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    public async Task The_package_installs_the_templates_and_file_overlap_reads_where_an_issue_written_with_it_lands()
    {
        HarnessPackage.Owned.ShouldContain(".aio/templates/issue.md");
        HarnessPackage.Owned.ShouldContain(".aio/templates/epic.md");
        Headings(Text(".aio/templates/issue.md")).ShouldBe(["Parent", "Qué pasa hoy", "Qué tiene que pasar", "Dónde cae", "Comprobación", "Lo que no es"]);
        Headings(Text(".aio/templates/epic.md")).ShouldBe(["Objetivo", "El grafo", "Sub-issues", "Criterios de aceptación", "Lo que no es"]);
        Text(".aio/templates/epic.md").ShouldContain("```mermaid\nflowchart LR\n```");

        var template = Text(".aio/templates/issue.md");
        var written = Regex.Replace(template, @"(## Dónde cae\n\n)<!--.*?-->", "$1`src/thing/Thing.cs`\n`.aio/steps/merge`");
        written.ShouldNotBe(template);

        var (code, line) = await FileOverlap(written);
        code.ShouldBe(0, line.ToString());
        line.GetProperty("clear").GetBoolean().ShouldBeTrue(line.ToString());
        line.GetProperty("why").GetString().ShouldBe("Nothing else is in flight.");

        // The template as it ships, help lines and all, declares no path of its own.
        (code, line) = await FileOverlap(template);
        code.ShouldBe(1, line.ToString());
        line.GetProperty("hold").GetBoolean().ShouldBeTrue();
        line.GetProperty("why").GetString().ShouldBe("#7's Dónde cae names no path, so its files cannot be compared. /aio:refine adds them.");
    }

    static List<string> Headings(string markdown) =>
        [.. markdown.Split('\n').Where(line => line.StartsWith("## ", StringComparison.Ordinal)).Select(line => line[3..].Trim())];

    /// <summary>
    /// Runs the package's <c>file-overlap</c> on issue 7 in a checkout whose origin has no claims, with a
    /// <c>gh</c> on PATH that answers <paramref name="body"/> as the issue's body.
    /// </summary>
    [System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
    static async Task<(int Code, JsonElement Line)> FileOverlap(string body)
    {
        var root = Directory.CreateTempSubdirectory("aio-file-overlap-").FullName;
        try
        {
            var work = Path.Combine(root, "work");
            var fake = Path.Combine(root, "fake");
            Directory.CreateDirectory(fake);
            await Git(root, "init", "--quiet", "--bare", "origin.git");
            await Git(root, "init", "--quiet", "work");
            await Git(work, "remote", "add", "origin", Path.Combine(root, "origin.git"));

            await File.WriteAllTextAsync(Path.Combine(fake, "body"), body);
            await File.WriteAllTextAsync(Path.Combine(fake, "gh"), "#!/usr/bin/env bash\ncat \"$FAKE/body\"\n");
            File.SetUnixFileMode(Path.Combine(fake, "gh"), UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            var script = Path.Combine(root, "steps", "file-overlap");
            Directory.CreateDirectory(Path.GetDirectoryName(script)!);
            await File.WriteAllBytesAsync(script, Entry(".aio/steps/file-overlap").Content);
            // Beside its folder, as the package installs it: file-overlap reads Dónde cae through ../lib/ (#758).
            Directory.CreateDirectory(Path.Combine(root, "lib"));
            await File.WriteAllBytesAsync(Path.Combine(root, "lib", "where-it-lands.js"), Entry(".aio/lib/where-it-lands.js").Content);
            // Installed, as the package writes it: file-overlap names the section by its heading (#655).
            Directory.CreateDirectory(Path.Combine(work, ".aio", "templates"));
            await File.WriteAllBytesAsync(Path.Combine(work, ".aio", "templates", "issue.md"), Entry(".aio/templates/issue.md").Content);

            var start = new ProcessStartInfo("node", [script, "7"]) { WorkingDirectory = work, RedirectStandardOutput = true, RedirectStandardError = true };
            start.Environment["PATH"] = $"{fake}{Path.PathSeparator}{Environment.GetEnvironmentVariable("PATH")}";
            start.Environment["FAKE"] = fake;
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync();
            var error = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var last = (await output).Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            last.ShouldNotBeNull($"file-overlap printed no line: {await error}");
            using var answer = JsonDocument.Parse(last);
            return (process.ExitCode, answer.RootElement.Clone());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    static async Task Git(string directory, params string[] arguments)
    {
        var start = new ProcessStartInfo("git", arguments) { WorkingDirectory = directory, RedirectStandardError = true };
        start.Environment["GIT_CONFIG_GLOBAL"] = "/dev/null";
        using var process = Process.Start(start)!;
        var error = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        process.ExitCode.ShouldBe(0, $"git {string.Join(' ', arguments)}: {await error}");
    }

    /// <summary>
    /// #618. The six commands are one set, this repository's <c>.ai/commands/aio/</c>, and without
    /// the context file they name nothing of this repository: every word of the list is in
    /// <c>.aio/context.md</c> or nowhere, and each command reads that file when it exists.
    /// </summary>
    [Fact]
    public void No_package_command_names_this_repositorys_ports_or_files_or_needs_openspec()
    {
        var commands = HarnessPackage.Files.Where(file => file.Path.StartsWith(".ai/commands/aio/", StringComparison.Ordinal)).ToList();

        commands.Count.ShouldBe(6);
        foreach (var command in commands)
        {
            var text = System.Text.Encoding.UTF8.GetString(command.Content);
            Forbidden.Concat(ThisRepositorys).Where(word => text.Contains(word, StringComparison.Ordinal)).ShouldBeEmpty(command.Path);
            text.ShouldContain($"`{Context}`, when the repository has one", customMessage: command.Path);
        }

        // The list catches what it is for: the context file names the ports, the retro log, OpenSpec's
        // files, and the skills that make the plan an OpenSpec change here.
        var context = File.ReadAllText(Path.Combine(Repository, Context));
        foreach (var word in new[] { "IPbiProvider", "five ports", "ARCHITECTURE.md", "retro-log", "proposal.md", "tasks.md", "openspec-propose", "openspec-apply-change", "skill" })
        {
            context.ShouldContain(word);
        }
    }

    [Fact]
    public void The_package_carries_no_archive_no_release_and_no_context()
    {
        File.Exists(Path.Combine(Repository, Context)).ShouldBeTrue("this repository keeps what it adds to the commands");
        HarnessPackage.Files.ShouldNotContain(file => file.Path == Context);
        // #760, #779. What only this repository runs is in scripts/: release (#652), trust-sweep, which
        // tidies this machine's Claude Code configuration (#655), pack, which makes this app's .NET tool
        // (#678), and keep-committer, which only its rebasing state runs (#741); and archive, a step of
        // its own loop, at the root of .aio/. None is in the package.
        foreach (var script in new[] { "release", "trust-sweep", "pack", "keep-committer" })
        {
            File.Exists(Path.Combine(Repository, "scripts", script)).ShouldBeTrue($"this repository keeps its own {script} in scripts/");
            File.Exists(Path.Combine(Repository, ".aio", script)).ShouldBeFalse($"{script} is in scripts/, not in .aio/");
            HarnessPackage.Files.ShouldNotContain(file => file.Path.EndsWith($"/{script}", StringComparison.Ordinal));
        }

        File.Exists(Path.Combine(Repository, ".aio", "archive")).ShouldBeTrue("archive is this repository's step, at the root of .aio/");
        HarnessPackage.Files.ShouldNotContain(file => file.Path == ".aio/archive");
        Directory.Exists(Path.Combine(Repository, ".harness")).ShouldBeFalse();
    }

    [Fact]
    public void Every_file_but_the_packages_own_is_this_repositorys_own_and_its_gate_is_not_the_stub()
    {
        foreach (var file in HarnessPackage.Files)
        {
            var onDisk = Path.Combine(Repository, file.Path);

            if (file.Target is not null)
            {
                new FileInfo(onDisk).LinkTarget.ShouldBe(file.Target, file.Path);
                continue;
            }

            if (PackagesOwn.Contains(file.Path))
            {
                File.ReadAllBytes(onDisk).ShouldNotBe(file.Content, $"{file.Path} is this repository's own");
                continue;
            }

            File.ReadAllBytes(onDisk).ShouldBe(file.Content, file.Path);
            if (!OperatingSystem.IsWindows())
            {
                File.GetUnixFileMode(onDisk).HasFlag(UnixFileMode.UserExecute).ShouldBe(file.Executable, file.Path);
            }
        }

        HarnessPackage.Files.Where(file => file.Path.StartsWith(".aio/", StringComparison.Ordinal) && file.Executable)
            .Select(file => file.Path)
            .ShouldBe(Scripts().Concat([".aio/scorers/laya/laya", HarnessPackage.Gate]).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// #779. A file of the package starts with the managed line, whatever its language says a comment
    /// is, and no file of the repository's does: not what the package writes once and the repository
    /// edits, not this repository's own steps and scripts. The exceptions are files whose bytes mean
    /// something to a program: <c>version</c>, a number the engine reads, <c>laya-requirements.txt</c>,
    /// whose hash is the pin of laya's venv, <c>settings.json</c>, and the six commands, which open with
    /// front matter.
    /// </summary>
    [Fact]
    public void Every_file_of_the_package_starts_with_the_managed_line_and_no_file_of_the_repository_does()
    {
        var line = $"{HarnessPackage.Managed}. Install harness overwrites it.";
        string[] exempt = [".aio/version", ".aio/scorers/laya/laya-requirements.txt", ".claude/settings.json"];

        var managed = HarnessPackage.Files
            .Where(file => file.Target is null && !HarnessPackage.Owned.Contains(file.Path) && !exempt.Contains(file.Path) && !file.Path.StartsWith(".ai/", StringComparison.Ordinal))
            .ToList();

        managed.Select(file => file.Path).ShouldContain(".aio/aio");
        managed.Select(file => file.Path).ShouldContain(".claude/hooks/pre-tool-use");
        foreach (var file in managed)
        {
            FirstLines(file.Content).ShouldContain(line, customMessage: file.Path);
        }

        foreach (var file in HarnessPackage.Files.Where(file => file.Target is null && HarnessPackage.Owned.Contains(file.Path)))
        {
            FirstLines(file.Content).ShouldNotContain(HarnessPackage.Managed, customMessage: file.Path);
        }

        var theirs = new[] { Path.Combine(".aio", "archive"), Path.Combine(".aio", "context.md"), Path.Combine(".aio", "config.json"), Path.Combine(".aio", "loop.yaml"), Path.Combine(".aio", "gate") }
            .Concat(Directory.GetFiles(Path.Combine(Repository, "scripts")).Select(script => Path.GetRelativePath(Repository, script)));
        foreach (var path in theirs)
        {
            FirstLines(File.ReadAllBytes(Path.Combine(Repository, path))).ShouldNotContain(HarnessPackage.Managed, customMessage: path);
        }
    }

    static string FirstLines(byte[] content) =>
        string.Join('\n', System.Text.Encoding.UTF8.GetString(content).Split('\n').Take(3));

    /// <summary>
    /// #729, #779. The root of <c>.aio/</c> holds what the repository owns, and the package says where
    /// each file was in harness 36 and before, nearest layout first, so an update over an old layout
    /// moves it and leaves no copy behind.
    /// </summary>
    [Fact]
    public void Every_file_of_the_package_says_where_it_was_before_harness_37()
    {
        HarnessPackage.Files
            .Where(file => file.Path.StartsWith(".aio/", StringComparison.Ordinal) && !HarnessPackage.Owned.Contains(file.Path) || file.Path == HarnessPackage.Gate)
            .Select(file => file.Path)
            .Where(path => path != ".aio/version" && path != ".aio/README.md")
            .ShouldAllBe(path => HarnessPackage.Former(path).Count > 0);

        HarnessPackage.Former(".aio/gate").ShouldBe([".harness/gate", ".harness/scripts/gate"]);
        HarnessPackage.Former(".aio/loop.yaml").ShouldBe([".harness/loop.yaml"]);
        HarnessPackage.Former(".aio/templates/issue.md").ShouldBe([".harness/templates/issue.md"]);
        HarnessPackage.Former(".aio/aio").ShouldBe([".harness/aio/aio", ".harness/scripts/aio"]);
        HarnessPackage.Former(".aio/steps/claim").ShouldBe([".harness/aio/claim", ".harness/scripts/claim"]);
        HarnessPackage.Former(".aio/checks/crap.sh").ShouldBe([".harness/aio/checks/crap.sh"]);
        HarnessPackage.Former(".aio/lib/issue-state.mjs").ShouldBe([".harness/aio/lib/issue-state.mjs", ".harness/lib/issue-state.mjs"]);
        HarnessPackage.Former(".aio/schemas/done.v1.json").ShouldBe([".harness/aio/schemas/done.v1.json", ".harness/schemas/done.v1.json"]);
        // #759. laya was straight in aio/ until harness 36.
        HarnessPackage.Former(".aio/scorers/laya/laya").ShouldBe([".harness/aio/scorers/laya/laya", ".harness/aio/laya"]);
        HarnessPackage.Former(".aio/scorers/laya/laya-requirements.txt").ShouldBe([".harness/aio/scorers/laya/laya-requirements.txt", ".harness/aio/laya-requirements.txt"]);
        // The config gathers three files and has no former place of its own; the claude files did not move.
        HarnessPackage.Former(".aio/config.json").ShouldBeEmpty();
        HarnessPackage.Former(".claude/settings.json").ShouldBeEmpty();
    }

    const string HarnessReadme = ".aio/README.md";

    /// <summary>
    /// #742, #779. The package carries <c>.aio/README.md</c>, this repository's own file, and it is a file
    /// the package overwrites, so it says first what the rule is and names every file a repository
    /// makes its own: each is in the layout, and is not in <see cref="HarnessPackage.Owned"/> by accident.
    /// </summary>
    [Fact]
    public void The_package_carries_the_readme_which_says_what_the_managed_line_is()
    {
        HarnessPackage.Owned.ShouldNotContain(HarnessReadme);
        Entry(HarnessReadme).Executable.ShouldBeFalse();

        var readme = Text(HarnessReadme);
        readme.Split('\n')[0].ShouldBe($"<!-- {HarnessPackage.Managed}. Install harness overwrites it. -->");
        Headings(readme).ShouldBe(["What is whose", "The loop", "Make it yours", "Where the loop leaves its work", "Updating"]);
        readme.ShouldContain("A file that starts with the line `Managed by aio. Install harness overwrites it.`");
        // Every file a repository makes its own is in the layout, and the package's files too.
        foreach (var path in HarnessPackage.Owned.Select(path => path[".aio/".Length..]).Select(path => path.StartsWith("templates/", StringComparison.Ordinal) ? "templates/" : path))
        {
            Layout(readme).ShouldContain(path);
        }
        LayoutProblems(readme).ShouldBeEmpty();
        readme.ShouldContain("What is deterministic is the path the loop takes with a result, never the result.");
        readme.ShouldContain("schemas/done.v1.json");
        // #746. quality's outcome, and the comment it leaves on the issue.
        readme.ShouldContain("| `quality` | `reported`, always with exit 0 |");
        readme.ShouldContain("`<!-- aio:quality <sha> -->`");
        // #779. The three files of configuration are three keys of one.
        readme.ShouldContain("`config.json` holds what three steps read, one key each. `evidence`");
        readme.ShouldContain("`protected` lists the paths a person always reviews, as an array of patterns.");
    }

    /// <summary>
    /// #759, #779. The README's layout names every folder of <c>.aio/</c> the package carries, so a
    /// folder added to the package is a folder the README says what it is.
    /// </summary>
    [Fact]
    public void The_readme_layout_names_every_folder_of_the_package_and_falls_behind_a_new_one()
    {
        var readme = Text(HarnessReadme);
        PackageFolders().ShouldBe(["checks/", "lib/", "schemas/", "scorers/laya/", "steps/", "templates/"]);
        LayoutProblems(readme).ShouldBeEmpty();

        LayoutProblems(readme.Replace("  scorers/laya/ ", "  scorers/      ", StringComparison.Ordinal))
            .ShouldBe(["The README's layout does not name `scorers/laya/`."]);
    }

    /// <summary>The README's layout block, between the fences under <c>## What is whose</c>.</summary>
    static string Layout(string readme)
    {
        var start = readme.IndexOf("```\n.aio/\n", StringComparison.Ordinal) + "```\n".Length;
        return readme[start..readme.IndexOf("```", start, StringComparison.Ordinal)];
    }

    /// <summary>Each folder of <c>.aio/</c> the package carries, as <c>scorers/laya/</c>.</summary>
    static IEnumerable<string> PackageFolders() =>
        HarnessPackage.Files
            .Select(file => file.Path)
            .Where(path => path.StartsWith(".aio/", StringComparison.Ordinal))
            .Select(path => path[".aio/".Length..])
            .Where(rest => rest.Contains('/', StringComparison.Ordinal))
            .Select(rest => rest[..(rest.LastIndexOf('/') + 1)])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal);

    static List<string> LayoutProblems(string readme) =>
        [.. PackageFolders().Where(folder => !Layout(readme).Contains($"  {folder} ", StringComparison.Ordinal)).Select(folder => $"The README's layout does not name `{folder}`.")];

    /// <summary>
    /// #742. The README's loop table is the package's <c>loop.yaml</c>: one row per declared state,
    /// naming its agent and command, or its scripts, so the README cannot fall behind the loop.
    /// </summary>
    [Fact]
    public void The_readme_loop_table_is_the_packages_loop_and_falls_behind_it_on_any_change()
    {
        var readme = Text(HarnessReadme);
        LoopTableProblems(LoopStates(), readme).ShouldBeEmpty();

        // A state taken out of loop.yaml, the README untouched, is a row the loop does not have.
        var fewer = LoopStates();
        fewer.Children.Remove(new YamlScalarNode("retro"));
        LoopTableProblems(fewer, readme).ShouldBe(["The README's loop table has `retro`, which loop.yaml does not declare."]);

        // And a row taken out of the README is a state it does not describe.
        var row = readme.Split('\n').Single(line => line.StartsWith("| `merging` |", StringComparison.Ordinal));
        LoopTableProblems(LoopStates(), readme.Replace(row + "\n", "", StringComparison.Ordinal))
            .ShouldBe(["The README's loop table has no row for `merging`."]);

        // A script renamed in loop.yaml is a row that no longer names it.
        var renamed = LoopStates();
        ((YamlMappingNode)renamed.Children[new YamlScalarNode("merging")]).Children[new YamlScalarNode("script")] = new YamlScalarNode("land");
        LoopTableProblems(renamed, readme).ShouldBe(["The README's row for `merging` does not name `land`."]);
    }

    /// <summary>
    /// #742. The README's example of a <c>merge-review</c> state, put before <c>merging</c> in the
    /// package's loop, is a loop the engine accepts: the score, its three outcomes, and a hold with a next.
    /// </summary>
    [Fact]
    public async Task The_readme_merge_review_example_is_a_valid_state_of_the_packages_loop()
    {
        var readme = Text(HarnessReadme);
        var start = readme.IndexOf("```yaml\n", StringComparison.Ordinal) + "```yaml\n".Length;
        var example = readme[start..readme.IndexOf("```", start, StringComparison.Ordinal)];
        example.ShouldStartWith("  merge-review:\n    score: diff-risk\n");

        var loop = Text(".aio/loop.yaml");
        var retro = "  retro:\n    agent: coder\n    command: /aio:review\n    next: merging\n";
        loop.ShouldContain(retro);
        var yaml = loop.Replace(retro, retro.Replace("next: merging", "next: merge-review", StringComparison.Ordinal) + "\n" + example, StringComparison.Ordinal);

        using var client = fixture.CreateClient();
        var check = await (await client.PostAsJsonAsync("/api/loop/validate", new { yaml })).Content.ReadFromJsonAsync<JsonElement>();

        check.GetProperty("problems").EnumerateArray().Select(problem => problem.GetString()).ShouldBeEmpty();
        check.GetProperty("valid").GetBoolean().ShouldBeTrue();
        check.GetProperty("states").EnumerateArray().Select(state => state.GetProperty("name").GetString()).ShouldContain("merge-review");
    }

    /// <summary>The <c>states</c> of the package's <c>loop.yaml</c>, read fresh so a test can change them.</summary>
    static YamlMappingNode LoopStates()
    {
        var stream = new YamlStream();
        stream.Load(new StringReader(Text(".aio/loop.yaml")));
        return (YamlMappingNode)((YamlMappingNode)stream.Documents[0].RootNode).Children[new YamlScalarNode("states")];
    }

    /// <summary>
    /// What the loop table under <c>## The loop</c> gets wrong about <paramref name="states"/>: a state
    /// with no row, a row with no state, and a row that does not name the state's agent, command,
    /// score or every script.
    /// </summary>
    static List<string> LoopTableProblems(YamlMappingNode states, string readme)
    {
        var section = readme.Split('\n').SkipWhile(line => line != "## The loop").Skip(1).TakeWhile(line => !line.StartsWith('#'));
        var rows = section
            .Where(line => line.StartsWith("| `", StringComparison.Ordinal))
            .Select(line => line.Split('|', StringSplitOptions.TrimEntries))
            .ToDictionary(cells => cells[1].Trim('`'), cells => cells[2], StringComparer.Ordinal);

        var problems = new List<string>();
        foreach (var (key, value) in states.Children)
        {
            var name = ((YamlScalarNode)key).Value!;
            if (!rows.TryGetValue(name, out var runs))
            {
                problems.Add($"The README's loop table has no row for `{name}`.");
                continue;
            }

            var named = new List<string>();
            foreach (var (field, content) in ((YamlMappingNode)value).Children)
            {
                switch (((YamlScalarNode)field).Value)
                {
                    case "agent" or "command" or "score":
                        named.Add(((YamlScalarNode)content).Value!);
                        break;
                    case "script" when content is YamlScalarNode script:
                        named.Add(script.Value!);
                        break;
                    case "script" when content is YamlSequenceNode scripts:
                        named.AddRange(scripts.Children.Select(script => ((YamlScalarNode)script).Value!));
                        break;
                }
            }
            problems.AddRange(named.Where(word => !runs.Contains($"`{word}`", StringComparison.Ordinal)).Select(word => $"The README's row for `{name}` does not name `{word}`."));
        }

        var declared = states.Children.Keys.Select(key => ((YamlScalarNode)key).Value!).ToHashSet(StringComparer.Ordinal);
        problems.AddRange(rows.Keys.Where(row => !declared.Contains(row)).Select(row => $"The README's loop table has `{row}`, which loop.yaml does not declare."));
        return problems;
    }

    /// <summary>
    /// #732, #779. diff-risk decides with <c>protected</c> in <c>.aio/config.json</c>, so the package
    /// carries it: an example that names nothing, and the repository's own once installed, so a
    /// reinstall keeps the one it wrote. Its three keys are the ones the three steps read.
    /// </summary>
    [Fact]
    public void The_package_carries_config_json_as_an_example_that_captures_nothing_and_protects_nothing()
    {
        HarnessPackage.Owned.ShouldContain(HarnessPackage.Config);
        var example = Text(HarnessPackage.Config);
        example.ShouldContain("diff-risk reads the list from the base commit");
        example.ShouldContain("nothing the gate already fails");

        using var config = JsonDocument.Parse(string.Join('\n', example.Split('\n').Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal))));
        config.RootElement.EnumerateObject().Select(property => property.Name).ShouldBe(["evidence", "quality", "protected"]);
        config.RootElement.GetProperty("evidence").EnumerateObject().ShouldBeEmpty();
        config.RootElement.GetProperty("quality").EnumerateObject().ShouldBeEmpty();
        config.RootElement.GetProperty("protected").EnumerateArray().ShouldBeEmpty();
    }

    /// <summary>
    /// This repository's scripts the package carries: <c>aio</c> and every step of <c>.aio/steps/</c>.
    /// What is only this repository's, <c>archive</c> in <c>.aio/</c> and <c>release</c> (#652),
    /// <c>trust-sweep</c> (#655) and <c>pack</c> (#678) in <c>scripts/</c>, is not in the package.
    /// </summary>
    static IEnumerable<string> Scripts() =>
        Directory
            .GetFiles(Path.Combine(Repository, ".aio", "steps"))
            .Where(script => !Path.HasExtension(script))
            .Select(script => $".aio/steps/{Path.GetFileName(script)}")
            .Append(".aio/aio");

    static HarnessFile Entry(string path) => HarnessPackage.Files.Single(file => file.Path == path);

    static string Text(string path) => System.Text.Encoding.UTF8.GetString(Entry(path).Content);

    /// <summary>Walks up from the test binary to <c>src/</c>, from a worktree as readily as from the main checkout.</summary>
    static string SourceRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AiOrchestrator.slnx")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("No AiOrchestrator.slnx above the test binary.");
    }
}
