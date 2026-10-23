## Context

#311 removes the global chrome from the header. The header of 56px today carries: a search hint (220px, no handler), a "New run" button (navigates to `/tasks`, one rail row away), four icon buttons (workflows, history, settings, theme) behind a divider, a 520px breakpoint that folds those four into an overflow menu, an attention chip (agents blocked), a holds counter, and a refresh button.

Meanwhile the workflow editor lives on a global route `/workflows` (`WorkflowsScreen.tsx`, 869 lines) while the project's "AI workflows" tab (`WorkflowsTab.tsx`, 73 lines) only shows a catalogue and navigates away to edit. The rail already has a logo block at the top and a "this machine" card at the bottom — two natural homes for the chrome the header is losing.

## Goals / Non-Goals

**Goals**

- Header carries only what belongs to the screen open under it: title, subtitle, "Open in editor" and "Run a task here" when a folder is showing.
- Attention chip, holds counter and refresh live in the rail's logo block, beside the app name.
- History, settings and theme live at the bottom of the rail, beside "this machine".
- Workflow editor embedded in the project's "AI workflows" tab; `/workflows` route removed.
- 520px breakpoint and its overflow menu gone — nothing left to fold.

**Non-Goals** (the issue's "What it is not")

- No search or command palette — removed, not implemented.
- No backend changes — same endpoints, same queries.
- No changes to the Runs tab of a project.

## Decisions

### 1. The rail holds the attention chip, holds counter and refresh, not the header

`Sidebar` already calls `useFolders`, `useLiveRuns` and `useWorkspaceUpdates`. Adding `useTasks`, `useRuns`, `useAllTerminals` and `useHoldRelease` brings the attention count and the holds list into the rail — the shell's own queries today.

The `heldWithContext` memo (which maps holds to folders and runs) moves with them, since the rail's `useFolders` answer is the same `folderList` the shell uses. The `holdChoices` menu and `ReleaseHold` component follow.

Alternative: a context provider that shares the shell's data downward. Rejected — the rail already fetches its own data and is a self-contained component. Duplicating the hooks is simpler than threading a provider between siblings.

### 2. `/workflows` redirects to `/tasks`, not 404

The route was a published link in `WorkflowsTab.tsx`'s "Edit workflows" button and may survive in a bookmark. A redirect is mild — the destination is the inbox, which is the app's landing screen and where the person was already going. A 404 is hostile to a bookmark that did nothing wrong.

The redirect is one `<Route>` element in `routes.tsx` with `<Navigate to="/tasks" replace />`. The route lives in `routes.tsx`, not in a separate component, because it is one line.

### 3. `WorkflowsTab.tsx` becomes the editor, moved from `WorkflowsScreen.tsx`

The entire body of `WorkflowsScreen` — the master/detail layout, the list pane, the `StepList` component, `SourceNotice`, `RenameWorkflowDialog`, `DeleteWorkflowDialog`, `NewWorkflowDialog` — moves from `features/workflows/WorkflowsScreen.tsx` into `features/folders/WorkflowsTab.tsx`. The tab becomes `export function WorkflowsTab()` with the same body. The old file is deleted; the route is removed.

This is a move, not a copy: the component tree, the hooks and the state all travel. The one thing that changes is the container — a tab inside `FolderDetailScreen` instead of a standalone route. `FolderDetailScreen` already renders `<WorkflowsTab />` at line 944; the tab's internal layout (two-column master/detail inside the folder screen's content area) adapts to the folder screen's width.

### 4. The 520px breakpoint is removed, not lowered

`headerCompactBelow` (520px) existed to fold four icon buttons into one overflow menu. The four icons are moving to the rail, and the search and "New run" are gone — there is nothing left to fold. The breakpoint, `useBelow(headerCompactBelow)`, `compact`, `moreChoices`, `iconLink`, and the overflow `MenuButton` are all removed.

The rail's own breakpoint (`railFreeAbove`, 900px) is unchanged: below 900px the rail is an overlay. When the rail is hidden (below 900px, not asked for), the attention chip and holds counter are not visible — the rail toggle button gains the amber holds count badge, so the person sees that something is waiting and can open the rail to reach it.

### 5. History, settings and theme at the rail's bottom, above "this machine"

Three `NavLink` / button items in a column beside the "this machine" card. History uses `NavLink to="/history"`, settings uses `NavLink to="/settings"`, and theme is a button that calls `toggleTheme`. Same icons (`Clock`, `Settings`, `Sun`/`Moon`), same labels, same behaviour — just in the rail instead of the header.

Below 220px (the rail minimum), the three items and the machine card must fit without overflow. At 220px each item is a 32px-high row with a 16px icon and 11px text — three rows plus the machine card's ~60px total fits in 220px of height, which even a phone viewport (844px tall) has plenty of.

### 6. The rail toggle carries the holds count when the rail is hidden

Below 900px, when the rail is an overlay and closed, the attention chip and holds counter are inside the hidden rail. The toggle button (the `PanelLeft` icon in the header) gains the same amber badge the header's holds counter carried — a small count beside the icon. Clicking it opens the rail, which reveals the full holds menu and attention chip.

No badge when `holds.length === 0` — zero is not drawn, same rule as the header counter.

## Risks / Trade-offs

- [The attention chip is not visible when the rail is hidden below 900px] → the rail toggle carries the holds count badge; the attention chip is in the overlay the badge opens.
- [Moving `WorkflowsScreen` into a tab changes its available width] → the master/detail layout was 280px list + flex-1 editor on a full-screen route; inside a tab it gets the folder screen's content width, which is the full window minus the rail. At 1440px that is 1176px; at 900px (rail visible) it is 636px — the list pane's 280px is 44% of that, which is tight but fitable. The list pane can shrink: `width: 280` becomes `minWidth: 200, maxWidth: 280, flex: "0 1 280px"`.
- [Removing `headerCompactBelow` means the header is always full-width] → the header now carries only the title/subtitle, the rail toggle (below 900px), and "Open in editor"/"Run a task here" (when a folder is showing, above 900px). At 390px the title gets almost the full row, which is the point — #217 measured 43px for the title and this change gives it the row.

## Migration Plan

Frontend-only, no data migration. The route change (`/workflows` → redirect) is backward-compatible for bookmarks. Rollback is reverting the commit.

## Open Questions

None — the issue's seam section decided the file boundaries, and the remaining decision (how the rail toggle carries the count below 900px) is resolved in decision 4 above.
