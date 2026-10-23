## MODIFIED Requirements

### Requirement: Dispatch recovery observes uncertainty rather than replaying
The system SHALL expose a permission-scoped query by effectId. Pending admission, launch intent,
process observation and refusal SHALL be distinct. Launch returning SHALL NOT count as process
observation. A Run and launch intent SHALL commit together before launch. Recovery and duplicate
delivery SHALL NEVER replay an Agent or Worktree effect. The watcher's tick SHALL be one recovery
client: a `pending` or `unknown` intent past the escalation window is resolved by querying this
mapping by effectId and nothing else, and what cannot be established remains blocked with its
typed reason.

#### Scenario: Response lost
- **WHEN** the caller loses the start response
- **THEN** querying effectId returns the original Run binding and current observation

#### Scenario: The watcher's reconcile finds the Run the lost response hid
- **WHEN** a tick reconciles an intent whose Run exists
- **THEN** the intent settles and no Run or Worktree effect is replayed

#### Scenario: The watcher's reconcile finds nothing and blocks
- **WHEN** a tick reconciles an intent past its window with no observable outcome
- **THEN** the intent stays `unknown`, one attention comment keys the effectId, and the tick
  moves on without resubmitting
