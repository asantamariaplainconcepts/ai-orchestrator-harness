## MODIFIED Requirements

### Requirement: Seven routes inside one shell, with the inbox as the landing screen
`src/frontend/app/routes.tsx` SHALL declare one shell route rendering `AppShell` with children
`pbis`, `pbis/:key`, `folders`, `folders/:folderId` and `settings`, and the index SHALL redirect to
`/pbis`. `pbis/:key` SHALL render the PBI's own screen, with the folder in the `folder` query; the
static `pbis/pull-requests` routes SHALL keep precedence over it. A
client route asked for directly SHALL come back as the application, and moving between screens SHALL
be the router rather than a page load. A request for `/workflows` SHALL redirect to `/pbis` rather
than 404, because the route was a published link and a bookmark that followed it did nothing wrong.
A request for `/runs/:runId` SHALL redirect to the run's owning folder screen scoped to its working
directory with the run handed to the tab strip, for the same reason — and `/history` SHALL render the
global History screen (the journeys grouped by PBI identity), because the question History answers is
global, not per worktree. A request for the old `/tasks` and `/tasks/pull-requests` routes SHALL
redirect to `/pbis` and `/pbis/pull-requests` respectively, keeping the query the request carried,
for the same reason a bookmark is never punished.

A `?worktree=` query on a folder route SHALL resolve to the PBI whose voyage owns that worktree when
the relation is unambiguous (the worktree's folder and branch identify one PBI by full identity) and
the URL names no tab, and SHALL stay on the folder serving the tab the URL asked for when it is not
— opening another PBI that shares a key or path is what it SHALL NOT do, the same rule `pbi-details`
enforces for its entries. A link that names a tab is the folder serving a tab rather than a bookmark
for a journey, so it stays. The "explain rather than stay" the proposal carried is amended below:
the unambiguous case opens the voyage and the rest stay, leaving the rail and the inbox as the way
a person reaches a voyage from an ambiguous link.

#### Scenario: A deep link is served the application
- **WHEN** `/folders/{id}` is requested directly from the browser
- **THEN** the host returns the application and the client resolves the route

#### Scenario: Navigation is not a page load
- **WHEN** a person moves between two screens
- **THEN** the document is not re-fetched

#### Scenario: A run deep link lands on its worktree
- **WHEN** `/runs/{runId}` is requested
- **THEN** the application resolves the run's folder and working directory, navigates there, and
opens the run as a tab beside that screen's content

#### Scenario: A bookmark to the old history route lands on the inbox
- **WHEN** `/history` is requested
- **THEN** the client renders the global History screen grouping journeys by PBI identity, rather
than redirecting to `/pbis`

#### Scenario: A bookmark to the old workflows route lands on the inbox
- **WHEN** `/workflows` is requested
- **THEN** the client redirects to `/pbis`

#### Scenario: A bookmark to the old inbox still lands
- **WHEN** `/tasks` is requested
- **THEN** the client redirects to `/pbis`

#### Scenario: The old inbox keeps its query
- **WHEN** `/tasks?folder=/repo` is requested
- **THEN** the client lands on `/pbis?folder=/repo`

#### Scenario: A PBI has a route
- **WHEN** `/pbis/%23412?folder=/repo` is requested directly
- **THEN** the host returns the application and the client draws the PBI screen

#### Scenario: A worktree link resolves to its PBI
- **WHEN** a folder route carries `?worktree={path}`, names no tab, and the worktree identifies one
PBI by full identity
- **THEN** the client opens that PBI's voyage

#### Scenario: An ambiguous worktree link explains rather than reselecting
- **WHEN** a folder route carries `?worktree={path}` whose relation to a PBI is not unambiguous
- **THEN** the client shows an explanation naming the reason, and does not open another PBI that
shares a key or path

### Requirement: The header carries only what belongs to the screen underneath it
`AppShell`'s header SHALL carry the rail toggle (below 900px), the title and subtitle, and the "Open in editor" action when a folder screen is underneath. It SHALL NOT carry "Run a task here": launching an agent is the PBI's step, and the shortcut sent the person to a form with no PBI chosen. The header SHALL NOT carry a search affordance, a "New run" button, the four icon links (workflows, history, settings, theme), the attention chip, the holds counter, or the overflow menu that folded them. The 520px breakpoint (`headerCompactBelow`) and its `moreChoices` / `iconLink` machinery SHALL be removed entirely, because nothing is left to fold.

#### Scenario: The header at 390px carries only title and rail toggle
- **WHEN** the window is 390px wide and no folder is open
- **THEN** the header holds the rail toggle and the title, and nothing else on the action side

#### Scenario: A folder screen adds its own actions
- **WHEN** the screen underneath is a folder screen and the window is wider than 900px
- **THEN** "Open in editor" appears in the header beside the title, and no launch shortcut does

### Requirement: The rail toggle carries the holds count when the rail is hidden
Below 900px, when the rail is an overlay and closed, the Loop row's need-you count is inside the hidden rail. The rail toggle button (the `PanelLeft` icon in the header) SHALL carry an amber badge with the holds count, so the person sees that something is waiting and can open the rail to reach it. Zero holds SHALL draw no badge — zero is not drawn, the same rule the Loop row's count follows.

#### Scenario: A hold is visible from the header when the rail is closed
- **WHEN** the window is below 900px, the rail is closed, and a task is held
- **THEN** the rail toggle button carries an amber count of the holds, and clicking it opens the rail

#### Scenario: No badge when nothing is held
- **WHEN** no task is held in any project and the rail is closed
- **THEN** the rail toggle carries no badge

## ADDED Requirements

### Requirement: The rail creates no worktree, and a folder row carries a two-entry menu

The rail SHALL NOT offer to create a worktree: the `Projects` group's `+` SHALL offer adding a folder
only, and a folder row SHALL carry no new-worktree control, because the launcher creates and removes
worktrees for the loop. A folder row SHALL carry a `…` menu with exactly two entries: **Explore
idea**, which opens the explore dialog for that folder, and **Terminal here**, which starts a manual
shell session in the folder's checkout and lists it in the session tray. The menu SHALL NOT offer to
open an agent on the folder.

#### Scenario: The Projects plus adds a folder only

- **WHEN** the `Projects` group's `+` is opened
- **THEN** it offers adding a folder, and no worktree entry

#### Scenario: A folder row's menu

- **WHEN** a folder row's `…` is opened
- **THEN** it offers Explore idea and Terminal here, and nothing else

#### Scenario: Terminal here lands in the tray

- **WHEN** Terminal here is chosen on a folder
- **THEN** a shell session starts in that folder's checkout and appears in the session tray

### Requirement: The rail's primary navigation carries PBIs, Loop and History
The rail SHALL draw its primary navigation as three links, in this order: `PBIs` — the inbox, to
`/pbis`, with the count of PBIs it holds — then `Loop`, to `/loop`, then `History`, to `/history`.
Each moves by the router rather than a page load. The folder list is not a primary link: the
`Projects` group's `+` adds a folder and a folder row opens its folder.

The `Loop` link SHALL carry one warning badge, "N need you", counting the PBIs in the loop's
attention-required band — held, failed, gated — plus the agents stopped at a question. It replaces
the attention chip and the holds counter the rail used to draw above the navigation. A count of zero
SHALL be drawn as no badge.

#### Scenario: The rail's primary links read PBIs, Loop, History
- **WHEN** the rail is open
- **THEN** its three primary links are, in order, `PBIs`, `Loop` and `History`, and no `All projects` link is drawn

#### Scenario: One count says what needs a person
- **WHEN** one PBI is held, one run has failed and no agent is waiting
- **THEN** the `Loop` link reads "2 need you", and no other hold or attention control is drawn above the navigation

#### Scenario: Nothing needs a person
- **WHEN** nothing is held, failed or waiting
- **THEN** the `Loop` link carries no badge

### Requirement: The rail's foot is one line
The foot of the rail SHALL be one row: the machine's folder and live counts as a sentence, then
refresh, settings and theme as icon buttons. Settings SHALL be a link to `/settings` and theme a
button that toggles the theme. The row SHALL fit at the rail's 220px minimum, the sentence
ellipsising before the buttons do.

#### Scenario: Settings and theme from the foot
- **WHEN** the rail is open
- **THEN** its last row carries the folder and live counts, refresh, a Settings link and the theme toggle

### Requirement: A held PBI's rail row carries a pause mark
A PBI row in the rail SHALL mark a hold with a pause glyph carrying the accessible name "held",
rather than a word badge, so a held row keeps its title's width.

#### Scenario: A held row
- **WHEN** a PBI under a folder is held
- **THEN** its row carries the pause mark and no "held" badge

## REMOVED Requirements

### Requirement: The rail carries the attention chip, holds counter and refresh at its top
**Reason**: Condensed into the `Loop` link's "N need you" count (#471, rail A); refresh moved to the foot.
**Migration**: Read what needs a person from the `Loop` badge and release a hold from the PBI's screen or the Loop row.

### Requirement: History, settings and theme live at the bottom of the rail
**Reason**: History is a primary destination now, beside PBIs and Loop; settings and theme sit in the one-line foot.
**Migration**: History is the third primary link; settings and theme are the foot's icon buttons.

### Requirement: The rail's primary navigation carries PBIs, Loop and All projects
**Reason**: `All projects` is withdrawn (#471); the folder list is reached from the `Projects` group.
**Migration**: Add a folder with the `Projects` `+`; open one from its rail row.
