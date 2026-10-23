using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// <see cref="IAgentProvider"/> for GitHub Copilot CLI: the interactive <c>copilot</c>, with the prompt
/// given by <c>--interactive</c> so it runs at once and the TUI stays open, the way the claude-code adapter leaves
/// <c>claude</c>. Verified against 1.0.95. <c>-p</c> would exit when done, but it refuses to start
/// without <c>--allow-all-tools</c>, which is more than the loop lets an agent do.
/// <para>
/// <b>Permissions.</b> <c>--allow-tool=write</c> lets every file edit through, as <c>acceptEdits</c>
/// does, and the repository's allow list of <c>.claude/settings.json</c> becomes
/// <c>--allow-tool=shell(…)</c> and <c>--deny-tool=shell(…)</c> (<see cref="ShellAllowList"/>). Any other
/// command asks, nobody answers, and the run is marked stale. That is <c>allow-list</c>; <c>auto</c> and
/// <c>yolo</c> widen it (<see cref="Permissions"/>, #578). The folder-trust menu is answered before
/// the CLI starts (<see cref="CopilotTrust"/>).
/// </para>
/// <para>
/// <b>Cost.</b> A run's id becomes Copilot's session id (<c>--session-id</c> sets the UUID of a new
/// session), so its usage is <c>session.usage_record</c> rows of
/// <c>~/.copilot/session-state/&lt;run id&gt;/events.jsonl</c>. Copilot bills in premium requests and AI
/// credits, not dollars, so the tokens are kept and the cost is left empty.
/// </para>
/// </summary>
/// <param name="cli">The executable; <c>copilot</c> on the PATH unless a test names another.</param>
/// <param name="home">Copilot's configuration and state; the user's unless a test names another.</param>
sealed class CopilotProvider(string cli = CopilotProvider.Cli, string? home = null) : IAgentProvider
{
    public const string ProviderName = "copilot";

    const string Cli = "copilot";

    /// <summary>The permission that lets an edit through without asking, as Claude Code's <c>acceptEdits</c> does.</summary>
    public const string EditPermission = "--allow-tool=write";

    string Home => home ?? CopilotTrust.DefaultHome;

    public string Name => ProviderName;

    /// <summary>
    /// A static list: <c>copilot</c> has no command that lists its models, and <c>--model</c> takes
    /// these ids, the ones OpenCode's <c>github-copilot</c> provider lists for the same account.
    /// </summary>
    public IReadOnlyList<string> Models { get; } = ["claude-opus-5.5", "gpt-5.5", "claude-sonnet-5.5", "claude-haiku-5.5"];

    public SessionOptions Launch(AgentRequest request)
    {
        var rules = ShellAllowList.Read(request.Place.WorkingDirectory);
        List<string> arguments = [.. Permissions(rules, request.Permissions)];

        if (request.RunId != Guid.Empty)
        {
            arguments.Add($"--session-id={request.RunId:D}");
        }

        if (request.Model is { Length: > 0 } model)
        {
            arguments.Add($"--model={model}");
        }

        if (request.Command is { Length: > 0 } command)
        {
            // Joined to its flag, so a prompt that starts with a dash is not read as another option.
            arguments.Add($"--interactive={AgentCommands.Prompt(command, request.Place.WorkingDirectory)}");
        }

        var options = AgentLaunch.Options(
            ProviderName,
            cli,
            "Install it with `npm install -g @github/copilot`, then run `copilot login`.",
            arguments,
            request
        );

        CopilotTrust.Ensure(Home, request.Place.WorkingDirectory);

        return options;
    }

    /// <summary>The trust <see cref="Launch"/> wrote for the place, taken back before the place goes.</summary>
    public void Retire(RunPlace place) => CopilotTrust.Retire(Home, place.WorkingDirectory);

    public async Task<AgentEnd> EndAsync(AgentRequest request, CancellationToken ct) =>
        new(await AgentLaunch.EndRecord(request, ct), request.RunId == Guid.Empty ? null : await Usage(request.RunId, ct));

