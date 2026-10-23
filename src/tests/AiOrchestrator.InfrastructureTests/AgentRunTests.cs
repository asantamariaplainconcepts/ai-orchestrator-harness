using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.AgentProviders;
using AiOrchestrator.Infrastructure.Environments;
using AiOrchestrator.Infrastructure.Files;
using AiOrchestrator.Infrastructure.Sessions;
using Json.Schema;
using Microsoft.Extensions.Configuration;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// A run of the claude-code provider from end to end: a real worktree prepared by
/// <c>.aio/steps/prepare</c>, a real PTY, this repository's own <c>aio</c> and a real
/// <c>cleanup</c>. The one stand-in is the CLI, a shell script answering to <c>claude</c>'s
/// arguments, because the real one needs a sign-in and costs a model call per test.
/// </summary>
public sealed class AgentRunTests : IDisposable
{
    const string Marker = "CLAUDE_CODE_AIO_AGENT_RUN_TEST";
    static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

    readonly GitCli _git = new();
    string _root = Directory.CreateTempSubdirectory("aio-agent-run-").FullName;

    string Checkout => Path.Combine(_root, "checkout");
    string RunDirectory => Path.Combine(_root, "runs", "1");

    /// <summary>Claude Code's configuration and transcripts for these tests, never the user's own.</summary>
    string ConfigFile => Path.Combine(_root, "claude", ".claude.json");

    string Transcripts => Path.Combine(_root, "claude", "projects");

