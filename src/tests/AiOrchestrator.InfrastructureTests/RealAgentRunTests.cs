using System.Text.Json;
using System.Text.Json.Nodes;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure;
using AiOrchestrator.Infrastructure.AgentProviders;
using AiOrchestrator.Infrastructure.Environments;
using AiOrchestrator.Infrastructure.Files;
using AiOrchestrator.Infrastructure.Sessions;

namespace AiOrchestrator.InfrastructureTests;

/// <summary>
/// One run of a real agent CLI, signed in as the person, against a throwaway repository whose only
/// command asks it to call <c>aio done</c>. It spends a model call, so it runs only when
/// <c>AIO_REAL_AGENTS</c> names the providers to run (<c>opencode,copilot,claude-code</c>), and
/// <c>AIO_REAL_AGENTS_EVIDENCE</c> names where the record, the recording and the cost are kept.
/// Without them it passes having done nothing, like the PTY tests on Windows. <c>claude</c> runs in a
/// configuration directory of the test's own and is signed in by <c>CLAUDE_CODE_OAUTH_TOKEN</c>.
/// <code>
/// AIO_REAL_AGENTS=opencode,copilot AIO_REAL_AGENTS_EVIDENCE=/tmp/evidence \
///   dotnet test src/tests/AiOrchestrator.InfrastructureTests --filter RealAgentRunTests
/// </code>
/// </summary>
public sealed class RealAgentRunTests : IDisposable
{
    /// <summary>A model call, a tool call and a shell command; a run that takes longer than this has stopped.</summary>
    static readonly TimeSpan Deadline = TimeSpan.FromMinutes(4);

    /// <summary>What RunExecution gives an agent between its end record and the kill.</summary>
    static readonly TimeSpan Grace = TimeSpan.FromSeconds(5);

    readonly GitCli _git = new();
    string _root = Directory.CreateTempSubdirectory("aio-real-agent-").FullName;

