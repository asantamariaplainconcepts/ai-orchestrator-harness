using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Folders;
using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.Infrastructure.AgentProviders;
using AiOrchestrator.Infrastructure.Files;
using CliWrap;

namespace AiOrchestrator.Infrastructure.Environments;

/// <summary>
/// <see cref="IEnvironment"/> in a Docker sandbox (#614): the worktree is prepared as for
/// <see cref="WorktreeEnvironment"/>, and the agent runs in a sandbox of its own per PBI, created on
/// the first run and removed when the place is retired. What it does is what the #613 probe measured
/// and <c>infra/README.md</c> records:
/// <list type="bullet">
/// <item><b>A clone, not a mount of the worktree.</b> <c>sbx create --clone</c> of the folder's
/// checkout: the worktree alone has no git, and the repository's <c>.git</c> would let the agent
/// write hooks the host runs. The clone is switched to the PBI's branch as this machine has it, and
/// its commits come back into the worktree when the place is retired, where the gate runs as today.</item>
/// <item><b>The end record through a second workspace.</b> The run's directory is mounted at the same
/// path and <c>AIO_DONE_FILE</c> is passed with <c>sbx exec -e</c>; the session, its PTY and its
/// recording stay on this machine around <c>sbx exec -it</c>.</item>
/// <item><b>Credentials are sbx's, never ours.</b> Claude through the <c>anthropic</c> secret a
/// <c>/login</c> left in sbx, GitHub through a <c>github</c> secret scoped to the sandbox or else
/// the global one. This class creates and stores none; it only checks they are there.</item>
/// <item><b>Network rules per sandbox, complete at creation.</b> Denies on <c>sbx create</c>, allows
/// right after, never global: under <c>balanced</c> an unmatched request may wait for an approval
/// nobody gives.</item>
/// <item><b>The control plane at <c>host.docker.internal</c>.</b> <c>aio issue</c> reaches the PBI
/// through <c>AIO_API</c> (#634), which on this machine is a loopback address the sandbox cannot
/// connect to. Measured on 11 October with sbx 0.38.0: inside a sandbox <c>127.0.0.1:5199</c> is
/// refused, and <c>host.docker.internal:5199</c> goes through sbx's proxy to the host's
/// <c>localhost:5199</c>, which <c>balanced</c> answers with 403 until the sandbox has
/// <c>localhost:5199</c> allowed; then 200. So <see cref="Enter"/> rewrites the host, and the allow
/// for the control plane's port is added with the others.</item>
/// </list>
/// The gate does not run here: scripts stay in <c>Runs:Environment</c>.
/// </summary>
/// <param name="api">The control plane's address as <c>AIO_API</c> gives it, read when a sandbox is created; null when there is none.</param>
sealed class SbxEnvironment(WorktreeEnvironment worktree, SbxSettings settings, Func<Uri?>? api = null) : IEnvironment
{
    public const string SbxName = "sbx";

    /// <summary>How a sandbox names the machine it runs on; sbx's proxy sends it to the host's <c>localhost</c>.</summary>
    internal const string HostAlias = "host.docker.internal";

    /// <summary>The sbx agent template the sandbox is made from; it carries <c>claude</c>, git and gh.</summary>
    const string Agent = "claude";

    /// <summary>Where sbx mounts the host's checkout, read-only, inside a <c>--clone</c> sandbox.</summary>
    const string Source = "/run/sandbox/source";

    /// <summary>A pull of the template the first time took 10.3 s; a cached create 4.0–5.4 s.</summary>
    static readonly TimeSpan CreateTimeout = TimeSpan.FromMinutes(3);

    static readonly TimeSpan CallTimeout = TimeSpan.FromSeconds(60);

    /// <summary>The sandbox of each prepared place, by its worktree, so <see cref="Enter"/> needs no call.</summary>
    readonly ConcurrentDictionary<string, Sandbox> _sandboxes = new(StringComparer.Ordinal);

    readonly GitCli _git = new(CallTimeout);

    public string Name => SbxName;

    /// <summary>
    /// <c>yolo</c> unless <c>Environments:Sbx:Permissions</c> lowers it: the sandbox is the allow list,
    /// with its own files, network rules and credentials.
    /// </summary>
    public AgentPermissions? Permissions => settings.Permissions;

    public bool Sandboxed => true;

