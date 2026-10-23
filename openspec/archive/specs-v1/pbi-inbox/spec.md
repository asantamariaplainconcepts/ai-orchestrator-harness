# pbi-inbox Specification

## Purpose
TBD - created by archiving change rename-task-to-pbi. Update Purpose after archive.
## Requirements
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

### Requirement: A kind is the vendor's own spelling, and the filter is over kinds the PBIs carry
A kind filter SHALL match the vendor's own spelling, SHALL treat a kind containing a comma as one
kind, SHALL list the PBIs carrying either of two kinds when two are given, SHALL list nothing for a
kind no PBI carries, and SHALL treat "all" as the unfiltered list. A promoted `status:` label SHALL
NOT be a kind, while an unrecognised label SHALL be one.

#### Scenario: Two kinds list either
- **WHEN** two kinds are selected
- **THEN** PBIs carrying either are listed

#### Scenario: A loop label is not a kind
- **WHEN** a PBI carries a `status:` label the product promotes
- **THEN** that label is not offered as a kind, while an unrecognised label is

### Requirement: Sync asks every vendor for a fresh answer, and an empty only clears what nobody owns
The system SHALL expose `POST /api/pbis/sync` (`SyncPbis.cs`, `Access.AnyCaller`) asking every
configured vendor for a fresh answer and answering with the workspace's read. A read that failed
SHALL NOT be mistaken for an empty set — an inbox that clears because one vendor was unreachable
would tell a person their work is gone.

#### Scenario: A failed read answers with the workspace's read, not an empty inbox
- **WHEN** a vendor cannot be reached during a sync
- **THEN** the answer is the workspace's current read rather than an inbox emptied by the failure

#### Scenario: A connection revived mid-watch refreshes on its own
- **WHEN** a vendor was unreachable in one sync and answers in the next
- **THEN** the inbox shows what it answered without a manual reload

### Requirement: The inbox is a live list — a PBI that changed while the client was holding it becomes current
A client that opens a PBI from the inbox SHALL have what it opened: the row it clicked SHALL be
drawn from the workspace's live read, and a PBI changed at the vendor between the click and the
fetch SHALL answer its current state rather than the state the click saw.

#### Scenario: The vendor's change lands in the open detail
- **WHEN** a PBI's state changes at the vendor between a click and the detail's read
- **THEN** the detail answers the current state, and nothing claims the row was stale

### Requirement: One PBI is read by key, whatever its state, and the repository disambiguates it
The system SHALL expose `GET /api/pbis/{key}` (`GetPbi.cs`, `Access.FiltersToCaller`) as the one read
that ignores the open filter, because a run still needs its issue after the person closes it — closing is
the natural end of the job and exactly when the person re-reads it. A key alone SHALL NOT be treated as
identifying a PBI: with two repositories in one scope, `#217` is two PBIs, so the caller SHALL be able to
name the repository, a mismatch SHALL be a 404 rather than the other PBI, the match SHALL ignore case, and a
caller that cannot name one SHALL still get a **deterministic** answer — open before closed, then by vendor,
then by URL. A PBI the snapshot does not carry SHALL be fetched from the vendor by key before the read
answers 404. The response SHALL carry the PBI's vendor state and `readAt`: when the PBI was last read
from the vendor — the snapshot's read time, or now for a PBI fetched by key.

#### Scenario: A key in the named repository is the one returned
- **WHEN** the read names a repository
- **THEN** the PBI from that repository is returned, and a key absent from it is a 404

#### Scenario: Without a repository the answer is at least deterministic
- **WHEN** two repositories hold the same key and the read names neither
- **THEN** the answer is the same one every time rather than whichever vendor answered first

### Requirement: A PBI row carries only what the vendor said
Every field of a PBI row — key, source, title, body, labels, meta, URL, state, whether the body was cut —
SHALL come from the vendor's last answer, with the loop status and the hold derived from its labels. This
product SHALL add no field of its own to a PBI: there SHALL be no folder attribution and no suggested
workflow on the row, and a screen that needs a PBI's checkout SHALL resolve it from the PBI's URL.

#### Scenario: A refresh replaces everything on the row
- **WHEN** the vendor changes a PBI's title or labels and the snapshot is read again
- **THEN** the row shows the vendor's new values and nothing of the earlier read survives

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

### Requirement: Inbox rows open the PBI screen, and Split draws the same body

A row of the inbox's Table and Board layouts SHALL navigate to the PBI's own screen. The Split
layout SHALL draw, beside the list, the same body the PBI screen draws for the selected PBI, so the
inbox carries no second reading of a PBI.

#### Scenario: A Board card opens the PBI screen

- **WHEN** a card of the Board layout is opened
- **THEN** the PBI screen for that key and folder is drawn

#### Scenario: Split draws the PBI body

- **WHEN** a row is selected in the Split layout
- **THEN** the pane beside the list draws the PBI screen's tabs for it, not a separate panel

### Requirement: Needs a person draws one row per blocked PBI

The inbox's *Needs a person* panel SHALL draw one row per PBI identity and cause — provider, host,
repository, key and cause together — however many attention rows name it; the newest run's row
SHALL stand for the group.

#### Scenario: Two failed runs of one PBI

- **WHEN** the attention queue holds two `run-failed` rows for `#185` of one repository
- **THEN** the panel draws `#185` once

