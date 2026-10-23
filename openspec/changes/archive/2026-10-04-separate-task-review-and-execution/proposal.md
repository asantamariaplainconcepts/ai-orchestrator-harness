## Why

The task detail panel resolves a run by matching `taskKey` alone — the first
run that carries the number wins (`TasksScreen.tsx:2112`) — and review and
execution are not two independent entries: one detail, one destination. With
two repositories in one scope, `#12` is two tasks, and today the panel's runs
lookup scopes only by the folder's path, finds the first run with the same
key, and offers no way to review a PR while an agent works.

## What Changes

- The task detail offers **Review PR** and **Watch execution** as two
  independent entries; both can be available at once.
- The entries reuse the existing remote PR viewer and the existing run tabs
  mechanism; no new destination screens are invented.
- Destinations are resolved by the task's full identity — source, host,
  repository, key — not by number, branch name, or the first run with a
  matching key.
- With several runs, active and historical runs are distinguished and the
  person chooses; no silent first match.
- Loading, confirmed absence, error, and ambiguous association are four
  different states, each named.
- A resolution error does not start a run and does not present itself as
  absence.
- Closing the issue or merging the PR does not redirect or collapse the
  detail into the PR view.
- Unsent-text protections and permissions are respected by both entries.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `task-details`: the detail panel gains two independent destinations —
  review and execution — resolved by full task identity, with run selection
  among active and historical runs. New requirements for the entries
  themselves and for error/absence states.
- `read-a-run`: runs are matched to a task by full identity (source, host,
  repository, key) where they are today matched by task key and working
  path; several runs per task are selectable rather than first-matched.
- `review-a-change-in-one-tab`: a closed issue or a merged PR does not
  redirect the task detail to the PR view; review remains an entry beside
  execution.

## Impact

- `src/frontend/features/tasks/TasksScreen.tsx` — detail panel: two entries,
  identity-based resolution, run selection.
- Existing PR entry points (`PullRequestEntry.tsx`) and run tab/session
  components — reused, not duplicated.
- `src/frontend/shared/http/contracts.ts`, `queries.ts`, and
  `src/frontend/shared/i18n/en.ts` — owner blocks only, appended.
- Frontend and end-to-end tests for navigation and identity.
