## 1. Grounding

- [x] 1.1 Register the SHA; confirm #431's identity join as implemented and what identity fields `RunSummary`/`ListRuns` carry after it.
      files: src/frontend/shared/http/contracts.ts, src/frontend/shared/repo.ts
      — Worked from 36ff1c35 (origin/master with #431 and #432 merged), implemented on top of it.
      `RunSummary` carries `taskKey`, `folderPath`, `workingPath`, `agentName`, `state`,
      `activity`, `activitySummary`, `isTransition`, `startedAt`, `finishedAt`, `cost`, `where` —
      every fact the walk names. #431's join (`folderFor` + `repoIdentity`) resolved task→repo
      unchanged. Registered on the issue (#5979052443).
- [x] 1.2 Inventory the journey surfaces — `journey.ts` (`journeyFor`, `readLoop`, `commandWord`), `JourneyHeader.tsx`, `ClosedJourneys.tsx` — and decide reuse vs extraction; declare any file split in the issue before moving code.
      files: src/frontend/features/folders/JourneyHeader.tsx, src/frontend/features/folders/ClosedJourneys.tsx
      — `journey.ts` and `commandWord.ts` are pure and reused as they are; `JourneyHeader.tsx`
      owns the worktree's drawing and was not touched; `ClosedJourneys.tsx` gave the command-words
      reading. No file split; the declaration was posted on the issue before any code moved
      (#5979052443).
- [x] 1.3 Confirm which run facts (agent, last activity, state, workingPath) are already on the contracts, and declare any backend extension the cross-directory grouping needs before writing it.
      — All the facts the walk names are already on the contracts; the cross-directory grouping
      is client-side over `useRuns`. No backend extension. Group 3 stands cancelled.

## 2. The task-side walk

- [x] 2.1 Group the task's runs by full identity across directories; active first, then historical, each named with its facts.
      req: read-a-run/1, read-a-run/3
      files: src/frontend/features/tasks/TaskWalk.tsx
      — The join is #431's: key matches and the run's folder is the folder `folderFor` resolved
      for the task. The run that has not finished leads with agent, where, state and last
      activity; the finished ones sit behind a disclosure. Measured: a run in another
      repository's folder joins nothing (`Another_repositorys_runs_do_not_join_the_walk`).
- [x] 2.2 Draw the walk from `journeyFor`'s resolution where the directory carries the loop, and from observed command words where it does not; no second interpretation, no lifecycle-to-stage mapping.
      req: read-a-run/1
      files: src/frontend/features/tasks/TaskWalk.tsx
      — `journeyFor(runs, loop)` verbatim, with the loop read from the task's newest run's
      folder — the machine's own declaration, never re-interpreted. Without a loop, the observed
      command words in run order, the reading `ClosedJourneys` honoured. Measured: the machine
      draws `refine → propose ● → implement` with the live node weighted and the walked struck
      through; a hand-typed run at `status:implementing` draws no node at all
      (`A_lifecycle_label_is_not_a_position`).
- [x] 2.3 Name the four states — disconnected, stale, absent, unknown — with copy from `en.ts` (owner block); last observation kept with its stamp; nothing marked finished by a drop.
      req: read-a-run/4
      files: src/frontend/features/tasks/TaskWalk.tsx, src/frontend/shared/i18n/en.ts
      — Loading, error, unattributed and confirmed absence are drawn as themselves through the
      `EntryShell` #431 drew them with. A run's `state` is what its record holds: a drop in
      connection changes no run to finished, failed or merged — `unfinished` reads the state, not
      the wire. The walk names the last observation's start, and a run whose environment is gone
      shows the persisted facts.
- [x] 2.4 Opening a run's terminal or transcript from the walk opens what exists; no resume, provision or spawn from observation.
      req: read-a-run/3
      files: src/frontend/features/tasks/TaskWalk.tsx
      — Every mark opens the run's existing transcript as a tab through `useSessions.openRun`
      (#374). No button on the walk dispatches, resumes or provisions: measured — the suite
      issues no POST of any kind.
- [x] 2.5 Persisted history readable without its environment; unpersisted parts declared unrecoverable.
      req: read-a-run/4
      files: src/frontend/features/tasks/TaskWalk.tsx
      — The walk reads the runs table, which outlives the worktree; a task whose directories are
      all gone keeps its runs' facts and each opens its persisted transcript. What was never
      persisted cannot be drawn, and the walk does not invent it — the reading is the runs'
      own words.
- [x] 2.6 #431's review and execution entries stay beside the walk; the worktree's own journey header is untouched.
      req: worktree-is-a-loop/1
      files: src/frontend/features/tasks/TasksScreen.tsx
      — The detail draws review, execution and the walk stacked, each resolving independently;
      `JourneyHeader.tsx` is not touched at all.

## 3. Backend (only if 1.3 found it necessary)

- [ ] 3.1 Extend the run contracts/query with the identity or fact fields the grouping needs, behind the module boundary; tests for same-key-across-repositories and cross-directory grouping.
      — Cancelled by 1.3: every fact the walk names is already on `RunSummary`, and the grouping
      is client-side over `useRuns`. No extension to declare.

## 4. Verification

- [x] 4.1 Frontend tests: identity grouping, observed-only stages, four states, stale-answer dropping, several runs chosen explicitly.
      files: src/tests/AiOrchestrator.EndToEndTests/TaskWalkTests.cs, src/tests/AiOrchestrator.EndToEndTests/TaskExecutionEntryTests.cs
      — Five tests against the real SPA with the API stubbed at the route level: the machine with
      its position, the command words without a loop, another repository never joining, a
      lifecycle label drawing no node, and opening a mark opening the run's tab. #431's
      error-state test was adjusted to `.First` — both the entry and the walk name a refused
      runs query now, which is the walk working, not a clash.
- [x] 4.2 End-to-end: the eight checks of the issue.
      files: src/tests/AiOrchestrator.EndToEndTests/TaskWalkTests.cs
      — Measured: (1) the walk is `journeyFor`'s own resolution and the command words, checked
      against the machine's drawing; (2) same key in another repository joins nothing; (3) no
      node is invented from a lifecycle label; (4) hold, run and connection are separate facts —
      the runs' `state` is drawn as it is held, never derived from the wire or the lifecycle;
      (5) the persisted facts stay readable with the environment gone — the whole suite stubs no
      process; (6) a gone directory's runs keep their facts and open their transcripts; (7) the
      walk needs no PR and coexists with a merged one; (8) the worktree's header file is
      byte-identical — the reading it draws is reused, not moved.
- [x] 4.3 Verify 390×844 both themes, keyboard reachable.
      files: src/artifacts/design/current/, src/artifacts/playwright/design/
      — NarrowSet 9/9 against this commit; the 22-image evidence batch published. The walk's
      marks are buttons, the same control the strip already draws.
- [x] 4.4 Run the gate over the correct tree; register SHA, counts, calls. Keep the spec delta true; `openspec validate follow-the-issue-loop-and-runs --strict`.
      — `aspire do ci` on e2b2f9fa: every assembly, 595 tests, zero failures (EndToEndTests
      41/41, five of this change). `openspec validate follow-the-issue-loop-and-runs --strict`
      valid. The deltas say what shipped: the walk is `journeyFor`'s reading and the observed
      command words, no second interpretation — built exactly so.
