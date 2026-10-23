# Proposal: label-sits-the-proposal

## Why

No label means "propose this". An issue at the loop's entry label `status:ready-for-proposal` resumes at `refine`, and `refine` declares no `x-harness.status`, so the next tick seats `/aio:refine` again: the loop runs refine forever and a person moves every label by hand. And `claim` and `implement` both declare `in-progress`, so `ResumeAt` — which takes the last node that moves a label — routes `in-progress` to `gate`, never to `propose`. Measured on `master` following `DeclaredIssueLoop.ResumeAt` (`DeclaredIssueLoop.cs:78`) over `.harness/issue-loop.yaml`: the issue a person wants proposed has no label the loop can act on.

## What Changes

- `.harness/issue-loop.yaml` — `refine` gains `x-harness: { status: spec-ready }`: refine completed means the spec is ready. `claim` stops moving `in-progress` (the claim since #392 is the live Run, not a label). The resume from `spec-ready` is `overlap` → `claim` → `propose`, and the watcher's `FirstCommandAtOrDownstream` already seats `aio-propose` from there: declaring the hop is the whole change.
- `.ai/commands/aio/refine.md` — where refine succeeds it applies `status:spec-ready` instead of re-applying `status:ready-for-proposal`; gaps still go to `status:needs-refinement`.
- `.ai/commands/aio/propose.md` — the gate requires `status:spec-ready`, accepting `status:ready-for-proposal` as the migration for issues refined before this change.
- `LoopStatus` and the inbox derivation (`MirroredTask.cs`, `ListTasks.cs`) learn `spec-ready` as a seventh state between `ReadyForProposal` and `ReadyForImplementation`, so the board renders the new lane and `SetTaskStatus` can write it; `TasksScreen.tsx` adds the column, its icon, colour and command button (`/aio:propose`), and `en.ts` its copy.

The ladder becomes: `ready-for-proposal` (refine) → `spec-ready` (claim + propose) → `ready-for-implementation` (waiting on the hold) → implement → gate → land.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `run-the-declared-loop`: the resumption requirement gains the entry hop — a task whose label names a node's completed status resumes at that node's dependents, and `spec-ready` (moved by `refine`) routes to propose rather than to refine again. The `claim` node stops declaring a status.
- `task-status-and-holds`: `LoopStatus` names seven states in loop order; `spec-ready` derives from its label like the rest, by precedence.
- `issue-lifecycle`: the six commands requirement records that refine ends on `spec-ready` and propose gates on it.

## Impact

- `.harness/issue-loop.yaml`, `.ai/commands/aio/` — declarations and prompts, symlinked across runtimes.
- `src/modules/Tasks/` — `LoopStatus` enum, label-to-status map, and the status-chip/lane surface that reads it; no migration (status is derived, not stored).
- `src/frontend/` — `TasksScreen.tsx` lane list, `contracts.ts` type, `en.ts` copy (append beside the task screen's block in each).
- No World seam change, no cross-module path, no migrations.
- Issues already carrying `ready-for-proposal` (e.g. #394) get one more refine run before `spec-ready` — not re-entrant by label, absorbed on first completion.
