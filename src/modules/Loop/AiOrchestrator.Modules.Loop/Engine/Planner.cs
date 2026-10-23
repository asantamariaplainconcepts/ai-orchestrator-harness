using AiOrchestrator.BuildingBlocks.Ports;
using AiOrchestrator.BuildingBlocks.RunRequests;
using AiOrchestrator.Modules.Loop.Definition;
using AiOrchestrator.Modules.Loop.Journal;

namespace AiOrchestrator.Modules.Loop.Engine;

/// <summary>
/// The decision: given the photograph of one PBI, what the engine does next. Pure — no port, no
/// clock, no store — so every transition of <c>.aio/loop.yaml</c> is a row in a table test, and
/// what the engine did can be explained after the fact from what it saw.
/// <para>
/// The order of the questions is the order of the rules in README.md#the-loop: whose PBI it is,
/// whether a person holds it, whether a run it asked for has ended, whether there is room under the
/// WIP limit, what the state's scripts said, and only then the agent.
/// </para>
/// </summary>
static class Planner
{
    /// <summary>The outcome of a state that declares <c>next</c> and no <c>on</c>.</summary>
    public const string Next = "next";

    public static Decision Decide(Situation situation)
    {
        var (loop, pbi) = (situation.Loop, situation.Pbi);

        if (pbi.State is not { } name || loop.States.FirstOrDefault(state => state.Name == name) is not { } state)
        {
            return new Ignore($"{pbi.State ?? "no state"} is not a state the loop runs");
        }

        if (pbi.Assignee is { } assignee && !assignee.Equals(situation.Identity, StringComparison.OrdinalIgnoreCase))
        {
            return new Ignore($"assigned to {assignee}");
        }

        var hold = HoldOn(pbi, situation.Last);

        if (pbi.Held)
        {
            if (hold is { To: { } to } && situation.Approved)
            {
                return new Release(to, "the pull request was approved at the vendor");
            }

            return new Wait(hold is null ? $"held at {state.Name} by a person" : $"held at {state.Name}: {hold.Why}");
        }

        if (hold is { To: { } released })
        {
            return Enter(situation, state, released, "the hold was released", VendorStep.None);
        }

        if (situation.Run is { } run)
        {
            return Ended(situation, state, run);
        }

        var agent = state.Agent is not null || state.Command is not null;
        if (agent && loop.Wip is { } wip && situation.LiveRuns >= wip)
        {
            return new Wait($"WIP {situation.LiveRuns} of {wip}");
        }

        IReadOnlyList<string> scripts = state.Score is { } score ? [.. state.Scripts, score] : state.Scripts;
        if (scripts.Count > 0 && situation.Scripts is null)
        {
            return new RunScripts(scripts);
        }

        // A failure or a wait anywhere in the list stops the state. An outcome the state's on does
        // not name, from a script that exited non-zero, is a failure too: a loop.yaml written before
        // evidence had outcomes still holds on its blocked (#733).
        var verdicts = situation.Scripts ?? [];
        if (verdicts.FirstOrDefault(verdict => verdict.Kind == VerdictKind.Fail || verdict is { Kind: VerdictKind.Outcome, Exit: not 0 } && !Names(state, verdict.Outcome!)) is { } failed)
        {
            return new Hold($"{failed.Script} failed: {failed.Why}", null);
        }
        if (verdicts.FirstOrDefault(verdict => verdict.Kind == VerdictKind.Wait) is { } waiting)
        {
            return new Wait(waiting.Why);
        }

        // The first outcome that holds is the state's, then the first outcome: the gate's green does
        // not carry the PBI past the evidence's blocked, and the gate's red still comes first (#733).
        var outcomes = verdicts.Where(verdict => verdict.Kind == VerdictKind.Outcome).ToList();
        if ((outcomes.FirstOrDefault(verdict => Holds(state, verdict.Outcome!)) ?? outcomes.FirstOrDefault()) is { } decided)
        {
            return Transition(situation, state, decided.Outcome!, decided.Why);
        }

        if (agent)
        {
            return Start(situation, state);
        }

        return state.Transitions is [{ Outcome: null }]
            ? Transition(situation, state, Next, scripts.Count == 0 ? "nothing to run" : $"{string.Join(" and ", scripts)} passed")
            : new Hold($"{state.Name} ran {string.Join(", ", scripts)} and none of them named an outcome its on knows", null);
    }

