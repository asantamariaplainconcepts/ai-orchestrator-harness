## MODIFIED Requirements

### Requirement: A blocking claim escalates to the blocked task's own issue
A coordinator that observes a claim it does not own, blocking a candidate it would otherwise
acquire, with no observable progress over the policy-declared window, SHALL publish one typed
attention comment on that task's issue through the World comment-writer seam. The comment SHALL
carry the reason code, claim owner, generation, last known effect and the remedy. The escalation
SHALL be published at most once per owner and generation. The coordinator SHALL NOT take over the
claim, drain it, or modify the registry as part of escalating. A failed or unknown comment write
SHALL NOT be retried within the tick and SHALL be logged.

An uncertain journal intent has its own escalation, disjoint from a claim's: an effect `unknown`
past the declared window escalates once keyed on its effectId. The two keys never merge — and
neither escalation takes over, retries, or resubmits what it names. A hold reason carries the
same vocabulary discipline: typed, projected through the same comment-writer seam, and rendered
raw when unrecognized — an unknown reason never disables the hold it explains.

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

#### Scenario: A hold reason is typed and projected
- **WHEN** a hold is applied with a typed reason
- **THEN** one attention comment carries the reason code with the hold's task and remedy, and
  readers that do not recognize the code display it raw with the hold intact

### Requirement: Exact repository registry CAS
The registry mutations — bootstrap, acquire, release, grant, revoke — SHALL be one atomic
compare-and-swap each: an explicit expected revision OID, a fresh mutation id, and read-back
before any caller treats the mutation as its effect. Two independent writers racing the same
mutation produce one confirmed result and one typed conflict, never two applied effects.

#### Scenario: Bootstrap race
- **WHEN** two independent clones propose distinct successors to confirmed absence
- **THEN** exactly one is confirmed and the other receives a typed conflict

### Requirement: Grant and revoke are ledger operations with exact bindings
An `approve-candidate` grant SHALL be recorded only by an explicit owner-operation through the
control CLI, binding actor, Task identity, candidate HEAD, tree, base, spec pin, authorization
epoch and expiry in the registry document by CAS. A grant SHALL occupy no capacity and claim no
task. Re-submitting the identical grant SHALL replay its recorded result; a different binding
under the same grant id SHALL conflict. A revocation SHALL be append-only and SHALL be an
operation of the same shape. Reading grants SHALL answer the domain question — which grants for
this Task are live at a caller-supplied now — applying expiry, epoch and revocation at read
time; the document stores facts, the read decides validity. A grant on an unbootstrapped
registry SHALL be refused.

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
