# app-shell-and-navigation Specification

## Purpose
One React SPA served same-origin by the host, with a projects rail down the left and seven routes inside it.
Moving between screens is the router rather than a page load, and the rail says which folders want a person's
attention. Reverse-engineered from the shipped code, not negotiated ahead of it.
## Requirements
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

### Requirement: The document is served by the host, from the dev server in development
The host SHALL serve the SPA same-origin: in development the document SHALL come from the development server
rather than from disk, and the folders screen SHALL show the checkout the server is running from, which is what
makes the end-to-end tests a statement about the real product.

#### Scenario: The document comes from the dev server
- **WHEN** the app is opened during development
- **THEN** the document served is the dev server's, not a file on disk

#### Scenario: The app knows its own checkout
- **WHEN** the folders screen loads
- **THEN** the checkout the server runs from is listed

### Requirement: The rail is free above 900px and asked for below it
`AppShell` SHALL stop giving the 264px projects rail for free below **900px** of width, which is an arithmetic
threshold rather than a taste one: measured at 560×700 the rail took 264 of 560 and left the terminal 60,
clipped off the right edge. Below that width the rail SHALL be something a person asks for, and the choice
SHALL be remembered.

#### Scenario: A narrow window keeps the terminal readable
- **WHEN** the window is narrower than 900px
- **THEN** the rail is not shown until asked for, and the run screen keeps its width

### Requirement: A row's dot answers one question with three states
`Sidebar` SHALL give a row three states rather than four, because the row answers "do I need to open this?" and
the two ways a run can want a person are the same answer: `blocked` (an agent stopped at a safety gate it will
not pass) and `waiting` (a run stopped at a workflow hold) SHALL read the same on the row, with the tooltip
carrying the distinction and the run screen being where the two separate.

#### Scenario: A blocked agent and a held run read the same on the row
- **WHEN** one folder has an agent at a safety gate and another a run at a hold
- **THEN** both rows show the same attention state, and their tooltips differ

### Requirement: Every screen fits a phone, and the first tab is reachable without dragging
The folders screen, a folder's overview, a folder's review tab and a run route SHALL each fit a phone
viewport, and the first tab of a tab strip SHALL be reachable without dragging the strip.

A worktree's Change tab at phone width SHALL put its steps' content within one scroll of the top of
the tab: the header, the step rail and the step panel lay out in one scrollable column, and no
fixed-height block between the top of the tab and the step panel SHALL stand taller than the
viewport so that the panel is unreachable.

#### Scenario: A run route fits a phone
- **WHEN** the run screen is rendered at phone width
- **THEN** it fits without horizontal overflow

#### Scenario: The first tab is not behind a drag
- **WHEN** a tab strip is wider than the viewport
- **THEN** its first tab is reachable without dragging

#### Scenario: A Change step panel is reachable by scroll
- **WHEN** the Change tab is rendered at 390 px and a step is selected
- **THEN** the first row of that step's panel can be brought into the viewport by scrolling, and
  the tab overflows vertically rather than clipping the panel below an unscrollable fold

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

### Requirement: The rail lists the folder's active PBIs, not its branches
`Sidebar`'s `Projects` group SHALL list, under each folder, that folder's **active PBIs** as rows —
key, title, loop position and state — rather than the folder's branches. A row SHALL be drawn from
the global inbox read (`usePbis`) reduced to the folder by matching the PBI's `origin` against the
folder's remotes (`folderFor`), the same reduction `OverviewTab`'s `usePbisHere` performs; the server
SHALL NOT be asked for a per-folder list, because the server holds no folder filter and a second read
would duplicate the inbox's identity resolution. A row SHALL NOT depend on a worktree existing, and
clicking it SHALL open the PBI's voyage — the same destination the inbox and the `Loop` entry open —
reached by the PBI's full identity (source, repository, key).

#### Scenario: A folder shows its PBIs, not its branches
- **WHEN** the rail's `Projects` group lists a folder with active PBIs
- **THEN** each row under the folder is a PBI with its key, title, loop position and state, and no
row is a branch

#### Scenario: A row opens without a worktree
- **WHEN** a PBI has no worktree and its row is clicked
- **THEN** its voyage opens, reached by source, repository and key, not by a working directory

#### Scenario: A row opens the same voyage the inbox opens
- **WHEN** a PBI is opened from a rail row and from the inbox
- **THEN** both open the same voyage destination, reached by full identity

### Requirement: The rail orders a folder's PBIs by attention, then activity, then the rest
Under each folder, the rail SHALL order the PBIs by attention required first (held, blocked, or a
position the Connector names as needing a person), then activity (a run that has not finished), then
the rest, so the eye lands on what needs a person before what is merely in progress. The order SHALL
be derived from the Connector's position and hold and the runs' states, not re-parsed from labels in
the UI, the same rule `pbi-details` follows for the position.

#### Scenario: A held PBI leads the folder
- **WHEN** a folder has a held PBI and a PBI in progress
- **THEN** the held PBI rows above the one in progress

#### Scenario: A live run leads the unattended
- **WHEN** a folder has a PBI with a live run and a PBI at rest
- **THEN** the live PBI rows above the one at rest

### Requirement: The rail's text search finds PBIs by key and title
`Sidebar`'s text search (`nav.filterProjects`) SHALL match a folder's PBIs by `${folder.name}
${pbi.key} ${pbi.title}` — not by branch name. A query that matches no PBI in a folder SHALL hide that
folder's PBIs, and a folder with no matching PBIs SHALL hide (or show an empty note consistent with the
design set), the way the prototype's `Filtrar PBIs…` field reduces the rail.

