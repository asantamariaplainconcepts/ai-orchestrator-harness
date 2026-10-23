## Context

The merge is one of the most-developed seams in the product. On master (`89e7ce5a`) the chain is:

- `IPullRequestMerge.Squash(path, number, expectedHead, ct)` — squash and nothing else, read-verify-
  observe, `MergeOutcome(Failure, ConfirmedByObservation)`, nothing retried. GitHub adapter
  `GhPullRequestLookup`. Registered in `World.cs:150`.
- `MergeChange.cs` — `POST /api/folders/{id}/merge`, body `{ WorktreeId }`,
  `[Requires(WorkspacePermissions.ManageWorktrees)]`. Handler order: folder → worktree → unarchived
  change (guard 1) → dirty tree (guard 2) → `MergeGrantAdmission.Ask` → reserve effectId → find PR
  → squash → settle journal → stop environment → remove worktree → remove branch → notify.
- `MergeGrantAdmission.Ask` — resolves the PBI identity from the folder's remote + the worktree's
  branch, reads the control registry for a live grant, and answers `Missing` / `CandidateChanged` /
  admitted with an `ExpectedHead`. Validates grant + candidate/head + (via the handler) tree +
  archive. **Does not read `status:holding`.**
- The loop's `merge` bash node (`.harness/issue-loop.yaml:264`) runs `.harness/scripts/merge` —
  squash with `--match-head-commit`, read-verify-observe, deletes the remote branch. Three callers
  and one copy: the loop node, `/aio:sync`, the app's button.
- `MergeEffectId.Derive` — SHA-256 over the canonical PBI identity fields + folderId + operation,
  folded into a Guid. The journal's `Reserve`/`Settle` use it for idempotent re-entry. Epic #448's
  decision; **not touched by this change.**

What master does **not** have, and the refine (`ac3b391d`, re-verified against `89e7ce5a`) named as
three gaps:

1. **The hold gap.** `/aio:sync` refuses if the PR "carries `status:holding`" (its own words,
   `sync.md:11`), but the *server* that the button hits does not — `MergeGrantAdmission.Ask` reads
   the registry's grant and the registry's candidate, and never reads the PBI's position/hold. The
   acceptance criterion *«servidor valida… holds»* is unmet by the server.
2. **The scope gap.** The use case is keyed by `WorktreeId`. The journey's `done` step (which
   `PbiSpine` now draws, #463) has no merge entry addressed by the PBI's identity — only the
   worktree's `ChangeTab` does, via `MergeChangeDialog`.
3. **The presentation gap.** `MergeChangeDialog` is a worktree-screen control. The journey's final
   step is where the epic (#447) wants the merge offered, with state and block motive visible.

The hold is already readable: `IPbiSource.GetPosition` returns `PbiPositionRead(Position, Held,
Reason, Consumed)`, and the GitHub adapter answers it. The seam needed to close gap 1 exists.

The dependencies are OpenSpec changes on their branches: #451 (`origin/change/451`, the `Loop` view
where the merge appears as a step), #453 (`origin/change/453`, `ISession`/`IEnvironment` that the
close-environment step consumes by contract), #455 (`origin/change/455`, the publication frontier —
*«Publication SHALL NOT merge a PR, bypass policies, remove a Hold or change issue lifecycle
status»*). `docs/redesign.md` is not on master at `89e7ce5a`.

## Goals / Non-Goals

**Goals:**

- Close the hold gap: the server refuses a merge when the PBI it resolves to carries an active
  `status:holding`, read through `IPbiSource.GetPosition` before the vendor request.
- Close the scope gap: the merge is launchable from the journey's `done` step by the PBI's identity
  (source, repository, key), with the worktree as the launcher's resolved detail.
- Close the presentation gap: the merge is offered by the `done` step of `PbiSpine` with state and
  block motive, not only by the worktree's `ChangeTab`.
- Preserve the seam, the effect identity, the journal discipline, the read-verify-observe merge, and
  the loop's `merge` node untouched.

**Non-Goals:**

- No new merge method, no fast-forward, no merge-commit. `Squash` stays the only operation.
- No rewrite of the loop's `merge` node or `/aio:sync`. The two-implementations-of-one-queue
  assumption `MergeChange.cs` already records stays as an assumption.
- No change to `MergeEffectId.Derive`, `IEffectsJournal.Reserve`/`Settle`, or the grant/candidate/
  head admission. The hold is a new refusal path **beside** those, not in place of them.
- No publication, no verdicts, no resolve-thread — the frontier with #455 holds.
- No new session/environment seam — #453's are consumed by contract.
- No new PBI journey or global view — #450/#463's `PbiSpine` and #451's `Loop` view are the surfaces
  this change composes into.

## Decisions

### 1. The hold is read through `IPbiSource.GetPosition`, refused inside the admission

