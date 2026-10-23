using System.Text.Json;
using AiOrchestrator.BuildingBlocks.Ports;
using CliWrap;

namespace AiOrchestrator.Infrastructure.Environments;

/// <summary>
/// Runs <c>prepare</c> or <c>cleanup</c> of <c>.aio/steps/</c> for <see cref="WorktreeEnvironment"/>:
/// the same file a person runs by hand, so a worktree the app makes is not a second copy of one.
/// <para>
/// The repository's copy wins when there is one, read from <paramref name="directory"/>; a repository
/// that never adopted the harness gets the copy this build carries, written to a temporary file for
/// the one run. A temporary file and not <c>bash -s</c>: with the script on standard input, any child
/// that reads its input would eat the rest of the script.
/// </para>
/// <para>Bash scripts only; every script the app runs answers one JSON line on its last line.</para>
/// </summary>
static class HarnessScript
{
    /// <summary>A fetch, a merge and a few <c>gh</c> calls in one process.</summary>
    static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    /// <summary>Null when the repository has no copy and this build carries none either.</summary>
    public static async Task<ProcessOutcome?> Run(
        string directory,
        string name,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken
    )
    {
        var script = HarnessScripts.Find(directory, name);
        string? carried = null;

        if (script is null)
        {
            if (Carried(name) is not { } bytes)
            {
                return null;
            }

            carried = Path.Combine(Path.GetTempPath(), $"aio-{name}-{Guid.NewGuid():N}");
            await File.WriteAllBytesAsync(carried, bytes, cancellationToken);
            script = carried;
        }

        // The login shell's PATH, or git and gh are not found when the app was started from anywhere
        // but a terminal. No pager and no prompt: either would wait for somebody who is not there.
        var environment = LoginShellEnvironment.PathOverride();
        environment["GH_PAGER"] = "cat";
        environment["GH_PROMPT_DISABLED"] = "1";
        environment["GIT_TERMINAL_PROMPT"] = "0";

        try
        {
            return await ProcessRun.Capture(
                Cli.Wrap("bash")
                    .WithArguments([script, .. arguments])
                    .WithWorkingDirectory(directory)
                    .WithEnvironmentVariables(environment),
                Timeout,
                cancellationToken
            );
        }
        finally
        {
            if (carried is not null)
            {
                File.Delete(carried);
            }
        }
    }

    /// <summary>
    /// The script's last line as JSON, or null when it printed none — which a caller reads as the
    /// script failing in its own words, from standard error.
    /// </summary>
    public static JsonElement? Answer(string stdout)
    {
        var line = stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();

        if (line is null)
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(line);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A string property of the answer, or null.</summary>
    public static string? Text(JsonElement answer, string property) =>
        answer.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    /// <summary>A boolean property of the answer; anything but <c>true</c> is false.</summary>
    public static bool Flag(JsonElement answer, string property) =>
        answer.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.True;

    /// <summary>What a run that answered nothing said, for the person reading the refusal.</summary>
    public static string Said(ProcessOutcome run, string name)
    {
        if (run.Missing)
        {
            return $".aio/steps/{name} could not be started: {run.NotStarted}";
        }

        var said = (run.StandardError + "\n" + run.StandardOutput)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0);

        return said ?? $".aio/steps/{name} stopped without saying why.";
    }

    /// <summary>The copy of a script this build carries (see the project file), or null.</summary>
    static byte[]? Carried(string name)
    {
        using var stream = typeof(HarnessScript).Assembly.GetManifestResourceStream($"harness.aio.{name}");
        if (stream is null)
        {
            return null;
        }

        using var copy = new MemoryStream();
        stream.CopyTo(copy);
        return copy.ToArray();
    }
}
