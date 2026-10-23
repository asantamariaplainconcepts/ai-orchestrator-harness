## Why

Issue #317. The `/aio:*` loop has no single description, and the repository currently holds **three
disagreeing answers to "what are the states"**: six in `LoopStatus`, nine in `set-issue-status`'s
prose, and four that the commands actually write. `done` is in the enum and no command writes it;
`proposal-review` is documented as *"set by no command"* — a state with no incoming transition.

The transition table is written three times, and the third copy is a week old: #316 added
`LabelLaunchPlan.CommandFor`, a compiled `switch`, because a `BackgroundService` cannot read a
prompt. The WIP limit went the same way — a sentence asking a person to count, and now also a
counter in code. Every correction adds a fourth copy.

ADR-0005 (Accepted) fixed the ownership: GitHub keeps the state, and every checkout computes the
next step from a definition **versioned in the repository**. It deliberately left open whether that
definition ships with the product or is declared per repository. **This change answers that: it is
declared, per repository, and therefore configurable without a release.**

## What Changes

- A repository declares its loop in a new declared file — states with the observable condition that
  recognises each one, transitions with their guards, and the effect each transition applies.
- `LabelLaunchPlan` stops carrying the `switch` and decides by interpreting that definition. It
  stays a pure function, so it stays testable without a socket.
- An absent or unreadable definition makes the watcher **start nothing**. Null never reads as "no
  rules": the opposite reading starts a run per eligible task on one tick, which is the only
  mistake in this loop that stopping it does not undo.
- `staged` and `ship` become one definition and a guard, which is what `WorkflowsSeed.cs` already
  says they are in its own comment — *"the same shape with the holds removed"* — while storing them
  as two separate ordered lists.
- Changing the loop becomes a pull request in the working repository, reviewed like any other,
  rather than an edit to prompts plus a rebuild of C#.
- **BREAKING for nobody**: no endpoint's contract changes, and a person can still start any run by
  hand exactly as today. A repository that declares no machine keeps the manual loop it has.

## Capabilities

### New Capabilities
- `declared-loop-machine`: a checkout declares its own loop — the states, what observation
  recognises each, the transitions and their guards — and the harness obeys that declaration
  instead of a compiled one, refusing to act at all when it cannot read it.

### Modified Capabilities
- `issue-lifecycle`: its requirement that `.ai/commands/aio/` **provides the loop** stops being the
  whole truth. The prompts keep providing the *steps*; the order between them, and the guard on
  each, move to the declared machine. Its requirement that `.harness/config.json` carries the
  loop's tunables gains a sibling file, and the WIP limit stops being enforced only by "the implement
  gate".

## Impact

- **New**: one declared-file seam in `BuildingBlocks/World` with its reader in
  `Infrastructure/Workspace` and a registration in `World.cs` — the fourth declared file, after
  `commands.json`, the workflow plans and `config.json`, with the same bounds and the same reading
  of a malformed file as null.
- **Changed**: `LabelLaunchPlan` (`BuildingBlocks/World`) becomes an interpreter rather than a
  switch; `LabelWatcher` (`AiOrchestrator.Server`) feeds it the definition it read.
- **Not touched**: `status:holding`'s meaning, who removes one, the merge path, the content of the
  `/aio:*` prompts, and every module's HTTP contract.
- **Not opened here**: the branch claim of ADR-0005. The app has never pushed a branch —
  `IWorktreeManager` exposes `Create`, `Ensure`, `Remove` and `RemoveBranch`, and there is no
  `git push` anywhere in `Infrastructure/Git` — so that capability is its own change.
- **Depends on #316** (`label-triggered-launch`), which is where `LabelWatcher` and
  `LabelLaunchPlan` land. This change edits both.