    string Checkout => Path.Combine(_root, "checkout");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("opencode", "github-copilot/claude-haiku-4.5")]
    [InlineData("copilot", "claude-haiku-5.5")]
    [InlineData("claude-code", "claude-haiku-4-5")]
    public async Task A_real_cli_runs_a_loop_command_in_a_worktree_and_ends_it_with_aio_done(string name, string model)
    {
        if (Evidence(name) is not { } evidence)
        {
            return;
        }

        using var record = (await Run(name, model, "/aio:ping 549", AgentPermissions.AllowList, Path.Combine(evidence, name))).Record;

        record.RootElement.GetProperty("outcome").GetString().ShouldBe("approved");
        record.RootElement.GetProperty("reason").GetString().ShouldBe("ping 549");
    }

    /// <summary>
    /// #578. In <c>yolo</c> the agent runs <c>ls /tmp</c>, which the throwaway repository's allow list
    /// (only <c>aio done</c>) does not name and which would stop an <c>allow-list</c> run at a prompt.
    /// The listing it leaves in the worktree is the proof that the command ran unasked.
    /// </summary>
    [Theory]
    [InlineData("opencode", "github-copilot/claude-haiku-4.5")]
    [InlineData("copilot", "claude-haiku-5.5")]
    [InlineData("claude-code", "claude-haiku-4-5")]
    public async Task A_real_cli_in_yolo_runs_a_command_outside_the_allow_list_and_ends_with_aio_done(string name, string model)
    {
        if (Evidence(name) is not { } evidence)
        {
            return;
        }

        var run = await Run(name, model, "/aio:list 578", AgentPermissions.Yolo, Path.Combine(evidence, $"{name}-yolo"));
        using var record = run.Record;
        var listed = run.Listed;

        record.RootElement.GetProperty("outcome").GetString().ShouldBe("approved");
        record.RootElement.GetProperty("reason").GetString().ShouldBe("listed 578");
        listed.ShouldBeTrue("ls /tmp wrote its listing into the worktree");
    }

    /// <summary>Where the evidence of <paramref name="name"/>'s run goes, or null when it is not to run.</summary>
    static string? Evidence(string name)
    {
        var chosen = (Environment.GetEnvironmentVariable("AIO_REAL_AGENTS") ?? string.Empty).Split(',', StringSplitOptions.TrimEntries);
        var evidence = Environment.GetEnvironmentVariable("AIO_REAL_AGENTS_EVIDENCE");
        return OperatingSystem.IsWindows() || !chosen.Contains(name) || evidence is not { Length: > 0 } ? null : evidence;
    }

    /// <summary>The variable that signs a <c>claude</c> in when its configuration is not the person's.</summary>
    const string ClaudeToken = "CLAUDE_CODE_OAUTH_TOKEN";

    /// <summary>
    /// #655. A configuration directory of the test's own for <c>claude</c>, trusting only the run's
    /// worktree and its checkout, so a run never writes the person's <c>~/.claude.json</c>: 51 trust
    /// entries for folders long gone were counted there on 11 October. A configuration that is not
    /// the person's has no sign-in either, because on macOS the keychain item is named after the
    /// directory, so the run is signed in by <see cref="ClaudeToken"/>, which <c>claude setup-token</c> prints.
    /// </summary>
    string ClaudeConfig(string worktree)
    {
        if (Environment.GetEnvironmentVariable(ClaudeToken) is not { Length: > 0 })
        {
            throw new InvalidOperationException(
                $"A real claude-code run uses a configuration directory of its own, which has no sign-in. Set {ClaudeToken} to what `claude setup-token` prints."
            );
        }

        var directory = Directory.CreateDirectory(Path.Combine(_root, "claude-config")).FullName;
        var trusted = new JsonObject { ["hasTrustDialogAccepted"] = true };
        File.WriteAllText(
            Path.Combine(directory, ".claude.json"),
            new JsonObject
            {
                ["hasCompletedOnboarding"] = true,
                ["projects"] = new JsonObject
                {
                    [NativePaths.RealPath(Checkout) ?? Checkout] = trusted.DeepClone(),
                    [NativePaths.RealPath(worktree) ?? worktree] = trusted.DeepClone(),
                },
            }.ToJsonString()
        );
        return directory;
    }

    /// <summary>
    /// <paramref name="launch"/> pointed at <paramref name="config"/>, with the token kept: the launch
    /// removes every <c>CLAUDE_CODE_</c> variable of this process, the token among them.
    /// </summary>
    static SessionOptions InOwnConfig(SessionOptions launch, string config)
    {
        var arguments = launch.Arguments.ToList();
        var unset = arguments.IndexOf(ClaudeToken);
        if (unset > 0 && arguments[unset - 1] == "-u")
        {
            arguments.RemoveRange(unset - 1, 2);
        }

        var environment = new Dictionary<string, string>(launch.Environment, StringComparer.Ordinal)
        {
            ["CLAUDE_CONFIG_DIR"] = config,
            [ClaudeToken] = Environment.GetEnvironmentVariable(ClaudeToken)!,
        };
        return launch with { Arguments = arguments, Environment = environment };
    }

    /// <summary>One run of the real CLI in a fresh worktree; its end record, and whether it left <c>tmp-listing.txt</c>.</summary>
    async Task<(JsonDocument Record, bool Listed)> Run(string name, string model, string command, AgentPermissions permissions, string evidence)
    {
        await Initialize();
        var environment = new WorktreeEnvironment();
        var place = await environment.PrepareAsync(new PlaceRequest(Checkout, $"change/{name}", "HEAD"), CancellationToken.None);
        var claude = name == "claude-code" ? ClaudeConfig(place.WorkingDirectory) : null;
        IAgentProvider provider = name switch
        {
            "opencode" => new OpenCodeProvider(),
            "copilot" => new CopilotProvider(),
            _ => new ClaudeCodeProvider(configFile: Path.Combine(claude!, ".claude.json"), transcripts: Path.Combine(claude!, "projects")),
        };
        var directory = Directory.CreateDirectory(evidence).FullName;
        var request = new AgentRequest(
            command,
            model,
            place,
            Path.Combine(directory, "done.json"),
            Path.Combine(directory, "session.cast"),
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow,
            permissions
        );
        File.Delete(request.EndRecordPath);

        var launch = provider.Launch(request);
        if (claude is not null)
        {
            launch = InOwnConfig(launch, claude);
        }

        await using (var session = new PtySession(TimeProvider.System))
        {
            await session.StartAsync(launch, CancellationToken.None);
            await ReadUntilEnded(session, request.EndRecordPath);
        }

        var end = await provider.EndAsync(request, CancellationToken.None);
        var listing = Path.Combine(place.WorkingDirectory, "tmp-listing.txt");
        var listed = File.Exists(listing);
        if (listed)
        {
            File.Copy(listing, Path.Combine(directory, "tmp-listing.txt"), overwrite: true);
        }

        provider.Retire(place);
        await environment.RetireAsync(place, CancellationToken.None);

        await File.WriteAllTextAsync(
            Path.Combine(directory, "summary.json"),
            JsonSerializer.Serialize(new
            {
                name,
                model,
                permissions = permissions.ToString(),
                launch = launch.Arguments,
                request.RunId,
                place.WorkingDirectory,
                end.Record,
                end.Usage,
                listed,
                Version = (await provider.ProbeAsync(CancellationToken.None)).Version,
            })
        );
        end.Record.ShouldNotBeNull();
        Cast.Read(request.RecordingPath!).Output.ShouldNotBeEmpty();
        return (JsonDocument.Parse(end.Record), listed);
    }

    /// <summary>As RunExecution does: the TUIs stay open after <c>aio done</c>, so the record ends the run.</summary>
    static async Task ReadUntilEnded(ISession session, string recordPath)
    {
        using var deadline = new CancellationTokenSource(Deadline);
        var reading = Task.Run(async () =>
        {
            await foreach (var _ in session.ReadAsync(deadline.Token))
            {
            }
        });

        while (!File.Exists(recordPath) && !reading.IsCompleted && !deadline.IsCancellationRequested)
        {
            await Task.Delay(250);
        }

        await Task.WhenAny(reading, Task.Delay(Grace));
        await session.KillAsync(CancellationToken.None);
        await reading.WaitAsync(TimeSpan.FromSeconds(10));
    }

    async Task Initialize()
    {
        await Git(_root, "init", "--quiet", "--initial-branch=master", Checkout);
        _root = Path.GetDirectoryName((await Git(Checkout, "rev-parse", "--show-toplevel")).Trim())!;
        await Git(Checkout, "config", "user.name", "Real agent tests");
        await Git(Checkout, "config", "user.email", "real-agent-tests@localhost");
        await Git(Checkout, "config", "commit.gpgSign", "false");

        var aio = Path.Combine(Checkout, ".aio", "aio");
        Directory.CreateDirectory(Path.GetDirectoryName(aio)!);
        File.Copy(Path.Combine(ThisRepository.Root(), ".aio", "aio"), aio);
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(aio, (UnixFileMode)0b111_101_101);
        }

        Write(".claude/settings.json", """{"permissions": {"allow": ["Bash(aio done:*)", "Bash(.aio/aio done:*)"]}}""");
        Write(".claude/commands/aio/ping.md", Command);
        Write(".ai/commands/aio/ping.md", Command);
        Write(".claude/commands/aio/list.md", ListCommand);
        Write(".ai/commands/aio/list.md", ListCommand);
        Write(".gitignore", ".worktrees/\ntmp-listing.txt\n");
        await Git(Checkout, "add", ".");
        await Git(Checkout, "commit", "--quiet", "-m", "base");
    }

    const string Command = """
        ---
        description: End the run at once.
        argument-hint: <issue>
        ---

        This is a test of the harness. Do not read or change any file. Run exactly this one shell
        command, and nothing else:

        aio done approved --reason "ping $ARGUMENTS"

        Then stop.
        """;

    /// <summary>#578. A command the allow list does not name, then the end.</summary>
    const string ListCommand = """
        ---
        description: List /tmp and end the run.
        argument-hint: <issue>
        ---

        This is a test of the harness. Do not read or change any other file. Run exactly these two
        shell commands, one after the other, and nothing else:

        ls /tmp > tmp-listing.txt

        aio done approved --reason "listed $ARGUMENTS"

        Then stop.
        """;

    void Write(string relative, string text)
    {
        var path = Path.Combine(Checkout, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, text);
    }

    async Task<string> Git(string directory, params string[] arguments)
    {
        var result = await _git.Run(directory, arguments);
        result.Succeeded.ShouldBeTrue(result.StandardError);
        return result.StandardOutput;
    }
}
