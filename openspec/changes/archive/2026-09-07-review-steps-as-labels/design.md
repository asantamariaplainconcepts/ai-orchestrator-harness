## Context

#290 shipped `ReviewStep` — a Workspace SQLite aggregate keyed by `(folderId, workingPath, stepKey)`
with a `Commit` column, read through `GetReviewSteps` (`GET /api/folders/{id}/review-steps`) and
written through `MarkReviewStep` (`PUT /api/folders/{id}/review-steps`). Its four-state freshness
model — done here, done on an earlier commit, no longer comparable, never looked at — was the
central finding: a tick that survives a `git commit` is a false claim wearing the shape of a true one.

#300 sets that aside. The step state moves to `reviewed:*` labels on the vendor issue, written through
`SetTaskLabels` (`POST /api/tasks/{key}/labels`), which already writes vendor-first through
`ITaskSource.SetLabels` and is implemented by all four connectors. The read is the task's `labels`
array, already on `TaskDetail` and `TaskItem`, mirrored through the ordinary poll.

Three things on the branch decide most of this design, and all three already exist:

- **`SetTaskLabels`** (`SetTaskLabels.cs:24`) — takes `key`, `source`, `repository`, `add[]`,
  `remove[]`, resolves the mirror row by source/repository/key, finds the connector, calls
  `source.SetLabels(key, url, add, remove)`, nudges the sync. Its permission is `Access.AnyCaller`.
  It is the write path this change uses for its second use case.
- **`TaskDetail.labels`** (`contracts.ts:423`) — the vendor's labels, less the `status:*` ones
  derived into `status` and `held`. Already returned by `useTask` and already the source
  `useSetTaskLabels` patches optimistically. Reading whether `reviewed:proposal` is present is a
  filter on an array that is already on screen.
- **`taskFor(runsHere, branch)`** (`task.ts:30`) — resolves the issue a worktree is about, from the
  most recent run or from the branch. `ChangeTab.tsx:197` already calls it and feeds the key to
  `useTask`, which loads `task.data`. The labels are already in hand.

## Goals / Non-Goals

**Goals:**

- A marked step is a `reviewed:*` label on the issue, visible in any browser and surviving across
  machines and sessions.
- The Change tab reads its ticks from the task's labels, through the same `useTask` call it already
  makes — no new query, no new endpoint.
- The write goes through `SetTaskLabels`, vendor-first, mirror through poll, exactly as a label typed
  in a browser would.
- The `ReviewStep` aggregate, its migration, both use cases, the `workspace:review` permission, and
  the freshness model are removed — not left as dead code.

**Non-Goals:**

- **Not commit-stale.** A label survives a rebase. This is the trade-off #290 refused and #300
  accepts, and it is said out loud in the issue.
- **Not a verdict.** Five labels say a person looked. The screen deduces nothing from them except how
  many are present.
- **Not per-worktree.** The label is on the issue, which one branch claims. Two worktrees on two
  branches from one issue share the label.
- **Does not replace `status:holding`.** The hold is the vendor's, removed by a person.
- **Does not touch `status:*` labels.** `reviewed:*` is a separate namespace.
- **Does not block merge.** `/aio:sync` stays the merge path.

## Decisions

### The five labels and their prefix

`reviewed:proposal`, `reviewed:code`, `reviewed:tests`, `reviewed:evidence`, `reviewed:app`. The
prefix `reviewed:` is separate from `status:` the way `status:` is separate from kinds. The five
match the five step keys in `ReviewStep.Known` and the five `StepId` values in `ChangeTab.tsx`.

The label prefix is not derived into a typed field on `TaskDetail` the way `status:*` is derived into
`status` and `held`. Deriving it would mean a new field on every task row and a new extraction in the
sync pipeline, for a namespace only the Change tab reads. The Change tab filters the `labels` array
for `reviewed:*` prefixes, which is one `startsWith` per label.

### The write: `useSetTaskLabels` from the Change tab

The mark button calls `useSetTaskLabels` with `add: ["reviewed:<step>"]` or `remove: ["reviewed:<step>"]`,
passing the `key`, `source`, and `repository` that `taskFor` resolved and `useTask` loaded. The
optimistic patch in `useSetTaskLabels` already updates the `labels` array in the list cache, so the
tick appears immediately and reverts on refusal — the same behaviour a label added from the inbox
already has.

No new mutation hook. `useSetTaskLabels` exists at `queries.ts:1305` and is already used by
`TasksScreen.tsx`. The Change tab imports it and calls it with the same shape.

