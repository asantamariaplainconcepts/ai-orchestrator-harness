using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.AgentProviders;
using AiOrchestrator.Infrastructure.Environments;
using AiOrchestrator.Infrastructure.Files;
using AiOrchestrator.Infrastructure.Sessions;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// The OpenCode and Copilot adapters (#549), each run against a stand-in that answers to the real
/// CLI's arguments: a shell script that prints what it was given and ends with a bare <c>aio done</c>.
/// The real CLIs need a sign-in and spend a model call; <see cref="RealAgentRunTests"/> runs them.
/// </summary>
public sealed class OtherAgentProvidersTests : IDisposable
{
    static readonly TimeSpan Deadline = TimeSpan.FromSeconds(60);

    readonly GitCli _git = new();
    string _root = Directory.CreateTempSubdirectory("aio-other-agents-").FullName;

    string Checkout => Path.Combine(_root, "checkout");
    string RunDirectory => Path.Combine(_root, "runs", "1");
    string CopilotHome => Path.Combine(_root, "copilot");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task OpenCode_runs_the_rendered_command_with_the_model_and_the_allow_list_in_its_configuration()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Initialize();
        var environment = new WorktreeEnvironment();
        var place = await environment.PrepareAsync(new PlaceRequest(Checkout, "change/1", "HEAD"), CancellationToken.None);
        var runId = Guid.CreateVersion7();
        var request = Request("/aio:ping 7", "github-copilot/claude-haiku-4.5", place, runId);
        var provider = new OpenCodeProvider(StandIn("opencode"));

        var seen = await Run(provider.Launch(request));
        var end = await provider.EndAsync(request, CancellationToken.None);

        seen.ShouldContain($"cwd:{place.WorkingDirectory}");
        seen.ShouldContain($"args:--standalone --session ses_{runId:N} --prompt Ping issue 7, then end.");
        seen.ShouldContain("""config:{"permissions":[{"action":"shell","resource":"*","effect":"ask"},{"action":"shell","resource":"aio done","effect":"allow"},{"action":"shell","resource":"aio done *","effect":"allow"},{"action":"shell","resource":"gh pr merge","effect":"deny"},{"action":"shell","resource":"gh pr merge *","effect":"deny"}],"model":"github-copilot/claude-haiku-4.5"}""");
        End(end).ShouldBe("approved");

