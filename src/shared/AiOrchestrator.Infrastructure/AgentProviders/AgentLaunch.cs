using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Infrastructure.AgentProviders;

/// <summary>
/// What every agent CLI is spawned with, whichever vendor it is: the PATH it sees, the variables of
/// this process it must not see, where <c>aio done</c> writes, where <c>aio issue</c> reaches the
/// control plane, and the committer that marks a run's commits (<see cref="CommitMark"/>). Each
/// <see cref="IAgentProvider"/> adds only its own spelling of the command and the model.
/// </summary>
static class AgentLaunch
{
    /// <summary>The variable <c>.aio/aio</c> reads for where the end record goes.</summary>
    public const string EndRecordVariable = "AIO_DONE_FILE";

    /// <summary>The variable <c>aio issue</c> reads for the control plane's address (#634).</summary>
    public const string ApiVariable = "AIO_API";

    /// <summary>The variable <c>aio issue</c> reads for the PBI's repository (#634).</summary>
    public const string RepositoryVariable = "AIO_REPOSITORY";

    /// <summary>The terminal size a run starts at, the same as the first tree's runner.</summary>
    const int Columns = 120;
    const int Rows = 30;

    /// <summary>
    /// <c>/usr/bin/env</c>, used only to remove variables. Porta.Pty merges the environment it is
    /// given over this process's own, so a variable left out of the dictionary is inherited anyway;
    /// <c>env -u</c> is what removes it, and it needs no shell, so nothing in the command is
    /// interpreted.
    /// </summary>
    const string EnvExecutable = "/usr/bin/env";

    /// <summary>
    /// The session options for <paramref name="cli"/> with <paramref name="arguments"/>, or a refusal
    /// that names the CLI and how to install it when it is not on the PATH. <paramref name="variables"/>
    /// are the CLI's own settings, such as OpenCode's configuration, given on top of the ones every agent gets.
    /// </summary>
    public static SessionOptions Options(
        string provider,
        string cli,
        string installHint,
        IReadOnlyList<string> arguments,
        AgentRequest request,
        IReadOnlyDictionary<string, string>? variables = null
    )
    {
        var inherited = Inherited();
        var executable =
            Resolve(cli, inherited)
            ?? throw new InvalidOperationException(
                $"{provider} cannot start: `{cli}` is not on the PATH of the login shell. {installHint}"
            );

        var directory = request.Place.WorkingDirectory;
        var environment = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // The checkout's own scripts first, so a bare `aio done` is this checkout's `aio`: in
            // .aio/, or where a harness before 37 has it (#729, #779).
            ["PATH"] = string.Join(Path.PathSeparator, [.. HarnessScripts.Commands(directory), inherited]),
            [EndRecordVariable] = request.EndRecordPath,
        };

        // `aio issue` talks to the PBI through this control plane, not through the vendor's CLI.
        if (request.Api is not null)
        {
            environment[ApiVariable] = request.Api.ToString().TrimEnd('/');
        }

        if (request.Repository is not null)
        {
            environment[RepositoryVariable] = request.Repository;
        }

        // #741. A run's commits say whose they are; a folder session, which has no run, commits as the person.
        if (request.RunId != Guid.Empty)
        {
            foreach (var (name, value) in CommitMark.Run(request.RunId))
            {
                environment[name] = value;
            }
        }

        foreach (var (name, value) in variables ?? new Dictionary<string, string>())
        {
            environment[name] = value;
        }

        var unset = Unset();

        return new SessionOptions(
            unset.Count == 0 ? executable : EnvExecutable,
            unset.Count == 0 ? arguments : [.. unset.SelectMany(name => new[] { "-u", name }), executable, .. arguments],
            directory,
            environment,
            Columns,
            Rows,
            request.RecordingPath
        );
    }

    /// <summary>The end record <c>aio done</c> wrote, or null when the agent never called it.</summary>
    public static async Task<string?> EndRecord(AgentRequest request, CancellationToken ct)
    {
        try
        {
            return await File.ReadAllTextAsync(request.EndRecordPath, ct);
        }
        catch (Exception missing) when (missing is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
    }

    /// <summary>The PATH a GUI-launched process would otherwise lack, recovered from the login shell.</summary>
    /// <summary>Where <see cref="Options"/> would find <paramref name="cli"/>, or null when it would refuse.</summary>
    public static string? Locate(string cli) => Resolve(cli, Inherited());

    static string Inherited() =>
        LoginShellEnvironment.Path() ?? Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

    /// <summary>
    /// The CLI's absolute path. It is looked for on the machine's PATH and not the checkout's
    /// scripts, so a repository cannot stand in for the agent. A path is taken as given.
    /// </summary>
    static string? Resolve(string cli, string path)
    {
        if (Path.IsPathRooted(cli) || cli.Contains('/', StringComparison.Ordinal))
        {
            return File.Exists(cli) ? cli : null;
        }

        string[] extensions = OperatingSystem.IsWindows() ? [".exe", ".cmd", ".bat", ""] : [""];

        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .SelectMany(directory => extensions.Select(extension => Path.Combine(directory, cli + extension)))
            .FirstOrDefault(File.Exists);
    }

    /// <summary>
    /// The variables that describe the agent session this process runs in, which a child agent must
    /// not inherit. Started from a Claude Code session, a run's <c>claude</c> read the parent's
    /// <c>CLAUDE_CODE_CHILD_SESSION</c>, took itself for a nested call and stopped writing its
    /// transcript. Skipped on Windows, which has no <c>env</c>.
    /// </summary>
    static IReadOnlyList<string> Unset()
    {
        if (OperatingSystem.IsWindows() || !File.Exists(EnvExecutable))
        {
            return [];
        }

        return
        [
            .. Environment
                .GetEnvironmentVariables()
                .Keys.OfType<string>()
                .Where(IsSessionMarker)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];
    }

    internal static bool IsSessionMarker(string key) =>
        key.StartsWith("CLAUDE_CODE_", StringComparison.Ordinal)
        || key.StartsWith("CLAUDECODE", StringComparison.Ordinal)
        || key is "CLAUDE_SESSION_ID" or "ANTHROPIC_SESSION_ID" or "COPILOT_SESSION_ID" or "OPENCODE_SESSION_ID";
}