    /// <summary>
    /// Offered when sbx is installed, signed in and holds a Claude credential. With only the global
    /// GitHub token it is offered with a caution, because that token reaches every repository the
    /// person can.
    /// </summary>
    public async Task<EnvironmentReadiness> ReadinessAsync(CancellationToken ct)
    {
        var (readiness, secrets) = await Secrets(ct);

        if (readiness is not null)
        {
            return readiness;
        }

        if (!secrets.Any(secret => secret is { Global: true, Service: "github" }))
        {
            return new(
                true,
                "sbx holds no GitHub token, so a PBI placed here waits. Store yours for every sandbox, or a fine-grained one for the PBI's sandbox.",
                "gh auth token | sbx secret set github"
            );
        }

        return new(
            true,
            "This environment pushes with a token that reaches all your repositories. A fine-grained token for this repository alone, with Contents, Pull requests and Issues, is the recommended one: store it for the PBI's sandbox.",
            "sbx secret set github --sandbox aio-<repository>-change-<n>"
        );
    }

    public async Task<string?> WaitsAsync(PlaceRequest request, CancellationToken ct)
    {
        var (readiness, secrets) = await Secrets(ct);

        if (readiness is not null)
        {
            return Sentence(readiness);
        }

        var name = SandboxName(LocalPath.Expand(request.RepositoryPath), request.Branch);
        return secrets.Any(secret => secret.Service == "github" && (secret.Global || secret.Scope == name))
            ? null
            : $"sbx holds no GitHub token for {name}, so its agent could not push. Run: gh auth token | sbx secret set github, or sbx secret set github --sandbox {name} for a fine-grained one.";
    }

    public async Task<RunPlace> PrepareAsync(PlaceRequest request, CancellationToken ct)
    {
        if (await WaitsAsync(request, ct) is { } waits)
        {
            throw new InvalidOperationException(waits);
        }

        var place = await worktree.PrepareAsync(request, ct);
        var repository = LocalPath.Expand(request.RepositoryPath);
        var sandbox = new Sandbox(SandboxName(repository, request.Branch), repository, request.Branch);

        try
        {
            // sbx adds its sandbox-<name> remote to the checkout's config as it creates the sandbox.
            bool created;
            using (await RepositoryLock.Acquire(repository, ct))
            {
                created = await Create(sandbox, request.RunDirectory, ct);
            }

            // A sandbox kept from another run is on the branch already, with that run's commits.
            if (created)
            {
                await Switch(sandbox, ct);
            }
        }
        catch (Exception refused) when (refused is not OperationCanceledException)
        {
            // Nothing half made is left behind: the run fails with this sentence and no place.
            using (await RepositoryLock.Acquire(repository, CancellationToken.None))
            {
                await Sbx(["rm", "--force", sandbox.Name], CallTimeout, CancellationToken.None);
            }

            await worktree.RetireAsync(place, CancellationToken.None);
            throw;
        }

        _sandboxes[place.WorkingDirectory] = sandbox;
        return new RunPlace(SbxName, place.WorkingDirectory);
    }

    /// <summary>
    /// <c>sbx exec -it</c> around the launch, in the clone at the checkout's path: the CLI by its name,
    /// which the sandbox has on its own PATH, with the checkout's scripts first on it and every
    /// variable of the launch passed with <c>-e</c>. The host's <c>env -u</c> is dropped: nothing of
    /// this process reaches a sandbox.
    /// </summary>
    public SessionOptions Enter(RunPlace place, SessionOptions launch)
    {
        if (!_sandboxes.TryGetValue(place.WorkingDirectory, out var sandbox))
        {
            throw new InvalidOperationException($"No sandbox was prepared for {place.WorkingDirectory}, so the run has nowhere to start.");
        }

        var sbx = Locate() ?? throw new InvalidOperationException(NotInstalled);
        var (executable, arguments) = Unwrapped(launch);
        List<string> wrapped = ["exec", "-it", "-w", sandbox.Repository];

        foreach (var (name, value) in launch.Environment.Where(variable => variable.Key != "PATH").OrderBy(variable => variable.Key, StringComparer.Ordinal))
        {
            wrapped.AddRange(["-e", $"{name}={(name == AgentLaunch.ApiVariable ? FromInside(value) : value)}"]);
        }

        // Positional, so nothing of the launch is read by the shell: $0 is the checkout, "$@" the CLI and its arguments.
        var commands = string.Join(':', HarnessScripts.CommandFolders.Select(folder => "$0/" + folder));
        wrapped.AddRange([sandbox.Name, "sh", "-c", $"PATH=\"{commands}:$PATH\" exec \"$@\"", sandbox.Repository, executable, .. arguments]);

        return launch with
        {
            FileName = sbx,
            Arguments = wrapped,
            Environment = new Dictionary<string, string>(),
        };
    }

