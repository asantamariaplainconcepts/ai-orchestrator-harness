using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// <see cref="IAgentProvider"/> for Claude Code: the interactive <c>claude</c> with its model and the
/// command as a bare positional prompt, the spelling the first tree's <c>AgentCatalog</c> verified on 2.1.234.
/// It starts in the <see cref="PermissionMode"/> of the request's permissions, so an unattended run is
/// not stopped by the first file it edits.
/// The TUI stays open after the agent has called <c>aio done</c>, so ending the session is the
/// caller's.
/// <para>
/// A run's id becomes Claude Code's session id, so its transcript is
/// <c>~/.claude/projects/&lt;cwd&gt;/&lt;run id&gt;.jsonl</c> and what the run cost is read from that one
/// file by <see cref="ClaudeUsageReader"/>. Trust is read, never written (#608): the engine only
/// launches in a folder <see cref="Trust"/> says Claude Code already trusts (<see cref="ClaudeTrust"/>).
/// </para>
/// </summary>
/// <param name="cli">The executable; <c>claude</c> on the PATH unless a test names another.</param>
/// <param name="configFile">Claude Code's <c>.claude.json</c>; the user's unless a test names another.</param>
/// <param name="transcripts">Where Claude Code keeps its transcripts; the user's unless a test names another.</param>
sealed class ClaudeCodeProvider(
    string cli = ClaudeCodeProvider.Cli,
    string? configFile = null,
    string? transcripts = null
) : IAgentProvider
{
    public const string ProviderName = "claude-code";

    const string Cli = "claude";

    /// <summary>
    /// <c>--permission-mode</c> for each of the instance's modes (#578), as <c>claude --help</c> lists
    /// them on 2.1.288. <c>default</c> would stop a run at its first edit. Claude Code reads the deny
    /// rules of <c>.claude/settings.json</c> itself, and its PreToolUse hook still refuses in
    /// <c>bypassPermissions</c> (#525), so no mode needs them passed.
    /// </summary>
    public static string PermissionMode(AgentPermissions permissions) =>
        permissions switch
        {
            AgentPermissions.Auto => "auto",
            AgentPermissions.Yolo => "bypassPermissions",
            _ => "acceptEdits",
        };

    /// <summary>
    /// The answer to the menu Claude Code shows before it starts in a wider mode, given as settings of
    /// the one session (<c>--settings</c>), so the person's own files are not written. Without it, the
    /// first real run in <c>bypassPermissions</c> sat at "Yes, I accept" until its four minutes ran out.
    /// </summary>
    static string? OptIn(AgentPermissions permissions) =>
        permissions switch
        {
            AgentPermissions.Auto => """{"skipAutoPermissionPrompt":true}""",
            AgentPermissions.Yolo => """{"skipDangerousModePermissionPrompt":true}""",
            _ => null,
        };

    public string Name => ProviderName;

    /// <summary>
    /// A static list: <c>claude</c> has no command that lists its models, and <c>--model</c> takes
    /// any of these ids as they are.
    /// </summary>
    public IReadOnlyList<string> Models { get; } = ["claude-opus-5-5", "claude-fable-5-1", "claude-sonnet-5-5", "claude-haiku-4-5"];

    public SessionOptions Launch(AgentRequest request)
    {
        // In allow-list, edits go through without a prompt and Bash asks .claude/settings.json, whose
        // allow list is the loop's commands (#544). A command outside it still prompts, nobody answers,
        // and the run ends waiting for a person (#706); the PreToolUse hook refuses what must never run in every mode.
        List<string> arguments = ["--permission-mode", PermissionMode(request.Permissions)];

        if (OptIn(request.Permissions) is { } accepted)
        {
            arguments.AddRange(["--settings", accepted]);
        }

        if (request.RunId != Guid.Empty)
        {
            arguments.AddRange(["--session-id", request.RunId.ToString("D")]);
        }

        if (request.Model is { Length: > 0 } model)
        {
            arguments.AddRange(["--model", model]);
        }

        // Last and bare: an argument after the prompt would be read as part of it. None at all opens
        // the agent waiting for a person, which is what a manual session without a command is.
        if (request.Command is { Length: > 0 } command)
        {
            arguments.Add(command);
        }

        return AgentLaunch.Options(
            ProviderName,
            cli,
            "Install it with `npm install -g @anthropic-ai/claude-code`.",
            arguments,
            request
        );
    }

    /// <summary>
    /// The trust a launch wrote for the place before #608, taken back before the place goes (#546),
    /// so a worktree from those days leaves no entry behind.
    /// </summary>
    public void Retire(RunPlace place) => ClaudeTrust.Retire(configFile ?? ClaudeTrust.DefaultFile, place.WorkingDirectory);

    /// <summary>The end record as <c>aio done</c> wrote it, and what the run's session cost.</summary>
    public async Task<AgentEnd> EndAsync(AgentRequest request, CancellationToken ct) =>
        new(await AgentLaunch.EndRecord(request, ct), await Usage(request, ct));

    /// <summary>
    /// Whether <c>claude</c> opens on its trust question in <paramref name="folder"/> (#606). The
    /// answer is the person's to give, once, in the CLI itself; <c>--permission-mode</c> does not skip it.
    /// </summary>
    public AgentTrust Trust(string folder) =>
        ClaudeTrust.IsTrusted(configFile ?? ClaudeTrust.DefaultFile, folder)
            ? new(true, $"Claude Code trusts {folder}.", null)
            : new(
                false,
                $"Claude Code does not trust {folder}, so a run there would wait on its trust question. Run claude there once and accept it.",
                $"cd {Quoted(folder)} && claude"
            );

    /// <summary>A permission prompt or a login the CLI asks for, as <see cref="ClaudeScreen"/> reads its terminal (#706).</summary>
    public PersonWait? WaitsForPerson(string screen) => ClaudeScreen.Waits(screen);

    static string Quoted(string path) => $"'{path.Replace("'", "'\\''", StringComparison.Ordinal)}'";

    /// <summary>
    /// The executable <see cref="Launch"/> would spawn, so a missing one is missing in both, and
    /// whether it is signed in (#680), asked at the same time: <c>auth status</c> took 0.9 s here.
    /// </summary>
    public async Task<CliProbe> ProbeAsync(CancellationToken ct)
    {
        var path = AgentLaunch.Locate(cli);
        if (path is null)
        {
            return await CliProbing.Probe(cli, path, ct);
        }

        var probe = CliProbing.Probe(cli, path, ct);
        var status = CliProbing.Answer(path, ["auth", "status"], ct);
        return (await probe) with { Problem = SignedOut(await status) };
    }

    /// <summary>
    /// Why <c>claude auth status</c> says nobody is signed in, or null. It prints JSON with
    /// <c>loggedIn</c> on 2.1.x; anything else, such as an older CLI with no such command, tells
    /// nothing, and a CLI that cannot be asked is not reported as signed out.
    /// </summary>
    internal static string? SignedOut(string? printed)
    {
        try
        {
            using var status = JsonDocument.Parse(printed ?? string.Empty);
            return status.RootElement.ValueKind == JsonValueKind.Object
                && status.RootElement.TryGetProperty("loggedIn", out var signedIn) && signedIn.ValueKind == JsonValueKind.False
                ? "Claude Code is not signed in."
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Null for a request with no run id: without the session's name, the transcript of this run
    /// cannot be told apart from another session's in the same directory.
    /// </summary>
    async Task<AgentUsage?> Usage(AgentRequest request, CancellationToken ct)
    {
        if (request.RunId == Guid.Empty)
        {
            return null;
        }

        var reader = new ClaudeUsageReader(transcripts ?? DefaultTranscripts());
        var query = new UsageQuery(
            ProviderName,
            request.Place.WorkingDirectory,
            request.StartedAt,
            DateTimeOffset.UtcNow,
            request.RunId
        );

        return await reader.Read(query, ct) is { } measured ? Ported(measured) : null;
    }

    static AgentUsage Ported(MeasuredUsage measured) =>
        new(
            measured.InputTokens + measured.CacheReadTokens + measured.CacheWriteTokens,
            measured.OutputTokens,
            measured.CostUsd
        );

    static string DefaultTranscripts() =>
        Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") is { Length: > 0 } directory
            ? Path.Combine(directory, "projects")
            : ClaudeTranscripts.DefaultRoot;
}
