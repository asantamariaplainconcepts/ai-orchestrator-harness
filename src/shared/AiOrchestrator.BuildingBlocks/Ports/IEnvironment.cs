namespace AiOrchestrator.BuildingBlocks.Ports;

/// <summary>
/// Where a run executes: prepares the place before the run, turns the session's launch into the one
/// that starts inside it, and retires it after. The loop never sees this port — Runs picks the
/// environment a folder's settings name for the run's state (#612) — so the loop does not know
/// whether its code lives in a worktree or a sandbox.
/// <para>
/// Adapters: <c>worktree</c>, a worktree on this machine, and <c>sbx</c>, that worktree's branch in a
/// Docker sandbox (#614); later ACA. Several are registered at once and told apart by <see cref="Name"/>.
/// </para>
/// </summary>
public interface IEnvironment
{
    /// <summary>The name a folder's settings use to pick this environment, such as <c>worktree</c>.</summary>
    string Name { get; }

    /// <summary>
    /// What an agent may do without asking in this environment when it says, or null to follow the
    /// instance's setting (#578). A sandbox can afford <c>yolo</c> where this machine cannot.
    /// </summary>
    AgentPermissions? Permissions { get; }

    /// <summary>
    /// Whether the agent runs with a configuration of the place's own rather than this machine's, as
    /// in a sandbox. Then what this machine's CLI trusts does not decide whether a run starts (#606).
    /// </summary>
    bool Sandboxed => false;

    /// <summary>
    /// Whether this machine can place a run here now, for the folder's page: an environment that is
    /// not offered is left out of its selector, and a note a person should read is shown beside it.
    /// </summary>
    Task<EnvironmentReadiness> ReadinessAsync(CancellationToken ct) => Task.FromResult(EnvironmentReadiness.Ready);

    /// <summary>
    /// Why a run of <paramref name="request"/> cannot be placed here now, with what fixes it, or null
    /// when it can. The engine asks before it asks for a run, so the PBI waits instead of failing, as
    /// it waits on a folder its CLI does not trust.
    /// </summary>
    Task<string?> WaitsAsync(PlaceRequest request, CancellationToken ct) => Task.FromResult<string?>(null);

    /// <summary>Makes the code of <paramref name="request"/>'s branch ready for a run and says where it is.</summary>
    Task<RunPlace> PrepareAsync(PlaceRequest request, CancellationToken ct);

    /// <summary>
    /// The launch that runs <paramref name="launch"/> inside <paramref name="place"/>: for a sandbox,
    /// its <c>exec</c> around the same executable, arguments, directory and variables. The session,
    /// its recording and the end record do not change: the PTY stays on this machine, and the end
    /// record is written where <c>AIO_DONE_FILE</c> says, which the environment keeps in reach.
    /// </summary>
    SessionOptions Enter(RunPlace place, SessionOptions launch);

    /// <summary>
    /// Gives back what <see cref="PrepareAsync"/> took. Pushed work survives it: the branch, its
    /// commits and its pull request belong to the vendor, not to the place.
    /// </summary>
    Task RetireAsync(RunPlace place, CancellationToken ct);
}

/// <summary>What a run needs a place for.</summary>
/// <param name="RepositoryPath">The folder's checkout on this machine.</param>
/// <param name="Branch">The branch the run works on, such as <c>change/497</c>.</param>
/// <param name="BaseRef">What the branch starts from when it does not exist yet.</param>
/// <param name="RunDirectory">
/// The run's own directory, where <c>AIO_DONE_FILE</c> points, or null for a script. A sandbox mounts
/// it so the end record written inside is on this machine at the same path.
/// </param>
public sealed record PlaceRequest(string RepositoryPath, string Branch, string BaseRef, string? RunDirectory = null);

/// <summary>A prepared place: the environment that made it and the directory a session starts in.</summary>
public sealed record RunPlace(string Environment, string WorkingDirectory);

/// <summary>Whether an environment can take a run on this machine now, and what a person should know.</summary>
/// <param name="Offered">Whether the folder's page offers it; a run is not placed in one that is not.</param>
/// <param name="Note">Why it is not offered, or a caution about how it runs; null when there is none.</param>
/// <param name="Remedy">The command that fixes or improves it, or null.</param>
public sealed record EnvironmentReadiness(bool Offered, string? Note, string? Remedy)
{
    public static readonly EnvironmentReadiness Ready = new(true, null, null);
}
