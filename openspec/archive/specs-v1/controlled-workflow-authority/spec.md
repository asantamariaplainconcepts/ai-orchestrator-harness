# controlled-workflow-authority Specification

## Purpose
TBD - created by archiving change controlled-workflow-authority. Update Purpose after archive.
## Requirements
### Requirement: Shared authority is opt-in and distinct from local execution
Shared controlled dispatch SHALL remain disabled until published policy, authenticated human
grants, safe claim/capacity reservation and durable CAS-admitted effect permits are
available and explicitly enabled. An inactive primitive or a Proposed ADR SHALL NOT confer
activation authority. GitHub SHALL remain authoritative for Task phase and Hold, git for
pinned policy/specification, and SQLite for local Run history.

#### Scenario: Only local controlled execution exists
- **WHEN** an installed host supports a local controlled Workflow but no shared authority path
- **THEN** shared dispatch is refused and local execution cannot advance shared controlled phase

#### Scenario: A primitive is implemented before the whole path
- **WHEN** local idempotency or remote reservation support ships without approved activation
- **THEN** it remains opt-in and no watcher obtains permission to launch from its existence

### Requirement: Task and transition identities are unambiguous
Every controlled operation SHALL carry provider, canonical host, canonical repository and
provider key. Run, PR, Workflow, transition and effect identities SHALL be distinct. Unknown
providers and identity mismatches SHALL stop admission. One Task SHALL have at most one
active PR association under version 1; prior Runs SHALL remain history.

#### Scenario: Equal issue numbers are not equal Tasks
- **WHEN** two repositories or hosts each contain issue 42
- **THEN** their claims, failures, grants and effects remain separate

#### Scenario: A repository is renamed or transferred
- **WHEN** provider immutable IDs and the enrolled identity no longer match
- **THEN** existing grants are not silently remapped and dispatch waits for explicit resolution

### Requirement: Observations distinguish absence from uncertainty
The decision SHALL consume observed, absent or unknown inputs with provenance and freshness.
A failed, stale or incomplete read SHALL NOT be treated as absence. The same snapshots,
policy pin and explicit time SHALL produce the same decision. Missing prerequisites,
unsupported input, ambiguous phases and unresolved dependencies SHALL produce zero dispatches.

#### Scenario: The provider is unavailable
- **WHEN** the Hold or ownership read fails
- **THEN** the decision is blocked with an observation reason rather than assuming no Hold or owner

#### Scenario: An aggregate epic is supplied
- **WHEN** validated Task input identifies a coordinating epic rather than executable work
- **THEN** it cannot authorize an implementation Agent

### Requirement: Active policy comes from a published pinned revision
Shared dispatch SHALL use the registry's active published policy pin and exact command,
Workflow and specification bindings. Policy activation SHALL require authenticated human
authority and expected-version conflict detection, and SHALL invalidate unused old-epoch
grants. Working-copy policy evaluation SHALL have no execution effects.

#### Scenario: A candidate edits its policy
- **WHEN** working-copy policy differs from the active published bundle
- **THEN** it can be simulated but cannot change the current Run's authority

#### Scenario: Active policy cannot be read
- **WHEN** a pinned object is absent, malformed or unavailable
- **THEN** the host refuses execution instead of falling back to imported editable rows

### Requirement: Claim and capacity are one verified remote CAS mutation
The registry SHALL use `refs/heads/harness-control-v1` with unique-mutation commits and
explicit expected-OID receive-pack compare-and-swap. Task ownership and repository capacity
reservation SHALL be acquired in one document update. Read-back SHALL verify the exact
mutation, owner, generation, transition and reservation. A successful process exit alone
SHALL NOT establish ownership. Public operations SHALL validate domain mutations rather
than expose arbitrary document writes.

#### Scenario: Two owners compete for one Task
- **WHEN** distinct owner commits propose acquisition against the same expected ref
- **THEN** at most one acquires ownership and the loser dispatches nothing

#### Scenario: Two Tasks compete for the final slot
- **WHEN** each proposes a reservation while the pinned repository limit has one free slot
- **THEN** at most one reservation is accepted and no partial claim exists without its slot

#### Scenario: The desired object already exists
- **WHEN** a same-SHA push reports success or up-to-date with an obsolete expected OID
- **THEN** no new owner is authorized without exact receipt read-back

#### Scenario: The acquisition response is lost
- **WHEN** push outcome is uncertain
- **THEN** the caller queries its operation identity and does not mint or blindly resend an acquisition

### Requirement: Ownership never transfers on silence
Each claim SHALL retain owner, monotonically increasing generation, transition and reservation.
Released generation tombstones SHALL remain. Active, draining and uncertain-effect
reservations SHALL count against capacity. Release SHALL require trusted proof of process
quiescence and settled effects; heartbeat expiry, local restart and a caller boolean alone
SHALL NOT provide that proof.

#### Scenario: A heartbeat expires
- **WHEN** an owner cannot be reached but its process or effect may still run
- **THEN** another owner cannot take over and its capacity remains reserved

#### Scenario: Registry history reaches its bound
- **WHEN** a safe update would exceed the supported document or receipt limit
- **THEN** acquisition is refused rather than dropping history or resetting generations

### Requirement: Authoritative effects are fenced at a trusted executor
Version 1 SHALL require every cooperating executor to validate owner/generation, policy and
grant in the same CAS that admits a durable effect permit. A lifetime exclusive local journal
lock SHALL prevent competing local consumers. Only the original winning execution instance
SHALL send the effect; restarted instances SHALL observe rather than resend. Handover SHALL
NOT advance generation while an old permit or process could execute. CAS alone SHALL NOT be
advertised as fencing GitHub REST or process creation. The contract SHALL explicitly exclude
malicious same-user credential use and external writers bypassing the protocol.

