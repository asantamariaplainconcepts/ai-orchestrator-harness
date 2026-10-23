using System.Text.Json;
using System.Text.Json.Nodes;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// <see cref="IAgentProvider"/> for OpenCode: its TUI with the prompt given by <c>--prompt</c>, which
/// submits it at once and stays open. A bare argument would be read as a directory to open. Verified
/// against 2.0.23, whose interface is not 1.x's: the TUI has no <c>--model</c>, and the tool that runs
/// commands is <c>shell</c>.
/// <para>
/// <b>Model and permissions</b> go in <c>OPENCODE_CONFIG_CONTENT</c>, which OpenCode reads as one more
/// configuration file. It is read by the server, and by default the TUI talks to a background service
/// started with somebody else's environment, so the run gets a private one with <c>--standalone</c>.
/// Measured: the same <c>model</c> was ignored without it and used with it. OpenCode allows every tool
/// by default; the run is narrowed to Claude Code's: edits go through, <c>shell</c> asks, and the
/// repository's allow list of <c>.claude/settings.json</c> is allowed (<see cref="ShellAllowList"/>).
/// Later rules win, so the list comes after the rule that asks. That is <see cref="AgentPermissions.AllowList"/>;
/// <c>auto</c> adds <c>--auto</c>, and <c>yolo</c> allows every command and adds <c>--auto</c> (#578).
/// </para>
/// <para>
/// <b>Cost.</b> <c>--session</c> creates a session under an id it is given, so a run's session is
/// <c>ses_&lt;run id&gt;</c>, and <c>opencode session export</c> answers its tokens and its cost in
/// dollars, which OpenCode prices itself.
/// </para>
/// </summary>
/// <param name="cli">The executable; <c>opencode</c> on the PATH unless a test names another.</param>
sealed class OpenCodeProvider(string cli = OpenCodeProvider.Cli) : IAgentProvider
{
    public const string ProviderName = "opencode";

    const string Cli = "opencode";

    /// <summary>The variable OpenCode reads one more configuration from.</summary>
    public const string ConfigVariable = "OPENCODE_CONFIG_CONTENT";

    /// <summary><c>opencode models</c> and <c>opencode session export</c> answer in about three seconds.</summary>
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>How long a listed set of models is shown before the CLI is asked again.</summary>
    static readonly TimeSpan ModelsFresh = TimeSpan.FromMinutes(5);

    /// <summary>Replaced whole, so a reader never sees one probe's models with another's time.</summary>
    volatile Listed _models = new([], DateTimeOffset.MinValue);

    sealed record Listed(IReadOnlyList<string> Models, DateTimeOffset At);

    public string Name => ProviderName;

    /// <summary>What <c>opencode models</c> printed when <see cref="ProbeAsync"/> last asked, one <c>provider/model</c> each.</summary>
    public IReadOnlyList<string> Models => _models.Models;

    public SessionOptions Launch(AgentRequest request)
    {
        List<string> arguments = ["--standalone"];

        // "Auto-approve permissions that are not explicitly denied" (2.0.23's --help): what would ask
        // runs, and a deny rule of the configuration still holds.
        if (request.Permissions is not AgentPermissions.AllowList)
        {
            arguments.Add("--auto");
        }

        if (request.RunId != Guid.Empty)
        {
            arguments.AddRange(["--session", Session(request.RunId)]);
        }

        if (request.Command is { Length: > 0 } command)
        {
            arguments.AddRange(["--prompt", AgentCommands.Prompt(command, request.Place.WorkingDirectory)]);
        }

        return AgentLaunch.Options(
            ProviderName,
            cli,
            "Install it from https://opencode.ai, then run `opencode auth login`.",
            arguments,
            request,
            new Dictionary<string, string>
            {
                [ConfigVariable] = Config(request.Model, ShellAllowList.Read(request.Place.WorkingDirectory), request.Permissions),
            }
        );
    }

    public async Task<AgentEnd> EndAsync(AgentRequest request, CancellationToken ct) =>
        new(await AgentLaunch.EndRecord(request, ct), request.RunId == Guid.Empty ? null : await Usage(request.RunId, ct));

