using AiOrchestrator.BuildingBlocks.Ports;

namespace AiOrchestrator.BuildingBlocks.RunRequests;

/// <summary>
/// How the loop asks for a run and learns how it ended, without either module knowing the other.
/// <c>Modules.Runs</c> implements it and <c>Modules.Loop</c> consumes it; both reference only this
/// assembly, and the architecture tests fail if one references the other.
/// <para>
/// The loop asks rather than waits. Each pass of the engine reads the runs again, so a run that
/// ended while the process was down is found on the first pass after it comes back.
/// </para>
/// </summary>
public interface IRunRequests
{
    /// <summary>Starts a run and returns its id at once; the run goes on in the background.</summary>
    Task<Guid> StartAsync(RunRequest request, CancellationToken ct);

    /// <summary>A run as it stands, or null when there is no run with that id.</summary>
    Task<RunStatus?> GetAsync(Guid runId, CancellationToken ct);

    /// <summary>Every run that has not ended yet, which is what the WIP limit counts.</summary>
    Task<IReadOnlyList<RunStatus>> ListLiveAsync(CancellationToken ct);

    /// <summary>
    /// The place a state's scripts run in, one after the other: the PBI's branch, prepared by the
    /// environment a run of that PBI would get. The folder's own checkout is on whatever branch a
    /// person left it, and <c>archive</c> commits and pushes the branch it runs on. The place is
    /// prepared when its first script runs and retired when it is disposed, unless a live run works
    /// there, so a script sees what the one before it left uncommitted: the gate's reports reach
    /// <c>quality</c> (#751). A place that cannot be prepared answers each script exit -1 with no
    /// line and the environment's sentence, the way a script that could not start does.
    /// </summary>
    /// <param name="pbi">The PBI the scripts are about.</param>
    /// <param name="folderPath">The folder's checkout, which the PBI's place is prepared from.</param>
    /// <param name="branch">The PBI's branch, such as <c>change/497</c>.</param>
    IScriptPlace OpenScriptPlace(PbiRef pbi, string folderPath, string branch);

    /// <summary>
    /// Where a run of <paramref name="request"/> would be placed, and why it cannot be now (#614), so
    /// the engine makes the PBI wait rather than start a run that fails. Null when there is nothing to
    /// say: the run goes to this machine and its failures are the run's.
    /// </summary>
    Task<RunPlacement?> PlacementAsync(RunRequest request, CancellationToken ct) => Task.FromResult<RunPlacement?>(null);

    /// <summary>
    /// Every run of the PBI that has ended, the loop's and the ones a person opened, oldest first:
    /// what its journey comment lists (#697). <see cref="GetAsync"/> leaves a person's run out, because
    /// its end is never an outcome; the comment shows it beside the loop's, as the Journey does.
    /// </summary>
    Task<IReadOnlyList<RunStatus>> ListEndedAsync(PbiRef pbi, CancellationToken ct) => Task.FromResult<IReadOnlyList<RunStatus>>([]);

    /// <summary>
    /// #734. Every run of a repository, the loop's and the ones a person opened, live and ended, oldest
    /// first: what the Metrics screen counts, times and prices.
    /// </summary>
    Task<IReadOnlyList<RunStatus>> ListRepositoryAsync(string repository, CancellationToken ct) => Task.FromResult<IReadOnlyList<RunStatus>>([]);

    /// <summary>
    /// How long a live run may write nothing before it reads as <see cref="RunStatus.Stale"/>, as a
    /// person set it or as configured. The loop holds a running script to the same threshold (#597).
    /// </summary>
    Task<TimeSpan> StaleAfterAsync(CancellationToken ct);
}

/// <summary>Where one state's scripts run on a PBI, from <see cref="IRunRequests.OpenScriptPlace"/>.</summary>
public interface IScriptPlace : IAsyncDisposable
{
    /// <summary>Runs one script in the place, preparing it first if no script has run there yet.</summary>
    /// <param name="script">The script's name, as <see cref="HarnessScripts.Find"/> finds it.</param>
    /// <param name="arguments">Its arguments, one per element and unquoted.</param>
    Task<ScriptResult> RunAsync(string script, IReadOnlyList<string> arguments, CancellationToken ct);
}

/// <summary>One agent step of the loop to run on a PBI.</summary>
/// <param name="Pbi">The PBI the run works on.</param>
/// <param name="State">The <c>loop.yaml</c> state the run belongs to, such as <c>in-progress</c>.</param>
/// <param name="FolderPath">The folder's checkout, whose default environment the run uses.</param>
/// <param name="Branch">The branch the run works on, such as <c>change/497</c>.</param>
/// <param name="Provider">The CLI, as <see cref="IAgentProvider.Name"/> spells it.</param>
/// <param name="Model">The model, or null for the CLI's own default.</param>
/// <param name="Command">What the agent is asked to do, such as <c>/aio:implement 497</c>.</param>
/// <param name="Outcomes">
/// The outcomes the state's <c>on</c> knows. An end record naming any other fails the run, so the
/// loop never receives an outcome it has no transition for.
/// </param>
public sealed record RunRequest(
    PbiRef Pbi,
    string State,
    string FolderPath,
    string Branch,
    string Provider,
    string? Model,
    string Command,
    IReadOnlyList<string> Outcomes
);