#### Scenario: An old owner returns after a valid handover
- **WHEN** the old generation requests phase publication or dispatch
- **THEN** the executor refuses before any new authoritative effect

#### Scenario: A deployment uses implicit local-owner identity
- **WHEN** an approved cooperative pilot runs with the local-owner identity
- **THEN** it still enforces permits and grants without claiming adversarial Agent isolation

#### Scenario: Permission exists but the originating instance restarts
- **WHEN** recovery finds an admitted permit without a settled effect
- **THEN** it observes the outcome and cannot use that permit to send the effect again

### Requirement: Human grants bind action and reviewed content
Grants SHALL come from an explicit operation that verifies current authorized repository-owner
identity and action scope. They SHALL bind action, Task input/specification, policy epoch,
expiry and transition consumption; candidate grants SHALL additionally bind final HEAD,
tree, base and verification manifest. Unknown identity status, expired or revoked grants
and changed bindings SHALL block. Clearing Hold SHALL NOT create a grant, and recording
a grant SHALL NOT itself remove Hold.

In version 1 the implemented action is `approve-candidate`, recorded by the control CLI as a
CAS mutation of the shared registry document; `approve-proposal` remains deferred to
hold-release authorization (ADR-0006 acceptance delta 3), delegation remains absent
(owner-only), and the implicit local owner identity carries the accepted cooperative same-user
limitation.

#### Scenario: The candidate changes after approval
- **WHEN** archive, retro, rebase or another edit changes the approved candidate
- **THEN** its grant cannot authorize integration of the new candidate

#### Scenario: Two human decisions use different registry revisions
- **WHEN** a grant or revocation is submitted against an obsolete expected registry OID
- **THEN** the system returns a conflict instead of overwriting the other decision

#### Scenario: A grant is observed twice
- **WHEN** a later tick sees a grant already consumed by a transition
- **THEN** it cannot authorize a second transition

#### Scenario: The identical grant submitted twice
- **WHEN** an owner reruns the same approve-candidate command with the same binding
- **THEN** the replay returns the recorded grant and the registry is unchanged


#### Scenario: Merging consults what was approved
- **WHEN** a squash-merge runs for a task with a live grant
- **THEN** the merge's expected head is the grant's candidate, and divergence refuses typed with
  the re-approval remedy

**The merge consumes what the grant binds (#337):** the worktree's squash-merge requires a live
grant for the resolved task whose candidate is the pull request's observed head — a changed
candidate after approval blocks with the re-approval remedy, and merging without a grant is
refused with the approve-candidate operation as the remedy.
### Requirement: Revocation has an explicit in-flight boundary
Grant/enrollment revocation and policy activation SHALL block new permits. An already
admitted external operation SHALL be drained or independently observed; revocation SHALL
NOT pretend to undo it. Fresh GitHub phase/Hold observations SHALL precede admission and
changes SHALL block successor effects, without promising an atomic GitHub-read/dispatch
transaction.

#### Scenario: A human revokes during a remote call
- **WHEN** revocation occurs after an effect permit has been durably admitted
- **THEN** no successor is admitted and the existing effect remains tracked until settled

### Requirement: Durable effect identity precedes dispatch and survives restart
Every effect SHALL have one stable identity and immutable payload binding across coordination,
StartRun and launcher. Local and shared intent SHALL precede execution. Runs SHALL atomically
persist the dispatch-to-Run mapping before spawn; a duplicate with the same binding SHALL
return the existing Run and a conflicting binding SHALL be refused. Unknown gaps between
remote CAS, SQLite and process start SHALL block rather than launch on recovery.

#### Scenario: A Run is created but the response is lost
- **WHEN** the same dispatch identity is queried or submitted again with matching parameters
- **THEN** it resolves to the existing Run and no second process starts

#### Scenario: The host crashes before launch is observed
- **WHEN** an intent or Run mapping exists without a confirmed process outcome after restart
- **THEN** recovery records uncertainty and does not spawn the Agent

### Requirement: Failure publication cannot authorize retry
Failure or uncertain-start state SHALL be durable before Hold/reason publication is attempted.
If final persistence fails, prior unresolved intent SHALL remain blocking. Failed Agent work
SHALL NOT retry automatically through a new tick, process restart, label release or new Run
ID. A new attempt SHALL require explicit human resolution and a separate scoped grant;
unknown effects SHALL first settle.

#### Scenario: Launch and Hold publication both fail
- **WHEN** a start fails, provider Hold publication fails, and the host restarts
- **THEN** the prior attempt still blocks dispatch and exposes publication-pending separately

### Requirement: Cancellation and closure preserve truthful outcomes
Cancellation SHALL stop successor admission and retain ownership until process and effects
are quiescent. Human closure without confirmed integration SHALL be represented as closure
without code, not merge success. Confirmed integration SHALL not be repeated; cleanup SHALL
be separately authorized and preserve unpublished work.

#### Scenario: A human closes without a merge
- **WHEN** GitHub reports a closed Task but no bound candidate integration
- **THEN** automatic work stops and the outcome does not claim code was integrated

#### Scenario: A local Run ends held while GitHub is unavailable
- **WHEN** local execution is held but Hold publication cannot be confirmed
- **THEN** the trace distinguishes local ending from unknown shared Hold

