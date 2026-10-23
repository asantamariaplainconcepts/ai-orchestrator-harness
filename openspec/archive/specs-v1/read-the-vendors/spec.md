# read-the-vendors Specification

## Purpose
Reading every configured vendor's whole set of tasks and holding the answer in memory for a short while, so
the screens that ask together share one read. The vendor is the record and nothing is copied to disk: the
snapshot (`Features/Inbox/TaskSnapshot.cs`) is a cache of the last `ITaskSource.Poll()` answers, emptied by
every write, by "Sync now" and by a verified webhook. A webhook is an optimisation that can only make the
inbox current sooner, never make it wrong. Reverse-engineered from the shipped code, not negotiated ahead of
it.
## Requirements
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

### Requirement: One vendor's failure is not the read's, and it keeps its last good answer
A vendor that fails SHALL NOT stop the others being read; its failure SHALL be reported per source label in
the read, and that vendor SHALL contribute the tasks it **last answered successfully** in this process, so a
network blip does not empty its half of the inbox. Each connector's last read time and last failure SHALL be
held in memory, and a vendor that answers again SHALL stop reporting its last failure. Nothing of this
survives a restart: after one, a vendor that fails contributes nothing until it answers.

#### Scenario: A vendor that fails lets the others through
- **WHEN** one of several configured vendors fails
- **THEN** its failure is reported and the other vendors' tasks are read

#### Scenario: A vendor that fails keeps what it last answered
- **WHEN** a vendor answered once and fails on the next read
- **THEN** its tasks from the earlier answer are still in the read

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

### Requirement: The webhook endpoint is additive, unauthenticated by design, and reads nothing
The system SHALL expose `POST /api/webhooks/{vendor}` (`ReceiveWebhook.cs`) which, once the delivery's
signature verifies, SHALL only empty the snapshot so the next read goes to the vendor. It SHALL answer
**202 Accepted**, not 200, because nothing has been read yet. It SHALL NOT act on the payload: the body says
which issue changed and the next read takes the whole set anyway. It SHALL NOT be a use case — no `ICommand`,
nothing through the dispatch pipeline — because every request in that pipeline declares what it requires of
its caller and a vendor is not a caller. What authorizes the request SHALL be the signature over its body
(an HMAC for GitHub, Jira and Linear, the latter with a freshness check on its signed timestamp; a Basic
password for Azure DevOps), checked in the endpoint where a reviewer can see it. A vendor with no
`TaskSources:<Kind>:WebhookSecretName` SHALL answer 404 as though the route did not exist, a body over the
ceiling SHALL be refused before verification completes, and a delivery that does not verify SHALL be 401 and
SHALL NOT empty the snapshot.

#### Scenario: A verified delivery empties the snapshot and nothing else
- **WHEN** a correctly signed webhook says which issue changed
- **THEN** the snapshot is emptied, the answer is 202, and the payload is not acted on

#### Scenario: An unverified delivery changes nothing
- **WHEN** a delivery's signature does not verify
- **THEN** it is refused with 401 and the snapshot is left as it was

#### Scenario: A refused or absent webhook changes only latency
- **WHEN** the webhook is never configured
- **THEN** the inbox is as current as the snapshot's lifetime

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

### Requirement: The snapshot holds the position the vendor answered, never one deduced
The snapshot SHALL carry, per PBI, the position and hold the vendor's answer gave — the Connector's
translation applied to what the vendor actually said — and SHALL NOT hold a position deduced from the
last run, the last write, or the product's own move. A refresh SHALL take the position from the
vendor's fresh answer, so a change made in a browser and one made through this product are
indistinguishable downstream. The read side SHALL NOT write anything at the vendor. A vendor whose
position could not be read, in a read the rest of which succeeded, SHALL contribute the rest of its
answer with the position as `unknown` and the failure named, rather than having its whole answer
dropped or its position defaulted.

#### Scenario: A remote change arrives on the next read
- **WHEN** a position is changed at the vendor outside this product and the snapshot is emptied
- **THEN** the next read polls the vendor and the position shown is what it answered

#### Scenario: The last run does not decide the position
- **WHEN** a PBI's run finished and the vendor still answers an earlier position
- **THEN** the snapshot holds the vendor's answer, and no code path writes a position implied by the run over it

#### Scenario: A vendor that failed its position read keeps the rest of its answer
- **WHEN** a vendor answers its PBIs but the position part of the read fails
- **THEN** those PBIs arrive with position `unknown` and the failure named, and neither the rest of the read nor the other vendors is affected

