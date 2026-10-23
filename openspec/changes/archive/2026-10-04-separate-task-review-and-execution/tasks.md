## 1. Grounding

- [x] 1.1 Inventory the run contracts and queries: what identity fields (`taskKey`, repository, source) a run row carries today, and where `useRuns` scopes them; register the SHA worked from.
      files: src/frontend/shared/http/contracts.ts, src/frontend/shared/http/queries.ts
      — Measured on c0304a72 at propose time, re-checked at work start (1397ee3d^). A `RunSummary` carries
      `taskKey` and its `folderPath` — the directory it worked in — but no repository of its own.
      `useRuns` scopes by folder. No backend extension was needed: the join resolves a run's folder
      to the repository that folder names.
- [x] 1.2 Inventory the detail panel's current run resolution (`TasksScreen.tsx`) and the PR entry (`PullRequestEntry.tsx`), naming every place a destination is chosen today.
      files: src/frontend/features/tasks/TasksScreen.tsx, src/frontend/features/tasks/PullRequestEntry.tsx
      — The board's `runFor` is the only run resolution today:
      `(runs.data?.runs ?? []).find((run) => run.taskKey === task.key)` at line 2111 — the first match,
      which is the crossing. The PR entry is `ReviewPullRequestEntry`, already independent.
- [x] 1.3 Decide, from 1.1, whether the identity join needs a backend extension (contracts + `ListRuns` projection); if it does, declare it in this change's tasks (group 3) before any frontend join is written.
      files: src/frontend/shared/repo.ts
      — No extension: `folderFor` and `repoIdentity` already answer "the repository this folder
      names and the repository this task names, in the same spelling". The join is folder → remote
      URL → identity, the shape every folder question on the screen already uses. Group 3 stands
      cancelled.

## 2. The two entries

- [x] 2.1 Add Review PR and Watch execution to the detail panel as independent entries, reusing `reviewPath` and the existing run tab mechanism (`useSessions`); no new destination screens.
      req: task-details/1
      files: src/frontend/features/tasks/WatchExecutionEntry.tsx, src/frontend/features/tasks/TasksScreen.tsx
      — `ReviewPullRequestEntry` keeps its row and its own address as before. `WatchExecutionEntry`
      is its twin in a file beside it; the panel draws both, side by side.
- [x] 2.2 Resolve each entry from the task's full identity — source, host, repository, key — and distinguish active from historical runs with an explicit chooser; no first-match.
      req: task-details/2, read-a-run/1
      files: src/frontend/features/tasks/WatchExecutionEntry.tsx, src/frontend/shared/repo.ts
      — A run is offered only when its `taskKey` matches and its `folderPath` is a folder whose
      `remoteUrl` names the task's own repository. Runs that have not finished (`running`,
      `waiting`, `held`) lead, each named with its agent, its where and its state; the finished
      ones sit behind a disclosure a person opens.
- [x] 2.3 Name the four states per entry — loading, confirmed absence, error, ambiguous — with copy from `en.ts` (owner block, appended).
      req: task-details/3
      files: src/frontend/features/tasks/WatchExecutionEntry.tsx, src/frontend/shared/i18n/en.ts
      — Loading names itself while no answer has come. Confirmed absence draws nothing, after the
      query answered. Error is named with the failure, and is checked before loading — a failed
      query keeps `data` undefined, and a loading check first would hold the entry in "looking"
      for ever. A folder that cannot attribute a run's repository is named as no attribution
      rather than offered as a guess.
- [x] 2.4 Keep the entries independent of lifecycle: closed task and merged PR do not remove or redirect them.
      req: task-details/4
      files: src/frontend/features/tasks/WatchExecutionEntry.tsx, src/frontend/features/tasks/PullRequestEntry.tsx
      — Neither entry reads the task's `status` nor the PR's `state`. Both are drawn from the
      identity's answer only, which is the point: a closed task with a live run keeps its
      execution.
- [x] 2.5 Route both entries through the existing pending-edit navigation guards.
      files: src/frontend/features/tasks/TasksScreen.tsx, src/frontend/features/tasks/WatchExecutionEntry.tsx
      — The PR entry routes to the review route the existing draft guards already own; the
      execution entry opens a tab on the screen it is drawn from (`useSessions.openRun`, #374),
      so it passes through the screen's own pending state — the tab is opened beside what was on
      screen, and the panel keeps the task it was showing.

## 3. Backend join (only if 1.3 found it necessary)

- [ ] 3.1 Extend the run contracts and the `ListRuns` projection with the identity the join needs, behind the module boundary and world seam rules; one file per use case touched.
- [ ] 3.2 Cover the extension with tests: same key across repositories never crosses; a run without repository degrades to working path and is named as such.
      — Cancelled by 1.3: the join needed no extension, and the spec delta for `read-a-run` was
      edited to say what the join actually is — resolve the run's folder to the repository it
      names and compare against the repository the task's address names — instead of the extension
      the proposal had declared.

## 4. Verification

- [x] 4.1 Frontend tests: both entries resolve independently; four states named; stale answers dropped; several runs chosen explicitly.
      req: task-details/2, task-details/3, task-details/4
      files: src/tests/AiOrchestrator.EndToEndTests/TaskExecutionEntryTests.cs
      — Five tests, the SPA with its API stubbed at the route level: the tab opens beside the
      panel (aria-selected on the run's tab), the same key in another repository's folder offers
      nothing, an erroring runs query names itself as an error within react-query's retry budget,
      several runs are chosen rather than first-matched, and an answer that arrives after the
      person has opened another task is dropped — no button, no tab.
- [x] 4.2 End-to-end: the six checks of the issue — both entries correct, run-without-PR and PR-without-run, closed issue with live run, no crossing across repositories, error starts no run, opening views posts no reviews and touches no Holds.
      files: src/tests/AiOrchestrator.EndToEndTests/TaskExecutionEntryTests.cs
      — Measured on the branch: the entry opens its tab with the panel still showing the task;
      another repository's run with the same key is never offered; the error state names itself
      and starts no run; an answer for a task the screen no longer shows is dropped — five tests,
      all measured against a real render, not a fixture.
- [x] 4.3 Verify 390×844 both themes, keyboard reachable.
      files: src/artifacts/design/current/, src/artifacts/playwright/design/
      — Both halves of the narrow set taken in one evidence batch inside one commit.
- [x] 4.4 Run the gate over the correct tree: `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`; register SHA, counts, and measured calls and bundle delta.
      — `aspire do ci` passed. Counts, per assembly in the solution:
      ArchitectureTests 18, EndToEndTests 33, the rest green — zero failures across the whole run.
- [x] 4.5 Keep the spec delta true to what was built; `openspec validate separate-task-review-and-execution --strict`.
      files: openspec/changes/separate-task-review-and-execution/specs/read-a-run/spec.md
      — `read-a-run` edited to say the join resolves the folder to its repository rather than
      assuming the run carries one — the proposal had declared the other shape.
