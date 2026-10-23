## Context

Measured on master c0304a72 (propose time). What draws a journey today:

- `JourneyHeader.tsx` — the walk for **one worktree**: `journeyFor`'s one
  resolution against the checkout's loop declaration (`journey.ts`,
  `readLoop`), drawn above the Change strip on `FolderDetailScreen`. It
  needs the directory, the loop file, and the runs scoped to it.
- `ClosedJourneys.tsx` (#394) — closed walks for a checkout's gone
  worktrees, grouped by `taskKey` alone (`dead.get(run.taskKey)`), runs
  sorted by `startedAt`, command words rather than the declaration.
- `useRuns(folderPath)` — the run query every journey surface reads;
  `RunSummary` carries `taskKey`, `workingPath`, `state`, `startedAt`.
- The task detail (after #431) resolves runs by full identity but shows
  entries, not a walk.

What this change adds is the **walk read from the task side**: the same
facts those surfaces draw, grouped by task identity — across the
directories a task's runs worked in — with the honesty rules the issue
names. What it refuses is a second interpretation of the loop: the
journey is `journeyFor`'s answer wherever it is drawn from, or the
observed command words where no declaration can be asked, and never a
lifecycle label dressed as a stage.

## Goals / Non-Goals

**Goals:**

- The task detail answers "where does this loop stand" for its own task:
  stages and transitions that are **observed**, named with their runs.
- Identity grouping: a task's runs across directories, never another
  repository's runs with the same key.
- Four honesty states: disconnected, stale, absent, unknown.
- Persisted history readable without its environment; unrecoverable parts
  declared.
- Reuse: `journeyFor` for a resolved working copy, command words for a
  gone one, existing run tabs and transcripts for observation.

**Non-Goals:**

- A new workflow engine, a parallel state machine, or a new remote
  provider.
- Agent controls (start/stop/cancel) — observation only.
- Recovering logs that were never persisted.
- Displacing #431's entries; review and execution stay beside the walk.

## Decisions

- **The walk is a reading, not a model.** From the task's runs — joined
  by identity — the walk is drawn the way `JourneyHeader` draws it when
  the runs' newest working directory still exists (declaration walk), and
  the way `ClosedJourneys` draws it when it does not (observed command
  words in run order). No third derivation is invented, and the lifecycle
  label the task carries names the lifecycle, never a stage.
- **Grouping is by identity, across directories.** `RunSummary` carries
  `taskKey` and `workingPath`; the group is the task's identity (#431's
  join), and the directories are facts **within** it — a task that
  proposed on one machine and implemented on another shows both, rather
  than a first-match or a single-directory guess.
- **Observed means observed.** A stage appears when a run, a transition
  record or a declared command's own transcript says it happened. A task
  at `status:implementing` with no observed node says the lifecycle and
  says the node is unknown — no percentage, no invented position.
- **Observation does not compute.** Opening the walk reads what exists —
  run summaries, persisted transcripts, the loop file when its directory
  exists. Nothing resumes, provisions or spawns; #434's rules for remote
  connection are the same discipline at a distance.
- **Staleness carries its timestamp.** Disconnected shows the last
  observation and its time, not a guess at current state. Stale data (a
  run summary from before a disconnect) is named stale, not fresh.
- **What is gone is declared.** A run whose transcript was never persisted
  says so; a directory that vanished takes its declaration walk with it
  and the command words remain, which is exactly the split
  `ClosedJourneys` already honours.

## Risks / Trade-offs

- **Cross-directory grouping may need the run contracts to carry more
  identity** (source/repository per run row). If #431's join work added
  it, this reads it; if not, this change declares the extension — measured
  against `ListRuns`' projection — rather than string-matching a
  repository out of branch names.
- **The walk's surface is new to the task detail.** The risk is a second
  journey implementation drifting from the first; the mitigation is
  reusing `journeyFor` and the command-word reading verbatim, extracting
  them where they live only with an extraction declared first.
- **Transcript persistence is partial.** The declared limits are the
  honest answer; nothing here retro-fit persistence to runs that never
  had it.

## Alternatives rejected

- Deriving the walk from the task's lifecycle labels: labels are the
  workflow's claim, not the machine's observation — the issue forbids it
  and #361 (the dispatch bug) is the recorded cost of trusting them.
- A parallel state machine reading events into a new model: duplicate
  truth; the fix for drift is one reading, not two.
- Requiring a working path to show anything: a remote or gone environment
  still has observed facts, and hiding them behind a missing directory is
  the failure this change exists to fix.

## Error and temporal consistency

Disconnected, stale, absent and unknown are distinct states with distinct
copies. A late answer for a different task's walk is dropped (the identity
join scopes it). Persisted history answers from the store of record
(`RunLogLine`), and the missing parts are named as missing.

## Verification plan

Each item maps to a delta scenario:

1. Several transitions and runs: the walk is checkable against its
   source (run summaries, transcript).
2. Same key in two repositories: no mixed history.
3. No observed node: no invented node or percentage.
4. Hold, run and connection each show their own state.
5. Disconnection: last observation kept, process not marked finished.
6. Environment withdrawn: persisted history readable, nothing recreated.
7. No PR, or merged PR with an active run: observation available.
8. The local journey (FolderDetailScreen) and run tabs keep their
   entries.

Plus 390×844 both themes, keyboard, and the documented gate at
implementation time.

## Questions to resolve before implementation

- What identity fields does `RunSummary` carry after #431's join, and is
  a `ListRuns` projection extension needed for cross-directory grouping?
- Can `journeyFor` be reused as-is from the task side, or does its
  signature (directory-scoped) need a named extraction?
- Which run facts (agent name, last activity) are already on
  `RunSummary`, and which need the contracts extended?
