## Why

The loop's journey is drawn on a worktree's screen (`JourneyHeader`,
`FolderDetailScreen`) and nowhere else: answering "where is #431 going?"
means knowing which directory its runs work in. The task detail panel has
no journey, and the closed-journey grouping (`ClosedJourneys.tsx`) groups
by `taskKey` alone — no repository — which is the same crossing #431
fixes for destinations.

## What Changes

- The task detail shows the loop's walk for **its own task** — stages and
  transitions, runs active and historical — grouped by full task identity,
  not by working path.
- Stages and transitions are shown **only when observed**: the task's
  known lifecycle is named as what it is, and no node or percentage is
  invented from it.
- Several runs are distinguished — active first, then historical — each
  named with its agent, last activity, state and location when known.
- Execution facts can be shown without a local working path; opening an
  existing terminal or transcript is how observation presents itself, and
  observing never starts a process.
- Disconnection, stale data, confirmed absence and unknown are four
  different states, each named.
- Persisted history stays readable when its environment is gone, and the
  parts that cannot be recovered are declared as unrecoverable.
- Review and execution stay available at the same time (#431's entries
  are not displaced).

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `read-a-run`: the runs-to-task grouping gains identity (already
  #431's join); this change adds the observed-walk reading the journey
  surfaces draw from, and the honesty rules for what is not observed.
- `worktree-is-a-loop`: the question "where does this loop stand" becomes
  answerable from the task, not only from the worktree directory.

## Impact

- `src/frontend/features/tasks/TasksScreen.tsx` — the detail gains the
  observed walk.
- Existing journey components (`journey.ts`, `JourneyHeader.tsx`) —
  reused where possible; any extraction declared before files move.
- Existing session/terminal/transcript components and run contracts —
  reused; identity and temporality tests added.
- Contracts, queries and i18n in owner blocks.
- Depends on #431's identity join; declares any backend extension or
  persistence before writing it.
