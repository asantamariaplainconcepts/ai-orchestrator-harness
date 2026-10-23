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
