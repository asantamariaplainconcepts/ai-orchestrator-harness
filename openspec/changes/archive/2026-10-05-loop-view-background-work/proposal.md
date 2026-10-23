## Why

Today the only global view of work is the inbox (`/pbis`), and its protagonist is the form that starts
an execution. A person who wants to know what is moving in the background — which PBIs need them, which
are running, which wait — has no surface that answers that question without opening terminals or
filling in the execution form. The inbox answers "what is there"; the question "what is the loop doing
right now, across every folder" is unanswered.

## What Changes

- A rail entry `Loop` between `PBIs` and `All projects`, opening a `/loop` route: one global screen, one
  row per PBI — folder, PBI, current position, activity, and the next step or the reason for waiting.
- The screen composes three existing reads — `GET /api/pbis`, `GET /api/runs?live=true`,
  `GET /api/attention` — into one projection, client-side. No scheduler, no watcher, no parallel store
  of vendor state; the screen reads, it never writes.
- Ordering: attention required (holds/failures from the attention queue) first, then work in motion
  (a live run), then the rest. A hold with no live process is not an active execution.
- A folder filter, and counts bound to the visible set: today's `sourceCounts` are computed over the
  whole set and do not react to a filter; this screen's counters count the rows on screen, joined by
  the PBI's identity (key plus repository) so a PBI is never counted twice for its runs or its copies
  of work.
- Four named states: loading, empty, error, and the result. A failed read does not draw zero PBIs as
  a valid result; the attention queue's `unknownSources` is the existing signal of a partial read.
- A row's link goes to the same PBI journey the inbox reaches: the destination is the PBI-keyed one
  that #450 fixes in the PBI detail. #450 owns the destination contract; this work names it as what
  a row must point at, and `tasks.md` is ordered so the screen opens after #450 merges — its
  destination is the contract, not a guess at it.
- The position a row draws is the loop position as the Connector reads it — the `PbiStatus` vocabulary
  from `GET /api/pbis` (the `LoopPosition` of 10 positions plus `unknown`, #449/#461) — and the next
  step comes from the declared loop's nodes via the existing `journeyFor` projection, reused, not
  re-implemented.

## Capabilities

### New Capabilities

- `loop-view`: the background-work screen behind the `Loop` rail entry — its route, its three-feed
  composition, its ordering, its folder filter with set-bound counts, its four states, and the identity
  join that keeps one PBI one row.

### Modified Capabilities

- `app-shell-and-navigation`: the route table gains `/loop` inside the existing shell, and the rail
  gains the `Loop` entry between `PBIs` and `All projects`, carrying the count of the rows that
  need a person.

## Impact

- Frontend: `src/frontend/shared/i18n/en.ts` gets `nav.loop` (between `nav.pbis` and `nav.allProjects`)
  and a `loop.*` family for the row states and the bands; the screen's reads wire the **existing**
  queries — `usePbis`, `useLiveRuns`, `useAttention` — and its visible-set counters are its own
  composition over those rows, not a new endpoint and no change to `GET /api/pbis`'s counts.
- `src/frontend/app/routes.tsx`: the `/loop` route. `src/frontend/app/Sidebar.tsx`: the `Loop` rail
  entry, block-appended beside the existing two, carrying the count of the rows that need a person.
- A new screen under `src/frontend/features/` (the Loop screen): the row, its stage, the per-row
  time, the visible-set counters, the folder filter, the four states, and the row's link to the
  PBI-journey destination #450 fixes.
- Reused, not replicated: `journeyFor` and the status maps (`loopOrder`/`statusColor`/`statusIcon`/
  `statusLabel`) that `LoopFlow` already imports from `features/pbis/PbisScreen` and
  `features/folders/journey.ts`; the sidebar's client-side clock for the per-row time.
- No backend change, no migration, no new seam. The dependency on #450's destination is ordered in
  `tasks.md` (the screen opens after #450 merges), not re-decided here.
