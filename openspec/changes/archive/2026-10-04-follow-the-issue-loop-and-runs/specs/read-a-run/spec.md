## ADDED Requirements

### Requirement: The task detail answers where the loop stands, for its own task

The task detail SHALL draw, for the task it is about, the loop's walk —
the stages and transitions observed for that task — grouped by the task's
full identity across the working directories its runs used, and SHALL NOT
require a working directory to answer. The walk it draws SHALL be the same
reading the worktree's journey header draws — the declared machine with
its observed position, from `journeyFor`'s resolution — when the task's
newest run's directory carries the declared loop, and the observed command
words in run order when it does not, which is the reading closed journeys
already honour. It SHALL NOT derive a second interpretation of the loop:
no parallel state machine, no lifecycle-label-to-stage mapping.

#### Scenario: The walk for a task whose directory carries the loop

- **WHEN** the task's newest run worked in a directory that carries the declared loop
- **THEN** the detail draws the machine with the observed position marked, the same reading the worktree's header draws

#### Scenario: The walk for a task whose directories are gone

- **WHEN** none of the task's runs' working directories exists any more
- **THEN** the detail draws the observed command words in run order, and no position is invented

#### Scenario: The same key in two repositories

- **WHEN** two repositories each carry a task with the same key and runs
- **THEN** each task's walk shows only its own runs, and neither directory's history mixes into the other task's walk

### Requirement: A stage appears only when observed

A stage or transition SHALL appear on the walk only when a fact observes
it — a run's transition, a command's transcript, a recorded step — and
SHALL NOT be inferred from the task's lifecycle label or a known
declarative progress. The task's own lifecycle SHALL be named as what it
is — the workflow's claim — beside the walk, and where no node is
observed the walk SHALL say no transitioned run rather than drawing a
position, a percentage or a stage.

#### Scenario: A lifecycle label is not a stage

- **WHEN** a task carries `status:implementing` and its runs show no transitioned node
- **THEN** the walk names the lifecycle as the workflow's claim, draws no position and says no transitioned run

#### Scenario: A run's transition marks the stage

- **WHEN** the task's latest transitioned run names the node it dispatched
- **THEN** the walk marks that node as where the journey stands, with that run's facts — command, activity, duration

### Requirement: Several runs are distinguished and chosen, never first-matched

Where a task has several runs, the walk SHALL distinguish runs that have
not finished from runs that have, and each run SHALL be named with what
is known of it — its agent, its last activity, its state and where it
worked — and opened in its existing terminal or transcript by the
person's choice. Observing SHALL NOT start, resume or provision
anything: reading the walk and opening what exists are the whole of it.

#### Scenario: An active run and historical runs

- **WHEN** a task has a run that has not finished and two that have
- **THEN** the unfinished run leads the walk's live part, the finished runs are drawn as history, and each opens its own existing transcript

#### Scenario: Observation never computes

- **WHEN** the walk is opened for a run whose environment is stopped or gone
- **THEN** nothing is resumed, provisioned or spawned, and the persisted facts are what the walk shows

### Requirement: Disconnection, staleness, absence and unknown are four named states

The walk SHALL name its connection state, and the four SHALL NOT collapse
into one another: **disconnected** keeps the last observation and its
time without asserting the process's current state, **stale** data is
named stale with the stamp it was read at, **absence** is a confirmed
answer that there is nothing, and **unknown** says the fact is not
known. A disconnected run SHALL NOT be marked finished, failed or
cancelled by the disconnection alone.

#### Scenario: A disconnected run is not a finished one

- **WHEN** the connection to a run's environment drops mid-run
- **THEN** the walk shows disconnected with the last observation and its time, and the run's state is unchanged by the drop

#### Scenario: An environment withdrawn keeps its persisted history

- **WHEN** a task's working directories are gone and its runs' transcripts were persisted
- **THEN** the walk reads the persisted history without recreating any environment, and the parts not persisted are declared unrecoverable