/// <summary>Where a run would be placed.</summary>
/// <param name="Environment">The environment its folder chose for its state, such as <c>sbx</c>.</param>
/// <param name="Sandboxed">The agent runs with the place's own configuration, so this machine's trust does not apply.</param>
/// <param name="Waits">Why it cannot be placed now, with what fixes it, or null when it can.</param>
public sealed record RunPlacement(string Environment, bool Sandboxed, string? Waits);

/// <summary>A run as it stands.</summary>
/// <param name="RunId">The run.</param>
/// <param name="Pbi">The PBI it works on.</param>
/// <param name="State">The <c>loop.yaml</c> state it belongs to.</param>
/// <param name="End">How it ended, or null while it is still running.</param>
public sealed record RunStatus(Guid RunId, PbiRef Pbi, string State, RunEnd? End)
{
    /// <summary>When the run last wrote to its terminal, or null once it has ended.</summary>
    public DateTimeOffset? LastOutputAt { get; init; }

    /// <summary>
    /// The run is live and has written nothing for longer than the threshold, <c>Runs:StaleAfter</c>
    /// (ten minutes unless configured). It goes back to false with the run's next output.
    /// </summary>
    public bool Stale { get; init; }

    /// <summary>The CLI it runs, as <see cref="RunRequest.Provider"/> named it; what the journey comment says ran (#696).</summary>
    public string? Provider { get; init; }

    /// <summary>The model, or null for the CLI's own default.</summary>
    public string? Model { get; init; }

    /// <summary>What the agent was asked to do, such as <c>/aio:implement 497</c>.</summary>
    public string? Command { get; init; }

    /// <summary>When it started.</summary>
    public DateTimeOffset? StartedAt { get; init; }

    /// <summary>A person opened it; it moved nothing (#697).</summary>
    public bool Manual { get; init; }

    /// <summary>
    /// Where its recording is copied on the repository's <c>aio/evidence</c> branch, as
    /// <c>EvidenceBranch.RecordingOf</c> names it, once it ended with one; null otherwise (#697).
    /// </summary>
    public string? Recording { get; init; }
}

/// <summary>
/// How a run ended: an outcome when it succeeded, a failure with its cause when it did not. Exactly
/// one of <paramref name="Outcome"/> and <paramref name="Failure"/> is set.
/// </summary>
/// <param name="Outcome">The outcome its end record names, one of <see cref="RunRequest.Outcomes"/>.</param>
/// <param name="Reason">Why, in the agent's words, or the failure's explanation. What the journal shows.</param>
/// <param name="Failure">Why the run failed, or null when it succeeded.</param>
/// <param name="Problems">
/// What the agent found and did not fix, one per entry, as <c>aio done --problems</c> wrote them.
/// Empty when it named none or wrote no record.
/// </param>
public sealed record RunEnd(string? Outcome, string? Reason, RunFailure? Failure, IReadOnlyList<string> Problems)
{
    /// <summary>When it ended, which the journey comment's row says with how long it took (#696).</summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>What it cost in dollars, or null when the CLI did not say.</summary>
    public decimal? CostUsd { get; init; }

    /// <summary>Every token the model read, or null when the CLI did not say (#734).</summary>
    public long? InputTokens { get; init; }

    /// <summary>Every token the model wrote, or null when the CLI did not say (#734).</summary>
    public long? OutputTokens { get; init; }
}

/// <summary>Why a run failed. The cause is a field so a screen can say it without reading a transcript.</summary>
public enum RunFailure
{
    /// <summary>The agent exited without calling <c>aio done</c>.</summary>
    NoEndRecord,

    /// <summary>The end record does not validate against its schema.</summary>
    InvalidEndRecord,

    /// <summary>The end record names an outcome the state does not know.</summary>
    UnknownOutcome,

    /// <summary>The CLI the step names is not installed.</summary>
    AgentNotFound,

    /// <summary>The run went on longer than it is allowed to.</summary>
    Timeout,

    /// <summary>The CLI exited with a non-zero code.</summary>
    NonZeroExit,

    /// <summary>The agent said it could not go on.</summary>
    Blocked,

    /// <summary>The environment could not prepare a place for the run, so no agent started.</summary>
    EnvironmentFailed,

    /// <summary>A person stopped it from the PBI's header (#511).</summary>
    Stopped,

    /// <summary>The agent asked for a permission and the session wrote nothing more (#706).</summary>
    WaitingForPerson,

    /// <summary>The CLI said it is not logged in and the session wrote nothing more (#706).</summary>
    NotLoggedIn,

    /// <summary>The session wrote nothing for the stale threshold and left no end record (#764).</summary>
    WaitingForInput,
}

/// <summary>
/// #697. How Runs tells the loop that a run of a PBI ended, so the journey comment carries it at once,
/// a run a person opened included: that one leads to no transition, which is otherwise what writes
/// the comment. <c>Modules.Loop</c> implements it; with no loop in the host there is nobody to tell.
/// </summary>
public interface IRunEndings
{
    /// <summary>Writes the PBI's journey comment again. Never throws: the run has already ended.</summary>
    Task EndedAsync(PbiRef pbi, CancellationToken ct);
}