#### Scenario: A search matches a PBI by key
- **WHEN** the search field holds a PBI's key
- **THEN** only folders whose PBIs match by key or title show their matching rows

#### Scenario: A search that matches nothing is named
- **WHEN** the search field holds a string no PBI carries in key or title
- **THEN** the rail states that nothing matched, rather than showing every PBI

### Requirement: Live narrows to PBIs with a live run, and a hold is not a live run
The rail's `Live` filter SHALL narrow the rows to PBIs that have a run which has not finished, and a
PBI that is held SHALL NOT be shown as a live run. A row SHALL carry a **hold badge** (a person is
waited on) and a **live marker** (a run is in progress) as separate affordances, and `Live` SHALL
include the held PBI only when it also has a live run — because a hold is a wait, not execution. The
live count SHALL be the count of PBIs with a live marker, not the count of runs.

#### Scenario: A held PBI is not shown as live
- **WHEN** a PBI is held and has no live run
- **THEN** its row shows the hold badge and no live marker, and `Live` excludes it

#### Scenario: A run in progress shows a live marker
- **WHEN** a PBI has a run that has not finished
- **THEN** its row shows the live marker, and `Live` includes it

### Requirement: The folder keeps its Change tab until the voyage carries it
`FolderDetailScreen`'s tab strip SHALL keep `overview`, `change`, `files`, `commands` and `loop` as
master has them. The proposal asked for the `change` tab to move to the PBI's voyage, but the voyage
#446 (#450) built draws the PBI's loop-stages and runs by identity (`PbiSpine`) and does not carry
the diff, pull-request and evidence reading `ChangeTab` serves from the worktree; removing the tab
here would strand `ChangeTab` and the review-step rail, so the tab stays. A future change that
absorbs `ChangeTab`'s reading into the voyage will move it then. The folder's `loop` and `commands`
tabs, which are the checkout's own, SHALL also stay.

#### Scenario: The folder's tabs include Change
- **WHEN** a folder screen is open on a worktree
- **THEN** its tab strip offers Overview, Change, Files, Commands and Loop as master does

### Requirement: A `?worktree=` link opens its PBI's voyage only when the relation is unambiguous
A `?worktree=` query on a folder route SHALL resolve to the PBI whose voyage owns that worktree when
the relation is unambiguous — the worktree's branch is a loop claim branch of one PBI of this folder
(`change/<n>` matching `#<n>`, the convention `.harness/aio/prepare` and `.harness/aio/claim`
mint), found by full identity (`folderFor`), the same join the rail and Overview perform — and the
URL names no tab, and SHALL open that PBI's voyage, replacing the folder route so Back does not leave
the folder screen under it. It SHALL NOT select a PBI that shares a key or path with another, the
same rule `pbi-details` enforces for its entries.

When the URL names a tab the screen stays on the folder and serves it: `?worktree=` with `&tab=change`
is the folder's own ChangeTab reading of a worktree, not a bookmark for a journey, and the change kept
that reading on the folder rather than move it to the voyage (see the requirement that the folder
keeps its Change tab). When the worktree named has gone (the server resolved the parameter to the
checkout), when no PBI claims its branch, or when more than one does, the screen SHALL stay on the
folder and serve the tab the URL asked for, the same behaviour it had before this change. The stricter "explain rather than
stay" the proposal carried is amended: opening the voyage on anything less than a proven one-PBI
match would risk the reselection the rule exists to prevent, so the unambiguous case opens and the
rest stay, leaving the rail and the inbox as the way a person reaches a voyage from an ambiguous
link. The "never reselecting another PBI that shares a key or path" half of the rule is held by never
redirecting on anything less than one.

#### Scenario: A worktree link resolves to its PBI
- **WHEN** a folder route carries `?worktree={path}` and the worktree's branch is `change/{n}` for
one PBI of this folder whose key is `#{n}`, and the URL names no tab
- **THEN** the client opens that PBI's voyage, replacing the folder route

#### Scenario: A worktree link that names a tab stays on the folder
- **WHEN** a folder route carries `?worktree={path}` and `?tab=` for a PBI of this folder
- **THEN** the client stays on the folder and serves that tab, and does not open the voyage

#### Scenario: A worktree link with no PBI match stays on the folder
- **WHEN** a folder route carries `?worktree={path}` whose branch names no PBI of this folder
- **THEN** the client stays on the folder and serves the tab the URL asked for, and does not open
another PBI

#### Scenario: A worktree link that names a gone worktree stays on the checkout
- **WHEN** a folder route carries `?worktree={path}` the server resolved to the checkout
- **THEN** the client stays on the checkout's scope and the folder's tab strip, and does not redirect

### Requirement: Empty states, no-match and read errors are distinct on the rail and History
The rail and the History screen SHALL distinguish four states the way `pbi-details`'s entries do:
**loading** while the read is on its way, **empty** when the read answered and there is nothing, **no
match** when a filter or search left nothing, and **error** when the read failed, with the failure's
own words. An error SHALL NOT be shown as empty, and empty SHALL NOT be shown as no match.

#### Scenario: A failed rail read is an error
- **WHEN** the inbox read fails while the rail is open
- **THEN** the rail shows the error with the failure's words, not an empty rail

#### Scenario: A History with no journeys is empty, not an error
- **WHEN** the History read answered and there are no closed journeys
- **THEN** History shows an empty state, not an error and not "no match"

#### Scenario: A filter that matches nothing is no-match
- **WHEN** History or the rail is filtered and nothing matches
- **THEN** the screen names the no-match, distinct from empty and from error

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