`MergeGrantAdmission.Ask` already resolves the `ControlPbiIdentity` (provider/host/repository/key)
from the folder's remote and the worktree's branch. That identity is exactly what
`IPbiSource.GetPosition(key, url, ct)` takes. The admission SHALL call `GetPosition` with the
resolved identity before the grant lookup, and SHALL refuse with a typed `HeldByPbi` refusal when
`PbiPositionRead.Held` is true — a refusal that names the remedy (*«release the hold on the PBI
before merging»*) in the handler's sentence, the way every other refusal does.

**Why beside the grant and not above it.** The grant answers *«who approved which head»*; the hold
answers *«may this PBI advance at all»*. They are orthogonal — a valid grant on the right head with
an active hold is a merge that must not happen, and reading the hold first short-circuits the
grant/vendor round-trips a hold forbids. Reading it inside the admission keeps every refusal behind
one typed answer (`Answer.Refusal`), so the handler's `switch` stays the single place sentences are
written.

**Alternative rejected: read the hold in the handler.** That would put a vendor read in the handler
before the admission, and the admission's typed answers would no longer be the whole story — a second
refusal vocabulary would grow beside them. The admission was factored out so the handler reads one
question with four answers; a fifth answer keeps that shape.

**Alternative rejected: read the hold from the snapshot.** The snapshot is emptied after every write
(`pbi-status-and-holds`): the position read polls the vendor. A hold read off a stale snapshot is
exactly the bug the hold exists to prevent. `GetPosition` is the vendor read.

### 2. The PBI-keyed entry is a second route, not a replacement

`POST /api/folders/{id}/merge` stays — the worktree's `ChangeTab` still launches it, and the loop's
node runs the script that does not call HTTP at all. A second entry, addressed by the PBI's identity,
SHALL exist so the journey's `done` step launches the same operation: the worktree is resolved from
the PBI's identity and the folder, used for the merge, and cleaned up after. The permission
(`WorkspacePermissions.ManageWorktrees`) is unchanged.

**Why a second route and not a parameter.** The folder-scoped route's path identifies the folder and
the body identifies the worktree — two facts the worktree screen has. The journey's step has the
PBI's identity and the folder it is running in; the worktree is what the launcher resolves. Making
the worktree optional on the folder route would collapse two contracts into one shape that serves
neither cleanly, and a second route keeps each entry's body saying what its caller actually has.

### 3. The presentation composes `MergeChangeDialog` into the `done` step

`MergeChangeDialog` already renders the confirmation, the server's refusals (via `explain()`), and
the landed/result state. The `done` step of `PbiSpine` SHALL compose that dialog as its action,
offered by the step itself, with the step's state drawn from the admission's typed refusals (hold,
grant, head) and the merge's result. The step is where the launch lives; a form above the pane is
what #463 removed and what this change does not bring back.

**Why compose and not move.** `ChangeTab` still offers the merge for the worktree view; moving the
dialog would break that. Composing it into the step reuses the one confirmed-merged renderer and
keeps the server's sentence as the contract on both surfaces.

### 4. The loop node, the script and the assumption are untouched

`.harness/issue-loop.yaml`'s `merge` node runs `.harness/scripts/merge`; `/aio:sync` runs the same
script; the app's button calls `MergeChange`. These are two implementations of one queue, and
`MergeChange.cs` already records that as an assumption for a person to ratify. This change does not
unify them — it closes the server-side hold gap so the button and the journey meet the criterion the
script's `/aio:sync` caller already meets, and leaves the unification as the later slice the
assumption names.

### 5. `docs/redesign.md` is already gone

Verified at `89e7ce5a`: `docs/redesign.md` is not on master. The decisions the epic carried live in
`README.md`, `ARCHITECTURE.md`, the accepted ADRs and the capability specs. The last criterion's
fold-and-retire is satisfied for the decisions this change touches; if the implement step finds a
remaining sole-source reference in a doc, it folds it into the canonical doc before removing it.

## Risks / Trade-offs

- **Hold read adds a vendor round-trip to every merge.** → It is one `gh`/API call before the grant
  lookup, and a held PBI short-circuits the grant and vendor calls it would forbid. The cost is paid
  once per merge, and a merge is not high-frequency.
- **The position read could race the vendor between read and merge.** → The grant's `expectedHead`
  already closes the window for the head; the hold is read in the same admission, immediately before
  the grant, and a hold applied in the residual window is a hold the journal's settle does not carry.
  This is the same residual window the head has, and the same read-verify-observe discipline covers
  it: a merge that the vendor refuses because the world moved is a refusal, not a corruption.
- **Two merge routes could drift.** → Both routes share one handler, one admission and one journal;
  the only difference is how the worktree is resolved (named by the caller vs. resolved from the
  PBI's identity). The admission and the journal are the contract; the routes are two doors into one
  room.
- **The two-implementations-of-one-queue assumption is not ratified by this change.** → It is named
  as the assumption it already is, and the implement step does not unify what the assumption leaves
  open. A later slice can make `/aio:sync` call the use case.
- **Dependency on #451/#453/#455 branches.** → This change reads their contracts and names them as
  dependencies; the implement step is ordered after they merge, the way #451's tasks ordered
  themselves after #450. The contracts are on their branches; this change does not re-decide them.
