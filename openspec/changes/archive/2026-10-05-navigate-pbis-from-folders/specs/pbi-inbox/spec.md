## MODIFIED Requirements

### Requirement: The inbox is filtered on the server, because the counts have to agree with the list
The system SHALL expose `GET /api/pbis` (`Features/Inbox/UseCases/ListPbis.cs`,
`Access.FiltersToCaller`) returning the inbox filtered **server-side**. A client that filtered a full
payload would have to recompute the source chips' counts itself and would eventually disagree with
the server about what a source includes — pull requests are a separate source. The filters SHALL be a
source (`?source=`) and any number of kinds (`?kind=`, repeated); there SHALL be no folder filter.
Both count dictionaries SHALL be computed over the **whole** set, so a chip reading "bug 0" never
means "there are none", and the browser SHALL only ever receive the list after the filters ran, so it
has nothing to count a removed PBI from. The list SHALL keep the vendor's own order — most recently
updated first for every reader — rather than sorting it again, and a closed PBI SHALL NOT be in the
inbox — it is a to-do list, not an archive.

The rail's per-folder PBI rows SHALL be a client-side reduction of the same inbox read, matching each
PBI's `origin` against the folder's remotes (`folderFor`) the way `OverviewTab`'s `usePbisHere` does,
and SHALL NOT ask the server for a per-folder list or a folder-scoped count — the server holds no
folder filter, and the rail is a view of the inbox, not a second source.

#### Scenario: A source filter narrows the list and leaves every count alone
- **WHEN** the list is filtered to one source
- **THEN** the rows narrow and every count stays over the whole set

#### Scenario: The list keeps the vendor's order
- **WHEN** a vendor answers its PBIs most recently updated first
- **THEN** the inbox lists them in that order

#### Scenario: A pull request counts and filters apart from an issue
- **WHEN** the source chips are drawn
- **THEN** pull requests are their own source, counted and filtered separately

#### Scenario: A closed PBI leaves the inbox
- **WHEN** the vendor reports a PBI as closed
- **THEN** it is not in the inbox list

#### Scenario: The rail reduces the inbox, not a second read
- **WHEN** the rail's `Projects` group lists a folder's PBIs
- **THEN** the rows come from the same inbox read the inbox screen uses, reduced client-side by
`folderFor`, and no per-folder fetch is made

## ADDED Requirements

### Requirement: History groups journeys by the PBI's full identity, globally
The History screen SHALL group runs into journeys by the PBI's **full identity** — source, host,
repository and key together — the same join `PbiWalk` performs (`run.key === task.key &&
run.folderPath === repo.path`), and not by `run.key` alone. A journey SHALL stay present in History
after its worktree is removed, because the runs-by-identity read does not depend on a working copy
existing. Two PBIs that share a remote key under different repositories or folders SHALL keep separate
histories, the rule `pbi-details` enforces for reads and writes. History SHALL be global — every
folder, every repository — and SHALL NOT be a per-folder grouping; the folder-local `useClosedJourneys`
reading `OverviewTab` uses stays local and is not widened.

#### Scenario: A closed journey stays after its worktree is gone
- **WHEN** a PBI's worktree is removed and History is opened
- **THEN** the journey's runs are still listed, grouped by full identity

#### Scenario: Two PBIs sharing a key keep separate histories
- **WHEN** two repositories each hold a PBI numbered 12 and History is opened
- **THEN** two journeys appear, one per repository, and neither absorbs the other's runs

#### Scenario: History is global across folders
- **WHEN** History is opened and a PBI was closed in a folder other than the one open
- **THEN** its journey appears, because History is not scoped to a folder

### Requirement: History reuses the runs-by-identity join the voyage serves
The History screen SHALL group runs into journeys by the PBI's full identity — the same join `PbiWalk`
performs (`run.key === task.key && run.folderPath === repo.path`) — and SHALL discover which journeys
exist from the runs table the sidebar and the journey header already read, joined client-side exactly
the way `PbiWalk` joins. The served `GET /api/runs/by-pbi` answers one PBI's runs and is not a
catalog of journeys, so History's global question is answered by the full-identity join over the runs
already read — the join `design.md` sanctions as the global path — rather than by a per-journey fetch.
The four connection states (loading, error, absence, unattributed) SHALL be named as themselves on a
journey's read, the way `pbi-details`'s entries name them, and a failed read SHALL be an error, not
an absence.

#### Scenario: A journey's runs come from the same runs table the rail reads
- **WHEN** a journey is opened in History
- **THEN** its runs are read by full identity from the runs table already read for the rail and the
journey header, joined client-side the way `PbiWalk` joins, and no per-journey fetch is made

#### Scenario: A failed journey read is an error, not an absence
- **WHEN** the runs read for History fails
- **THEN** History shows the error with the failure's words, and the journey is not shown as empty
