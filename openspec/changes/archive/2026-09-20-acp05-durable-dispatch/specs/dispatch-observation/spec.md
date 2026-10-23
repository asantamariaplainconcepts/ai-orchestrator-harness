## MODIFIED Requirements

### Requirement: Observation is opt-in and never dispatches
The watcher SHALL be disabled by default and SHALL observe when `LabelWatcher:Mode` is `observe`,
observe and acquire when it is `observe-claims`, or observe, acquire and dispatch when it is
`dispatch-claims`. In `observe` and `observe-claims`, every tick SHALL be dispatch-free as before.
In `dispatch-claims`, the tick SHALL dispatch only a task at `ready-for-implementation` whose hold
is absent, after acquiring its registry claim and reserving a journal intent, through the Runs
admission that binds effectId to Run — and a task at `ready-for-proposal` SHALL receive the same
observed log line as any other mode and SHALL NOT be dispatched. A mode value other than `observe`,
`observe-claims`, `dispatch-claims` or `disabled` SHALL be refused with a log that names controlled
admission and trusted authority as what is missing.

#### Scenario: Default configuration observes nothing
- **WHEN** `LabelWatcher:Mode` is unset
- **THEN** the watcher starts no tick loop and issues no request at all

#### Scenario: Observe mode advises a prompt
- **WHEN** a task is `ready-for-proposal` and `LabelWatcher:Mode` is `observe`
- **THEN** the watcher logs an `Observed` candidate with the `/aio:propose #<key>` prompt and
  every request it makes is a `GET`

#### Scenario: Observe-claims acquires and still never dispatches
- **WHEN** `LabelWatcher:Mode` is `observe-claims` and a candidate is eligible
- **THEN** the watcher logs the observed candidate and the acquisition outcome, and no Run,
  label, hold or module write API is called

#### Scenario: A conflict is a stop, not a retry
- **WHEN** the acquisition returns a conflict
- **THEN** the watcher logs the observed revision, proceeds to no other effect for that task,
  and waits for the next tick to decide again

#### Scenario: Dispatch-claims dispatches only implementation work with its hold released
- **WHEN** a task is `ready-for-implementation` with no hold and `LabelWatcher:Mode` is
  `dispatch-claims`
- **THEN** the watcher reserves a journal intent, acquires the claim, and starts the Run through
  the durable dispatch admission, and the started Run is named with the task's own effect identity

#### Scenario: A proposal is never machine-written
- **WHEN** a task is `ready-for-proposal` and `LabelWatcher:Mode` is `dispatch-claims`
- **THEN** the watcher logs the observed candidate and issues no write to any module

#### Scenario: An unknown mode is refused
- **WHEN** `LabelWatcher:Mode` is set to a value other than `observe`, `observe-claims`,
  `dispatch-claims` or `disabled`
- **THEN** the loop does not run and an error names controlled admission and trusted authority

### Requirement: The tick reconciles uncertain intents before deciding anything new
Before evaluating candidates, a dispatch-mode tick SHALL resolve journal intents left `pending` or
`unknown` past the declared escalation window, by querying Runs by effectId and reading the
registry — never by resubmitting the effect. An intent whose Run exists SHALL settle to `started`;
an intent past the window that cannot be established SHALL remain blocked with its typed reason
and SHALL escalate once, through the attention-comment seam, keyed on its effectId. A tick SHALL
NOT retry a refused or failed dispatch.

#### Scenario: Response lost, then found by observation
- **WHEN** a start's response was lost and the next tick queries the effectId
- **THEN** the intent settles to the existing Run, and no second Run is created

#### Scenario: The unknown stays blocked and is named once
- **WHEN** an intent is `unknown` past the window and observation establishes nothing
- **THEN** one attention comment carrying its effectId is published, the intent keeps its typed
  state, and no later tick republishes for the same effectId

#### Scenario: Failure is not retried
- **WHEN** a dispatch journey recorded `failed` or `refused`
- **THEN** no later tick resubmits that effect, and the task's next step is a person's