    /// <summary>Where it is and which version and, when it is there, the models it lists now.</summary>
    public async Task<CliProbe> ProbeAsync(CancellationToken ct)
    {
        var path = AgentLaunch.Locate(cli);
        var probe = await CliProbing.Probe(cli, path, ct);

        if (path is not null && DateTimeOffset.UtcNow - _models.At > ModelsFresh)
        {
            // An answer with no model in it is not kept: the next probe asks again.
            if (await Ask(path, ["models"], ct) is { } listed && ListedModels(listed) is { Count: > 0 } models)
            {
                _models = new Listed(models, DateTimeOffset.UtcNow);
            }
        }

        return probe;
    }

    /// <summary>OpenCode's ids are <c>ses_</c> and its own suffix; the run's id in hex takes its place.</summary>
    internal static string Session(Guid runId) => $"ses_{runId:N}";

    /// <summary>
    /// The configuration a run is started with: its model, and Claude Code's permissions in OpenCode's
    /// words. In <see cref="AgentPermissions.Yolo"/> every command is allowed and the allow list says
    /// nothing more; the denied commands come last in every mode, so they win.
    /// </summary>
    internal static string Config(string? model, ShellAllowList rules, AgentPermissions mode = AgentPermissions.AllowList)
    {
        var yolo = mode is AgentPermissions.Yolo;
        JsonArray permissions = [Permission("*", yolo ? "allow" : "ask")];

        foreach (var rule in yolo ? [] : rules.Allowed)
        {
            foreach (var resource in Resources(rule))
            {
                permissions.Add(Permission(resource, "allow"));
            }
        }

        foreach (var rule in rules.Denied)
        {
            foreach (var resource in Resources(rule))
            {
                permissions.Add(Permission(resource, "deny"));
            }
        }

        JsonObject config = new() { ["permissions"] = permissions };
        if (model is { Length: > 0 })
        {
            config["model"] = model;
        }

        return config.ToJsonString();
    }

    static JsonObject Permission(string resource, string effect) => new() { ["action"] = "shell", ["resource"] = resource, ["effect"] = effect };

    /// <summary>A prefix is the command alone and the command with anything after it: <c>git</c> and <c>git *</c>.</summary>
    static IEnumerable<string> Resources(ShellRule rule) => rule.Prefix ? [rule.Command, rule.Command + " *"] : [rule.Command];

    internal static IReadOnlyList<string> ListedModels(string printed) =>
        [.. printed.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(line => line.Contains('/', StringComparison.Ordinal) && !line.Contains(' ', StringComparison.Ordinal))];

    /// <summary>
    /// The session's tokens and cost as <c>opencode session export</c> answers them; null when the
    /// session is not there. Input counts the cache reads and writes, and output the reasoning.
    /// </summary>
    async Task<AgentUsage?> Usage(Guid runId, CancellationToken ct)
    {
        if (AgentLaunch.Locate(cli) is not { } path || await Ask(path, ["session", "export", Session(runId)], ct) is not { } exported)
        {
            return null;
        }

        return Exported(exported);
    }

    internal static AgentUsage? Exported(string exported)
    {
        try
        {
            using var session = JsonDocument.Parse(exported);
            if (!session.RootElement.TryGetProperty("info", out var info) || !info.TryGetProperty("tokens", out var tokens))
            {
                return null;
            }

            var cache = tokens.TryGetProperty("cache", out var cached) ? cached : default;
            var input = Count(tokens, "input") + Count(cache, "read") + Count(cache, "write");
            var output = Count(tokens, "output") + Count(tokens, "reasoning");
            decimal? cost = info.TryGetProperty("cost", out var dollars) && dollars.TryGetDecimal(out var value)
                ? Math.Round(value, 4, MidpointRounding.AwayFromZero)
                : null;

            return new AgentUsage(input, output, cost);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static long Count(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetInt64(out var count) ? count : 0;

    /// <summary>What the CLI printed, or null when it failed. Its standard input is an empty pipe: with none, <c>opencode models</c> prints nothing.</summary>
    static async Task<string?> Ask(string path, string[] arguments, CancellationToken ct)
    {
        var answer = await ProcessRun.Capture(
            CliWrap.Cli.Wrap(path).WithArguments(arguments).WithEnvironmentVariables(LoginShellEnvironment.PathOverride()),
            Timeout,
            ct
        );

        return answer.Succeeded ? answer.StandardOutput : null;
    }
}
