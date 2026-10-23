## Why

#290 put review step state in Workspace's SQLite, keyed by working directory and commit, so a tick
expired when the branch moved. The trade-off was honesty: a tick that survives a `git commit` is a
false claim wearing the shape of a true one. What that bought was precision; what it cost was
visibility — the state is invisible on the issue itself, does not survive across machines, and is
unreadable by a person who is not running this product.

#288's issue names the trade-off and accepts it: five labels on the issue (`reviewed:proposal`,
`reviewed:code`, `reviewed:tests`, `reviewed:evidence`, `reviewed:app`) through the existing
`SetTaskLabels` write path, read from the task's labels via the mirror. A label survives a rebase,
and that is the cost. What it buys is that the state is on the issue in any browser, for anyone,
on any machine.

Issue: https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/300 — `enhancement`,
`status:ready-for-proposal`.

## What Changes

- **`reviewed:*` labels on the issue replace `ReviewStep` rows in Workspace's SQLite.** A step marked
  from the Change tab is a label written through `SetTaskLabels` (`POST /api/tasks/{key}/labels`),
  which already writes vendor-first through `ITaskSource.SetLabels`. A step unmarked removes the label
  through the same path.
- **The Change tab reads its ticks from the task's labels** — the `labels` array already on
  `TaskDetail` and `TaskItem`, mirrored through the ordinary poll — instead of
  `GET /api/folders/{id}/review-steps`.
- **`ReviewStep`, `MarkReviewStep`, `GetReviewSteps`, the `ReviewStepsAreWorktreeState` migration,
  and the `GET`/`PUT /api/folders/{id}/review-steps` endpoints are removed.** The `ReviewStep`
  aggregate, its `DbSet`, the migration that created `review_steps`, and both use cases are deleted.
  The `workspace:review` permission and its grant are removed: the write goes through `SetTaskLabels`,
  which has its own permission (`Access.AnyCaller`).
- **The four-state freshness model is gone.** A label has no commit dimension: it stays after a rebase
  and the screen draws it the same. The `since` field, the `markSaid` wording, the amber/green tone
  split — all removed. A step is either marked or not, and that is the whole answer.
- **The rail's tick/ordinal, the panel header's mark control, and the footer's count are all preserved
  in shape.** They read from labels instead of rows, and the mark/unmark button calls
  `useSetTaskLabels` instead of `useMarkReviewStep`.

Not breaking on the wire: the `ReviewStepMark` and `ReviewSteps` types are removed from
`contracts.ts`, and the `useReviewSteps` / `useMarkReviewStep` hooks from `queries.ts`. Nothing
existing changes shape — two hooks and two interfaces leave, and the Change tab reads what
`useTask` already returns.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `review-steps-per-worktree`: **superseded.** Its five requirements — the commit-keyed mark, the
  four-state read, the refusal of unknown steps, the remar/replace rule, the "not a verdict" rule —
  are rewritten. The mark is a label on the issue; the read is the task's labels; there is no commit
  dimension and no freshness state. The capability is renamed to `review-steps-as-labels` to say what
  it now is.
- `review-a-change-in-one-tab`: the requirement about the rail and the footer keeps its shape but its
  source changes — "how many steps are done" is read from the task's labels, not from Workspace's
  review-step rows. The "navigate stores nothing" scenario stays verbatim. The "branch moved past"
  scenario is removed: a label does not expire.

## Impact

- `src/modules/Workspace/.../Domain/ReviewStep.cs` — **deleted**.
- `.../Features/Folders/UseCases/GetReviewSteps.cs` — **deleted**.
- `.../Features/Folders/UseCases/MarkReviewStep.cs` — **deleted**.
- `.../Persistence/WorkspaceDbContext.cs` — `DbSet<ReviewStep>` and the `Entity<>` block removed.
- `.../Persistence/Migrations/` — the `ReviewStepsAreWorktreeState` migration, its `.Designer.cs`,
  and the snapshot's `ReviewStep` entity are removed. The migration is dropped, not reversed: the
  table existed on #290's branch and never shipped to `master`.
- `.../WorkspacePermissions.cs` — `Review` constant removed.
- `.../WorkspaceModule.cs` — the `workspace:review` grant removed.
- `.../Features/Folders/UseCases/GetFolder.cs` — `FolderErrors.NoSuchStep` and
  `FolderErrors.StepNotComparable` removed.
- `src/frontend/shared/http/contracts.ts` — `ReviewStepMark` and `ReviewSteps` removed.
- `src/frontend/shared/http/queries.ts` — `useReviewSteps`, `useMarkReviewStep`, and the `reviewSteps`
  key removed.
- `src/frontend/features/folders/ChangeTab.tsx` — `useStepMarks` reads from `task.data.labels`; the
  mark button calls `useSetTaskLabels`; `markSaid`, `stepsDone`, and the freshness wording are
  simplified to "marked or not".
- `src/frontend/shared/i18n/en.ts` — the `changeStep.*` freshness block (`adrift`, `here`, `behindOne`,
  `behind`, `on`) is removed. The `changeStep.mark` / `changeStep.unmark` / `changeStep.marking` stay.
- `src/tests/.../ReviewStepTests.cs` — **deleted**. The test that lives there is about a row in SQLite
  compared against a commit, and that is the thing this change removes.
- `src/tests/.../AnnotationTests.cs` — the annotation referencing `ReviewStep.cs` in its expected
  diff is updated.
- `src/tests/.../RailExpiryJourneyTests.cs` — the end-to-end test about the rail expiring after a
  commit is deleted, because that behaviour is gone.

**No new `World` seam.** `ITaskSource.SetLabels` is the write; `GetTaskLabels` / the mirror is the
read. Both exist and are used today.