        // 10 fresh, 300 read and 40 written from the cache; 20 written and 5 of reasoning.
        end.Usage.ShouldBe(new AgentUsage(350, 25, 0.0028m));
        await environment.RetireAsync(place, CancellationToken.None);
    }

    [Fact]
    public async Task Copilot_runs_the_rendered_command_with_edits_and_the_allow_list_let_through_and_trusts_the_worktree_until_it_retires()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        await Initialize();
        const string Config = """
            // User settings belong in settings.json.
            {
              "trustedFolders": ["/elsewhere"],
              "firstLaunchAt": "2026-03-11T00:00:00.000Z"
            }

            """;
        Directory.CreateDirectory(CopilotHome);
        File.WriteAllText(Path.Combine(CopilotHome, "config.json"), Config);
        var environment = new WorktreeEnvironment();
        var place = await environment.PrepareAsync(new PlaceRequest(Checkout, "change/2", "HEAD"), CancellationToken.None);
        var runId = Guid.CreateVersion7();
        var request = Request("/aio:ping 7", "claude-haiku-5.5", place, runId);
        var provider = new CopilotProvider(StandIn("copilot"), CopilotHome);

        var seen = await Run(provider.Launch(request));
        var end = await provider.EndAsync(request, CancellationToken.None);

        seen.ShouldContain(
            $"args:--allow-tool=write --allow-tool=shell(aio:*) --deny-tool=shell(gh pr merge:*) --session-id={runId:D} --model=claude-haiku-5.5 --interactive=Ping issue 7, then end."
        );
        End(end).ShouldBe("approved");

        // Two calls; a checkpoint row and a half-written last line count for nothing.
        end.Usage.ShouldBe(new AgentUsage(1000 + 3000, 20 + 50, null));
        var trusted = Trusted();
        trusted.ShouldContain("/elsewhere");
        trusted.ShouldContain(Path.TrimEndingDirectorySeparator(place.WorkingDirectory));
        File.ReadAllText(Path.Combine(CopilotHome, "config.json")).ShouldStartWith("// User settings belong in settings.json.\n{");

        provider.Retire(place);
        await environment.RetireAsync(place, CancellationToken.None);

        Trusted().ShouldBe(["/elsewhere"]);
        File.Exists(Path.Combine(CopilotHome, CopilotTrust.Ledger)).ShouldBeFalse();
    }

    [Fact]
    public void A_folder_trusted_by_the_person_is_not_written_and_retiring_leaves_it()
    {
        Directory.CreateDirectory(CopilotHome);
        var real = Path.TrimEndingDirectorySeparator(Infrastructure.NativePaths.RealPath(_root) ?? _root);
        File.WriteAllText(Path.Combine(CopilotHome, "config.json"), new JsonObject { ["trustedFolders"] = new JsonArray(real) }.ToJsonString());
        var below = Directory.CreateDirectory(Path.Combine(_root, "below")).FullName;

        CopilotTrust.Ensure(CopilotHome, below).ShouldBeFalse();
        CopilotTrust.Ensure(CopilotHome, _root).ShouldBeFalse();
        CopilotTrust.Retire(CopilotHome, _root).ShouldBeFalse();

        Trusted().ShouldBe([real]);
    }

    [Theory]
    [InlineData("opencode")]
    [InlineData("copilot")]
    public void A_cli_that_is_not_on_the_path_does_not_start_and_says_how_to_install_it(string name)
    {
        IAgentProvider provider = name == "opencode" ? new OpenCodeProvider("aio-no-such-cli") : new CopilotProvider("aio-no-such-cli", CopilotHome);
        var request = new AgentRequest("/aio:ping 1", null, new RunPlace("worktree", _root), Path.Combine(RunDirectory, "done.json"), null);

        var refusal = Should.Throw<InvalidOperationException>(() => provider.Launch(request));

        refusal.Message.ShouldStartWith($"{name} cannot start: `aio-no-such-cli` is not on the PATH of the login shell. Install it ");
    }

    [Fact]
    public void Without_a_command_or_a_run_both_open_waiting_for_a_person()
    {
        var request = new AgentRequest("", null, new RunPlace("worktree", _root), Path.Combine(RunDirectory, "done.json"), null);

        string[] openCode = [.. new OpenCodeProvider(StandIn("opencode")).Launch(request).Arguments];
        string[] copilot = [.. new CopilotProvider(StandIn("copilot"), CopilotHome).Launch(request).Arguments];

        openCode[^1].ShouldBe("--standalone");
        copilot[^1].ShouldBe(CopilotProvider.EditPermission);
    }

    [Theory]
    [InlineData(AgentPermissions.AllowList, "--standalone --prompt go", "ask", true)]
    [InlineData(AgentPermissions.Auto, "--standalone --auto --prompt go", "ask", true)]
    [InlineData(AgentPermissions.Yolo, "--standalone --auto --prompt go", "allow", false)]
    public void Each_permission_mode_is_the_flags_and_configuration_opencode_starts_with(AgentPermissions permissions, string arguments, string rest, bool allowList)
    {
        WriteAllowList();
        var request = new AgentRequest("go", null, new RunPlace("worktree", Checkout), Path.Combine(RunDirectory, "done.json"), null, Permissions: permissions);

        var options = new OpenCodeProvider(StandIn("opencode")).Launch(request);

        string.Join(' ', options.Arguments.SkipWhile(argument => argument != "--standalone")).ShouldBe(arguments);
        var permissionsGiven = JsonNode.Parse(options.Environment[OpenCodeProvider.ConfigVariable])!["permissions"]!.AsArray()
            .Select(rule => $"{rule!["resource"]}:{rule["effect"]}");
        string[] expected = allowList
            ? [$"*:{rest}", "aio done:allow", "aio done *:allow", "gh pr merge:deny", "gh pr merge *:deny"]
            : [$"*:{rest}", "gh pr merge:deny", "gh pr merge *:deny"];
        permissionsGiven.ShouldBe(expected);
    }

    [Theory]
    [InlineData(AgentPermissions.AllowList, "--allow-tool=write --allow-tool=shell(aio:*) --deny-tool=shell(gh pr merge:*) --interactive=go")]
    [InlineData(AgentPermissions.Auto, "--allow-all-tools --deny-tool=shell(gh pr merge:*) --interactive=go")]
    [InlineData(AgentPermissions.Yolo, "--allow-all-tools --allow-all-paths --deny-tool=shell(gh pr merge:*) --interactive=go")]
    public void Each_permission_mode_is_the_flags_copilot_starts_with_and_the_denied_commands_stay_denied(AgentPermissions permissions, string arguments)
    {
        WriteAllowList();
        var request = new AgentRequest("go", null, new RunPlace("worktree", Checkout), Path.Combine(RunDirectory, "done.json"), null, Permissions: permissions);

        var cli = StandIn("copilot");
        var options = new CopilotProvider(cli, CopilotHome).Launch(request);

        // Behind /usr/bin/env when this process runs inside an agent session, whose markers it removes.
        string[] line = [options.FileName, .. options.Arguments];
        string.Join(' ', line[(Array.IndexOf(line, cli) + 1)..]).ShouldBe(arguments);
    }

    void WriteAllowList() =>
        Write(".claude/settings.json", """{"permissions": {"allow": ["Bash(aio done:*)", "Read(**)"], "deny": ["Bash(gh pr merge:*)"]}}""");

    [Fact]
    public void A_command_that_is_not_a_loop_command_or_has_no_file_is_sent_as_it_is()
    {
        AgentCommands.Prompt("Explain the build", _root).ShouldBe("Explain the build");
        AgentCommands.Prompt("/aio:missing 3", _root).ShouldBe("/aio:missing 3");
        AgentCommands.Prompt("/aio:../secrets 3", _root).ShouldBe("/aio:../secrets 3");
    }

    [Fact]
    public void A_command_file_without_a_place_for_its_arguments_gets_them_after_it()
    {
        var file = Path.Combine(_root, ".ai", "commands", "aio", "plain.md");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllText(file, "---\ndescription: x\n---\n\nDo the thing.\n");

        AgentCommands.Prompt("/aio:plain 12", _root).ShouldBe("Do the thing.\n\n12");
        AgentCommands.Prompt("/aio:plain", _root).ShouldBe("Do the thing.");
    }

    [Theory]
    [InlineData("Bash(git:*)", "git", true)]
    [InlineData("Bash(gh pr view:*)", "gh pr view", true)]
    [InlineData("Bash(npm test)", "npm test", false)]
    [InlineData("Read(**)", null, false)]
    public void Only_bash_entries_of_the_allow_list_are_shell_rules(string entry, string? command, bool prefix)
    {
        var rule = ShellAllowList.Rule(entry);

        if (command is null)
        {
            rule.ShouldBeNull();
        }
        else
        {
            rule.ShouldBe(new ShellRule(command, prefix));
        }
    }

    [Fact]
    public void This_repositorys_allow_list_reaches_both_clis()
    {
        var rules = ShellAllowList.Read(ThisRepository.Root());

        rules.Allowed.ShouldContain(new ShellRule(".aio/aio done", true));
        rules.Allowed.ShouldContain(new ShellRule("dotnet", true));
        rules.Denied.ShouldContain(new ShellRule("gh pr merge", true));
        rules.Allowed.Select(CopilotProvider.Allowed).ShouldContain("shell(git:*)");
    }

    [Theory]
    [InlineData("aio done", true, "shell(aio:*)")]
    [InlineData("gh pr view", true, "shell(gh pr view:*)")]
    [InlineData("git", true, "shell(git:*)")]
    [InlineData("npm test", false, "shell(npm test)")]
    public void Copilot_is_allowed_what_it_can_match_of_a_prefix(string command, bool prefix, string allowed) =>
        CopilotProvider.Allowed(new ShellRule(command, prefix)).ShouldBe(allowed);

    [Fact]
    public void Opencode_models_are_the_lines_that_name_a_provider_and_a_model() =>
        OpenCodeProvider.ListedModels("github-copilot/gpt-5.5\nopencode/big-pickle\n\nUpdate available 2.0.24\n").ShouldBe(["github-copilot/gpt-5.5", "opencode/big-pickle"]);

    AgentRequest Request(string command, string model, RunPlace place, Guid runId) =>
        new(command, model, place, Path.Combine(RunDirectory, "done.json"), null, runId, DateTimeOffset.UtcNow);

    static string? End(AgentEnd end)
    {
        end.Record.ShouldNotBeNull();
        using var record = JsonDocument.Parse(end.Record);
        return record.RootElement.GetProperty("outcome").GetString();
    }

    List<string> Trusted()
    {
        var text = File.ReadAllText(Path.Combine(CopilotHome, "config.json"));
        var json = string.Join('\n', text.Split('\n').SkipWhile(line => line.StartsWith("//", StringComparison.Ordinal)));
        return [.. JsonNode.Parse(json)!["trustedFolders"]!.AsArray().Select(node => node!.GetValue<string>())];
    }

    /// <summary>
    /// A checkout carrying this repository's <c>aio</c>, an allow list and one command, <c>ping</c>.
    /// </summary>
    async Task Initialize()
    {
        await Git(_root, "init", "--quiet", "--initial-branch=master", Checkout);
        _root = Path.GetDirectoryName((await Git(Checkout, "rev-parse", "--show-toplevel")).Trim())!;
        await Git(Checkout, "config", "user.name", "Other agent tests");
        await Git(Checkout, "config", "user.email", "other-agent-tests@localhost");
        await Git(Checkout, "config", "commit.gpgSign", "false");

        var aio = Path.Combine(Checkout, ".aio", "aio");
        Directory.CreateDirectory(Path.GetDirectoryName(aio)!);
        File.Copy(Path.Combine(ThisRepository.Root(), ".aio", "aio"), aio);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(aio, (UnixFileMode)0b111_101_101);
        }

        Write(".claude/settings.json", """{"permissions": {"allow": ["Bash(aio done:*)", "Read(**)"], "deny": ["Bash(gh pr merge:*)"]}}""");
        Write(".ai/commands/aio/ping.md", "---\ndescription: Ping.\nargument-hint: <issue>\n---\n\nPing issue $ARGUMENTS, then end.\n");
        Write(".gitignore", ".worktrees/\n");
        await Git(Checkout, "add", ".");
        await Git(Checkout, "commit", "--quiet", "-m", "base");
    }

    void Write(string relative, string text)
    {
        var path = Path.Combine(Checkout, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    /// <summary>
    /// An <c>opencode</c> or <c>copilot</c> that prints where it is, what it was given and its
    /// configuration, leaves the usage its real namesake would, and calls <c>aio done</c>.
    /// </summary>
    string StandIn(string name)
    {
        var path = Path.Combine(_root, "bin", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(
            path,
            """
            #!/bin/sh
            if [ "$1" = "session" ] && [ "$2" = "export" ]; then
              echo '{"info":{"id":"'"$3"'","cost":0.002817,"tokens":{"input":10,"output":20,"reasoning":5,"cache":{"read":300,"write":40}}},"messages":[]}'
              exit 0
            fi
            session=""
            for argument in "$@"; do case "$argument" in --session-id=*) session="${argument#--session-id=}";; esac; done
            if [ -n "$session" ]; then
              mkdir -p "$HOME_DIR/session-state/$session"
              events="$HOME_DIR/session-state/$session/events.jsonl"
              echo '{"type":"session.start","data":{}}' > "$events"
              echo '{"type":"session.usage_record","data":{"usage":{"model":"claude-haiku-5.5","inputTokens":1000,"outputTokens":20,"cost":1.0}}}' >> "$events"
              echo '{"type":"session.usage_checkpoint","data":{"totalNanoAiu":5}}' >> "$events"
              echo '{"type":"session.usage_record","data":{"usage":{"model":"claude-haiku-5.5","inputTokens":3000,"outputTokens":50,"cost":1.0}}}' >> "$events"
              printf '{"type":"session.usage_rec' >> "$events"
            fi
            echo "cwd:$(pwd -P)"
            echo "args:$*"
            echo "config:$OPENCODE_CONFIG_CONTENT"
            aio done approved --reason "ran"
            """.Replace("$HOME_DIR", CopilotHome, StringComparison.Ordinal)
        );

        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, (UnixFileMode)0b111_101_101);
        }

        return path;
    }

    static async Task<string> Run(SessionOptions options)
    {
        await using var session = new PtySession(TimeProvider.System);
        await session.StartAsync(options, CancellationToken.None);
        using var deadline = new CancellationTokenSource(Deadline);
        var bytes = new List<byte>();

        await foreach (var chunk in session.ReadAsync(deadline.Token))
        {
            bytes.AddRange(chunk.ToArray());
        }

        return Encoding.UTF8.GetString([.. bytes]).Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    async Task<string> Git(string directory, params string[] arguments)
    {
        var result = await _git.Run(directory, arguments);
        result.Succeeded.ShouldBeTrue(result.StandardError);
        return result.StandardOutput;
    }
}
