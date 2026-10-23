namespace AiOrchestrator.Modules.Loop.Definition;

/// <summary>
/// <c>.aio/loop.yaml</c> once it has been read and validated — README.md#the-loop. Plain
/// records: the engine (#505) walks them, and nothing here knows how a state is run.
/// </summary>
/// <param name="States">In the order the file declares them. The order is meaningful: a transition
/// to a state declared above the one it leaves goes back, and going back needs <c>max_loops</c>.</param>
sealed record LoopDefinition(
    string Name,
    int? Wip,
    IReadOnlyDictionary<string, LoopAgent> Agents,
    IReadOnlyList<LoopState> States
)
{
    /// <summary>
    /// The targets no state declares, such as <c>done</c> and <c>needs-refinement</c>. The loop
    /// does nothing there — the PBI stays until a person moves it.
    /// </summary>
    public IReadOnlyList<string> Undeclared =>
        [
            .. States
                .SelectMany(state => state.Transitions)
                .Select(transition => transition.Target)
                .OfType<string>()
                .Where(target => States.All(state => state.Name != target))
                .Distinct(StringComparer.Ordinal),
        ];
}

/// <param name="Provider">The installed CLI: one of <see cref="LoopAgent.Providers"/>.</param>
/// <param name="Model">Optional; the CLI's own default when absent.</param>
sealed record LoopAgent(string Provider, string? Model)
{
    public static readonly IReadOnlyList<string> Providers = ["claude-code", "copilot", "opencode", "codex"];
}

/// <summary>One state, named after the <c>status:*</c> label the PBI carries while it is in it.</summary>
/// <param name="Scripts">What runs first, in order, each found in <c>.aio/</c> or <c>.aio/steps/</c> (<see cref="AiOrchestrator.BuildingBlocks.Ports.HarnessScripts"/>).</param>
/// <param name="Agent">A role from <c>agents</c>, or null for the folder's default agent.</param>
/// <param name="Provider">Overrides the agent's provider for this state only.</param>
/// <param name="Model">Overrides the agent's model for this state only.</param>
/// <param name="Transitions">From <c>on</c>, one per outcome, or the single one <c>next</c> declares.</param>
/// <param name="Back">Where a person's <i>Request changes</i> sends the PBI from here, or null when
/// this state offers none.</param>
/// <param name="Tab">The PBI screen's tab to look at while the PBI is here, one of
/// <see cref="Tabs"/>, or null for none.</param>
sealed record LoopState(
    string Name,
    IReadOnlyList<string> Scripts,
    string? Agent,
    string? Command,
    string? Score,
    string? Provider,
    string? Model,
    int? MaxLoops,
    IReadOnlyList<LoopTransition> Transitions,
    string? Back = null,
    string? Tab = null
)
{
    /// <summary>The PBI screen's tabs, each by the path of its route.</summary>
    public static readonly IReadOnlyList<string> Tabs = ["journey", "issue", "proposal", "review", "files"];
}

/// <summary>Where one outcome of a state leads.</summary>
/// <param name="Outcome">The key under <c>on</c>, or null for <c>next</c>.</param>
/// <param name="Target">The state the PBI goes to — after the hold is released, when there is one.
/// Null only for a hold without <c>next</c>, where the person who releases it chooses.</param>
/// <param name="Hold">The reason a person reads, or null when nobody needs to act.</param>
/// <param name="Pr">What the engine does to the pull request on taking it: the transition's <c>pr</c>.</param>
sealed record LoopTransition(string? Outcome, string? Target, string? Hold, VendorStep Pr = VendorStep.None);

/// <summary>
/// What the engine does at the vendor on a transition, beside moving the label: the <c>pr</c> a
/// transition of the loop declares (README.md#the-loop). A command never does any of it.
/// </summary>
enum VendorStep
{
    None,

    /// <summary><c>pr: draft</c>: the pull request, as a draft that closes the PBI.</summary>
    OpenDraftPullRequest,

    /// <summary><c>pr: ready</c>: the pull request is ready for review.</summary>
    MarkReady,

    /// <summary><c>pr: close</c>: the PBI is closed.</summary>
    Close,
}
