## MODIFIED Requirements

### Requirement: Human grants bind action and reviewed content
Grants SHALL come from an explicit operation that verifies current authorized repository-owner
identity and action scope. They SHALL bind action, Task input/specification, policy epoch,
expiry and transition consumption; candidate grants SHALL additionally bind final HEAD, tree,
base and verification manifest. Unknown identity status, expired or revoked grants and changed
bindings SHALL block. Clearing Hold SHALL NOT create a grant, and recording a grant SHALL NOT
itself remove Hold.

In version 1 the implemented action is `approve-candidate`, recorded by the control CLI as a
CAS mutation of the shared registry document; `approve-proposal` remains deferred to
hold-release authorization (ADR-0006 acceptance delta 3), delegation remains absent
(owner-only), and the implicit local owner identity carries the accepted cooperative same-user
limitation. **The merge consumes what the grant binds (#337):** the worktree's squash-merge
requires a live grant for the resolved task whose candidate is the pull request's observed head
— a changed candidate after approval blocks with the re-approval remedy, and merging without a
grant is refused with the approve-candidate operation as the remedy.

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
