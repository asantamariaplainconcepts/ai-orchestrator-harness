# app-shell-and-navigation

## MODIFIED Requirements

### Requirement: Seven routes inside one shell, with the inbox as the landing screen
`src/frontend/app/routes.tsx` SHALL declare one shell route rendering `AppShell` with children `tasks`,
`runs/:runId`, `folders`, `folders/:folderId`, `history` and `settings`, and the index SHALL
redirect to `/tasks`. A client route asked for directly SHALL come back as the application, and moving between
screens SHALL be the router rather than a page load. A request for `/workflows` SHALL redirect to `/tasks`
rather than 404, because the route was a published link and a bookmark that followed it did nothing wrong.

#### Scenario: A deep link is served the application
- **WHEN** `/folders/{id}` is requested directly from the browser
- **THEN** the host returns the application and the client resolves the route

#### Scenario: Navigation is not a page load
- **WHEN** a person moves from Tasks to History
- **THEN** the document is not re-fetched

#### Scenario: A bookmark to the old workflows route lands on the inbox
- **WHEN** `/workflows` is requested
- **THEN** the client redirects to `/tasks`

## ADDED Requirements

### Requirement: The header carries only what belongs to the screen underneath it
`AppShell`'s header SHALL carry the rail toggle (below 900px), the title and subtitle, and the "Open in editor" and "Run a task here" actions when a folder screen is underneath. The header SHALL NOT carry a search affordance, a "New run" button, the four icon links (workflows, history, settings, theme), the attention chip, the holds counter, or the overflow menu that folded them. The 520px breakpoint (`headerCompactBelow`) and its `moreChoices` / `iconLink` machinery SHALL be removed entirely, because nothing is left to fold.

#### Scenario: The header at 390px carries only title and rail toggle
- **WHEN** the window is 390px wide and no folder is open
- **THEN** the header holds the rail toggle and the title, and nothing else on the action side

#### Scenario: A folder screen adds its own actions
- **WHEN** the screen underneath is a folder screen and the window is wider than 900px
- **THEN** "Open in editor" and "Run a task here" appear in the header beside the title

### Requirement: The rail carries the attention chip, holds counter and refresh at its top
`Sidebar` SHALL draw the attention chip (agents blocked, in every project) and the holds counter (amber badge with a menu) in the logo block beside the app name, where the person's eye lands when the rail opens. The refresh control SHALL be beside them. These are the same controls the header carried, moved rather than re-implemented: the queries (`useTasks`, `useRuns`, `useLiveRuns`, `useAllTerminals`) and the holds attribution (`heldWithContext`) move from `AppShell` to `Sidebar`.

#### Scenario: A hold is visible in the rail
- **WHEN** a task is held in any project and the rail is open
- **THEN** the amber holds counter is in the logo block beside the app name, and opening its menu reveals the hold with its project and run

#### Scenario: Zero holds draws no counter
- **WHEN** no task is held in any project
- **THEN** no holds counter is drawn in the rail, the same rule the header followed

### Requirement: The rail toggle carries the holds count when the rail is hidden
Below 900px, when the rail is an overlay and closed, the attention chip and holds counter are inside the hidden rail. The rail toggle button (the `PanelLeft` icon in the header) SHALL carry an amber badge with the holds count, so the person sees that something is waiting and can open the rail to reach it. Zero holds SHALL draw no badge — zero is not drawn, the same rule the holds counter follows.

#### Scenario: A hold is visible from the header when the rail is closed
- **WHEN** the window is below 900px, the rail is closed, and a task is held
- **THEN** the rail toggle button carries an amber count of the holds, and clicking it opens the rail where the holds menu lives

#### Scenario: No badge when nothing is held
- **WHEN** no task is held in any project and the rail is closed
- **THEN** the rail toggle carries no badge

### Requirement: History, settings and theme live at the bottom of the rail
`Sidebar` SHALL draw History, Settings and Theme as three items in a column at the bottom of the rail, beside or below the "this machine" card. History SHALL be a `NavLink` to `/history`, Settings a `NavLink` to `/settings`, and Theme a button that toggles the theme — the same icons, labels and behaviour they had in the header, moved rather than re-implemented. At 220px (the rail minimum) the three items and the machine card SHALL fit without overflow.

#### Scenario: Settings is reachable from the rail
- **WHEN** the rail is open
- **THEN** a Settings link is at the bottom of the rail, and clicking it navigates to `/settings`

#### Scenario: Theme is toggled from the rail
- **WHEN** the theme button at the bottom of the rail is pressed
- **THEN** the theme switches, the same way it did from the header

#### Scenario: The bottom of the rail fits at minimum width
- **WHEN** the rail is 220px wide
- **THEN** History, Settings, Theme and the "this machine" card all fit without horizontal overflow