    /// <summary>What a release says when a person takes it from the PBI's header (#511).</summary>
    public const string ReleasedByPerson = "a person released the hold";

    /// <summary>Why a release waits for the PBI's run to end (#707).</summary>
    public const string StillStopping = "a run of this PBI is still stopping; release it once it ends";

    /// <summary>
    /// A person's <i>Release hold</i> (DESIGN.md, "The one button"): the PBI goes where the
    /// hold leads, its <c>next</c>, exactly as when the pull request is approved at the vendor.
    /// When the hold leads nowhere, the person chooses the state. An <see cref="Ignore"/> says why
    /// there is nothing to release.
    /// </summary>
    /// <param name="last">The engine's latest move, hold or release of this PBI.</param>
    /// <param name="chosen">The state the person chose, read only when the hold has no <c>next</c>.</param>
    /// <param name="running">A run of the PBI has not ended yet (#707).</param>
    public static Decision Released(LoopDefinition loop, Pbi pbi, LastTransition? last, string? chosen, bool running = false)
    {
        if (!pbi.Held)
        {
            return new Ignore($"{pbi.Named()} is not in hold");
        }

        // Released now, its end would hold the PBI again on the next pass.
        if (running)
        {
            return new Ignore(StillStopping);
        }

        if ((HoldOn(pbi, last)?.To ?? chosen) is not { } to)
        {
            return new Ignore("this hold leads to no state on its own; choose the state it goes to");
        }

        return ReleaseTo(loop, to, ReleasedByPerson);
    }

    /// <summary>
    /// A person's <i>Move to…</i>: only from a state the loop does nothing in, such as <c>done</c> or
    /// <c>needs-refinement</c>, or from no state at all. Anywhere else the loop is at work, and the
    /// header offers no move (DESIGN.md, "The one button").
    /// </summary>
    public static Decision MovedByPerson(LoopDefinition loop, Pbi pbi, string to)
    {
        var from = pbi.State ?? "no state";
        if (pbi.State is { } state && loop.States.Any(declared => declared.Name == state))
        {
            return new Ignore($"{state} is a state the loop runs, so the loop moves the PBI from there");
        }

        if (!Known(loop, to))
        {
            return new Ignore($"{to} is not a state of the loop {loop.Name}");
        }

        return to == pbi.State ? new Ignore($"the PBI is already in {to}") : new Move(to, $"a person moved it from {from}", VendorStep.None);
    }

    /// <summary>What a request for changes on the proposal says in the journal (#512).</summary>
    public const string ProposalChangesRequestedByPerson = "a person requested changes on the proposal";

    /// <summary>The tab a state names with <c>tab:</c> when a person decides its proposal there.</summary>
    public const string ProposalTab = "proposal";

    /// <summary>
    /// A person's <i>Request changes</i> on the Proposal tab (#512) (DESIGN.md#the-screens): the PBI goes
    /// to the state's <c>back</c>, where the coder rewrites the proposal reading the review. Only from a
    /// state whose <c>tab</c> is the proposal's, held or not; a hold comes off on the way, as a release does.
    /// </summary>
    public static Decision ProposalChangesRequested(LoopDefinition loop, Pbi pbi)
    {
        if (StateOf(loop, pbi) is not { Tab: ProposalTab } state)
        {
            return new Ignore($"changes are requested on a proposal in a state with tab: {ProposalTab}, and {pbi.State ?? "no state"} is not one");
        }

        if (state.Back is not { } back)
        {
            return new Ignore($"{state.Name} declares no back, so a request for changes sends the PBI nowhere");
        }

        return pbi.Held ? new Release(back, ProposalChangesRequestedByPerson) : new Move(back, ProposalChangesRequestedByPerson, VendorStep.None);
    }

    static LoopState? StateOf(LoopDefinition loop, Pbi pbi) => loop.States.FirstOrDefault(state => state.Name == pbi.State);

