using System.Text.RegularExpressions;
using AiOrchestrator.BuildingBlocks.Ports;
using CliWrap;

namespace AiOrchestrator.Infrastructure;

/// <summary>
/// The <c>ProbeAsync</c> of every port adapter that drives a CLI (#515): the executable as the
/// adapter's own spawn would find it, asked <c>--version</c>. A CLI the spawn would not find is
/// reported as not on the PATH, which is the answer Settings exists to give.
/// </summary>
static partial class CliProbing
{
    /// <summary>A version is printed at once; a CLI that takes longer is answering something else.</summary>
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    /// <param name="cli">The executable as the adapter names it.</param>
    /// <param name="path">Where the adapter's spawn finds it, or null when it does not.</param>
    public static async Task<CliProbe> Probe(string cli, string? path, CancellationToken ct)
    {
        var name = Path.GetFileName(cli);
        if (path is null)
        {
            return new CliProbe(name, null, null);
        }

        var printed = await ProcessRun.Capture(
            Cli.Wrap(path).WithArguments(["--version"]).WithEnvironmentVariables(LoginShellEnvironment.PathOverride()),
            Timeout,
            ct,
            maxCharacters: 4000
        );

        return new CliProbe(name, path, printed.ExitCode == 0 ? Version(printed.StandardOutput) : null);
    }

    /// <summary>
    /// What a CLI printed to another question than its version (#680), such as whether it is signed
    /// in, whatever its exit code, because a CLI that is signed out says so and exits non-zero. Null
    /// when it did not start or did not answer in time.
    /// </summary>
    public static async Task<string?> Answer(string path, IReadOnlyList<string> arguments, CancellationToken ct)
    {
        var printed = await ProcessRun.Capture(
            Cli.Wrap(path).WithArguments(arguments).WithEnvironmentVariables(LoginShellEnvironment.PathOverride()),
            Timeout,
            ct,
            maxCharacters: 4000
        );

        return printed.Missing || printed.TimedOut ? null : printed.StandardOutput;
    }

    /// <summary>
    /// The first version number in what <c>--version</c> printed: <c>gh version 2.99.0 (…)</c> and
    /// <c>2.1.4 (Claude Code)</c> both say theirs among other words.
    /// </summary>
    // Copilot CLI ends its sentence after the number, "GitHub Copilot CLI 1.0.95-2.", and the full stop is not the version's.
    internal static string? Version(string printed) => VersionNumber().Match(printed) is { Success: true } found ? found.Value.TrimEnd('.') : null;

    [GeneratedRegex(@"\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.]+)?")]
    private static partial Regex VersionNumber();
}
