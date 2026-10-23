## Why

The merge is a real, working seam — `IPullRequestMerge.Squash`, its GitHub adapter, the `MergeChange`
use case and the loop's `merge` bash node all exist on master, and `/aio:sync` and the worktree
button already share one queue. What is missing is the half the acceptance criteria name and the
refine verified against `89e7ce5a`: the server does not refuse a merge when the PBI the worktree
resolves to carries an active `status:holding`, the loop's `merge` node cannot be launched from the
PBI journey's `done` step because the use case is scoped by `WorktreeId` rather than by the PBI's
identity, and the merge's presentation is a worktree button rather than the journey's final step. This
change closes those three gaps without rewriting the loop node, the squash seam or the effect
identity the epic #448 decided.

## What Changes

- The merge's admission (`MergeGrantAdmission.Ask`) SHALL refuse when the PBI the merge resolves to
  carries an active hold (`status:holding`), read through the `IPbiSource.GetPosition` seam that
  already answers `PbiPositionRead.Held`. A hold is orthogonal to the grant and to the head, and a
  PR approval does not substitute either: the server re-reads the position before the vendor request,
  and a hold that appeared between the PR's approval and the merge's execution invalidates the
  admission. This is the closure of the criterion *«servidor valida… holds»* and is the principal
  change — it is a new refusal path in an existing admission, not a new method.
- The merge use case (`MergeChange`) SHALL accept the PBI's identity (source, repository, key) as the
  entry the journey's `done` step launches, with the worktree as a detail of the launcher that is
  resolved from the identity and cleaned up after the merge is confirmed. The folder-scoped,
  `WorktreeId`-keyed route stays for the folder screen; a PBI-keyed entry exists beside it so the
  journey launches the same operation the loop's `merge` node and `/aio:sync` run.
- The merge's visual presentation moves from `MergeChangeDialog` (a worktree-screen control) to the
  final step of the PBI journey `PbiSpine` already draws — with the step's state, the motive for any
  block, and the launch action offered by the step itself rather than by a form above the pane. The
  worktree's merge button is not duplicated; the journey's step is where the merge is offered.
- `MergeEffectId.Derive`, the journal's `Reserve`/`Settle`, and the read-verify-observe discipline
  of `IPullRequestMerge.Squash` are **not touched** — they are the epic #448 decision and the seam's
  own contract.
- The loop's `merge` node in `.harness/issue-loop.yaml` is **not rewritten**. The coordination
  between `/aio:sync` and the journey's step stays as two implementations of one queue, recorded as
  the assumption it already is.
- `docs/redesign.md` is **not present on master** (verified at `89e7ce5a`); the decisions the epic
  carried live in `README.md`, `ARCHITECTURE.md`, the accepted ADRs and the capability specs. The
  last criterion's fold-and-retire is already satisfied for the decisions this change touches; if
  the implement step finds any remaining sole-source reference, it folds it before removing the doc.

## Capabilities

### New Capabilities

- None. The merge is a seam (`IPullRequestMerge`) and a use case (`MergeChange`) that already exist
  on master; this change shifts the admission and the launch entry rather than minting a capability.

### Modified Capabilities

- `merge-gate`: the admission's hold requirement — the server SHALL refuse a merge when the PBI the
  merge resolves to carries an active hold, read through `IPbiSource.GetPosition` before the vendor
  request. This is the closure of the criterion the existing `merge-gate` spec left to the admission,
  named now as a requirement rather than left to the code.
- `pbi-details`: the journey's `done` step SHALL present the merge as its action with state and
  block motive, rather than the merge living only as a worktree-screen control. The launch is offered
  by the step itself, addressed by the PBI's identity.

## Impact

- **Workspace module** (`MergeChange.cs`, `MergeGrantAdmission.cs`): the admission gains a hold read
  through `IPbiSource` (already injected through `AddWorld()`), refused typed beside the existing
  `Missing`/`CandidateChanged` refusals. The use case gains a PBI-identity entry resolved by source +
  repository + key, with the worktree resolved as the launcher's detail. The folder-scoped route
  stays; the permission (`WorkspacePermissions.ManageWorktrees`) is unchanged.
- **Frontend**: `MergeChangeDialog` is composed into the `PbiSpine` `done` step rather than only into
  `ChangeTab`; the step offers the action, states the block motive from the admission's typed
  refusals, and the worktree's path/branch are the launcher's detail. The three shared files
  (`contracts.ts`, `queries.ts`, `en.ts`) are touched block-appended next to this change's own
  screen; a neighbour's block is not tidied.
- **BuildingBlocks/World**: no new seam. `IPbiSource.GetPosition` is the hold read the admission uses
  — the seam and its GitHub adapter already answer it. `World.cs` is touched only if a registration
  moves; appended at the end of this change's block per the collision rule.
- **Loop declaration**: `.harness/issue-loop.yaml`'s `merge` node and `.harness/scripts/merge` are
  **not edited**. The two-implementation assumption `MergeChange.cs` already records stays.
- **Dependencies**: consumes `ISession`/`IEnvironment` by contract (#453, `origin/change/453` — the
  close-environment step at the merge's end), the `Loop` view where the merge appears as a step
  (#451, `origin/change/451`) and the publication frontier (#455, `origin/change/455` — *publishing
  SHALL NOT merge*). All three are OpenSpec changes ready for implementation on their branches; this
  change reads their contracts and does not re-decide them.
- **Migration**: none expected — the hold read is a vendor read, not a stored fact. If a column is
  needed for the PBI-keyed entry, it belongs to the Workspace module and this issue owns the
  module's migrations while the change is open.
- **ADRs**: no accepted ADR is rewritten. ADR-0005 (the branch is the claim) and the #448 effect-
  identity decision are extended in their consequence, not superseded.
