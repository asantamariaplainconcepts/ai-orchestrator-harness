# Design: label-sits-the-proposal

## Context

The watcher never runs the yaml; it reads it as a decision table. `LabelWatcher.Tick` asks `LabelLaunchPlan.For`, which asks `loop.ResumeAt(status, config.ReadyLabel)`: the first node after the last node whose `x-harness.status` names the label the issue carries. At the entry label (`status:ready-for-proposal`, from `.harness/config.json`'s `labels.ready`) the answer is the loop's first node, `refine`. `FirstCommandAtOrDownstream` then walks the dependents of the resume node and seats the first `command` node it finds, in the issue's own worktree (`StartRun` derives `change/<n>` and calls `worktrees.Ensure`).

Two declarations break the chain from refine to propose. `refine` moves no label, so a completed refine leaves the issue at the entry label and the next tick seats refine again. And `claim` moves `in-progress`, which `implement` also moves: `ResumeAt` takes the *last* node that moves the label — `implement` is later in the file — so `in-progress` resumes at `gate` (first command downstream: `retro-entry`), and no label anywhere reaches `propose`.

Since #392 the claim is the live Run, not a label: `LiveRunKeys` reads the runs a folder has going, and the plan skips keys that hold one. The `claim` node's `in-progress` is the pre-#392 spelling kept by inertia.

`LoopStatus` (`MirroredTask.cs:244`) is a six-state enum the inbox derives from labels, not stores — `ListTasks.cs` maps `status:*` labels by precedence, unknown labels render as chips and change nothing. The frontend board (`TasksScreen.tsx`) renders a lane per state.

## Goals / Non-Goals

**Goals:**

- A label exists whose meaning is "the spec is ready: propose", and the loop, reading only the yaml, seats `/aio:propose` on it.
- The chain refine → propose → (hold) → implement is connected by labels alone; a person releases holds, nothing else.
- The board keeps rendering the truth: a seventh derived status, not a dead label.

**Non-Goals:**

- No change to the watcher (`LabelWatcher`, `LabelLaunchPlan`, `ResumeAt`): the mechanism already routes any declared status; what was missing was the declaration.
- No execution of the yaml — `overlap`, `risk` and `gate` remain decision-table rows; the claim stays the live Run, approvals stay the hold.
- No new entry label, no change to `.harness/config.json`: `spec-ready` is a mid-loop status, not the entry.
- No WIP change, no hold change.

## Decisions

**1. `status:spec-ready`, moved by `refine`.** The resume from `spec-ready` is `overlap` (its only dependent is `claim`) → `claim` → `propose`, and `FirstCommandAtOrDownstream` finds `aio-propose` without code changes. No other free status name sits between the entry and `ready-for-implementation` without colliding: `in-progress` is taken twice, and reusing `ready-for-proposal` would make refine's output indistinguishable from its input — the very loop being fixed.

**2. `claim` moves nothing.** The branch push (`change/<n>`) stays as the node's bash body — it is still the cross-checkout claim — but it stops declaring `status: in-progress`. Between checkouts the branch is the claim; within this machine the live Run is (#392); a label that claims nothing and collides with `implement`'s is the residue of the order those two landed in. Removing the declaration changes one behaviour: an issue hand-labelled `in-progress` by a person now resumes at `implement` (the only remaining node that moves it) instead of at `gate` — which is the reading the vocabulary intends anyway.

**3. `/aio:refine` writes the new label on success; `needs-refinement` on gaps, as today.** The command's label write is the loop's input; the refine prompt already ends by moving a label, so it changes which one. `needs-refinement` is moved by `respec`'s cancel and has no dependents — an issue parked there starts nothing, which is the point.

**4. `/aio:propose` gates on `spec-ready`, accepting `ready-for-proposal` as migration.** Issues refined before this change carry `ready-for-proposal` and their spec is done; refusing them would make the change break its own first users. The acceptance is one line in the gate and disappears with the issues it migrates.

**5. `LoopStatus` learns `SpecReady` between `ReadyForProposal` and `ReadyForImplementation`.** Precedence (furthest along wins) needs the enum in loop order to stay the derivation; a status outside the enum would render as a dead chip — invisible on the board, unwritable by `SetTaskStatus`. One enum member, one map entry, one lane, one set of chip properties, one i18n string, one command button (`/aio:propose`, the same word the entry lane's button already uses for its command).

## Risks / Trade-offs

- **Issues mid-flight at `ready-for-proposal` see one extra refine run** before the ladder continues. Accepted: refine is idempotent on a ready issue (it re-checks the DoR and re-applies the new label), and the alternative — a one-off relabelling of open issues — is shared-state churn for a transition this repository can absorb.
- **`in-progress` hand-labelled by a person now resumes at `implement`, not `gate`.** Behaviour change on a label nobody should hand-apply (the loop moves it during implement); the #361 fact-guard still refuses a resumption whose change and branch are missing, so the worst case is a named refusal, not a wrong run.
- **The label must exist at the vendor before refine writes it.** `SetTaskStatus` writes through the connector; a `spec-ready` write against a vendor that refuses unknown labels would fail the label move. The task is instructed to create the label first (as this repository's GitHub already carries it, created with the issue).
- **`spec-ready` is one more state to hold in a person's head.** Traded against the alternative — no state meaning "ready to propose" at all, and a loop that cannot find its own second step.