    /// <summary>The hold on the PBI's current state, or null when its latest transition is not one.</summary>
    static LastTransition? HoldOn(Pbi pbi, LastTransition? last) => last is { Kind: JournalKind.Held } && last.From == pbi.State ? last : null;

    static Decision ReleaseTo(LoopDefinition loop, string to, string why) =>
        Known(loop, to) ? new Release(to, why) : new Ignore($"{to} is not a state of the loop {loop.Name}");

    /// <summary>A state the loop declares, or one of its undeclared targets such as <c>done</c>.</summary>
    static bool Known(LoopDefinition loop, string state) =>
        loop.States.Any(declared => declared.Name == state) || loop.Undeclared.Contains(state, StringComparer.Ordinal);

    /// <summary>The run the engine asked for has an answer, or has none yet.</summary>
    static Decision Ended(Situation situation, LoopState state, AskedRunStatus run)
    {
        if (!run.Found)
        {
            return new Hold($"Runs has no run {run.RunId}, which the loop asked for at {state.Name}", null);
        }

        return run.End switch
        {
            null => new Wait($"run {run.RunId} is in flight"),
            { Failure: RunFailure.Blocked } end => new Hold($"the agent is blocked: {end.Reason ?? "it gave no reason"}", null),
            { Failure: RunFailure.Stopped } end when ReleasedSince(situation, run) => new Note(
                $"the run failed (Stopped): {end.Reason ?? "no reason given"} The hold was released after the stop, so it stays off"
            ),
            { Failure: { } failure } end => new Hold($"the run failed ({failure}): {end.Reason ?? "no reason given"}", null),
            { Outcome: { } outcome } end => Transition(situation, state, outcome, end.Reason ?? "the agent gave no reason"),
            _ => new Hold($"run {run.RunId} ended with neither an outcome nor a failure", null),
        };
    }

    /// <summary>
    /// #707. A person released the PBI after the run was asked for. A release waits for the PBI's
    /// run to end (<see cref="Released"/>), so for a run a person stopped it came after the stop.
    /// </summary>
    static bool ReleasedSince(Situation situation, AskedRunStatus run) =>
        situation.Last is { Kind: JournalKind.Released, At: { } released } && run.AskedAt is { } asked && released > asked;

    static bool Names(LoopState state, string outcome) => state.Transitions.Any(to => to.Outcome == outcome);

    static bool Holds(LoopState state, string outcome) => state.Transitions.Any(to => to.Outcome == outcome && to.Hold is not null);

    /// <summary>Where an outcome leads, through its hold, its <c>max_loops</c> and the <c>pr</c> it declares.</summary>
    static Decision Transition(Situation situation, LoopState state, string outcome, string why)
    {
        var transition =
            state.Transitions.FirstOrDefault(to => to.Outcome == outcome)
            ?? (outcome == Next ? state.Transitions.FirstOrDefault(to => to.Outcome is null) : null);

        if (transition is null)
        {
            return new Hold($"{state.Name} produced {outcome}, which its on does not name: {why}", null);
        }

        if (transition.Hold is { } reason)
        {
            return new Hold($"{reason}: {why}", transition.Target);
        }

        var target = transition.Target!;
        if (Back(situation.Loop, state.Name, target) && state.MaxLoops is { } most && Returns(situation, state.Name) >= most)
        {
            return new Hold($"{SentBack(situation, state.Name, most)}, and {outcome} would send it back again: {why}", null);
        }

        return Enter(situation, state, target, $"{outcome}: {why}", transition.Pr);
    }

    /// <summary>
    /// A move into a state that can only go back, and has gone back as often as it may, holds at
    /// once: its agent would run only to be stopped. That is <c>rebasing</c> on a second conflict.
    /// </summary>
    static Decision Enter(Situation situation, LoopState from, string target, string why, VendorStep step)
    {
        if (
            situation.Loop.States.FirstOrDefault(state => state.Name == target) is { MaxLoops: { } most } into
            && into.Transitions.All(to => to.Target is { } back && Back(situation.Loop, into.Name, back))
            && Returns(situation, into.Name) >= most
        )
        {
            return new Hold($"{SentBack(situation, into.Name, most)}, so {from.Name} does not enter it again: {why}", null);
        }

        return new Move(target, why, step);
    }

