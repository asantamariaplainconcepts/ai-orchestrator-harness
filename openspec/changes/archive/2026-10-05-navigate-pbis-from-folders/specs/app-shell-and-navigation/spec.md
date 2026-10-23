## MODIFIED Requirements

### Requirement: Seven routes inside one shell, with the inbox as the landing screen
`src/frontend/app/routes.tsx` SHALL declare one shell route rendering `AppShell` with children
`pbis`, `folders`, `folders/:folderId` and `settings`, and the index SHALL redirect to `/pbis`. A
client route asked for directly SHALL come back as the application, and moving between screens SHALL
be the router rather than a page load. A request for `/workflows` SHALL redirect to `/pbis` rather
than 404, because the route was a published link and a bookmark that followed it did nothing wrong.
A request for `/runs/:runId` SHALL redirect to the run's owning folder screen scoped to its working
directory with the run handed to the tab strip, for the same reason — and `/history` SHALL render the
global History screen (the journeys grouped by PBI identity), because the question History answers is
global, not per worktree. A request for the old `/tasks` and `/tasks/pull-requests` routes SHALL
redirect to `/pbis` and `/pbis/pull-requests` respectively, for the same reason a bookmark is never
punished.

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

#### Scenario: A worktree link resolves to its PBI
- **WHEN** a folder route carries `?worktree={path}`, names no tab, and the worktree identifies one
PBI by full identity
- **THEN** the client opens that PBI's voyage

#### Scenario: An ambiguous worktree link explains rather than reselecting
- **WHEN** a folder route carries `?worktree={path}` whose relation to a PBI is not unambiguous
- **THEN** the client shows an explanation naming the reason, and does not open another PBI that
shares a key or path

### Requirement: History, settings and theme live at the bottom of the rail
`Sidebar` SHALL draw History, Settings and Theme as three items in a column at the bottom of the rail, beside or below the "this machine" card. History SHALL be a `NavLink` to `/history` and SHALL navigate to the global History screen (not a redirect), Settings a `NavLink` to `/settings`, and Theme a button that toggles the theme — the same icons, labels and behaviour they had in the header, moved rather than re-implemented. At 220px (the rail minimum) the three items and the machine card SHALL fit without overflow.

#### Scenario: Settings is reachable from the rail
- **WHEN** the rail is open
- **THEN** a Settings link is at the bottom of the rail, and clicking it navigates to `/settings`

#### Scenario: History is reachable from the rail
- **WHEN** the rail is open
- **THEN** a History link is at the bottom of the rail, and clicking it navigates to the History
screen grouping journeys by PBI identity

#### Scenario: Theme is toggled from the rail
- **WHEN** the theme button at the bottom of the rail is pressed
- **THEN** the theme switches, the same way it did from the header

#### Scenario: The bottom of the rail fits at minimum width
- **WHEN** the rail is 220px wide
- **THEN** History, Settings, Theme and the "this machine" card all fit without horizontal overflow

## ADDED Requirements

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
(`change/<n>` matching `#<n>`, the convention `.harness/scripts/prepare` and `.harness/scripts/claim`
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
