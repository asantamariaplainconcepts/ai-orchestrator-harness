## ADDED Requirements

### Requirement: Exact repository registry CAS

The system SHALL store a versioned registry in `control.json` on
`refs/heads/harness-control-v1`. Every new proposal MUST contain a unique mutation UUID and
name the observed OID as its sole parent, or have no parent for confirmed-absent bootstrap.
Updates MUST use receive-pack with an explicit exact expected OID and full read-back verification.

#### Scenario: Bootstrap race
- **WHEN** two independent clones propose distinct successors to confirmed absence
- **THEN** exactly one becomes confirmed and the other does not acquire a reservation

#### Scenario: Stale successor
- **WHEN** a writer proposes against an OID that another writer has replaced
- **THEN** the stale writer cannot overwrite the registry and no automatic retry occurs

### Requirement: Atomic claim and repository capacity

The system SHALL use a validated Task identity containing provider, canonical API host including
port, repository and provider key. A claim and its single repository WIP slot MUST change in one
registry commit. Active and draining claims MUST occupy capacity. A released claim MUST retain its
generation, and subsequent acquisition MUST increase it.

#### Scenario: Last slot race
- **WHEN** two independent clones claim different Tasks with one slot remaining
- **THEN** at most one claim and its reservation become confirmed

#### Scenario: Stale owner
- **WHEN** release names a previous owner, generation or reservation
- **THEN** it is refused without changing the registry

### Requirement: Conservative observations and release

The system SHALL distinguish confirmed, absent and unknown observations. Unknown reads, missing
documents on an existing ref, unsupported schema, inconsistent records and oversized documents
MUST block mutation. It MUST NOT take over claims on timeout or restart. Release MUST require
confirmed quiescence and absent unsettled effects in addition to matching ownership.

#### Scenario: Missing document
- **WHEN** the control ref exists but its tree lacks the required document
- **THEN** read reports unknown, not absent, and acquisition is blocked

#### Scenario: Unknown outcome or restart
- **WHEN** an acquisition acknowledgement is lost or another instance restarts
- **THEN** capacity remains reserved until an explicit owner-matched quiescent release

### Requirement: Operation identity and bounded history

The system SHALL retain bounded operation receipts and claim tombstones. Idempotent confirmation
MUST require the same operation UUID and identical request identity. Reusing an operation UUID
with different arguments MUST be refused. Exhausting bounds MUST refuse mutation rather than
discarding ownership history.

#### Scenario: Repeated request
- **WHEN** a confirmed operation is repeated with its original request identity
- **THEN** its receipt is returned without another registry mutation

#### Scenario: Reused operation UUID
- **WHEN** an operation UUID is supplied with another Task, owner or operation
- **THEN** it is refused

### Requirement: Inactive primitive and checkout isolation

The system SHALL provide only explicit registry operations with no active dispatcher. Registry
I/O MUST NOT change the checkout's HEAD, index or worktree and MUST NOT expose credentials through
diagnostics. Registry confirmation MUST NOT be described as fencing GitHub or OS effects.

#### Scenario: Registry mutation in a dirty checkout
- **WHEN** acquisition or release executes in a checkout with staged and unstaged changes
- **THEN** its branch HEAD, index and working files remain unchanged
