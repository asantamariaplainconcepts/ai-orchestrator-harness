## Context

#289 landed the Change tab at `31d1f24`: a header of facts, a rail of five steps, one panel and a
footer that counts. It stores nothing. `stepsDone()` at `ChangeTab.tsx:85` returns an empty set and
says why in its own doc comment — the product has never been able to record that a person read a
diff, and a tick the screen invented would be worse than no tick.

Three things on the branch decide most of this design, and all three already exist:

- **`IWorkspaceProbe.Since(path, commit)`** answers `0` for "still on it", a positive number for how
  far it has moved, and **`null` for "the commit is not in this repository at all, which is what a
  check recorded before a rebase looks like"**. Its own doc says the rest: *"Null and 0 are the
  difference between 'still true' and 'no longer comparable'. A check whose commit has been rewritten
  away cannot be called fresh or stale; it can only be called unmeasurable, and a screen that rounds
  that to 0 says the tick is current."* That is this ticket's third state, already written down.
- **`CheckResult`** is a row keyed by `(FolderId, WorkingPath, CommandKey)`, in its own `DbSet`
  rather than owned by `Worktree`, and `WorkspaceDbContext.cs:16-21` says why: *"the worktree rows a
  folder owns are reconciled against git, and evidence a person is about to merge on must not be
  deleted by a probe."* `FolderProbePoller` really does delete worktree rows git no longer lists.
- **`GetFolder.cs:225-231`** already loops a directory's recorded checks and asks `Since` for each,
  with the comment *"a folder with five commands is five cheap reads"*. A review step read is the
  same loop with at most five rows.

So there is nothing here that needs inventing, and quite a lot that needs *not* reinventing.

## Goals / Non-Goals

**Goals:**

- A marked step survives the app closing, and is scoped to one working directory.
- A marked step stops counting as done the moment the working directory's head moves, without being
  deleted — the row says it was put on something else.
- A commit git no longer has is a fourth answer, distinct from both a tick and a blank.
- One number: the tab's counter and the footer's progress keep reading one source.
- No new `World` seam, and no line in `World.cs`.

**Non-Goals:**

- **Not a verdict.** Five ticks say a person looked; the screen deduces nothing from them except how
  many are left.
- **Not a hold.** `status:holding` is the vendor's and a person removes it. Nothing here touches it.
- **Not synchronised.** This is local state on this machine, like everything else Workspace stores.
- Not the sidebar's `N steps left` — see the last decision.
- Not the five step panels. Those are #291–#295, and this change does not touch the five one-line
  slots at `ChangeTab.tsx:338-342` that keep them from colliding.

## Decisions

### The row: its own aggregate, keyed by path, holding the commit

```
ReviewStep : Aggregate
  Guid FolderId
  string WorkingPath     // the checkout or one of its worktrees; the path, not the branch
  string StepKey         // one of the five
  string Commit          // non-null, unlike CheckResult.Commit — see below
  DateTimeOffset MarkedAt
  unique (FolderId, WorkingPath, StepKey)
```

Its own `DbSet<ReviewStep>`, mirroring `CheckResult` line for line, for `CheckResult`'s own stated
reason: a tick is evidence a person is about to merge on, and the probe must not be able to delete it.
Keyed by **path** and not by worktree id for the same reason `CheckResult` is: a worktree removed and
recreated at the same path is the same place, and a foreign key into a table the poller rewrites would
take the ticks with it.

*Alternative considered:* a collection owned by `Worktree`. Rejected — `FolderProbePoller` deletes
those rows on a probe, so a bad probe would silently unmark a review. This is the exact hazard
`CheckResult` was moved out for.

**`Commit` is non-nullable here and nullable on `CheckResult`.** A check is *started by the product*
and its row is the only record that it ran at all, so a check with no commit still carries a fact. A
tick with no commit carries none: it can only ever read as "cannot compare", which is a stored value
that can never mean done. So the write is refused instead — `Folder.StepNotComparable`.

### The comparison lives in the query, computed per read, never stored

The response carries `since`, from `probe.Since(workingPath, row.Commit)`, exactly as `GetFolder`
already does for checks. Nothing caches it. A stored freshness is wrong the moment anybody commits,
which is the same argument `GetFolder.cs:212-217` makes about `Branch.state`.

Cost: one `rev-list` per marked step for the one directory on screen — at most five, and only when
the Change tab is open.

*Alternative considered:* store the head on `Worktree` at probe time and compare by equality. Cheaper,
and it would have made the sidebar count free. Rejected twice over: `WorkspaceWorktree` (the World
record the poller receives) carries no head, so adding one changes a seam this ticket is told not to
change; and equality cannot tell "an earlier commit" from "a commit git no longer has", which is the
whole third state.

