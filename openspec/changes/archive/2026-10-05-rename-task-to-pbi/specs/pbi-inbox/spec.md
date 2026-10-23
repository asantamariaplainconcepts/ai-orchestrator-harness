# pbi-inbox Delta

## ADDED Requirements

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
