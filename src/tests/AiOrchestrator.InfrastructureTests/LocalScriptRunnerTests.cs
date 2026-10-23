using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.Scripts;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// <see cref="LocalScriptRunner"/> against scripts written into a temporary checkout: what it runs,
/// and what it reads back from the last line.
/// </summary>
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class LocalScriptRunnerTests : IDisposable
{
    readonly string _root = Directory.CreateTempSubdirectory("aio-script-runner-").FullName;
    readonly LocalScriptRunner _runner = new();

    public void Dispose() => Directory.Delete(_root, recursive: true);

    void Script(string name, string body, string folder = ".aio/steps")
    {
        var path = Path.Combine(_root, folder, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "#!/usr/bin/env bash\n" + body + "\n");
        File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    Task<ScriptResult> Run(string name, params string[] arguments) =>
        _runner.RunAsync(new ScriptRequest(name, _root, arguments), CancellationToken.None);

    [Fact]
    public async Task The_last_line_is_read_as_json_and_the_arguments_arrive_unquoted()
    {
        Script("echo", """echo "working in $(pwd)" >&2; echo 'progress'; printf '{"got": "%s", "count": %s}\n' "$1" "$#" """);

        var result = await Run("echo", "two words");

        result.ExitCode.ShouldBe(0);
        result.Line!.Value.GetProperty("got").GetString().ShouldBe("two words");
        result.Line.Value.GetProperty("count").GetInt32().ShouldBe(1);
        result.Output.ShouldContain("progress");
        result.Output.ShouldContain("working in");
    }

    /// <summary>#741. Whatever a script of the engine commits is committed by that script.</summary>
    [Fact]
    public async Task A_script_commits_as_the_script()
    {
        Script("gate", """printf '{"name": "%s", "email": "%s"}\n' "$GIT_COMMITTER_NAME" "$GIT_COMMITTER_EMAIL" """, ".aio");

        var result = await Run("gate");

        result.Line!.Value.GetProperty("name").GetString().ShouldBe("aio script gate");
        result.Line.Value.GetProperty("email").GetString().ShouldBe("noreply@aio.invalid");
    }

    [Fact]
    public async Task A_non_zero_exit_is_a_result_with_its_line()
    {
        Script("refuse", """echo '{"ok": false, "why": "no"}'; exit 1""");

        var result = await Run("refuse");

        result.ExitCode.ShouldBe(1);
        result.Line!.Value.GetProperty("ok").GetBoolean().ShouldBeFalse();
    }

    /// <summary>
    /// #710. A red gate that quotes a coloured line of its log prints the escape codes inside its JSON
    /// string; the line is still its line, read without them.
    /// </summary>
    [Fact]
    public async Task A_red_gate_with_colour_codes_in_its_why_still_has_its_line()
    {
        Script("gate", """printf '{"outcome": "red", "go": false, "why": "aspire do ci failed (exit 6): \033[38;5;9m✗ [ERR] Step failed.\033[0m"}\n'; exit 1""");

        var result = await Run("gate");

        result.ExitCode.ShouldBe(1);
        result.Output.ShouldContain("\u001b[38;5;9m");
        result.Line.ShouldNotBeNull().GetProperty("outcome").GetString().ShouldBe("red");
        result.Line.Value.GetProperty("why").GetString().ShouldBe("aspire do ci failed (exit 6): ✗ [ERR] Step failed.");
    }

    [Fact]
    public async Task A_script_that_ends_without_json_has_no_line()
    {
        Script("plain", "echo 'done, no json'");

        var result = await Run("plain");

        result.ExitCode.ShouldBe(0);
        result.Line.ShouldBeNull();
        result.Output.ShouldContain("done, no json");
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("../escape")]
    [InlineData(".hidden")]
    public async Task A_name_that_is_not_a_script_of_the_harness_runs_nothing(string name)
    {
        var result = await Run(name);

        result.ExitCode.ShouldBe(-1);
        result.Line.ShouldBeNull();
        result.Output.ShouldNotBeEmpty();
    }

    /// <summary>
    /// #729, #779. A script is found by its name: the repository's at the root of <c>.aio/</c> first, the
    /// package's in <c>.aio/steps/</c> next, and the layout before harness 37 last (<c>.harness/</c>,
    /// <c>.harness/repo/</c>, <c>.harness/aio/</c> and, before harness 24, <c>.harness/scripts/</c>), so a
    /// repository that has not updated still runs.
    /// </summary>
    [Fact]
    public async Task The_root_is_read_before_steps_and_the_old_folders_last()
    {
        Script("gate", """echo '{"from": "root"}'""", ".aio");
        Script("gate", """echo '{"from": "steps"}'""");
        Script("claim", """echo '{"from": "steps"}'""");
        Script("claim", """echo '{"from": "harness aio"}'""", ".harness/aio");
        Script("merge", """echo '{"from": "harness aio"}'""", ".harness/aio");
        Script("archive", """echo '{"from": "harness repo"}'""", ".harness/repo");
        Script("evidence", """echo '{"from": "scripts"}'""", ".harness/scripts");
        Script("evidence", """echo '{"from": "harness root"}'""", ".harness");

        (await Run("gate")).Line!.Value.GetProperty("from").GetString().ShouldBe("root");
        (await Run("claim")).Line!.Value.GetProperty("from").GetString().ShouldBe("steps");
        (await Run("merge")).Line!.Value.GetProperty("from").GetString().ShouldBe("harness aio");
        (await Run("archive")).Line!.Value.GetProperty("from").GetString().ShouldBe("harness repo");
        (await Run("evidence")).Line!.Value.GetProperty("from").GetString().ShouldBe("harness root");
        (await Run("missing")).Output.ShouldBe($"{_root} has no missing in .aio/ or .aio/steps/, nor in the .harness/ folders of an install before harness 37.");
    }
}
