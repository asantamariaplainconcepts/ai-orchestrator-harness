# read-the-vendors Delta

## RENAMED Requirements

- FROM: `### Requirement: A task the snapshot does not carry is fetched from the vendor by key`
- TO: `### Requirement: A PBI the snapshot does not carry is fetched from the vendor by key`

## MODIFIED Requirements

### Requirement: One read of every vendor is shared for a configured lifetime
`PbiSnapshot` SHALL be one in-memory read per process of what every configured vendor answered to
`IPbiSource.Poll()`, reused for `PbiSources:CacheSeconds` seconds — **60** when the setting is absent
or negative, and a fresh read on every request when it is zero. The configuration key keeps its
historical spelling `TaskSources:CacheSeconds` — it is a setting existing deployments already carry,
and this change renames types, not configuration on disk. Callers that ask while a read is in flight
SHALL share that read rather than start another, so the inbox, the sidebar and the label watcher
asking together cost one poll of each vendor. The shared read SHALL NOT run on any one caller's
cancellation token: one caller giving up SHALL NOT cancel it for the others. A read that faulted SHALL
NOT be reused. A scope that names one repository twice SHALL yield one PBI each, identified by vendor,
repository and key.

#### Scenario: Reads inside the lifetime ask the vendor once
- **WHEN** the inbox is read twice within the configured cache seconds
- **THEN** the vendor is polled once

#### Scenario: Concurrent readers share one refresh
- **WHEN** several screens ask for the PBIs while a read is in flight
- **THEN** all of them are answered from that one read

### Requirement: Any write, "Sync now" and a verified webhook empty the snapshot
Every write this product makes at a vendor — status, hold release, labels, assignees — SHALL be made
at the vendor **first** and SHALL then empty the snapshot, so the next read polls again and sees the
change by the same path a change made in a browser arrives by. Nothing SHALL edit the PBIs the
snapshot holds. A write that changed nothing at the vendor SHALL NOT empty it.

The system SHALL expose `POST /api/pbis/sync` (`Features/Inbox/UseCases/SyncPbis.cs`,
`Access.AnyCaller`) which SHALL empty the snapshot, read every configured vendor again, and answer
`{ read, failures }`: how many PBIs the vendors answered with, open and closed, and per source label
what went wrong — empty when every vendor answered.

#### Scenario: Sync now asks the vendor again
- **WHEN** `POST /api/pbis/sync` is called inside the snapshot's lifetime
- **THEN** the vendor is polled again and the answer counts what it returned

#### Scenario: A write is seen by the next read
- **WHEN** a label is written at the vendor through this product
- **THEN** the snapshot is emptied and the next read polls the vendor

### Requirement: A PBI the snapshot does not carry is fetched from the vendor by key
`IPbiSource.GetPbi(key, url)` SHALL fetch one PBI from the vendor, and every vendor reader SHALL
implement it. Resolving a PBI by key SHALL look in the snapshot first — narrowed by the vendor and the
repository when the caller names them, and ordered open before closed, then by vendor, then by URL,
so a caller holding only a number gets the same PBI every time — and only when the snapshot has none
SHALL ask each vendor that could have it. A vendor that fails during that fallback SHALL be passed
over, and when no vendor has the PBI the answer SHALL be "not found". This is how a closed issue a
run still reads is found after it has fallen out of the vendor's closed window.

#### Scenario: A closed task the vendor no longer returns is fetched by key
- **WHEN** a run asks for a closed PBI that the last poll did not return
- **THEN** it is fetched from the vendor by key and returned

#### Scenario: A task no vendor has is not found
- **WHEN** neither the snapshot nor any vendor has the key
- **THEN** the read is a 404

### Requirement: Only a vendor with a scope is read, and a missing credential is that vendor's failure
`PbiSourceCatalog` SHALL build a reader only for a vendor whose scope configuration is set; a vendor
with no scope SHALL have no reader, SHALL NOT be polled, and SHALL NOT be reported as a failure.
GitHub SHALL be read even with no `SecretName`, unauthenticated. Every other vendor with a scope
SHALL require a credential, asked for by its reader at the moment of use, so a missing or
unresolvable one SHALL be reported as that connector's failure rather than making the vendor
silently absent.

#### Scenario: An unconfigured vendor is not an error
- **WHEN** a vendor has no scope configured
- **THEN** nothing reads it and no failure is reported

#### Scenario: A scope with no credential is a failure
- **WHEN** Jira, Linear or Azure DevOps has a scope and no credential that resolves
- **THEN** the read reports a failure against that vendor

### Requirement: GitHub is asked for open and closed separately, per repository
The GitHub reader SHALL ask for open and closed states separately, most recently updated first, for
every repository in the scope, and SHALL return both answers as one set carrying each PBI's state. A
pull request SHALL still arrive and SHALL still be told apart from an issue, and SHALL be read through
the GitHub connector — there is never a pull-request connector of its own.

#### Scenario: Both states arrive as one set
- **WHEN** a repository is polled
- **THEN** its open and closed PBIs arrive together, each carrying its state

#### Scenario: A pull request is read through the GitHub connector
- **WHEN** the GitHub scope has an open pull request
- **THEN** it arrives as a pull request, and a GitHub failure is what affects it
