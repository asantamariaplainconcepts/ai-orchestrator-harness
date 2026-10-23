using System.Collections.Concurrent;
using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.Modules.Runs.Features.PbiRuns;

/// <summary>
/// The runs this process is running now, with what only the process knows about them: their
/// session and when they last wrote. A singleton, because a run outlives every scope that reads it.
/// <para>
/// A row that has not ended and is not here belongs to a process that is gone: the server stopped
/// with the run live. <see cref="RunBook"/> ends such a row from its end record the first time it is
/// read, which is how a run that ended while the server was down is found when it comes back.
/// </para>
/// </summary>
sealed class LiveRuns
{
    readonly ConcurrentDictionary<Guid, LiveRun> _runs = new();

    public LiveRun Add(Guid runId, DateTimeOffset startedAt) => _runs[runId] = new LiveRun(startedAt);

    public LiveRun? Find(Guid runId) => _runs.GetValueOrDefault(runId);

    public void Remove(Guid runId) => _runs.TryRemove(runId, out _);

    public HashSet<Guid> Ids() => [.. _runs.Keys];

    /// <summary>The sessions a person opened on a folder, which have no row: <see cref="FolderSessions"/>.</summary>
    public IReadOnlyList<(Guid Id, LiveRun Run)> OnFolders() =>
        [.. _runs.Where(run => run.Value.Folder is not null).Select(run => (run.Key, run.Value))];

    /// <summary>Whether a live run other than <paramref name="except"/> works in <paramref name="directory"/>, so it must not be retired.</summary>
    public bool Uses(string directory, Guid except) =>
        _runs.Any(run => run.Key != except && string.Equals(run.Value.WorkingDirectory, directory, StringComparison.Ordinal));
}

/// <summary>One live run. Its session is null until the environment has prepared a place and the CLI started.</summary>
sealed class LiveRun(DateTimeOffset startedAt)
{
    long _lastOutput = startedAt.UtcTicks;

    public DateTimeOffset StartedAt { get; } = startedAt;

    public ISession? Session { get; private set; }

    public string? WorkingDirectory { get; private set; }

    /// <summary>What a session opened on a folder is, since it has no row to say it; null for a run.</summary>
    public FolderSession? Folder { get; private set; }

    /// <summary>A person stopped it, so it ends as <c>Stopped</c> whatever its end record says.</summary>
    public bool StoppedByPerson { get; private set; }

    /// <summary>What the session prints, for every browser attached to it (#508).</summary>
    public SessionOutput Output { get; } = new();

    /// <summary>When the run last wrote, or when it started if it has not written yet.</summary>
    public DateTimeOffset LastOutputAt => new(Interlocked.Read(ref _lastOutput), TimeSpan.Zero);

    public void Placed(string directory) => WorkingDirectory = directory;

    public void OnFolder(FolderSession folder) => Folder = folder;

    public void Started(ISession session) => Session = session;

    public void Stop() => StoppedByPerson = true;

    public void Wrote(DateTimeOffset at) => Interlocked.Exchange(ref _lastOutput, at.UtcTicks);

    public bool IsStale(DateTimeOffset now, TimeSpan after) => now - LastOutputAt >= after;
}

/// <summary>A session opened by hand on a folder: where, with which agent (null for a terminal) and command.</summary>
sealed record FolderSession(string FolderPath, string? Provider, string Command);