    /// <summary>
    /// Brings the clone's commits on the branch into the worktree, removes the sandbox and the secrets
    /// scoped to it, then retires the worktree as <see cref="WorktreeEnvironment"/> does. Commits that
    /// cannot come forward keep the worktree, and the sentence says where sbx left them.
    /// </summary>
    public async Task RetireAsync(RunPlace place, CancellationToken ct)
    {
        _sandboxes.TryRemove(place.WorkingDirectory, out var known);
        var sandbox = known ?? await Recovered(place.WorkingDirectory, ct);
        string? kept = null;

        if (sandbox is not null)
        {
            // The fetch, sbx removing its remote and mirroring refs, and the mirror's deletion all write
            // to the checkout's .git; the worktree's cleanup below takes the lock again on its own.
            using var held = await RepositoryLock.Acquire(sandbox.Repository, ct);
            kept = await BringBack(place.WorkingDirectory, sandbox, ct);
            await Sbx(["rm", "--force", sandbox.Name], CallTimeout, ct);

            foreach (var service in new[] { "github", "anthropic" })
            {
                await Sbx(["secret", "rm", service, "--sandbox", sandbox.Name, "-f"], CallTimeout, ct);
            }

            if (kept is null)
            {
                await ForgetMirror(sandbox, ct);
            }
        }

        if (kept is not null)
        {
            throw new InvalidOperationException(kept);
        }

        await worktree.RetireAsync(place, ct);
    }

