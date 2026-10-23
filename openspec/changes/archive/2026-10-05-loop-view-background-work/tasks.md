# Tasks

**Blocks on #450.** The screen's row links to the PBI-journey destination #450 fixes in the detail.
Tasks 1–4 stand on their own (route, rail entry, i18n, the projection); task 5 — the row's link —
and anything that opens the row SHALL NOT land before #450 has merged, so the link points at a
destination that exists rather than one the screen invents. The branch holds until #450 is in
master; the merge of #450 is the precondition for task 5, not a guess at its shape.

## 1. Route and rail entry (app-shell-and-navigation)

- [ ] 1.1 Add the `/loop` route to `src/frontend/app/routes.tsx`, inside the existing shell, resolving to the Loop screen; a direct request for `/loop` SHALL return the application, and moving to it SHALL be the router rather than a page load.
      req: app-shell-and-navigation/1
      files: src/frontend/app/routes.tsx
- [ ] 1.2 Add the `Loop` primary link to `Sidebar` between `PBIs` and `All projects`, pointing at `/loop`, block-appended beside the existing two links; it carries the count of the loop's rows that need a person (attention-required + in-motion bands), and a count of zero draws no badge.
      req: app-shell-and-navigation/2
      files: src/frontend/app/Sidebar.tsx
- [ ] 1.3 Verify by hand with `aspire start`: `/loop` resolves, navigation is not a page load, the link reads active under the loop screen, and the badge is absent when nothing needs a person.
      req: app-shell-and-navigation/2
      files: src/frontend/app/Sidebar.tsx

## 2. i18n (en.ts, block-appended)

- [ ] 2.1 Add `nav.loop` between `nav.pbis` and `nav.allProjects`, and a `loop.*` block for the screen: the three bands (attention required, in motion, the rest), the row's column words, the state words (loading, empty, error, and a partial read's own sentence), and the visible-set counters. No hardcoded copy anywhere in the screen.
      files: src/frontend/shared/i18n/en.ts

## 3. The screen's projection over the three existing reads (loop-view)

- [ ] 3.1 Compose `usePbis()`, `useLiveRuns()` and `useAttention()` in the Loop screen's feature into one row per open PBI, joined by the PBI's identity — source + repository + key, the run's `folderPath` resolved to the repository the PBI names; a PBI is never drawn twice for its runs or copies of work, and a run whose PBI cannot be attributed by identity is not attached to a same-numbered other.
      req: loop-view/2
      files: src/frontend/features/loop/loopBoard.ts
- [ ] 3.2 Draw the bands: attention-required (the attention queue names the PBI) first, in-motion (a run not `done`/`merged`/`failed`) second, the rest third; a hold with no such run stays in attention-required, never in-motion; within a band keep `GET /api/pbis`'s own order.
      req: loop-view/3
      files: src/frontend/features/loop/loopBoard.ts, src/frontend/features/loop/LoopScreen.tsx
- [ ] 3.3 Draw each row: the folder, the PBI's key and title, its stage (the `PbiStatus` as the Connector read it — `unknown` with the vendor's reason, never read as `backlog`), its activity from the live run(s) reusing the sidebar's clock pattern for the time, and the next step or reason for waiting from the declared loop's maps (`PbisScreen`'s exports, reused) or the attention row's typed cause.
      req: loop-view/2, loop-view/3
      files: src/frontend/features/loop/LoopScreen.tsx
- [ ] 3.4 Add the folder filter and the counters over the visible set only; `GET /api/pbis`'s whole-set counts are untouched on the wire, and a filter excluding a folder's rows stops it counting.
      req: loop-view/4
      files: src/frontend/features/loop/LoopScreen.tsx, src/frontend/features/loop/loopBoard.ts
- [ ] 3.5 Name the four states: loading while any of the three reads is on its way; empty only when all three answered and nothing is open; error with the failed read's own words, never as a calm empty result; and the attention queue's `unknownSources` drawn as the partial read they are — never dropped as if the queue were empty.
      req: loop-view/5
      files: src/frontend/features/loop/LoopScreen.tsx
- [ ] 3.6 Verify by hand with two folders — one in a run, one held: the held-without-process PBI is not drawn in motion; filter to one folder and the counters recount the visible set; a forced read failure (seam refused) shows the error, not zero rows.
      req: loop-view/4, loop-view/5
      files: src/frontend/features/loop/LoopScreen.tsx

## 4. The row opens the same journey as the inbox (after #450 merges)

- [ ] 4.1 Point the row's link at the PBI-journey destination #450 fixed in the detail, addressed by the PBI's identity (source, repository, key) — not at a `workingPath`. Read #450's landed destination from master; do not guess its shape.
      req: loop-view/6
      files: src/frontend/features/loop/LoopScreen.tsx
- [ ] 4.2 Verify from both entries: opening the same PBI's journey from the inbox and from the Loop row lands on the same journey, with the same back/forward behaviour and the same context; a closed PBI with a live run still opens it, the way the detail lets it.
      req: loop-view/6
      files: src/frontend/features/loop/LoopScreen.tsx

## 5. Gate and the issue's expected check

- [ ] 5.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` — full gate, and name what it counted.
- [ ] 5.2 Run the issue's expected check as written: two folders, one in a run and one held; apply the filter and read the counters; open both journeys; a real transition updating the screen in the background; a read error not presented as zero; visual review in both themes and at a narrow width, keyboard.
- [ ] 5.3 Keep `openspec/changes/loop-view-background-work`'s deltas true to what landed; `openspec validate loop-view-background-work --strict` stays green at the end.
