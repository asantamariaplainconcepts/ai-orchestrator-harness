using AiOrchestrator.BuildingBlocks.Files;
using AiOrchestrator.BuildingBlocks.Ports;
using CliWrap;

namespace AiOrchestrator.Infrastructure.Files;

/// <summary>
/// <see cref="IMachineProbe"/> on this machine (#680): CLIs found as every adapter finds its own, on
/// the login shell's PATH, and the install commands of the package manager this machine has.
/// Nothing is installed or run beyond <c>--version</c> and <c>info</c>.
/// </summary>
/// <param name="manager">The package manager the commands are written for; this machine's unless a test names another.</param>
sealed class MachineProbe(string? manager) : IMachineProbe
{
    public MachineProbe()
        : this(PackageManager.Value) { }

    /// <summary><c>podman info</c> answered in 0.16 s on the developer's machine; ten seconds is a runtime that is not there.</summary>
    static readonly TimeSpan InfoTimeout = TimeSpan.FromSeconds(10);

    static readonly Lazy<string?> PackageManager = new(Detect);

    public Task<CliProbe> ProbeAsync(string cli, CancellationToken ct) =>
        CliProbing.Probe(cli, LoginShellEnvironment.Resolve(cli), ct);

    /// <summary>Podman first, then Docker: either one answering is enough for Aspire's containers.</summary>
    public async Task<CliProbe> ContainerRuntimeAsync(CancellationToken ct)
    {
        CliProbe? silent = null;

        foreach (var cli in new[] { "podman", "docker" })
        {
            if (LoginShellEnvironment.Resolve(cli) is not { } path)
            {
                continue;
            }

            var probe = await CliProbing.Probe(cli, path, ct);
            var info = await ProcessRun.Capture(
                Cli.Wrap(path).WithArguments(["info"]).WithEnvironmentVariables(LoginShellEnvironment.PathOverride()),
                InfoTimeout,
                ct,
                maxCharacters: 20_000
            );

            if (info.Succeeded)
            {
                return probe;
            }

            silent ??= probe with { Problem = Said(info) };
        }

        return silent ?? new CliProbe("podman", null, null);
    }

    public MachineRemedy Install(string cli)
    {
        if (!Catalog.TryGetValue(cli, out var package))
        {
            return new(null, null);
        }

        var command = manager switch
        {
            "brew" => package.Brew is { } formula ? $"brew install {formula}" : null,
            "winget" => package.Winget is { } id ? $"winget install {id}" : null,
            "apt" => package.Apt is { } name ? $"sudo apt install {name}" : null,
            "dnf" => package.Dnf is { } name ? $"sudo dnf install {name}" : null,
            "pacman" => package.Pacman is { } name ? $"sudo pacman -S {name}" : null,
            _ => null,
        };

        // An agent CLI is an npm package on every platform, and node is a requirement already.
        return new(command ?? package.Npm, package.Link);
    }

    public string? MakeReady(string cli) =>
        cli switch
        {
            "gh" => "gh auth login",
            "claude" => "claude auth login",
            // On Linux Podman needs no machine; on macOS and Windows its VM is what answers.
            "podman" when manager is "brew" or "winget" => "podman machine start",
            _ => null,
        };

    /// <summary>
    /// The package names each manager uses. A null is a manager that has no package for it, and the
    /// npm line is the fallback an agent CLI has everywhere. Checked on 12 October 2026 against this
    /// Mac's Homebrew (the <c>claude-code</c> and <c>copilot-cli</c> casks) and each project's
    /// install page.
    /// </summary>
    static readonly Dictionary<string, Package> Catalog = new(StringComparer.Ordinal)
    {
        ["git"] = new("https://git-scm.com/downloads", "git", "Git.Git", "git", "git", "git"),
        ["gh"] = new("https://cli.github.com", "gh", "GitHub.cli", "gh", "gh", "github-cli"),
        ["node"] = new("https://nodejs.org/en/download", "node", "OpenJS.NodeJS.LTS", "nodejs", "nodejs", "nodejs"),
        ["claude"] = new(
            "https://docs.claude.com/en/docs/claude-code/setup",
            "--cask claude-code",
            Npm: "npm install -g @anthropic-ai/claude-code"
        ),
        ["opencode"] = new("https://opencode.ai/docs", Npm: "npm install -g opencode-ai"),
        ["copilot"] = new("https://docs.github.com/copilot/how-tos/set-up/install-copilot-cli", "--cask copilot-cli", Npm: "npm install -g @github/copilot"),
        // Docker Sandboxes ships through its own installer, with no package to name.
        ["sbx"] = new("https://docs.docker.com/ai/sandboxes/"),
        ["podman"] = new("https://podman.io/docs/installation", "podman", "RedHat.Podman", "podman", "podman", "podman"),
        ["docker"] = new("https://docs.docker.com/get-started/get-docker/", "--cask docker", "Docker.DockerDesktop"),
    };

    sealed record Package(
        string Link,
        string? Brew = null,
        string? Winget = null,
        string? Apt = null,
        string? Dnf = null,
        string? Pacman = null,
        string? Npm = null
    );

    static string? Detect()
    {
        if (OperatingSystem.IsMacOS())
        {
            return "brew";
        }

        if (OperatingSystem.IsWindows())
        {
            return "winget";
        }

        const string release = "/etc/os-release";
        return OperatingSystem.IsLinux() && File.Exists(release) ? FromOsRelease(File.ReadAllText(release)) : null;
    }

    /// <summary>
    /// The manager of a Linux distribution, by the <c>ID</c> and <c>ID_LIKE</c> of its
    /// <c>/etc/os-release</c>: Ubuntu says <c>ID_LIKE=debian</c>, Rocky <c>ID_LIKE="rhel centos fedora"</c>.
    /// </summary>
    internal static string? FromOsRelease(string text)
    {
        var names = text.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("ID=", StringComparison.Ordinal) || line.StartsWith("ID_LIKE=", StringComparison.Ordinal))
            .SelectMany(line => line[(line.IndexOf('=') + 1)..].Trim('"', '\'').Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .ToHashSet(StringComparer.Ordinal);

        if (names.Overlaps(["debian", "ubuntu"]))
        {
            return "apt";
        }

        if (names.Overlaps(["fedora", "rhel", "centos"]))
        {
            return "dnf";
        }

        return names.Contains("arch") ? "pacman" : null;
    }

    /// <summary>The first line the runtime wrote about why it did not answer.</summary>
    static string Said(ProcessOutcome outcome) =>
        outcome.TimedOut
            ? $"it did not answer within {InfoTimeout.TotalSeconds:0} s"
            : (outcome.StandardError + "\n" + outcome.StandardOutput)
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault() ?? $"it exited with {outcome.ExitCode}";
}