### The read: `task.data.labels` filtered for `reviewed:*`

`useStepMarks` — currently a hook over `useReviewSteps` — becomes a `useMemo` over
`task.data?.labels`, filtering for the `reviewed:` prefix and mapping each to its `StepId`. A step
is present or absent; there is no `since`, no `commit`, no `markedAt`.

`stepsDone` — currently filtering `mark.since === 0` — becomes the set of step IDs present in the
labels. `stepsLeft` derives the same way.

### What leaves the frontend

- `ReviewStepMark` and `ReviewSteps` from `contracts.ts`.
- `useReviewSteps`, `useMarkReviewStep`, and the `reviewSteps` key from `queries.ts`.
- `markSaid` and its freshness wording (`changeStep.here`, `changeStep.behindOne`,
  `changeStep.behind`, `changeStep.adrift`, `changeStep.on`) from `en.ts`.
- The amber/green tone split on the rail glyph: a tick is green, an ordinal is muted, and there is no
  third colour.

### What leaves the backend

- `ReviewStep.cs` — the aggregate.
- `GetReviewSteps.cs` — the query use case and its `GET /api/folders/{id}/review-steps` route.
- `MarkReviewStep.cs` — the command use case and its `PUT /api/folders/{id}/review-steps` route.
- `WorkspaceDbContext.cs` — `DbSet<ReviewStep>` and the `Entity<ReviewStep>` block.
- `20260905220723_ReviewStepsAreWorktreeState` — the migration, its `.Designer.cs`, and the snapshot
  entity. The migration is dropped, not reversed: the table existed on #290's branch and never shipped
  to `master`.
- `WorkspacePermissions.cs` — the `Review` constant.
- `WorkspaceModule.cs` — the `workspace:review` grant to `ProjectRole.Member`.
- `GetFolder.cs` — `FolderErrors.NoSuchStep` and `FolderErrors.StepNotComparable`, the two error
  factories #290 appended.

### The rail's tick/ordinal and the panel header's control

The rail row keeps its shape: an ordinal circle that becomes a tick when the step is marked. The
tick is success-green; the ordinal is muted-foreground. There is no amber state, because there is no
freshness to warn about. The second line of the row carries the hint until the step is marked, then
reads "marked" — one word, not a sentence about commits.

The panel header's control keeps its shape: one button that toggles the mark. The label is "Mark as
read" / "Mark as unread" / "Marking…". The call is `useSetTaskLabels`, not `useMarkReviewStep`.

### What leaves the test suite

- `ReviewStepTests.cs` — the functional test for #290's row-and-commit model. The model is gone.
- `RailExpiryJourneyTests.cs` — the end-to-end test about the rail expiring after a commit. The
  expiry is gone.
- `AnnotationTests.cs` — the annotation referencing `ReviewStep.cs` in its expected diff is updated
  to reflect the deletion.

## Risks / Trade-offs

- **A label can lie.** `reviewed:code` stays after a rebase, and the screen draws it the same. →
  Accepted. The issue says it, the issue's body says it, and the trade-off is recorded.
- **The label is per-issue, not per-worktree.** Two worktrees on two branches from one issue share
  the label. → The same trade-off the branch-as-claim already makes. The label says "somebody read
  this step of this issue," not "somebody read it in this directory."
- **The optimstic patch in `useSetTaskLabels` updates the list cache, not the detail cache.** →
  `ChangeTab` reads from `task.data` (the detail cache, via `useTask`), not from the list. The
  `onSuccess` handler in `useSetTaskLabels` invalidates the list cache but not the detail cache. The
  fix is to also invalidate `["tasks", "by-key", key, repository]` on success, or to patch it
  optimistically the same way the list is patched. The latter is cheaper and matches the existing
  pattern — `useSetTaskHold` already patches the detail cache.
- **No new `World` seam.** → `ITaskSource.SetLabels` is the write; the mirror is the read. Both exist.

## Migration Plan

The migration is a deletion, not a schema change. The `review_steps` table existed on #290's branch
and never shipped to `master` — #290's migration (`20260905220723_ReviewStepsAreWorktreeState`) is
removed along with the aggregate, so the table is never created. A database that ran #290's branch
has an orphan table; dropping it is harmless and is not this change's concern, because no code reads
it.

Rolling back is restoring the deleted files. The migration is only ever re-created if #290 is re-run,
which it is not — #300 supersedes it.