### Four answers, and the fourth is an absent row

| what the reader sees | on the wire |
| --- | --- |
| done on this commit | a row, `since: 0` |
| done on an earlier commit | a row, `since: n > 0` |
| no longer comparable | a row, `since: null` |
| never looked at | no row |

Only the first counts toward *done*. `stepsDone()` filters `since === 0`, so five ticks and one commit
takes the counter from 0 back to 5 without anything being deleted, and the rail still says the ticks
are there — put on something else.

**No new vocabulary for "cannot answer".** #283 established `ReadFromBody<T> = T | null | Unreadable`
for a value read out of a mirrored body; `since: number | null` was already established for exactly
this question about a check, is already documented in `contracts.ts:143-150` as three states, and is
already rendered as three sentences in `CommandsTab.tsx:73-80`. Echoing the closer precedent is the
point: a third spelling of the same idea is a third place to get it wrong.

### Two use cases, and the permission split

- `GetReviewSteps` — `GET /api/folders/{id:guid}/review-steps?worktree=…`, `[Requires(Read)]`.
  Seeing what has been looked at is seeing the folder.
- `MarkReviewStep` — `PUT /api/folders/{id:guid}/review-steps`, `[Requires(Review)]`, body
  `{ worktree, step, done }`. PUT because marking twice is one row; `done: false` deletes it.

`workspace:review` is minted here and granted to `ProjectRole.Member` in `WorkspaceModule.Add`,
beside `Read`, `RunCommands`, `OpenOnDesktop`, `Configure` and `Chat`. It is its own constant on
`WorkspacePermissions`' own stated rule — the line above each existing one says what it is for, and
none of them says "record that you read something". Member rather than Admin because reviewing what
is on your own machine is the everyday work here.

Both resolve the directory through `WorkingRoot.Resolve`, which honours a named worktree **only when
it is one of this folder's** and answers `NotFound` — not `Forbidden` — otherwise, so probing paths
teaches nothing.

*Alternative considered:* one use case with a `done` flag on a query. Rejected — a read and a write
are not one use case, and the read is the one every open of the tab makes.

### The five step keys are the server's

`ReviewStep.Known` is `proposal, code, tests, evidence, app`, and a key outside it is
`Folder.NoSuchStep`. Without it the column is free-text storage anybody with the route can fill, and
the error code the ticket allocates has nothing to be about. The frontend's `StepId` union stays the
same five; the two lists agreeing is checked by the functional test that marks each of them.

### `stepsDone()` becomes a hook, and that costs three lines in `FolderDetailScreen`

#289 left `stepsDone()` as the single call site to replace and predicted the tab counter would cost
one line. It costs three, and the reason is the rules of hooks: `changeStepsLeft()` is called at
`FolderDetailScreen.tsx:882` **inside a `.map` over `fixedHere`**, whose length is 8 on a checkout and
7 on a worktree. A hook cannot be called there — not stylistically, but because the hook order would
change with the scope. So the call is hoisted:

1. line 28 — the import name changes to `useChangeStepsLeft`;
2. one new line beside `runsHere` / `tasksHere`, which is where this screen already keeps its derived
   counts, and the doc comment there already describes exactly this pattern;
3. line 882 — `changeStepsLeft()` becomes the hoisted `changeStepsLeft`.

All three are single lines in different hunks, and the file is not otherwise touched. The alternative
— a hook returning a thunk so line 882 stays byte-identical — buys one line of diff with a layer of
indirection that exists only to preserve a call site. Reported rather than hidden.

The invariant #289 asked to keep is kept: `changeStepsLeft` still derives from `stepsDone`, both read
one query, and neither counts anything the other does not.

### Where the state is said on screen

- **The rail row.** The ordinal circle becomes a tick when the step is marked — success colouring when
  the mark is on this commit, warning colouring when it is not, because a tick that no longer applies
  must not look like one that does. The second line of the row, which carries the step's hint, says
  the mark's state instead once there is one: after you have marked a step, *"done 3 commits ago"* is
  the more useful of the two sentences. `aria-hidden` stays on the glyph and the state reaches the
  accessible name through that text, as it does today.
- **The panel header.** The sentence in full, plus the control that marks and unmarks — one button,
  never disabled by a fact, matching the header's stated rule that no fact disables an action.
- Colours are `--pc-success-700` / `--pc-warning-700` / `--muted-foreground`, which is exactly what
  `CommandsTab.checkLine` uses for the same three answers. Both are defined per theme; the raw ramp
  trap #289 hit was `--pc-info-100`, which is defined once in `:root`.

