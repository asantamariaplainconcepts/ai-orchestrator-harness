# ADR-0005: The loop's state lives in GitHub; its next step is computed locally

- **Status:** Accepted <!-- Proposed | Accepted | Rejected | Deprecated | Superseded by ADR-NNNN -->
- **Date:** 2026-09-18
- **Deciders:** repository owner; agent in the `/opsx:explore` session on the `/aio:*` loop
- **Tags:** backend, workflow, architecture

## Context

The `/aio:*` loop has no single description. Its state is a set of `status:` labels on a GitHub
issue; its transitions are prose spread across six command prompts. Nothing owns the machine, so
the machine is whatever those prompts happen to agree on — and they do not agree.

**There are three answers to "what are the states", and all three are in the repository today:**

| Source | States | Which |
| --- | --- | --- |
| `LoopStatus` (`MirroredTask.cs`) | 6 | backlog, ready-for-proposal, ready-for-implementation, in-progress, code-review, done |
| `.ai/skills/set-issue-status/SKILL.md` | 9 | the six, plus needs-refinement, proposal-review, blocked |
| What `.ai/commands/aio/*.md` actually writes | 4 | ready-for-proposal, ready-for-implementation, in-progress, code-review |

`done` is in the enum and no command writes it — `/aio:sync` closes the issue instead.
`proposal-review` is documented as *"set by no command"*: a state with no incoming transition.
`needs-refinement` and `blocked` exist only in the skill's prose.

**The transition table is now written three times, and the third copy is new.** It was prose in the
command prompts and prose in `set-issue-status`; #316 added `LabelLaunchPlan.cs`, which re-derives
"which status starts which command" in C# because a background service cannot read a prompt. The
WIP gate went the same way: a sentence in `implement.md` asking a person to count by hand, and now
also `HarnessConfigFile` plus a counter in code. Two enforcements of one number, both claiming to
enforce the file.

**Three states the loop is already in cannot be named.** A task that is eligible but over the WIP
limit is not a state, it is a refused event, so nothing displays it — #271 and #278 sat labelled
ready for thirteen and twelve days, which is the gap #313 was opened to close. A failed step is
`status:holding`, indistinguishable from the hold a reviewer is meant to release. An issue closed
without merging has no state at all.

**Two constraints are already settled and this decision must not break them.** `issue-lifecycle`
requires that *"GitHub owns the state of the work"* and that *"the branch SHALL be the only claim
two worktrees both see"*. Both exist because the harness is local-first and multi-machine: state
held on one laptop is invisible to the person on the other.

**Two mechanisms behave differently than they look.** Adding a label through GitHub's API is
idempotent — two harnesses can read "no label", both write it, and both succeed — so a label cannot
arbitrate a race. Pushing a new branch ref is rejected for the second writer, so it can. Today
`propose.md` creates the branch at step 1 and pushes it at step 5, leaving minutes of agent work
between deciding to act and claiming the right to.

Finally, the product already believes the loop is one graph. `WorkflowsSeed.cs` says so in its own
comment — the `ship` workflow is *"the same shape with the holds removed"* as `staged` — and then
stores the two as separate ordered lists of steps.

## Decision

We will model the loop as a state machine, and fix its ownership as follows.

**GitHub keeps the state.** The `status:` labels on an issue remain the store of record. Nothing
moves the state of record into a local database, because two people running the harness against one
repository would then hold two divergent copies of it.

**Every checkout computes the next step from a definition versioned in the repository.** The machine
is data, not prose: states with the observable condition that recognises each one, transitions with
their guards, and the effects a transition applies. Two harnesses reading the same state and the
same definition reach the same decision, so correctness comes from determinism rather than from
coordination.

**The loop is level-triggered, not event-driven.** Each tick observes the world and decides; it does
not consume a queue of events. A harness that was switched off for three hours recovers by looking,
where an interpreter would have lost what happened while it was down. The decision is a pure
function and the execution of effects is separate from it, which is the shape `LabelLaunchPlan` and
`LabelWatcher` already have.

**The branch is the claim, and it is taken first.** Any transition that starts work pushes the empty
`change/<key>` ref as its first effect; a rejected push means another checkout won the race and this
one does nothing further. The claim moves to the front of the transition rather than the end of the
agent's work.

**A lease announces live work; it does not arbitrate it.** A `bot-working` label says a process is
alive right now — the fact labels alone cannot express, since during `propose` the issue still reads
`ready-for-proposal`. It is not a lock, and it expires: the issue timeline carries when it was
added, and a lease older than its window is ignored and cleared.

