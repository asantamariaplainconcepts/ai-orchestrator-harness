# Tasks — the worktree is the loop's instance

## 1. The journey header, read-only first

- [x] 1.1 Resolve the position in `src/frontend/features/folders/` (new `journey.ts`, beside
      `task.ts`): from `useRuns(folderPath, workingPath)`, the latest `isTransition` run names the
      node it executes (its dispatch/command), the landed ones name where it has walked, and a
      worktree with no transitioned run says so — never guessing from labels; the machine is read
      from the declared loop (`useIssueLoop`) always from the checkout
      req: worktree-is-a-loop/1, worktree-lifecycle/1
      files: src/frontend/features/folders/journey.ts, src/frontend/shared/http/contracts.ts, src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/ListRuns.cs
- [x] 1.2 Draw the journey header above a worktree's strip: the machine as a horizontal strip of
      nodes with the position marked, the live transition clickable into the `?run=` tab
      mechanism, walked transitions stamped with command / duration / cost, hand-started runs
      drawn dimmed as off-loop; fold `LoopGraph`'s node drawing and `LoopInspector`'s readouts into
      it and delete their separate placements in the worktree scope
      req: worktree-is-a-loop/1, review-a-change-in-one-tab/1
      files: src/frontend/features/folders/JourneyHeader.tsx, src/frontend/features/folders/FolderDetailScreen.tsx, src/frontend/shared/i18n/en.ts

      **Deviation from the task as written**: `LoopGraph` and `LoopInspector` are *not* folded in.
      They turn out to be the loop **editor** (React Flow canvas, draft state, save/mutate), living
      on the checkout's Commands tab — not worktree surfaces at all, so the task's "delete their
      separate placements in the worktree scope" found nothing to delete. The journey header draws
      its own strip from `readLoop` (the shared parser), which was the intent. Stated in the PR.
- [x] 1.3 The sidebar worktree row gains the position word — the node a live run executes, or the
      hold it waits on — from the same liveness join the rail already runs, storing nothing in
      Workspace
      req: worktree-lifecycle/1
      files: src/frontend/app/Sidebar.tsx, src/frontend/shared/i18n/en.ts

## 2. Provenance in the change

- [x] 2.1 Each step panel of `ChangeTab` carries a provenance line naming the run that produced
      what it shows — command, duration, cost — resolved as the issue is today (most recent run in
      that directory), and says "nothing produced it" rather than drawing empty provenance
      req: worktree-is-a-loop/2
      files: src/frontend/features/folders/ProvenanceRow.tsx, src/frontend/features/folders/ChangeTab.tsx, src/frontend/shared/i18n/en.ts

      **Deviation**: the line sits once, on the step panel's heading, rather than inside each of
      the four step components — the heading is common to every step, so one component and one
      call site carry the requirement for all of them, and the per-step files stay as they were.

## 3. Run reachable through the loop only

- [x] 3.1 `/runs/:runId` becomes a redirect: resolve the run's folder and working directory,
      navigate to `folders/:folderId?worktree=&run=<id>`, which the existing `RunDeepLink`
      consumer already opens as a tab; the route stops rendering `RunScreen`
      req: app-shell-and-navigation/1
      files: src/frontend/app/RunRedirect.tsx, src/frontend/app/routes.tsx, src/frontend/app/AppShell.tsx

      **Deviation**: the run tab is opened by the redirect itself (`openRun`), not via `?run=` in
      the URL — landing with the parameter would leave `RunDeepLink` re-opening a closed tab on
      every reload of the link, so the open happens once at resolution time.
- [x] 3.2 Delete the full-page run shell and its route-only parts: `RunScreen`'s page wrapper,
      Steps column and Steps strip with their responsive logic; keep `RunView`, `AgentTerminal`,
      `IssueView` and `RunPane` as the embedded surface; move what they need out of `RunScreen.tsx`
      first
      req: read-a-run/2, read-a-run/3
      files: src/frontend/features/runs/RunScreen.tsx

      `RunScreen.tsx` 1,145 → 885 lines. `onOpenRun` became required on `RunView` and `RunHeader`
      — the navigate-to-`/runs/` fallback had no route left to navigate to.
- [x] 3.3 Stop "start a run" navigations from going to a run destination: the Tasks screen's
      success handlers open the owning worktree's screen instead of `/runs/:id`
      req: app-shell-and-navigation/1
      files: src/frontend/features/tasks/TasksScreen.tsx

      **Deviation, the smaller one**: a started run opens as a global tab over the screen the
      person is on, rather than navigating to the worktree's screen — the worktree may not exist
      yet (the server creates it on start), and the tab is the one mechanism every surface now
      shares. The task's wording predates the tab strip becoming global.

## 4. History dies

- [x] 4.1 Delete `HistoryScreen`, the `/history` route (redirecting to `/tasks`, as `/workflows`
      does), the History rail item, and the i18n keys that served only them; `GET /api/runs/spend`
      and its query stay untouched — spend's owned surface, with no consumer in this change
      req: app-shell-and-navigation/1, run-spend/1
      files: src/frontend/features/history/HistoryScreen.tsx, src/frontend/app/routes.tsx, src/frontend/app/Sidebar.tsx, src/frontend/app/AppShell.tsx, src/frontend/shared/i18n/en.ts

## 5. Vocabulary and close

- [x] 5.1 Update the vocabulary once: README's locked block and `AGENTS.md`'s "What this is" say
      the hierarchy — a worktree is the instance of a loop for one issue; a run is one transition
      of it by an agent — keeping every locked word and changing the hierarchy sentence only
      req: worktree-is-a-loop/1
      files: README.md, AGENTS.md
- [x] 5.2 Retro entry in `docs/process/retro-log.md`: what this taught about promoting a projection
      over stored state, and about deleting a destination screen once its embedded form exists
      files: docs/process/retro-log.md