Wording follows `folders.check.*` — `on this commit`, `1 commit ago`, `{count} commits ago`, `on a
commit this repository no longer has` — under the `changeStep.` prefix, so the two surfaces that
answer "is this still about the directory in front of me" say it the same way.

### D17 — the sidebar's `clean · N steps left` — goes back to the ledger

#289's ticket named that string and #290's issue says the number comes from here. It is not drawn
here, and the reasoning is measured rather than preferred:

- The sidebar lists **every worktree of every folder**. `ListFolders` runs no git at all today; it is
  a projection over rows. Adding the count means one `rev-parse` per worktree per list request on a
  query the sidebar keeps warm.
- `WorkspaceWorktree` — what the poller receives from the seam — carries no head, so the cheap route
  (compare by equality against a head the poller already had) requires changing a `World` record, and
  this ticket's Seam says no `World` change.
- Deriving "how many are left" a second way is exactly what #289 built one function to prevent.

The number itself now exists and is one query away, so whoever picks this up is adding a call site,
not a concept. Left as a debt with an owner rather than dropped.

## Risks / Trade-offs

- **A `rev-list` per marked step per read.** → At most five, only for the directory on screen, and
  only when the Change tab is open. `GetFolder` already accepted the identical cost for checks and
  wrote down why.
- **A tick and a commit can race**: somebody commits between the mark's `Head` read and its write, and
  the row records the previous commit. → The next read compares and says `1 commit ago`, which is
  true. There is no window in which the row claims more than it should.
- **Marking is refused when git cannot name a head.** A person could see a refusal where they expected
  a tick. → The Change tab is worktree-scoped, so this means git cannot describe a linked worktree —
  and the honest refusal is better than a row that can never read as done. The error carries the path.
- **Existing worktrees have no rows**, so every open change reads as five steps left. → Correct: nobody
  has marked anything, and that is what the screen should say.
- **The frontend's `StepId` union and `ReviewStep.Known` are two lists of the same five.** → A
  functional test marks each of the five and rejects a sixth; a rename on either side fails it.

## Migration Plan

The module's second migration, generated — never hand-written, and its `*.Designer.cs` carries its own
snapshot copy, so it is regenerated as a unit:

```
dotnet ef migrations remove --project src/modules/Workspace/AiOrchestrator.Modules.Workspace
dotnet ef migrations add ReviewStepsAreWorktreeState \
  --project src/modules/Workspace/AiOrchestrator.Modules.Workspace \
  --output-dir Persistence/Migrations
```

Additive: one table, no column altered, nothing backfilled. Rolling back is dropping it; no data
outside it depends on it.

## What was observed on the running app

Not argued from the code. `aspire start --isolated`, a real git repository with two linked worktrees
added as a folder, and the marks driven through the real HTTP API against the real probe.

| check | what was seen |
| --- | --- |
| 1 — marks survive the app closing | Three marked, `aspire stop`, `aspire start`, re-read: all three still there with the same commits |
| 2 — five marks and one commit | `steps left = 0` → one real `git commit` → **`steps left = 5`**, five rows still present, all naming `fe37c04` |
| 3 — the marked commit rewritten away | `since: null`, drawn as *read on a commit this repository no longer has* — neither a tick nor a blank, and it does not count |
| 4 — two worktrees | A mark in `change/290` left `change/289` with its own single mark and the checkout with none |
| 6 — both themes | Screenshot at 1440×900 in each; all four states legible, and the amber measures 5.03:1 on the dark card |

**Two things only running it could show.**

1. **A real rebase does not produce the third state on its own.** `Since` asks `git cat-file -e`
   first, and an amended-away commit is still in the object database — so immediately after the
   rewrite the answer is *"1 commit ago"*, which is true. The third state appeared only after
   `git reflog expire --expire-unreachable=now && git gc --prune=now` actually dropped the object.
   That is what the seam's own doc means by *"a commit git no longer has"*, and it is a narrower
   window than the ticket's wording suggests. Both answers are honest; neither is a tick.
2. **The mark button was labelled with a state instead of an action.** *"Not read yet"* sat beside an
   amber tick reading *"read 1 commit ago"* — the screen contradicting itself in the same box. It
   reads *"Mark as unread"* now. Nothing in the types or the suite could have caught it.

## Open Questions

None outstanding. The six decisions a person may want to overturn are recorded as assumptions in
`proposal.md`, and D17 above is the one piece of scope handed back rather than taken.
