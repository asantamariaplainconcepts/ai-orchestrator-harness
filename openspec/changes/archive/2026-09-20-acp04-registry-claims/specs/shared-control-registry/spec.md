## ADDED Requirements

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

### Requirement: A blocking claim escalates to the blocked task's own issue
A coordinator that observes a claim it does not own, blocking a candidate it would otherwise
acquire, with no observable progress over the policy-declared window, SHALL publish one typed
attention comment on that task's issue through the World comment-writer seam. The comment SHALL
carry the reason code, claim owner, generation, last known effect and the remedy. The escalation
SHALL be published at most once per owner and generation. The coordinator SHALL NOT take over the
claim, drain it, or modify the registry as part of escalating. A failed or unknown comment write
SHALL NOT be retried within the tick and SHALL be logged.

#### Scenario: One comment, not one per tick
- **WHEN** the same blocked claim persists across ticks and the comment for its owner and
  generation already exists
- **THEN** no second comment is published

#### Scenario: Escalation never takes over
- **WHEN** a claim is escalated
- **THEN** the registry is unchanged by the escalation itself, and the remedy names the
  owner-operation
