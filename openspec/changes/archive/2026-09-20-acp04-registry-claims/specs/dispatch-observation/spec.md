## MODIFIED Requirements

### Requirement: Observation is opt-in and never dispatches
The watcher SHALL be disabled by default and SHALL observe when `LabelWatcher:Mode` is `observe`, or
observe and acquire when it is `observe-claims`. Every tick SHALL be dispatch-free: it logs the
`/aio:propose` or `/aio:implement` prompt a person could start, and it SHALL NOT create a Run,
apply a label, write a hold, or call any write API of any module. In `observe-claims` mode the tick
SHALL, in addition, attempt one `Acquire` per eligible candidate against the folder's registry
through the control-registry seam, and SHALL log the outcome. A CAS conflict SHALL produce no
dispatch and no in-process retry; the next tick is a new decision. A mode value other than
`observe`, `observe-claims` or `disabled` SHALL be refused with a log that names controlled
admission and trusted authority as what is missing.

#### Scenario: Default configuration observes nothing
- **WHEN** `LabelWatcher:Mode` is unset
- **THEN** the watcher starts no tick loop and issues no request at all

#### Scenario: Observe mode advises a prompt
- **WHEN** a task is `ready-for-proposal` and `LabelWatcher:Mode` is `observe`
- **THEN** the watcher logs an `Observed` candidate with the `/aio:propose #<key>` prompt and every
  request it makes is a `GET`

#### Scenario: Observe-claims acquires and still never dispatches
- **WHEN** `LabelWatcher:Mode` is `observe-claims` and a candidate is eligible
- **THEN** the watcher logs the observed candidate and the acquisition outcome, and no Run, label,
  hold or module write API is called

#### Scenario: A conflict is a stop, not a retry
- **WHEN** the acquisition returns a conflict
- **THEN** the watcher logs the observed revision, proceeds to no other effect for that task, and
  waits for the next tick to decide again

#### Scenario: An unknown mode is refused
- **WHEN** `LabelWatcher:Mode` is set to a value other than `observe`, `observe-claims` or
  `disabled`
- **THEN** the loop does not run and an error names controlled admission and trusted authority
