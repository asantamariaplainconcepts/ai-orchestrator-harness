using System.Collections.Concurrent;
using AiOrchestrator.Infrastructure.Files;

namespace AiOrchestrator.Infrastructure.Environments;

/// <summary>
/// One writer at a time to a repository's own files (#631). Every worktree shares the checkout's
/// <c>.git/config</c> and refs, and git takes a lock file on each write and fails, not waits, when
/// another holds it: four prepares at once met each other's <c>config.lock</c> in the first minute.
/// So what writes there (<c>prepare</c>, <c>cleanup</c>, sbx adding and removing its remote, a fetch
/// from a sandbox) goes one after another per repository, keyed by the git common directory so the
/// checkout and every worktree of it are one key. What runs inside each worktree stays parallel.
/// <para>
/// In this process only, and static because the app holds two worktree environments, the plain one
/// and the one inside sbx. A person running git by hand at the same moment is what
/// <see cref="Contended"/> and one retry are for.
/// </para>
/// </summary>
static class RepositoryLock
{
    /// <summary>Long enough for the other writer's lock file to go: theirs take milliseconds.</summary>
    public static readonly TimeSpan RetryPause = TimeSpan.FromMilliseconds(500);

    static readonly ConcurrentDictionary<string, SemaphoreSlim> Gates = new(StringComparer.Ordinal);

    static readonly GitCli Git = new();

    /// <summary>Waits for the repository of <paramref name="directory"/>; disposing the result lets the next writer in.</summary>
    public static async Task<IDisposable> Acquire(string directory, CancellationToken ct)
    {
        var gate = Gates.GetOrAdd(await Key(directory, ct), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Held(gate);
    }

    /// <summary>What git says when another process holds the config's lock file.</summary>
    public static bool Contended(ProcessOutcome run) =>
        (run.StandardOutput + run.StandardError).Contains("could not lock config file", StringComparison.Ordinal);

    /// <summary>The git common directory, or the directory itself when git does not answer there.</summary>
    static async Task<string> Key(string directory, CancellationToken ct)
    {
        if (Directory.Exists(directory))
        {
            var common = await Git.Run(directory, ["rev-parse", "--path-format=absolute", "--git-common-dir"], ct);
            if (common.Succeeded && common.StandardOutput.Trim() is { Length: > 0 } path)
            {
                directory = path;
            }
        }

        return Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
    }

    sealed class Held(SemaphoreSlim gate) : IDisposable
    {
        int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                gate.Release();
            }
        }
    }
}
