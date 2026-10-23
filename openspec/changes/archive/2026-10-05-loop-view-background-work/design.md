# Design

## Context

The rail today has exactly two primary links, `PBIs` (the inbox) and `All projects` (the folder
list). The inbox is the only global view, and its protagonist is the form that starts an execution.
The three reads a global "what is the loop doing" screen needs already exist and are already wired:

- `usePbis()` → `GET /api/pbis` → `PbiList { pbis: PbiItem[], sourceCounts, kindCounts }`.
  `PbiItem` carries `status` (a `PbiStatus`), `held`, `key`, `source`, `sourceLabel`, `title`,
  `url`, and, where the position cannot be mapped, `status: "unknown"` + `statusReason`.
- `useLiveRuns()` → `GET /api/runs?live=true` → `RunSummary[]`. Each run carries `key`,
  `folderPath`, `workingPath`, `state`, `activity`, `isTransition`, `taskTitle`.
- `useAttention()` → `GET /api/attention` → `AttentionQueue { rows, unknownSources }`. Each
  `AttentionRow` carries `key`, `cause`, `provider/host/repository`, `runId`, `effectId`.

`journeyFor(runsHere, loop)` (`features/folders/journey.ts`) already resolves the declared loop
against the observed runs, and the status maps (`loopOrder`, `statusColor`, `statusIcon`,
`statusLabel`, `command`, `workflowFor`) are exported from `features/pbis/PbisScreen.tsx`, which
`LoopFlow.tsx` already imports. The sidebar already runs a client-side clock (`useNow`/`elapsed`)
for per-row time. None of this needs to be re-implemented; the screen composes it.

#450 (in implementation) makes the PBI detail's journey the centre of the detail and fixes the
row's destination to the PBI-keyed journey. That destination is the contract the Loop row's link
points at.

## Goals / Non-Goals

**Goals:**
- A `Loop` rail link (between `PBIs` and `All projects`) and a `/loop` route opening one screen.
- One row per open PBI, composed from the three reads, joined by the PBI's identity.
- Ordering: attention-required → in-motion → rest; a hold without a live run is not in-motion.
- A folder filter whose counters count the visible set, without touching `GET /api/pbis`'s counts.
- Four named states; a failed/partial read is not presented as a calm empty result.
- A row's link opens the same PBI-journey #450 opens, addressed by identity, not by worktree.

**Non-Goals:**
- Not the PBI's journey-by-stages screen (that is #450's).
- Not the PBI detail, its tabs, or the session tray (#453), or the folder rail (#452).
- Not a scheduler, watcher, new endpoint, new seam, or a second store of the loop's state; read,
  never write. Nothing here starts, stops, resumes, or authorises anything.

## Decisions

1. **One client-side projection composes the three reads.** A `useLoopBoard()` hook in the screen
   feature calls the three existing hooks and joins them in the browser over the PBI's identity:
   source + repository + key. `PbiItem` is the spine (one row per open PBI); `RunSummary` attaches
   to a PBI by full identity (a run's `folderPath` resolves to the repository the PBI names, the
   same join the PBI detail already does — never by key alone, so `#12` in two repositories does not
   attach to the wrong one); `AttentionRow` attaches the same way. Several runs of one PBI fold into
   that PBI's single row as its activity/history. Alternatives rejected:
   - *A new host endpoint that pre-joins the three.* Rejected — it would be a second store of the
     loop's state and a new surface to keep correct; the join is over three reads the client already
     holds and that already agree on identity. #450 establishes the same identity-join at the seam
     for the detail's run read; the global screen needs no second projection of the same question.

2. **Bands are derived, then within a band the vendor's own order is kept.** A PBI's band is:
   attention-required if the attention queue names a block on it (its `cause`), else in-motion if
   it has a run whose `state` is not `done`/`merged`/`failed`, else the rest. A PBI that is held but
   has no such run is attention-required (a hold is a wait, not a motion). Within a band, the
   `PbiItem` order from `GET /api/pbis` is preserved; no re-sort by title or folder.

3. **The visible-set counters are the screen's own math.** `GET /api/pbis` keeps answering
   whole-set `sourceCounts`/`kindCounts` — the spec's stated reason is that a filter that hid every
   row would read "there are none," which the whole-set rule avoids. The Loop screen's counters are
   the count of the rows actually drawn after the folder filter, per band, computed in the
   projection. Nothing is asked of the server to filter by folder; the spec is explicit that this
   must not change the `GET /api/pbis` contract.

4. **The four states are a product of the three reads, not a fourth read.** Each of the three feeds
   has its own loading/error/empty. The screen is not "ready" as a calm result until all three have
   answered; while any is loading, the screen is loading. If any has errored, the screen is in error
   for that feed, with the failure's own words, and it does not present the surviving rows as a
   complete result. The attention queue's `unknownSources` is the partial-read signal: when it is
   non-empty, the attention band is drawn as partially unknown (the queue says so), not as a calm
   empty queue.

5. **The position a row draws is the `PbiStatus`, and the next step is the declared loop's.** The
   row's stage chip is `PbiItem.status` (the Connector's translation, `PbiStatus`), with its reason
   when `unknown`. The "next step or reason it is waiting" text is composed from (a) the declared
   loop's next node for that position — the same maps `PbisScreen` exports — and (b) the attention
   queue's typed cause where one names the PBI. It is not re-parsed from labels.

6. **The row's link is #450's destination, by identity.** A row opens the PBI's journey addressed by
   the PBI's identity (source, repository, key) — the destination #450 fixes in the detail — never a
   link to a `workingPath`. This is a build-time dependency: the screen's link is written against
   #450's destination as it landed, and `tasks.md` orders the screen's wiring after #450 merges. The
   spec names #450's destination as the contract, not a guess.

7. **i18n is one new rail key and one `loop.*` block in `en.ts`.** `nav.loop` goes between
   `nav.pbis` and `nav.allProjects`. A `loop.*` block names the bands, the row's columns and state
   words, and the visible-set counters, all from `en.ts` — no hardcoded copy, per DESIGN.md. The
   three shared files are touched block-appended, next to this screen's own block, per the house
   rule; a neighbour's block is not tidied.

## Risks / Trade-offs

- **Identity join depends on the run's folder resolving to a repository** (the PBI detail's join).
  A run whose folder cannot be attributed to a repository is not attached to a PBI by key alone —
  the spec is explicit it must not be. → Row for such a PBI still draws its position from
  `PbiItem`; the unattributed run is not silently offered as its.
- **#450 not yet merged** means the destination is not yet a route. → `tasks.md` explicitly orders
  the screen open after #450's merge; the rail link and route can land first, the row's link wired
  to #450's destination in the final step. Nothing in this branch presumes #450's exact link shape
  beyond "the PBI's journey, by identity."
- **Three feeds means three loading/error surfaces.** → They are folded into one screen state as a
  product (all three must have answered for a calm result); the screen never reports a partial
  read as a complete, calm result.
- **Visible-set counters could be misread as the whole-set counts.** → They are labelled as the
  drawn rows' own count, per band, and the server's whole-set `sourceCounts`/`kindCounts` are
  left untouched on the wire.

## Migration Plan

None. Frontend-only; no schema, no migration, no new endpoint. The rail link and route can land;
the row link wires to #450's destination when it exists.

## Open Questions

- The exact shape of #450's destination (route + query params addressed by identity) is #450's to
  fix; this change names it as "the PBI's journey, addressed by source+repository+key." The final
  row-link task reads #450's landed destination rather than guessing it.
