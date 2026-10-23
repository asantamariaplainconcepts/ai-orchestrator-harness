## MODIFIED Requirements

### Requirement: Observation is opt-in and never dispatches
The watcher SHALL be disabled by default and SHALL observe when `LabelWatcher:Mode` is `observe`,
observe and acquire when it is `observe-claims`, or observe, acquire and dispatch when it is
`dispatch-claims`. In `dispatch-claims`, the tick SHALL dispatch only a task at
`ready-for-implementation` whose hold is absent, after acquiring its registry claim and reserving
a journal intent, through the Runs admission that binds effectId to Run — and the transition it
starts SHALL be the declared Workflow the machine's effect names, resolved through the
workflow-plan seam before anything is posted. A workflow the reader cannot resolve is a typed
`workflow-missing` stop for that task this tick: logged with the key it tried, zero writes, and
no prompt-only fallback. A task at `ready-for-proposal` SHALL receive the same observed log line
as any other mode and SHALL NOT be dispatched. A mode value other than `observe`,
`observe-claims`, `dispatch-claims` or `disabled` SHALL be refused with a log that names
controlled admission and trusted authority as what is missing.

#### Scenario: Default configuration observes nothing
- **WHEN** `LabelWatcher:Mode` is unset
- **THEN** the watcher starts no tick loop and issues no request at all

#### Scenario: Observe mode advises a prompt
- **WHEN** a task is `ready-for-proposal` and `LabelWatcher:Mode` is `observe`
- **THEN** the watcher logs an `Observed` candidate with the transition's workflow key and every
  request it makes is a `GET`

#### Scenario: Dispatch sends the declared workflow, not a prompt
- **WHEN** a `ready-for-implementation` task with no hold is dispatched
- **THEN** the start names the effect's workflow key as its WorkflowId, and the run carries the
  controlled plan's steps

#### Scenario: A missing workflow is a typed stop, never a degradation
- **WHEN** the transition's workflow key does not resolve in the folder
- **THEN** the tick logs `workflow-missing` with the key, writes nothing for that task, and never
  starts a prompt-only run instead

#### Scenario: A proposal is never machine-written
- **WHEN** a task is `ready-for-proposal` and `LabelWatcher:Mode` is `dispatch-claims`
- **THEN** the watcher logs the observed candidate and issues no write to any module

#### Scenario: An unknown mode is refused
- **WHEN** `LabelWatcher:Mode` is set to a value other than `observe`, `observe-claims`,
  `dispatch-claims` or `disabled`
- **THEN** the loop does not run and an error names controlled admission and trusted authority

### Requirement: One door for a transition, whatever started it
A run started through a lifecycle transition — by the watcher's dispatch or by a person starting
the task's next step in the UI — SHALL travel the same contract: the same declared workflow, the
same effect identity discipline, the same admission checks. A run a person starts with free-form
prompt text and no workflow SHALL carry `not-a-transition` visibly: it exists, it runs, and the
loop does not count it — not in-progress, not WIP, not claimable, and no transition of the
machine fired to start it.

#### Scenario: The person and the watcher use the same workflow
- **WHEN** a person starts the implement step of a loop task from the UI
- **THEN** the run starts the same declared workflow the machine's dispatch would start, and both
  carry the transition marker

#### Scenario: A prompt-only run is not a transition
- **WHEN** a person starts a run with typed prompt text and no workflow
- **THEN** the run is marked `not-a-transition`, and no in-progress count, WIP room or claim
  changes because of it
