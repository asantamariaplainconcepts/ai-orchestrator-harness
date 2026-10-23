## 1. Remove the header's global chrome

- [x] 1.1 Remove the search hint, "New run" button, attention chip, holds counter and refresh button from `AppShell.tsx`'s header section; remove the queries they required (`useTasks`, `useRuns`, `useLiveRuns`, `useAllTerminals`, `useHoldRelease`), the `heldWithContext` memo, the `holdChoices` array, and the `refresh` function from `AppShell`
      req: app-shell-and-navigation/3
      files: src/frontend/app/AppShell.tsx
- [x] 1.2 Remove the four icon links (workflows, history, settings, theme), the divider, the `moreChoices` array, and the `iconLink` helper from `AppShell.tsx`
      req: app-shell-and-navigation/3
      files: src/frontend/app/AppShell.tsx
- [x] 1.3 Remove the 520px breakpoint: `headerCompactBelow` constant, the `useBelow(headerCompactBelow)` call, the `compact` variable, and the overflow `MenuButton` branch from `AppShell.tsx`
      req: app-shell-and-navigation/3
      files: src/frontend/app/AppShell.tsx
- [x] 1.4 Remove the `/workflows` entry from the `titles` table in `AppShell.tsx`
      req: app-shell-and-navigation/1
      files: src/frontend/app/AppShell.tsx

## 2. Rail gains the attention, holds and refresh at the top

- [x] 2.1 In `Sidebar.tsx`, add the queries that moved from `AppShell`: `useTasks`, `useRuns`, `useLiveRuns`, `useAllTerminals`, `useHoldRelease`, and the `heldWithContext` memo with its `folderFor` attribution
      req: app-shell-and-navigation/4
      files: src/frontend/app/Sidebar.tsx
- [x] 2.2 Draw the attention chip (amber `CircleHelp` + count, agents blocked across projects) and the holds counter (`MenuButton` with `ReleaseHold`, amber, same as the header's) in the logo block beside the app name in `Sidebar.tsx`. Zero draws neither, same rule
      req: app-shell-and-navigation/4
      files: src/frontend/app/Sidebar.tsx
- [x] 2.3 Draw the refresh button (`RefreshCw`, invalidates the query cache) beside the attention and holds in `Sidebar.tsx`. Same local `refreshing` state pattern the shell used
      req: app-shell-and-navigation/4
      files: src/frontend/app/Sidebar.tsx

## 3. Rail toggle carries the holds count when hidden

- [x] 3.1 Pass the holds count from `AppShell` down to the rail toggle button (the `PanelLeft` icon) so it shows an amber badge when `holds.length > 0` and the rail is hidden (below 900px, closed). Zero draws no badge
      req: app-shell-and-navigation/5
      files: src/frontend/app/AppShell.tsx

## 4. Rail gains history, settings and theme at the bottom

- [x] 4.1 In `Sidebar.tsx`, add three items at the bottom (below the projects tree, beside or above the "this machine" card): History (`NavLink` to `/history`, `Clock` icon), Settings (`NavLink` to `/settings`, `Settings` icon), Theme (button, `Sun`/`Moon` icon, calls `toggleTheme`)
      req: app-shell-and-navigation/6
      files: src/frontend/app/Sidebar.tsx
- [x] 4.2 Wire `useTheme` into `Sidebar.tsx` — either by calling the hook directly or by receiving `theme` and `toggleTheme` as props from `AppShell`. Prefer props, so one `useTheme` instance owns the class on the root `div`
      req: app-shell-and-navigation/6
      files: src/frontend/app/Sidebar.tsx, src/frontend/app/AppShell.tsx

## 5. Move the workflow editor into the tab

- [x] 5.1 Move the full body of `WorkflowsScreen.tsx` (the `WorkflowsScreen` function, `StepList`, `SourceNotice`, `RenameWorkflowDialog`, `DeleteWorkflowDialog`, `NewWorkflowDialog`) into `WorkflowsTab.tsx`, replacing the current catalogue-only `WorkflowsTab`. The tab exports `WorkflowsTab()` and renders the master/detail editor inside the folder screen's content area
      req: app-shell-and-navigation/3
      files: src/frontend/features/folders/WorkflowsTab.tsx, src/frontend/features/workflows/WorkflowsScreen.tsx
- [x] 5.2 Delete `src/frontend/features/workflows/WorkflowsScreen.tsx`
      req: app-shell-and-navigation/1
      files: src/frontend/features/workflows/WorkflowsScreen.tsx
- [x] 5.3 Remove the `import { WorkflowsScreen }` and the `{ path: "workflows", element: <WorkflowsScreen /> }` from `routes.tsx`; replace with `{ path: "workflows", element: <Navigate to="/tasks" replace /> }`
      req: app-shell-and-navigation/1
      files: src/frontend/app/routes.tsx
- [x] 5.4 Adapt the master/detail layout to the tab: the list pane's `width: 280` becomes `minWidth: 200, maxWidth: 280, flex: "0 1 280px"` so it shrinks inside the folder screen's content width
      req: app-shell-and-navigation/3
      files: src/frontend/features/folders/WorkflowsTab.tsx

## 6. i18n

- [x] 6.1 Remove `app.search`, `app.newRun`, `app.more` from `en.ts` (no consumer remains)
      req: app-shell-and-navigation/3
      files: src/frontend/shared/i18n/en.ts
- [x] 6.2 Append any new keys the rail needs for the bottom block (history, settings, theme labels already exist as `nav.history`, `nav.settings`, `nav.theme`) alongside the rail's existing block in `en.ts`
      req: app-shell-and-navigation/6
      files: src/frontend/shared/i18n/en.ts

## 7. Gate

- [x] 7.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` passes, every assembly counted; `openspec validate "header-loses-global-chrome" --strict` passes
      req: app-shell-and-navigation/1, app-shell-and-navigation/3
      files: src/frontend/app/AppShell.tsx
- [x] 7.2 The issue's check observed on the running product: at 390×844 the header title takes the full row; the "AI workflows" tab holds the full editor (create, rename, reorder, holds, add/remove steps, SourceNotice with import/export); `/workflows` redirects to `/tasks`; a worktree does not offer the "AI workflows" tab; releasing a hold from the rail navigates to the run in its folder/worktree; at 220px rail width the bottom block fits; both themes
      req: app-shell-and-navigation/1, app-shell-and-navigation/3, app-shell-and-navigation/4, app-shell-and-navigation/5, app-shell-and-navigation/6
      files: src/frontend/app/AppShell.tsx, src/frontend/app/Sidebar.tsx, src/frontend/features/folders/WorkflowsTab.tsx