    /// <summary>
    /// <c>aio-&lt;repository&gt;-&lt;branch&gt;</c> in the characters sbx takes, one per PBI. A person who
    /// stores a fine-grained token for a PBI's sandbox stores it under this name.
    /// </summary>
    internal static string SandboxName(string repository, string branch)
    {
        var name = $"aio-{Clean(Path.GetFileName(repository.TrimEnd('/')))}-{Clean(branch)}";

        if (name.Length <= 60)
        {
            return name;
        }

        var hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8];
        return $"{name[..51].TrimEnd('-')}-{hash}";
    }

    static string Clean(string text)
    {
        var cleaned = new StringBuilder();

        foreach (var character in text.ToLowerInvariant())
        {
            var kept = char.IsAsciiLetterOrDigit(character) || character is '.' ? character : '-';
            if (kept != '-' || cleaned.Length == 0 || cleaned[^1] != '-')
            {
                cleaned.Append(kept);
            }
        }

        return cleaned.ToString().Trim('-');
    }

    /// <summary>Creates the sandbox, or keeps the one there; true when it made a new one.</summary>
    async Task<bool> Create(Sandbox sandbox, string? runDirectory, CancellationToken ct)
    {
        if (runDirectory is not null)
        {
            Directory.CreateDirectory(runDirectory);
        }

        if (await Workspaces(sandbox.Name, ct) is { } mounted)
        {
            // A sandbox another run of the PBI left: kept when it already reaches this run's directory.
            if (runDirectory is null || mounted.Any(path => LocalPath.Same(path, runDirectory)))
            {
                return false;
            }

            await Sbx(["rm", "--force", sandbox.Name], CallTimeout, ct);
        }

        List<string> create = ["create", "-q", "--name", sandbox.Name, "--clone"];
        foreach (var host in settings.Deny)
        {
            create.AddRange(["--deny-network", host]);
        }

        create.AddRange([Agent, sandbox.Repository]);
        if (runDirectory is not null)
        {
            create.Add(runDirectory);
        }

        Answered(await Sbx(create, CreateTimeout, ct), $"sbx could not create {sandbox.Name}");

        List<string> allow = [.. settings.Allow];
        if (api?.Invoke() is { IsLoopback: true } address)
        {
            // sbx's proxy names the host's loopback `localhost`, whatever the sandbox asked for.
            allow.Add($"localhost:{address.Port}");
        }

        if (allow.Count > 0)
        {
            Answered(
                await Sbx(["policy", "allow", "network", "--sandbox", sandbox.Name, string.Join(',', allow)], CallTimeout, ct),
                $"sbx could not open the network of {sandbox.Name}"
            );
        }

        return true;
    }

    /// <summary>
    /// <c>AIO_API</c> as the sandbox reaches it: a loopback host becomes <see cref="HostAlias"/>, and
    /// any other address is passed as it is.
    /// </summary>
    internal static string FromInside(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var address) && address.IsLoopback
            ? new UriBuilder(address) { Host = HostAlias }.Uri.ToString().TrimEnd('/')
            : value;

    /// <summary>
    /// The clone starts on the checkout's branch, so the PBI's is fetched from the host's read-only
    /// copy as prepare left it, pushed or not, and set to push to origin under its own name.
    /// </summary>
    async Task Switch(Sandbox sandbox, CancellationToken ct)
    {
        const string Script =
            "set -e; git fetch -q \"$0\" \"+refs/heads/$1:refs/heads/$1\"; git switch -q \"$1\"; "
            + "git config \"branch.$1.remote\" origin; git config \"branch.$1.merge\" \"refs/heads/$1\"";

        Answered(
            await Sbx(["exec", "-w", sandbox.Repository, sandbox.Name, "sh", "-c", Script, Source, sandbox.Branch], CallTimeout, ct),
            $"{sandbox.Name} could not switch to {sandbox.Branch}"
        );
    }

    /// <summary>
    /// Fetches the branch from the sandbox's git daemon and fast-forwards the worktree to it. Null when
    /// it came forward or had nothing new; otherwise why it did not, said for the person.
    /// </summary>
    async Task<string?> BringBack(string worktreePath, Sandbox sandbox, CancellationToken ct)
    {
        if (!Directory.Exists(worktreePath))
        {
            return null;
        }

        var fetched = await _git.Run(worktreePath, ["fetch", "-q", $"sandbox-{sandbox.Name}", $"refs/heads/{sandbox.Branch}"], ct);
        if (!fetched.Succeeded)
        {
            // sbx mirrors every branch into refs/sandboxes/<name>/ as it removes the sandbox.
            return $"{sandbox.Branch} could not be fetched from {sandbox.Name}: {First(fetched.StandardError)}. sbx keeps it at refs/sandboxes/{sandbox.Name}/{sandbox.Branch}.";
        }

        var merged = await _git.Run(worktreePath, ["merge", "-q", "--ff-only", "FETCH_HEAD"], ct);
        return merged.Succeeded
            ? null
            : $"{sandbox.Branch} in {worktreePath} and in {sandbox.Name} have both moved: {First(merged.StandardError)}. The sandbox's commits are at refs/sandboxes/{sandbox.Name}/{sandbox.Branch}.";
    }

    /// <summary>The refs sbx mirrored on removal, once their commits are on the branch: one set per run would pile up.</summary>
    async Task ForgetMirror(Sandbox sandbox, CancellationToken ct)
    {
        if (!Directory.Exists(sandbox.Repository))
        {
            return;
        }

        var listed = await _git.Run(sandbox.Repository, ["for-each-ref", "--format=%(refname)", $"refs/sandboxes/{sandbox.Name}/"], ct);
        foreach (var reference in listed.Succeeded ? listed.Lines() : [])
        {
            await _git.Run(sandbox.Repository, ["update-ref", "-d", reference], ct);
        }
    }

    /// <summary>
    /// The sandbox of a place this process did not prepare, after a restart: its name follows from the
    /// worktree's checkout and branch. Null when there is no such sandbox.
    /// </summary>
    async Task<Sandbox?> Recovered(string worktreePath, CancellationToken ct)
    {
        if (!Directory.Exists(worktreePath))
        {
            return null;
        }

        var common = await _git.Run(worktreePath, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct);
        var branch = await _git.Run(worktreePath, ["branch", "--show-current"], ct);
        if (!common.Succeeded || !branch.Succeeded || Path.GetDirectoryName(common.StandardOutput.Trim()) is not { } repository)
        {
            return null;
        }

        var sandbox = new Sandbox(SandboxName(repository, branch.StandardOutput.Trim()), repository, branch.StandardOutput.Trim());
        return await Workspaces(sandbox.Name, ct) is null ? null : sandbox;
    }

    /// <summary>The workspaces of the sandbox called <paramref name="name"/>, or null when there is none.</summary>
    async Task<IReadOnlyList<string>?> Workspaces(string name, CancellationToken ct)
    {
        var listed = await Sbx(["ls", "--json"], CallTimeout, ct);
        if (!listed.Succeeded)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(listed.StandardOutput);
            if (!document.RootElement.TryGetProperty("sandboxes", out var sandboxes) || sandboxes.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var sandbox in sandboxes.EnumerateArray())
            {
                if (sandbox.TryGetProperty("name", out var found) && found.GetString() == name)
                {
                    return sandbox.TryGetProperty("workspaces", out var workspaces) && workspaces.ValueKind == JsonValueKind.Array
                        ? [.. workspaces.EnumerateArray().Select(path => path.GetString() ?? string.Empty)]
                        : [];
                }
            }
        }
        catch (JsonException)
        {
        }

        return null;
    }

    /// <summary>
    /// What sbx answers about itself and its secrets: a readiness when it is not offered, otherwise
    /// null and the secrets it lists. The values are never read; <c>sbx secret ls</c> does not print them.
    /// </summary>
    async Task<(EnvironmentReadiness? NotOffered, IReadOnlyList<Secret> Secrets)> Secrets(CancellationToken ct)
    {
        if (Locate() is null)
        {
            return (new(false, NotInstalled, null), []);
        }

        var listed = await Sbx(["secret", "ls"], CallTimeout, ct);
        if (!listed.Succeeded)
        {
            return (new(false, $"sbx did not answer ({Said(listed)}), so no run is placed in a sandbox. Sign in to Docker.", "sbx login"), []);
        }

        var secrets = Parsed(listed.StandardOutput);
        if (!secrets.Any(secret => secret is { Global: true, Service: "anthropic" }))
        {
            return (
                new(
                    false,
                    "sbx holds no Claude credential. Sign in once inside a sandbox with /login and your Claude subscription; sbx keeps it for every sandbox after it.",
                    "sbx run claude"
                ),
                secrets
            );
        }

        return (null, secrets);
    }

    /// <summary>The rows of <c>sbx secret ls</c>: scope, type, service and a mark of what is stored.</summary>
    static IReadOnlyList<Secret> Parsed(string listed) =>
        [
            .. listed
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(line => line.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                .Where(columns => columns.Length == 4 && columns[0] != "SCOPE" && columns[1] == "service")
                .Select(columns => new Secret(columns[0], columns[2], columns[3])),
        ];

    /// <summary>The CLI and its arguments without <c>/usr/bin/env -u …</c>, the CLI by its file name.</summary>
    static (string Executable, IReadOnlyList<string> Arguments) Unwrapped(SessionOptions launch)
    {
        var rest = launch.Arguments.ToList();
        var executable = launch.FileName;

        if (Path.GetFileName(executable) == "env" && rest.Count > 0)
        {
            while (rest.Count >= 2 && rest[0] == "-u")
            {
                rest.RemoveRange(0, 2);
            }

            executable = rest[0];
            rest.RemoveAt(0);
        }

        var name = Path.GetFileName(executable);

        // A terminal a person opens runs their login shell, which the sandbox may not have.
        return (name is "zsh" or "fish" or "tcsh" or "csh" or "ksh" ? "bash" : name, rest);
    }

    string? Locate() => AgentLaunch.Locate(settings.Cli);

    async Task<ProcessOutcome> Sbx(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken ct)
    {
        if (Locate() is not { } sbx)
        {
            return new ProcessOutcome(-1, string.Empty, string.Empty) { NotStarted = NotInstalled };
        }

        return await ProcessRun.Capture(
            Cli.Wrap(sbx).WithArguments(arguments).WithEnvironmentVariables(LoginShellEnvironment.PathOverride()),
            timeout,
            ct,
            maxCharacters: 200_000
        );
    }

    static void Answered(ProcessOutcome run, string consequence)
    {
        if (!run.Succeeded)
        {
            throw new InvalidOperationException($"{consequence}: {Said(run)}");
        }
    }

    static string Said(ProcessOutcome run) =>
        run.NotStarted
        ?? (run.TimedOut ? "it did not finish in time" : First(run.StandardError + "\n" + run.StandardOutput) is { Length: > 0 } line ? line : $"exit {run.ExitCode}");

    static string First(string text) =>
        text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault() ?? string.Empty;

    static string Sentence(EnvironmentReadiness readiness) =>
        readiness.Remedy is { } remedy ? $"{readiness.Note} Run: {remedy}" : readiness.Note ?? "sbx cannot take a run now.";

    const string NotInstalled = "sbx is not on the PATH of the login shell, so no run is placed in a sandbox. Install Docker Sandboxes: https://docs.docker.com/ai/sandboxes/";

    /// <summary>One sandbox: its name, the checkout it clones, and the PBI's branch.</summary>
    sealed record Sandbox(string Name, string Repository, string Branch);

    /// <summary>One row of <c>sbx secret ls</c>.</summary>
    sealed record Secret(string Scope, string Service, string Stored)
    {
        public bool Global => Scope == "(global)";
    }
}
