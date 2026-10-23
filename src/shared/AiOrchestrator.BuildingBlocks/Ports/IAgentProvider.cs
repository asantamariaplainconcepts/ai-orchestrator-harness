namespace AiOrchestrator.BuildingBlocks.Ports;

/// <summary>
/// One agent CLI: how to launch it with a command and a model, and how to read its end record once
/// it has exited. The provider says what to spawn and the caller spawns it in an
/// <see cref="ISession"/>, because the session's bytes go to whoever is watching and to the
/// recording, not to the provider.
/// <para>
/// Adapters: Claude Code, OpenCode and Copilot; later Codex and Microsoft Agent Framework. Several
/// are registered at once and told apart by <see cref="Name"/>, which is the <c>provider</c> key of
/// <c>loop.yaml</c>.
/// </para>
/// </summary>
public interface IAgentProvider
{
    /// <summary>The CLI this adapter drives, as <c>loop.yaml</c> spells it: <c>claude-code</c>, <c>copilot</c>, <c>opencode</c>.</summary>
    string Name { get; }

    /// <summary>What a session must spawn so the CLI runs <paramref name="request"/>'s command.</summary>
    SessionOptions Launch(AgentRequest request);

    /// <summary>
    /// Reads what the run left behind once its session has exited: the end record <c>aio done</c>
    /// wrote and what the run cost.
    /// </summary>
    Task<AgentEnd> EndAsync(AgentRequest request, CancellationToken ct);

    /// <summary>
    /// Undoes what <see cref="Launch"/> wrote outside the place for it, such as Claude Code's trust
    /// of the directory (#546). Called once per run, just before the environment retires the place,
    /// while it is still on disk. Nothing by default: most CLIs write nothing outside the place.
    /// </summary>
    void Retire(RunPlace place) { }

    /// <summary>
    /// Whether the CLI starts in <paramref name="folder"/> without first asking a person to trust it
    /// (#606), read and never written: trusting a folder is the decision of whoever uses it. Null when
    /// the CLI asks no such thing.
    /// </summary>
    AgentTrust? Trust(string folder) => null;

    /// <summary>Where the CLI is on this machine and which version, or that it is not on the PATH.</summary>
    Task<CliProbe> ProbeAsync(CancellationToken ct);

    /// <summary>
    /// The models a person can pick for this CLI in <i>Explore with agent</i> (#516), most capable
    /// first. Empty when the adapter names none, and then the CLI's own default runs.
    /// </summary>
    IReadOnlyList<string> Models => [];

    /// <summary>
    /// What the CLI's terminal says it waits for from a person, a permission or a login (#706), or
    /// null when it says nothing of the kind. <paramref name="screen"/> is the tail of what the
    /// session printed, escapes included, and the caller asks only once the session has been silent
    /// for a while. The phrases are each CLI's own, so a CLI whose adapter names none is never asked.
    /// </summary>
    PersonWait? WaitsForPerson(string screen) => null;
}

/// <summary>What a run's agent waits for from a person, which nobody watching an unattended run gives.</summary>
/// <param name="Kind">A permission or a login.</param>
/// <param name="Why">One sentence a person reads, with what was asked and what to do before releasing the PBI.</param>
public sealed record PersonWait(PersonWaitKind Kind, string Why);

/// <summary>What a person has to give before an agent can go on.</summary>
public enum PersonWaitKind
{
    /// <summary>The agent asked to run something its permission mode does not allow.</summary>
    Permission,

    /// <summary>The CLI is not logged in, or its login expired.</summary>
    Login,
}

/// <summary>One agent step to run.</summary>
/// <param name="Command">What the agent is asked to do, such as <c>/aio:implement 497</c>.</param>
/// <param name="Model">The model, or null for the CLI's own default.</param>
/// <param name="Place">Where the run executes, as <see cref="IEnvironment"/> prepared it.</param>
/// <param name="EndRecordPath">Where <c>aio done</c> writes the end record.</param>
/// <param name="RecordingPath">Where the session records the run, or null for none.</param>
/// <param name="RunId">
/// The run, which a CLI that takes a session id is given as its own, so what the run cost can be
/// found in that CLI's transcript afterwards. Empty when the caller has no run to name.
/// </param>
/// <param name="StartedAt">When the run started, the earliest moment its cost can be counted from.</param>
/// <param name="Permissions">
/// What the agent may do without asking, the control plane's setting as it stood when the run started
/// (#578). Each adapter spells it in its CLI's flags.
/// </param>
/// <param name="Repository">
/// The PBI's repository, such as <c>owner/name</c>, which <c>aio issue</c> reads as <c>AIO_REPOSITORY</c>
/// (#634). Null when the caller has none.
/// </param>
/// <param name="Api">
/// Where this control plane listens, which <c>aio issue</c> reads as <c>AIO_API</c> to reach the PBI
/// through the folder's vendor (#634). Null when the server says no address.
/// </param>
public sealed record AgentRequest(
    string Command,
    string? Model,
    RunPlace Place,
    string EndRecordPath,
    string? RecordingPath,
    Guid RunId = default,
    DateTimeOffset StartedAt = default,
    AgentPermissions Permissions = AgentPermissions.AllowList,
    string? Repository = null,
    Uri? Api = null
);

/// <summary>
/// What an agent may do without asking, one for the whole instance (#578). A prompt nobody answers
/// stalls an unattended run, so the wider modes trade the repository's allow list for runs that do not
/// stop. The repository's deny rules hold in every mode whose CLI takes them.
/// </summary>
public enum AgentPermissions
{
    /// <summary>Edits and the repository's allow list of <c>.claude/settings.json</c>; any other command asks.</summary>
    AllowList,

    /// <summary>The CLI's own automatic approval decides what runs; what the repository denies stays denied.</summary>
    Auto,

    /// <summary>Everything runs without asking, except what the repository denies where the CLI takes it.</summary>
    Yolo,
}

/// <summary>Whether a CLI trusts a folder, as the CLI itself keeps it.</summary>
/// <param name="Trusted">Whether a run starts there without a trust question nobody would answer.</param>
/// <param name="Why">One sentence a person reads, with what to do when it is not trusted.</param>
/// <param name="Remedy">The command a person runs to answer the question once, or null when trusted.</param>
public sealed record AgentTrust(bool Trusted, string Why, string? Remedy);

/// <summary>What an agent run left behind.</summary>
/// <param name="Record">
/// The end record exactly as <c>aio done</c> wrote it, or null when the agent never called it. It
/// is untrusted input: the caller validates it, and a missing or invalid record fails the run.
/// </param>
/// <param name="Usage">What the run cost, or null when the CLI does not report it.</param>
public sealed record AgentEnd(string? Record, AgentUsage? Usage);

/// <summary>The tokens a run spent and, when the CLI says, their price.</summary>
/// <param name="InputTokens">Every token the model read, cache reads and writes included.</param>
/// <param name="OutputTokens">Every token the model wrote.</param>
/// <param name="CostUsd">Their price, or null when the model's rate is unknown.</param>
public sealed record AgentUsage(long InputTokens, long OutputTokens, decimal? CostUsd);