    public Task<CliProbe> ProbeAsync(CancellationToken ct) => CliProbing.Probe(cli, AgentLaunch.Locate(cli), ct);

    /// <summary>
    /// The flags of each mode (#578), as <c>copilot --help</c> spells them on 1.0.95. The denied
    /// commands come in every mode, because <c>--deny-tool</c> wins over <c>--allow-all-tools</c>.
    /// <c>yolo</c> is not <c>--yolo</c>, which also allows every URL.
    /// </summary>
    internal static IEnumerable<string> Permissions(ShellAllowList rules, AgentPermissions mode)
    {
        IEnumerable<string> allowed = mode switch
        {
            AgentPermissions.Auto => [AllTools],
            AgentPermissions.Yolo => [AllTools, AllPaths],
            _ => [EditPermission, .. rules.Allowed.Select(rule => $"--allow-tool={Allowed(rule)}").Distinct(StringComparer.Ordinal)],
        };

        return allowed.Concat(rules.Denied.Select(rule => $"--deny-tool={Shell(rule)}"));
    }

    const string AllTools = "--allow-all-tools";

    const string AllPaths = "--allow-all-paths";

    /// <summary><c>shell(git:*)</c> for every <c>git</c> command, <c>shell(npm test)</c> for that one.</summary>
    internal static string Shell(ShellRule rule) => rule.Prefix ? $"shell({rule.Command}:*)" : $"shell({rule.Command})";

    /// <summary>The commands Copilot tells apart by their first subcommand: <c>git push</c>, <c>gh pr create</c>.</summary>
    static readonly string[] Subcommanded = ["git", "gh"];

    /// <summary>
    /// What Copilot can match of an allowed prefix. It names any other command by its first word, so
    /// <c>shell(aio done:*)</c> did not let <c>aio done approved</c> through and the run sat at
    /// "Do you want to run this command?" for four minutes; <c>aio</c> is allowed instead, which is the
    /// same thing for a script whose one subcommand is <c>done</c>. A denied prefix is never widened.
    /// </summary>
    internal static string Allowed(ShellRule rule)
    {
        var words = rule.Command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return rule.Prefix && words.Length > 1 && !Subcommanded.Contains(words[0], StringComparer.Ordinal)
            ? $"shell({words[0]}:*)"
            : Shell(rule);
    }

    /// <summary>
    /// The sum of the session's usage records, one per model call; null when there are none. Input
    /// is Copilot's <c>inputTokens</c>, which already counts the cache reads and writes.
    /// </summary>
    async Task<AgentUsage?> Usage(Guid runId, CancellationToken ct)
    {
        var events = Path.Combine(Home, "session-state", runId.ToString("D"), "events.jsonl");
        if (!File.Exists(events))
        {
            return null;
        }

        long input = 0, output = 0;
        var calls = 0;

        foreach (var line in await File.ReadAllLinesAsync(events, ct))
        {
            if (Record(line) is { } usage)
            {
                input += usage.Input;
                output += usage.Output;
                calls++;
            }
        }

        return calls == 0 ? null : new AgentUsage(input, output, null);
    }

    internal static (long Input, long Output)? Record(string line)
    {
        try
        {
            using var row = JsonDocument.Parse(line);
            var root = row.RootElement;

            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("type", out var type) || type.GetString() != "session.usage_record"
                || !root.TryGetProperty("data", out var data) || !data.TryGetProperty("usage", out var usage))
            {
                return null;
            }

            return (Count(usage, "inputTokens"), Count(usage, "outputTokens"));
        }
        catch (JsonException)
        {
            // The last line of a session that was killed mid-write.
            return null;
        }
    }

    static long Count(JsonElement usage, string name) =>
        usage.TryGetProperty(name, out var value) && value.TryGetInt64(out var count) ? count : 0;
}