**Transitions record what fires them.** A transition is driven by a checkable fact (an exit code, a
merged pull request, an accepted push), by a person, or by an agent's claim about its own work. This
distinction already exists in `WorkflowStepKind` between `Command` and `FolderCommand`, and the
machine carries it per transition so a diagram cannot draw an agent's self-report with the same
authority as a gate's exit code.

This ADR fixes ownership. It does not choose a file format, a library, or a schedule.

## Consequences

- **Positive:**
  - One definition replaces three disagreeing ones, and the WIP number has one enforcement.
  - Decisions become replayable: given the labels at a moment and the definition at a commit, "why
    did nothing start for #271" is a function that can be run again rather than a log line that was
    lost on restart.
  - Waiting-for-room, needs-attention and abandoned become nameable, and the first of the three is
    derivable from facts that already exist.
  - `staged` and `ship` collapse into one graph and a guard, which is what the seed comment already
    says they are.
  - The agent stops deciding what comes next and only does the step, confining non-determinism to
    named nodes with defined exits.

- **Negative:**
  - The machine can validate transitions but not prevent them. A person relabelling an issue in a
    browser is still the state of record, so an illegal move becomes a finding after the fact rather
    than a refusal.
  - Two checkouts on different commits read different definitions and can decide differently.
  - A lease that is never released blocks work, and unlike a stale review mark it blocks silently;
    expiry is required, not optional.
  - The loop becomes a thing to learn. A prompt in markdown is reviewed in a pull request by anyone;
    a machine definition is not as approachable.

- **Neutral:**
  - Three questions are deliberately left open and are settled inside this decision rather than by
    it: whether the harness or the agent runs the gate; whether the definition ships with the product
    or is declared per repository; and, if per repository, whether it is read from the trunk or from
    the working copy. The third only exists if the second is answered "per repository".
  - #316's task 3.6 remains blocked on a separate question — `SetTaskHold` refuses `held: true` by
    design, so applying a hold has no exposed path. Under this decision the hold is an effect of a
    transition, which is what makes that question answerable.
  - Moving the branch push in `propose.md` from step 5 to step 1 is worth doing on its own, before
    any machine exists: it closes a real race between two people today.

## Alternatives considered

- **A control plane that owns the state in the local database, with GitHub as a projection** —
  rejected because two harnesses against one repository become two planes reconciling the same
  issues, and because it contradicts `issue-lifecycle`'s requirement that GitHub owns the state and
  the branch is the only claim two worktrees share.
- **A `bot-working` label as the lock** — rejected because adding a label is idempotent and subject
  to replication lag, so two readers can both see it absent and both succeed at writing it. The
  branch push is already an atomic compare-and-swap and needs no new concept.
- **An event-driven interpreter in the XState sense** — rejected because the transitions this loop
  cares about happen outside the process that would interpret them, and a missed event leaves the
  interpreter's state wrong until somebody notices. Level-triggered reconciliation recovers by
  looking.
- **Leaving the flow in the prompts and fixing the divergences by hand** — rejected because that is
  what produced three state lists and two WIP gates. Each correction is a fourth copy.
- **One machine per workflow, keeping `staged` and `ship` separate** — rejected because the seed
  comment already describes them as one shape differing by holds, so two definitions would be two
  spellings of one graph.

## References

- Related: ADR-0003 (local persistence on SQLite — the same local-first constraint that makes a
  single owning control plane unworkable)
- Specs: `openspec/specs/issue-lifecycle/spec.md`, `openspec/specs/task-status-and-holds/spec.md`
- Issue #313 and PR #316 (`label-triggered-launch`) — the first transition guard written as code,
  and the measurement of the thirteen- and twelve-day gaps
- `docs/ai-orchestator/loop-lifecycle.html` — the graph this decision describes, with its transitions
  typed by what fires them
- `src/modules/Workflows/AiOrchestrator.Modules.Workflows/Persistence/WorkflowsSeed.cs` — the comment
  describing `ship` as `staged` with the holds removed
- Prior art for the shape: Stately / XState's `transition(machine, state, event) -> (state, effects)`
  split between a pure decision and the execution of its effects

## Superseded clauses

- **2026-09-20, superseded by [ADR-0006](0006-shared-control-authority-is-explicit-and-fenced.md),
  accepted same day:** this ADR's claims that ordinary work-branch push establishes exclusive
  ownership, that deterministic evaluation removes the need for coordination, and that lease
  expiry can clear execution ownership. Two pushes to the same commit can both report success
  ("up to date"), which is the observed case ADR-0006's read-back rule exists for. Everything
  else here — GitHub-owned phase, the branch as the visible claim for the manual loop,
  level-triggered observation, pure decisions — stands.