    ClaudeCodeProvider Provider(string cli) => new(cli, ConfigFile, Transcripts);

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Marker, null);
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task A_run_starts_in_a_worktree_ends_with_aio_done_and_retiring_it_keeps_the_record_and_the_recording()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Initialize();
        Environment.SetEnvironmentVariable(Marker, "1");
        var environment = new WorktreeEnvironment();
        var provider = Provider(StandIn());
        // #546, #608. The user's own entries, which the run must leave exactly as they were.
        const string Trusted = """{"numStartups": 7, "projects": {"/elsewhere": {"hasTrustDialogAccepted": true}}}""";
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);
        File.WriteAllText(ConfigFile, Trusted);

        var place = await environment.PrepareAsync(new PlaceRequest(Checkout, "change/1", "HEAD"), CancellationToken.None);

        place.Environment.ShouldBe("worktree");
        place.WorkingDirectory.ShouldBe(Path.Combine(Checkout, ".worktrees", "change-1"));
        (await Git(place.WorkingDirectory, "branch", "--show-current")).Trim().ShouldBe("change/1");

        var request = new AgentRequest(
            "/aio:implement 1",
            "claude-opus-5-5",
            place,
            Path.Combine(RunDirectory, "done.json"),
            Path.Combine(RunDirectory, "session.cast")
        );
        var startedAt = DateTimeOffset.UtcNow;
        await using var session = new PtySession(TimeProvider.System);

        // #612. The worktree enters its place by starting the launch exactly as the provider gave it.
        var launch = provider.Launch(request);
        environment.Enter(place, launch).ShouldBeSameAs(launch);
        environment.Permissions.ShouldBeNull("the instance's mode decides on this machine");
        await session.StartAsync(environment.Enter(place, launch), CancellationToken.None);
        var seen = await ReadToEnd(session);

        seen.ShouldContain($"cwd:{place.WorkingDirectory}");
        seen.ShouldContain("args:--permission-mode acceptEdits --model claude-opus-5-5 /aio:implement 1");
        seen.ShouldContain("markers:0");
        seen.ShouldContain("The run ends with 'approved'.");

        var end = await provider.EndAsync(request, CancellationToken.None);

        end.Record.ShouldNotBeNull();
        using var record = JsonDocument.Parse(end.Record);
        Schema().Evaluate(record.RootElement).IsValid.ShouldBeTrue(end.Record);
        record.RootElement.GetProperty("outcome").GetString().ShouldBe("approved");
        DateTimeOffset.Parse(record.RootElement.GetProperty("at").GetString()!, CultureInfo.InvariantCulture).ShouldBeGreaterThanOrEqualTo(startedAt.AddSeconds(-1));
        File.ReadAllText(request.RecordingPath!).ShouldContain("approved");
        File.ReadAllText(ConfigFile).ShouldBe(Trusted, "a run writes no trust (#608)");

        // In the order RunExecution retires a run: the provider, while the worktree is on disk, then the place.
        provider.Retire(place);
        await environment.RetireAsync(place, CancellationToken.None);

        JsonNode.DeepEquals(JsonNode.Parse(File.ReadAllText(ConfigFile)), JsonNode.Parse(Trusted)).ShouldBeTrue(File.ReadAllText(ConfigFile));
        Directory.Exists(place.WorkingDirectory).ShouldBeFalse();
        (await Git(Checkout, "worktree", "list")).ShouldNotContain(".worktrees");
        (await Git(Checkout, "branch", "--list", "change/1")).ShouldContain("change/1");
        File.ReadAllText(request.EndRecordPath).ShouldBe(end.Record);
        Cast.Read(request.RecordingPath!).Output.ShouldContain("The run ends with 'approved'.");
    }

    [Fact]
    public void A_cli_that_is_not_on_the_path_does_not_start_and_says_why()
    {
        var provider = Provider("aio-no-such-agent-cli");
        var request = new AgentRequest(
            "/aio:implement 1",
            null,
            new RunPlace("worktree", _root),
            Path.Combine(RunDirectory, "done.json"),
            null
        );

        var refusal = Should.Throw<InvalidOperationException>(() => provider.Launch(request));

        refusal.Message.ShouldBe(
            "claude-code cannot start: `aio-no-such-agent-cli` is not on the PATH of the login shell. "
                + "Install it with `npm install -g @anthropic-ai/claude-code`."
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_setting_names_the_executable_claude_code_spawns_and_without_it_claude_is_looked_for(bool named)
    {
        var cli = StandIn();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(named ? [new(AgentProviderRegistration.ClaudeCodeCliSetting, cli)] : [])
            .Build();
        var request = new AgentRequest("/aio:refine 7", null, new RunPlace("worktree", _root), "/runs/7/done.json", null);

        var provider = AgentProviderRegistration.ClaudeCode(configuration, ConfigFile, Transcripts);

        // Without the setting, `claude` may or may not be installed here: either way the stand-in is not what runs.
        string[] line;
        try
        {
            var options = provider.Launch(request);
            line = [options.FileName, .. options.Arguments];
        }
        catch (InvalidOperationException refused) when (!named)
        {
            line = [refused.Message];
        }

        if (named)
        {
            line[^4..].ShouldBe([cli, "--permission-mode", "acceptEdits", "/aio:refine 7"]);
        }
        else
        {
            line.ShouldNotContain(cli);
        }
    }

    [Fact]
    public void Without_a_model_the_command_is_the_only_argument_and_the_checkouts_scripts_lead_the_path()
    {
        var cli = StandIn();
        var request = new AgentRequest("/aio:refine 7", null, new RunPlace("worktree", _root), "/runs/7/done.json", null);

        var options = Provider(cli).Launch(request);

        // With session markers in this process the line is `env -u … claude …`; without, `claude …`.
        string[] line = [options.FileName, .. options.Arguments];
        line[^4..].ShouldBe([cli, "--permission-mode", "acceptEdits", "/aio:refine 7"]);
        options.WorkingDirectory.ShouldBe(_root);
        options.Environment["PATH"].ShouldStartWith(Path.Combine(_root, ".aio") + Path.PathSeparator);
        options.Environment["AIO_DONE_FILE"].ShouldBe("/runs/7/done.json");
        options.Environment.ShouldNotContainKey("AIO_API", "a request with no address gives none");
    }

    [Fact]
    public void Every_agent_gets_the_control_planes_address_and_the_repository_for_aio_issue()
    {
        var request = new AgentRequest("/aio:refine 7", null, new RunPlace("worktree", _root), "/runs/7/done.json", null)
        {
            Repository = "owner/repo",
            Api = new Uri("http://localhost:5080/"),
        };

        var options = Provider(StandIn()).Launch(request);

        options.Environment["AIO_API"].ShouldBe("http://localhost:5080");
        options.Environment["AIO_REPOSITORY"].ShouldBe("owner/repo");
    }

    /// <summary>#741. A run's commits are committed by the run; a folder session, with no run, leaves the person's committer.</summary>
    [Fact]
    public void A_runs_agent_commits_as_the_run_and_a_folder_session_as_the_person()
    {
        var run = Guid.Parse("0199e2a4-1111-7000-8000-000000000741");
        var request = new AgentRequest("/aio:implement 7", null, new RunPlace("worktree", _root), "/runs/7/done.json", null, run);

        var options = Provider(StandIn()).Launch(request);
        var session = Provider(StandIn()).Launch(request with { RunId = Guid.Empty });

        options.Environment["GIT_COMMITTER_NAME"].ShouldBe("aio run 0199e2a4-1111-7000-8000-000000000741");
        options.Environment["GIT_COMMITTER_EMAIL"].ShouldBe("noreply@aio.invalid");
        options.Environment.ShouldNotContainKey("GIT_AUTHOR_NAME", "the author stays the person's");
        session.Environment.ShouldNotContainKey("GIT_COMMITTER_NAME");
    }

    [Fact]
    public async Task An_agent_that_never_called_aio_done_leaves_no_record()
    {
        var request = new AgentRequest("x", null, new RunPlace("worktree", _root), Path.Combine(RunDirectory, "done.json"), null);

        (await Provider("claude").EndAsync(request, CancellationToken.None)).Record.ShouldBeNull();
    }

    [Fact]
    public async Task A_place_that_is_not_a_repository_is_refused_in_the_scripts_words()
    {
        var refusal = await Should.ThrowAsync<InvalidOperationException>(() =>
            new WorktreeEnvironment().PrepareAsync(new PlaceRequest(_root, "change/1", "HEAD"), CancellationToken.None)
        );

        refusal.Message.ShouldContain("is not a git repository");
    }

    [Fact]
    public async Task A_run_names_the_session_after_itself_and_reads_what_it_cost_from_that_transcript()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Initialize();
        var environment = new WorktreeEnvironment();
        var place = await environment.PrepareAsync(new PlaceRequest(Checkout, "change/2", "HEAD"), CancellationToken.None);
        var runId = Guid.CreateVersion7();
        var request = new AgentRequest(
            "/aio:implement 2",
            "claude-opus-5-5",
            place,
            Path.Combine(RunDirectory, "done.json"),
            null,
            runId,
            DateTimeOffset.UtcNow
        );
        var provider = Provider(StandIn());

        await using var session = new PtySession(TimeProvider.System);
        await session.StartAsync(provider.Launch(request), CancellationToken.None);
        var seen = await ReadToEnd(session);
        var end = await provider.EndAsync(request, CancellationToken.None);

        seen.ShouldContain($"args:--permission-mode acceptEdits --session-id {runId:D} --model claude-opus-5-5 /aio:implement 2");
        end.Usage.ShouldNotBeNull();

        // The row is streamed twice, as Claude Code does, and counted once.
        end.Usage.InputTokens.ShouldBe(1000 + 3000 + 400);
        end.Usage.OutputTokens.ShouldBe(200);

        // claude-opus-5 at $5 and $25 a million, a cache read at a tenth of input and a write at 1.25.
        end.Usage.CostUsd.ShouldBe(0.005m + 0.005m + 0.0015m + 0.0025m);
        await environment.RetireAsync(place, CancellationToken.None);
    }

    [Fact]
    public void A_request_with_no_run_is_given_no_session_id()
    {
        var request = new AgentRequest("/aio:refine 7", null, new RunPlace("worktree", _root), "/runs/7/done.json", null);

        var options = Provider(StandIn()).Launch(request);

        options.Arguments.ShouldNotContain("--session-id");
    }

    [Fact]
    public void Without_a_command_the_agent_opens_with_no_prompt()
    {
        var cli = StandIn();
        var request = new AgentRequest("", null, new RunPlace("worktree", _root), "/runs/7/done.json", null);

        var options = Provider(cli).Launch(request);

        string[] line = [options.FileName, .. options.Arguments];
        line[^3..].ShouldBe([cli, "--permission-mode", "acceptEdits"]);
    }

    [Theory]
    [InlineData(AgentPermissions.AllowList, "--permission-mode acceptEdits")]
    [InlineData(AgentPermissions.Auto, """--permission-mode auto --settings {"skipAutoPermissionPrompt":true}""")]
    [InlineData(AgentPermissions.Yolo, """--permission-mode bypassPermissions --settings {"skipDangerousModePermissionPrompt":true}""")]
    public void Each_permission_mode_is_the_permission_mode_claude_starts_in_with_its_menu_answered(AgentPermissions permissions, string mode)
    {
        var cli = StandIn();
        var runId = Guid.CreateVersion7();
        var request = new AgentRequest("/aio:implement 1", null, new RunPlace("worktree", _root), "/runs/1/done.json", null, runId, Permissions: permissions);

        var options = Provider(cli).Launch(request);

        string[] line = [options.FileName, .. options.Arguments];
        string.Join(' ', line[(Array.IndexOf(line, cli) + 1)..]).ShouldBe($"{mode} --session-id {runId:D} /aio:implement 1");
    }

    [Fact]
    public void Launching_leaves_claude_codes_configuration_as_it_was()
    {
        // #608. Trusting a folder is the person's answer in Claude Code, so a launch writes none.
        var request = new AgentRequest("/aio:refine 7", null, new RunPlace("worktree", _root), "/runs/7/done.json", null);
        var provider = Provider(StandIn());

        provider.Launch(request);
        File.Exists(ConfigFile).ShouldBeFalse("a launch creates no configuration");

        const string Before = """{"numStartups": 7, "projects": {"/elsewhere": {"hasTrustDialogAccepted": true}}}""";
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);
        File.WriteAllText(ConfigFile, Before);
        var modified = File.GetLastWriteTimeUtc(ConfigFile);

        provider.Launch(request);

        File.ReadAllText(ConfigFile).ShouldBe(Before);
        File.GetLastWriteTimeUtc(ConfigFile).ShouldBe(modified);
    }

    [Theory]
    [InlineData("CLAUDE_CODE_CHILD_SESSION", true)]
    [InlineData("CLAUDECODE", true)]
    [InlineData("OPENCODE_SESSION_ID", true)]
    [InlineData("CLAUDE_CONFIG_DIR", false)]
    [InlineData("HOME", false)]
    public void Only_variables_naming_an_agent_session_are_removed(string name, bool removed) =>
        AgentLaunch.IsSessionMarker(name).ShouldBe(removed);

    /// <summary>
    /// A checkout carrying this repository's <c>aio</c>, with no origin, so <c>prepare</c> branches
    /// from where it stands.
    /// </summary>
    async Task Initialize()
    {
        await Git(_root, "init", "--quiet", "--initial-branch=master", Checkout);
        // macOS's temp directory is /var -> /private/var, and git names the real one.
        _root = Path.GetDirectoryName((await Git(Checkout, "rev-parse", "--show-toplevel")).Trim())!;
        await Git(Checkout, "config", "user.name", "Agent run tests");
        await Git(Checkout, "config", "user.email", "agent-run-tests@localhost");
        await Git(Checkout, "config", "commit.gpgSign", "false");

        var aio = Path.Combine(Checkout, ".aio", "aio");
        Directory.CreateDirectory(Path.GetDirectoryName(aio)!);
        File.Copy(Path.Combine(ThisRepository.Root(), ".aio", "aio"), aio);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(aio, (UnixFileMode)0b111_101_101);
        }

        await File.WriteAllTextAsync(Path.Combine(Checkout, ".gitignore"), ".worktrees/\n");
        await Git(Checkout, "add", ".");
        await Git(Checkout, "commit", "--quiet", "-m", "base");
    }

    /// <summary>
    /// A <c>claude</c> that says where it is, what it was asked and how many session markers it
    /// inherited, then ends the run the way an agent must: a bare <c>aio done</c>.
    /// </summary>
    string StandIn()
    {
        var path = Path.Combine(_root, "bin", "claude");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            """
            #!/bin/sh
            session=""; previous=""
            for argument in "$@"; do [ "$previous" = "--session-id" ] && session="$argument"; previous="$argument"; done
            if [ -n "$session" ]; then
              mkdir -p "$TRANSCRIPTS/project"
              row='{"type":"assistant","cwd":"%s","timestamp":"%s","requestId":"r1","message":{"id":"m1","model":"claude-opus-5-5","usage":{"input_tokens":1000,"output_tokens":200,"cache_read_input_tokens":3000,"cache_creation_input_tokens":400}}}\n'
              printf "$row" "$(pwd -P)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" > "$TRANSCRIPTS/project/$session.jsonl"
              printf "$row" "$(pwd -P)" "$(date -u +%Y-%m-%dT%H:%M:%SZ)" >> "$TRANSCRIPTS/project/$session.jsonl"
            fi
            echo "cwd:$(pwd -P)"
            echo "args:$*"
            echo "markers:$(env | grep -c '^CLAUDE_CODE_')"
            aio done approved --reason "ran $*"
            """.Replace("$TRANSCRIPTS", Transcripts, StringComparison.Ordinal)
        );

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, (UnixFileMode)0b111_101_101);
        }

        return path;
    }

    static JsonSchema Schema() =>
        JsonSchema.FromText(
            File.ReadAllText(Path.Combine(ThisRepository.Root(), ".aio", "schemas", "done.v1.json")),
            new BuildOptions { SchemaRegistry = new() }
        );

    static async Task<string> ReadToEnd(ISession session)
    {
        using var deadline = new CancellationTokenSource(Deadline);
        var bytes = new List<byte>();

        await foreach (var chunk in session.ReadAsync(deadline.Token))
        {
            bytes.AddRange(chunk.ToArray());
        }

        return Encoding.UTF8.GetString([.. bytes]);
    }

    async Task<string> Git(string directory, params string[] arguments)
    {
        var result = await _git.Run(directory, arguments);
        result.Succeeded.ShouldBeTrue(result.StandardError);
        return result.StandardOutput;
    }
}
