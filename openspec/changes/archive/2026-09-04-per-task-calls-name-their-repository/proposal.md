## Why

`decb2e3` put the repository into a mirrored task's identity, so `#272` is now genuinely two rows —
24 open issues of this repository are in the inbox and **19 of them share a number** with the other
repository in the scope. Seven per-task calls never learned. Against `0431f09`:

| File | How it finds the row | What it does with it |
| --- | --- | --- |
| `GetTaskComments.cs:77` | `task.Key == query.Key` | reads the thread |
| `GetTaskLabels.cs:62` | `task.Key == query.Key` | reads the palette |
| `GetTaskAssignees.cs:63` | `task.Key == query.Key` | reads the assignees |
| `SetTaskLabels.cs:77` | `task.Key == command.Key` | **writes** labels |
| `SetTaskAssignees.cs:77` | `task.Key == command.Key` | **writes** assignees |
| `SetTaskStatus.cs:136` | `task.Source == kind && task.Key == …` | **writes** the loop label |
| `SetTaskHold.cs:118` | `task.Source == kind && task.Key == …` | **releases the hold** |

The first three do not even filter by source, so `#271` the issue and `#271` the pull request are
candidates of one query. The last two filter by the pair `decb2e3` declared insufficient.

The guarantee is written beside the defect and defeated a line above it — `SetTaskLabels.cs:100`
passes `row.Url` with the comment *"The row's own URL, so this cannot write to another repository's
issue of the same number"*. The URL is the row's. The row is whichever one `FirstOrDefaultAsync`
reached over a number.

Reading the wrong row is recoverable. Writing to it is not, and four of the seven write.

## What Changes

- Each of the seven resolves its row by **source, repository and key**, with the rule `GetTask`
  already follows: the caller may name the repository, the comparison ignores case, a key absent
  from the named repository is a **404 rather than the other row**, and a caller that names none
  still gets the same answer every time.
- The resolution is written **once** and read by the seven, the way `TaskIdentity` was written once
  for the sync's two comparisons. Seven copies of a lookup are seven chances to fix six of them.
- `GET /api/tasks/{key}/comments`, `/labels` and `/assignees` take `?repository=`, as `GET
  /api/tasks/{key}` does. The four writes take it in the body they already post.
- The frontend hooks for those seven carry the repository **in the query cache key as well as the
  request** — `useTask` already does, for the reason written above it — and their callers pass it:
  the inbox rows carry `url`, and a worktree carries its remote.
- `MergeGate.tsx:325` names the repository too. It reads a task by key alone to parse the Seam a
  worktree's diff is compared against, with the folder's remote in hand.
- `SetTaskLabels` and `SetTaskAssignees` **start using the `Source` they already accept**, rather
  than dropping it: two vendors may use the same key (`sync-the-mirror`), so the source narrows as
  well — and a parameter that looks like it disambiguates while being ignored is the more misleading
  of the two states it could be in.

## Capabilities

### New Capabilities

None. This is the addressing `task-details` and `task-status-and-holds` already describe, applied to
the row and not only to the URL.

### Modified Capabilities

- `task-details`: the three reads and the two writes name the repository and resolve the row by it.
  The existing requirement that a write is *addressed* with the row's own URL stays and stops being
  the whole story — the right URL read off the wrong row is still the wrong issue.
- `task-status-and-holds`: the status write and the hold release take the repository in their body.
  Every guarantee about the six states, the precedence and the write order is unchanged; what
  changes is which row is found. A hold released on another repository's issue of the same number
  lets an unread change through, which is the one thing that label exists to prevent.

`task-inbox` is untouched: `GetTask` already takes a repository and already orders its candidates,
and this change copies that rule rather than altering it. `sync-the-mirror` is untouched: identity
and reconciliation are `decb2e3`'s and are what this builds on.

## Impact

**Backend.** `Modules/Tasks/Features/Inbox/UseCases/`: the seven files above, plus one shared
resolution they all call. No domain change, **no migration** — `Repository` has been a column since
`decb2e3`, so this change does not own the Tasks module's migrations and blocks nobody who needs
them.

**Frontend.** `shared/http/queries.ts` (seven hooks and their cache keys — task block only, rule 3),
`features/tasks/TasksScreen.tsx`, `features/runs/RunScreen.tsx`, `shared/tasks/holds.ts`,
`features/folders/MergeGate.tsx`.

**Tests.** The Tasks functional tests for those seven gain the two-repositories case: today a test
that mirrors one repository passes whichever row the query returns, because there is only one.

**Not affected.** `BuildingBlocks/World` — `ITaskSource` already receives the row's URL for every
per-task call, and `VendorRepository` already exists. `World.cs` is untouched.
