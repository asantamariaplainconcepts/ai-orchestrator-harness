## Context

Measured on `aeaa7516c8cfb7698547105fd98004da04a8ca36` (master c0304a72 at
propose time). The task detail panel in `TasksScreen.tsx` opens its runs as
global tabs via `useSessions` (#374) and looks runs up by
`runs.find((run) => run.taskKey === task.key)` scoped to the board's folder
path — the first run carrying the number. Per-task reads
(`GET /api/tasks/{key}`) already accept a repository and resolve by source,
repository and key together (`task-details`), and run records carry
`taskKey` and their working directory. The PR entry
(`PullRequestEntry.tsx`) parses the ref from the task's URL
(`pullRequestFromUrl`) and routes to the remote review screen. Nothing
today ties the two entries to one identity rule, and the runs lookup
ignores source and repository.

Issue #431 carries the intent, its Seam and its check; this design grounds
it in the code above.

## Goals / Non-Goals

**Goals:**

- Two independent detail entries — Review PR and Watch execution — both
  reachable at once.
- Destinations resolved by full task identity: source, host, repository,
  key.
- Several runs per task: active and historical distinguished, chosen
  explicitly.
- Loading, confirmed absence, error and ambiguity as four named states.
- Reuse of the remote PR viewer and the existing run tab mechanism.

**Non-Goals:**

- Withdrawing local history readers — that is #432's work.
- Building the loop journey — that is #433's.
- ACA or new remote controls — that is #434's.
- New destination screens; no second PR viewer, no second run screen.

## Decisions

- **Identity is resolved per entry, independently.** Review availability
  comes from the task's PR ref (as today); run availability comes from the
  runs query filtered by full task identity. One entry's failure does not
  disable the other.
- **The runs-to-task join moves to identity.** Run records already carry
  `taskKey` and working directory; the join adds source and repository from
  the run's own metadata rather than trusting the folder scoping alone.
  Exact fields are inventoried during implementation from the run
  contracts; the rule is that two repositories with the same key never
  cross.
- **No agent start as fallback.** A failed runs lookup shows the error and
  keeps both entries honest; starting a run is a dispatch decision
  (`dispatch-observation` governs it), not a navigation fallback.
- **Run selection is explicit.** With more than one run, the entry offers
  active runs first, then historical, each named — agent, last activity,
  direction (per `read-a-run`'s history ordering). No silent first match.
- **The detail is never redirected by lifecycle.** Closing the issue or
  merging the PR changes facts the detail shows, not which entries exist.
  A merged PR with an active run keeps both entries.
- **Unsent-text protections stay.** Navigating from the detail to either
  destination goes through the same navigation guards the panel already
  has (#430's pending-edit protection is the reference).

## Risks / Trade-offs

- **Run metadata may not carry source/repository today.** If the run
  contracts cannot answer the identity questions, a backend extension
  (contracts + query) becomes necessary. That extension is declared in
  this change's tasks, behind the module boundary, before the frontend
  join is written — rather than patched with string matching.
- **Increased surface for the detail panel.** Two entries with four states
  each is more UI; the mitigation is reusing the remote viewer and run
  tabs rather than building new screens, and reusing the existing
  pending-state patterns (`review-a-change-in-one-tab`'s skeleton rule).
- **The identity join must not slow the board.** The runs query is
  refetched per board today; the join is computed over already-fetched
  data, and any query change must justify itself in measured calls.

## Alternatives rejected

- Resolving by issue number or local path: collides between repositories
  and environments; the exact wrong-run bug this change fixes.
- A single combined review/execution mode: prevents their simultaneous
  use — reviewing while the agent works is the stated intent.
- Inferring run end from issue close or PR merge: mixes facts the system
  does not own; `read-a-run`'s liveness answers it separately.

## Error and temporal consistency

Loading, confirmed absence, error and ambiguity are distinct. A response
started for another task or run cannot replace the current destination. A
late-arriving answer for a stale entry is dropped the way every query on
this screen already drops it. Disconnection keeps the last observation
without asserting current state.

## Verification plan

Each item maps to a scenario in the delta specs:

1. Open PR plus active run: both entries open their correct destinations.
2. Run without PR: execution reachable; PR without run/folder: review
   reachable.
3. Closed issue with active run: the run stays reachable and active.
4. Several tasks with `#12` across repositories: no crossed destinations.
5. Resolution error starts no run and is not presented as absence.
6. Opening views posts no reviews and modifies no Holds.

Plus the transversal checks from the epic: 390×844 both themes, keyboard
reachability, and — at implementation time — the documented gate over the
correct tree with SHA and counts registered.
