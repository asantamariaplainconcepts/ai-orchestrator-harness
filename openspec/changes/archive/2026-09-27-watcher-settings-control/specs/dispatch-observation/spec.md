# dispatch-observation Delta

## MODIFIED Requirements

### Requirement: Observation is opt-in and never dispatches
The watcher SHALL be disabled by default and SHALL observe when its effective mode is `observe`,
observe and acquire when it is `observe-claims`, or observe, acquire and dispatch when it is
`dispatch-claims`. The effective mode SHALL be the persisted screen value when one exists and the
`LabelWatcher:Mode` configuration value otherwise, re-read per tick, so a change from the settings
screen governs the next tick without a restart. In `observe` and `observe-claims`, every tick SHALL
be dispatch-free as before. In `dispatch-claims`, the tick SHALL dispatch only a task at
`ready-for-implementation` whose hold is absent, after acquiring its registry claim and reserving a
journal intent, through the Runs admission that binds effectId to Run — and a task at
`ready-for-proposal` SHALL receive the same observed log line as any other mode and SHALL NOT be
dispatched. A CAS conflict SHALL produce no dispatch and no in-process retry; the next tick is a new
decision. A mode value — configured or persisted — other than `observe`, `observe-claims`,
`dispatch-claims` or `disabled` SHALL be refused with a log that names controlled admission and
trusted authority as what is missing.

#### Scenario: Default configuration observes nothing
- **WHEN** no mode is set anywhere
- **THEN** the watcher starts no tick loop and issues no request at all, and the settings screen
  names disabled as the mode

#### Scenario: Observe mode advises a prompt
- **WHEN** a task is `ready-for-proposal` and the effective mode is `observe`
- **THEN** the watcher logs an `Observed` candidate with the `/aio:propose #<key>` prompt and every
  request it makes is a `GET`

#### Scenario: Observe-claims acquires and still never dispatches
- **WHEN** the effective mode is `observe-claims` and a candidate is eligible
- **THEN** the watcher logs the observed candidate and the acquisition outcome, and no Run, label,
  hold or module write API is called

#### Scenario: A conflict is a stop, not a retry
- **WHEN** the acquisition returns a conflict
- **THEN** the watcher logs the observed revision, proceeds to no other effect for that task, and
  waits for the next tick to decide again

#### Scenario: Dispatch-claims dispatches only implementation work with its hold released
- **WHEN** a task is `ready-for-implementation` with no hold and the effective mode is
  `dispatch-claims`
- **THEN** the watcher reserves a journal intent, acquires the claim, and starts the Run through the
  durable dispatch admission, and the started Run is named with the task's own effect identity

#### Scenario: A proposal is never machine-written
- **WHEN** a task is `ready-for-proposal` and the effective mode is `dispatch-claims`
- **THEN** the watcher logs the observed candidate and issues no write to any module

#### Scenario: Dispatch sends the declared workflow, not a prompt
- **WHEN** a `ready-for-implementation` task with no hold is dispatched
- **THEN** the start names the effect's workflow key as its WorkflowId, and the run carries the
  controlled plan's steps

#### Scenario: A missing workflow is a typed stop, never a degradation
- **WHEN** the transition's workflow key does not resolve in the folder
- **THEN** the tick logs `workflow-missing` with the key, writes nothing for that task, and never
  starts a prompt-only run instead

#### Scenario: An unknown mode is refused
- **WHEN** the effective mode is a value other than `observe`, `observe-claims`, `dispatch-claims`
  or `disabled`
- **THEN** the loop does not run and an error names controlled admission and trusted authority

#### Scenario: A mode changed from the screen governs the next tick
- **WHEN** the persisted mode is lowered or raised between two ticks
- **THEN** the following tick decides and acts under the new mode, and no restart occurred