    static string SentBack(Situation situation, string state, int most) =>
        $"{state} has sent this PBI back {Returns(situation, state)} of {most} times (max_loops)";

    static Decision Start(Situation situation, LoopState state)
    {
        LoopAgent? role = null;
        if (state.Agent is { } name && !situation.Loop.Agents.TryGetValue(name, out role))
        {
            return new Hold($"{state.Name} names the agent {name}, which the loop does not declare", null);
        }

        // A state that names no agent runs with the folder's (#546), and the folder's model goes with
        // it, because a model belongs to an agent. A model the state names still wins.
        var fallback = role is null && state.Provider is null ? situation.FolderAgent : null;
        var provider = state.Provider ?? role?.Provider ?? fallback?.Provider;
        if (provider is null)
        {
            return new Hold($"{state.Name} names no agent, and its folder has no default agent", null);
        }

        if (state.Command is not { } command)
        {
            return new Hold($"{state.Name} names an agent and no command for it", null);
        }

        IReadOnlyList<string> outcomes = state.Transitions is [{ Outcome: null }]
            ? [Next]
            : [.. state.Transitions.Select(to => to.Outcome!)];

        return new StartRun(provider, state.Model ?? role?.Model ?? fallback?.Model, Command(command, situation.Pbi.Ref.Id), outcomes);
    }

    /// <summary>The command as the loop names it, with the PBI it is about: <c>/aio:implement 505</c>.</summary>
    public static string Command(string command, string pbi) => $"{command} {pbi}";

    /// <summary>Going back is reaching a state declared at or above <paramref name="from"/>: the file reads top to bottom.</summary>
    public static bool Back(LoopDefinition loop, string from, string to)
    {
        var (at, target) = (Index(loop, from), Index(loop, to));
        return at < loop.States.Count && target < loop.States.Count && target <= at;
    }

    /// <summary>The state's place in the file, or the count of states for one the file does not declare.</summary>
    static int Index(LoopDefinition loop, string name) => loop.States.TakeWhile(state => state.Name != name).Count();

    static int Returns(Situation situation, string state) => situation.Returns.GetValueOrDefault(state);

    // #513. The Review tab's two verdicts, decided like a release: the vendor has the review already,
    // and these say what the loop does with it.

    /// <summary>What the journal says when a person approves the pull request from the Review tab.</summary>
    public const string ApprovedByPerson = "a person approved the pull request";

    /// <summary>What the journal says when a person requests changes from the Review tab.</summary>
    public const string ChangesRequestedByPerson = "a person requested changes on the pull request";

    /// <summary>
    /// An approval written at the vendor: the hold goes where it leads, its <c>next</c>, exactly as
    /// the engine does when it sees the approval on its own. A hold that leads nowhere, or no hold,
    /// leaves the loop where it was.
    /// </summary>
    public static Decision Approved(LoopDefinition loop, Pbi pbi, LastTransition? last)
    {
        if (!pbi.Held)
        {
            return new Ignore($"{pbi.Named()} is not in hold, so the loop goes on by itself");
        }

        if (HoldOn(pbi, last)?.To is not { } to)
        {
            return new Ignore("this hold leads to no state on its own; Release hold above asks where it goes");
        }

        return ReleaseTo(loop, to, ApprovedByPerson);
    }

    /// <summary>
    /// A request for changes written at the vendor: a held PBI goes to its state's <c>back</c>, where
    /// the coder reads the review on its next run. One the loop is working on stays where it is, and
    /// the review waits on the pull request for whoever reads it next. A state without <c>back</c>
    /// sends the PBI nowhere, and the Review tab offers no button there.
    /// </summary>
    public static Decision ChangesRequested(LoopDefinition loop, Pbi pbi)
    {
        if (StateOf(loop, pbi)?.Back is not { } back)
        {
            return new Ignore($"{pbi.State ?? "no state"} declares no back in the loop {loop.Name}, so a request for changes sends the PBI nowhere");
        }

        if (!pbi.Held)
        {
            return new Ignore($"{pbi.State} is not in hold, so the loop is at work and the review waits on the pull request");
        }

        return new Release(back, ChangesRequestedByPerson);
    }
}
