## Why

Nothing in this product records what a person has already looked at. The only review trace it keeps
is `status:holding` — a boolean on the vendor: either somebody has to act or they do not. There is no
intermediate state, so closing the app loses where you were, and opening a held worktree tomorrow
does not tell you whether you read the diff yesterday.

#289 drew the rail of five steps and left `stepsDone()` answering "none", in one place, deliberately:
*"storing a tick is #290, and a tick this screen invented would be worse than no tick at all."* This
is that change.

And a stored tick that outlives the commit it was put on is the same untruth in a new place. #280 was
about a hold with no age; a tick that survives a `git commit` is a false claim wearing the shape of a
true one, which is worse than having no ticks.

Issue: https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/290 — `enhancement`,
`status:ready-for-proposal`, under epic
[#288](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/288). Blocked by
**#289**, merged at `31d1f24`, which is where the rail, the footer and the tab counter are drawn.

Measured on this branch at `31d1f24`:

| | |
| --- | --- |
| `stepsDone()` | `ChangeTab.tsx:85`, `new Set<StepId>()` — one call site, `changeStepsLeft()` at 94 |
| the tab counter | `FolderDetailScreen.tsx:882`, inside a `.map` over `fixedHere` |
| the Change tab's scope | `scope: "worktree"` (`FolderDetailScreen.tsx:141`) — never a checkout |
| the three-state precedent | `WorkspaceProbe.Since(path, commit)`: `0` here, `>0` behind, `null` **not comparable** |
| the row shape to copy | `CheckResult` — own `DbSet`, unique `(FolderId, WorkingPath, CommandKey)`, `Commit` column |
| Workspace migrations today | exactly one, `20260820164003_InitialSqlite` |

## What Changes

- **A review step's tick is a row in the Workspace schema, keyed by working directory.** One row per
  `(folder, working path, step)`, carrying the commit it was put on and when. Its own `DbSet`, not a
  collection owned by `Worktree`: `FolderProbePoller` reconciles those rows against git and deletes
  what git no longer lists, and evidence a person is about to merge on must not be removable by a
  probe. That is `CheckResult`'s stated reasoning, and it transfers exactly.
- **The tick expires when the branch moves, and it is not deleted.** It is compared, per read, against
  the working directory's head, and the answer is the distance — which is the existing three-state
  answer this product already gives about a check: `0` is *done on this commit*, greater is *done on
  an earlier one*, and `null` is *no longer comparable*, which is what a marked commit looks like
  after a rebase. **A fourth case, not a `false`** — the same house rule as #146, #232 and #283.
- **No second spelling of "cannot answer" is invented.** `since: number | null` is already on the wire
  for a check (`FolderCommand.lastCheck.since`), already documented as three states, and already
  rendered as three sentences in `CommandsTab.tsx`. The review step reuses that shape and that
  wording, rather than adding a third vocabulary beside it and `ReadFromBody`.
- **Never looked at is the absence of a row**, not a stored `false`. Three states stay three.
- **The count is the counter.** `stepsDone()` becomes a hook over the stored rows and counts only the
  steps whose distance is `0`; `changeStepsLeft()` still derives from it, so the tab's counter and the
  footer's progress remain one number. Marking all five and committing takes the counter back to 5,
  and that number is what says whether the expiry is real.
- **Two use cases, one query and one command**, in the Workspace module beside the others: the
  comparison lives in the use case, because it needs the rows and a seam whose job is to leave the
  process does not read a schema. That is the line `GetWorkflowSource` already drew, and it is why
  **no `World` seam is added** — `IWorkspaceProbe.Head` and `.Since` already exist and already say
  exactly this.
- **A new permission, `workspace:review`**, granted to Member. Reading the marks reuses
  `workspace:read`: seeing what has been looked at is seeing the folder.
- **Marking is refused where the mark could never mean anything.** A step key that is not one of the
  five is `Folder.NoSuchStep`; a working directory whose head git will not name is
  `Folder.StepNotComparable`, because a row stored with no commit can only ever read as "cannot
  compare" and storing it would be storing a tick that can never count.
- The rail says it per row, and the panel header says it in words and carries the control that marks
  and unmarks. Nothing is deduced from a tick beyond how many are left: five ticks say a person looked,
  not that the change is good.

Not breaking on the wire: every frontend contract addition is a new type or a new query — no existing
field changes shape, and nothing on `FolderDetail` is touched.

## Capabilities

### New Capabilities
- `review-steps-per-worktree`: a review step is marked against the commit it was marked on, stored per
  working directory, and read back as one of four answers — done here, done on an earlier commit, no
  longer comparable, never looked at — with only the first counting as done.

### Modified Capabilities
- `review-a-change-in-one-tab`: one requirement — *"The change is five ordered steps, drawn as a rail
  and counted in a footer"* — said the rail and the footer **navigate only** and that every step reads
  as not looked at because nothing stores them. That is now false and is rewritten. The three scenario
  titles are kept **verbatim** and stay true: selecting a step still records nothing, an unmarked
  worktree still reads none of five, and the count still has one source. Three requirements before,
  three after.

## Impact

- `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Domain/ReviewStep.cs` — **new** aggregate.
- `.../Persistence/WorkspaceDbContext.cs` — one `DbSet`, one `Entity<>` block, one unique index.
- `.../Persistence/Migrations/` — the module's **second** migration, generated. This change is the only
  one in epic #288 that adds one to Workspace.
- `.../Features/Folders/UseCases/GetReviewSteps.cs`, `.../MarkReviewStep.cs` — **new**, one file each.
- `.../Features/Folders/UseCases/GetFolder.cs` — two error factories appended to `FolderErrors`.
- `.../WorkspacePermissions.cs` — one constant appended; `.../WorkspaceModule.cs` — one grant.
- `src/frontend/shared/http/contracts.ts` — a new interface, appended. Nothing existing changes.
- `src/frontend/shared/http/queries.ts` — one key, one query hook, one mutation.
- `src/frontend/features/folders/ChangeTab.tsx` — `stepsDone` becomes a hook; the rail's ordinal, the
  panel header and one new control. **The five one-line panel slots at 338–342 are not touched.**
- `src/frontend/features/folders/FolderDetailScreen.tsx` — **three lines**: the import, one hoisted
  hook call beside the other derived counts, and the counter at 882. #289 predicted one; the reason it
  is three is recorded below.
- `src/frontend/shared/i18n/en.ts` — a new `changeStep.*` block at the end.
- `src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/` — a new test file. `WorkspaceFixture`
  already drives `Head` and `Since`; no fixture change is needed.

## Assumptions recorded

Written unattended, with nobody to ask. Each is a decision taken here that a person may overturn.

1. **The distance is on the wire, not a three-valued enum.** The ticket asks for three states; `since`
   gives them plus how far, in the shape this product already uses for the same question about a
   check. A screen may round it; the wire does not.
2. **Marking is refused when the head cannot be read**, rather than stored with a null commit the way
   `CheckResult` does. A check is *started* by the product and its row is the only record that it ran,
   so a null commit still carries a fact; a tick with no commit carries none — it can never read as
   "done on this commit". The Change tab is worktree-scoped, so this is git being unable to answer
   about a linked worktree, not an ordinary directory.
3. **Unmarking deletes the row.** Three states stay three, and "never looked at" and "unmarked again"
   are not worth telling apart. It is also the reversible direction: a later slice can add a tombstone,
   where a stored `false` would have to be migrated away.
4. **The marks are not broadcast over `IWorkspaceNotifier` and the query does not poll.** A tick is
   this person acting in this tab, and the tab invalidates its own key on success. The other thing that
   changes the answer — the branch moving — happens in a terminal, and the ticket's own check says
   *reload*. A poller here would be a git process per step per interval for a fact nobody is waiting on.
5. **The server owns the five step keys.** `Folder.NoSuchStep` requires it, and an unbounded key column
   is free-text storage anybody with the route can fill.
6. **D17 — the sidebar's `clean · N steps left` — is not drawn here**, and is reported back rather than
   dropped silently. See `design.md`; the short version is that the sidebar lists every worktree of
   every folder, `ListFolders` runs no git at all today, and `WorkspaceWorktree` carries no head — so
   the count there costs one `rev-parse` per worktree per list, or a second way of deriving the same
   number. Neither is this ticket's, and #289 already left it to the ledger for the same reason.
