# shared-control-registry Specification

## Purpose
TBD - created by archiving change p0-registry-reservations. Update Purpose after archive.
## Requirements
### Requirement: Exact repository registry CAS

The system SHALL store a versioned registry in `control.json` on
`refs/heads/harness-control-v1`. Every new proposal MUST contain a unique mutation UUID and
name the observed OID as its sole parent, or have no parent for confirmed-absent bootstrap.
Updates MUST use receive-pack with an explicit exact expected OID and full read-back verification.
One CAS serializes every mutation kind alike — bootstrap, acquire, release, grant, revoke — so
racing writers produce one confirmed result and one typed conflict, never two applied effects.

#### Scenario: Bootstrap race
- **WHEN** two independent clones propose distinct successors to confirmed absence
- **THEN** exactly one becomes confirmed and the other does not acquire a reservation

#### Scenario: Stale successor
- **WHEN** a writer proposes against an OID that another writer has replaced
- **THEN** the stale writer cannot overwrite the registry and no automatic retry occurs

### Requirement: Atomic claim and repository capacity

The system SHALL use a validated PBI identity containing provider, canonical API host including
port, repository and provider key. A claim and its single repository WIP slot MUST change in one
registry commit. Active and draining claims MUST occupy capacity. A released claim MUST retain its
generation, and subsequent acquisition MUST increase it.

The rename of the identity type in code, from `ControlTaskIdentity` to `ControlPbiIdentity`, SHALL
NOT change the tuple it serialises: the stored registry, the intents already journalled and the
effects already derived keep their shape, and a reservation made before the rename still matches a
claim made after it.

#### Scenario: Last slot race
- **WHEN** two independent clones claim different PBIs with one slot remaining
- **THEN** at most one claim and its reservation become confirmed

#### Scenario: Stale owner
- **WHEN** release names a previous owner, generation or reservation
- **THEN** it is refused without changing the registry

#### Scenario: The renamed identity still matches old reservations
- **WHEN** a claim is made after the rename against a reservation recorded before it
- **THEN** both serialise the same `{Provider, Host, Repository, Key}` tuple and the claim resolves

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

### Requirement: The registry is bootstrapped by an explicit owner command
A control registry SHALL be initialized only by an explicit host command a person runs, taking the
remote, the repository identity, the policy pin and the capacity limit as arguments. The command
SHALL perform the empty-lease initial push the contract specifies and SHALL print the ref-protection
precondition (deny force-push and deny delete on the control ref) where the person bootstrapping
will read it. A watcher tick SHALL NOT initialize a registry; observing no registry SHALL be logged
and suppress that folder's candidates without being an error. Running the command against an
existing registry SHALL return a typed conflict naming the observed revision.

#### Scenario: Bootstrap creates the control ref
- **WHEN** the command runs against a bare remote with no control ref
- **THEN** the ref exists carrying the pin and the declared capacity, and the output names the
  ref-protection precondition

#### Scenario: Bootstrap is refused loudly, not silently
- **WHEN** the command runs a second time against an existing control ref with the same pin
- **THEN** it fails with a typed conflict naming the observed revision, and the registry is
  unchanged

#### Scenario: Absence is not an invitation
- **WHEN** a watcher tick observes a folder whose repository has no control ref
- **THEN** no registry is created, the folder's candidates are suppressed with the reason logged,
  and no other folder is affected

### Requirement: Grant and revoke are ledger operations with exact bindings
An `approve-candidate` grant SHALL be recorded only by an explicit owner-operation through the
control CLI, binding actor, Task identity, candidate HEAD, tree, base, spec pin, authorization
epoch and expiry in the registry document by CAS. A grant SHALL occupy no capacity and claim no
task. Re-submitting an identical binding under the same grant id SHALL replay its recorded
result; a different binding under the same id SHALL conflict. A revocation SHALL be append-only
and SHALL be an operation of the same shape. Reading grants SHALL answer the domain question —
which grants for this Task are live at a caller-supplied now — applying expiry, epoch and
revocation at read time; the document stores facts, the read decides validity. A grant on an
unbootstrapped registry SHALL be refused.

#### Scenario: The same grant twice
- **WHEN** the owner resubmits an identical approve-candidate grant by grant id
- **THEN** the replay returns the recorded grant and the document is unchanged

#### Scenario: Two grants, one task, different candidates
- **WHEN** two live grants bind different candidate SHAs of the same Task
- **THEN** both are readable, each answering only for the candidate it names

#### Scenario: Race
- **WHEN** two clones grant different candidates for the same Task from the same expected
  revision
- **THEN** one CAS wins and the other conflicts, typed, with the observed revision

#### Scenario: Expired at read
- **WHEN** a grant's expiry is before the now a caller supplies
- **THEN** the read does not return it as live

### Requirement: A blocking claim escalates to the blocked task's own issue
A coordinator that observes a claim it does not own, blocking a candidate it would otherwise
acquire, with no observable progress over the policy-declared window, SHALL publish one typed
attention comment on that task's issue through the World comment-writer seam. The comment SHALL
carry the reason code, claim owner, generation, last known effect and the remedy. The escalation
SHALL be published at most once per owner and generation. The coordinator SHALL NOT take over the
claim, drain it, or modify the registry as part of escalating. A failed or unknown comment write
SHALL NOT be retried within the tick and SHALL be logged.

An uncertain journal intent has its own escalation, disjoint from a claim's: an effect `unknown`
past the declared window escalates once keyed on its effectId, carrying the effect's identity and
its last recorded state, through the same World comment-writer seam. The two keys never merge — a
claim is a long-lived owner fact, an effect is a per-dispatch fact — and neither escalation takes
over, retries, or resubmits what it names.

#### Scenario: One comment, not one per tick
- **WHEN** the same blocked claim persists across ticks and the comment for its owner and
  generation already exists
- **THEN** no second comment is published

#### Scenario: Escalation never takes over
- **WHEN** a claim is escalated
- **THEN** the registry is unchanged by the escalation itself, and the remedy names the
  owner-operation

#### Scenario: An unknown effect escalates on its own key
- **WHEN** an effect past the window is still `unknown` and observation establishes nothing
- **THEN** one comment keyed on the effectId is published, and a later tick facing the same
  unchanged unknown publishes nothing

