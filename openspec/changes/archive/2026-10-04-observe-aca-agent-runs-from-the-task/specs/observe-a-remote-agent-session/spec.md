## ADDED Requirements

### Requirement: The task observes its run's own session, not a shell beside it

The system SHALL open, from the task detail for a run it dispatched to a
remote environment, the **agent session that run created** — its process
and terminal — and SHALL NOT offer an independent shell in the same
environment as the observation. The session SHALL be resolved from the
run's own metadata — the environment it was dispatched to and the session
its process established — joined to the task by full identity, and two
repositories carrying the same key SHALL NOT cross sessions.

#### Scenario: The session the run created

- **WHEN** a run for a task works in a remote environment and the task's execution entry is opened
- **THEN** the terminal shown is the session that run's process established, and no new shell is offered in its place

#### Scenario: Two repositories, several sessions

- **WHEN** two repositories carry the same key and each has a run with its own session
- **THEN** each task's detail opens only its own run's session

#### Scenario: Remote without a local checkout

- **WHEN** a task's runs all worked in remote environments and no local folder or worktree exists
- **THEN** the task detail still opens the run's session, and review and execution remain independent entries

### Requirement: Querying is effect-free; connecting is authorized and confirmed

Status and history queries for a remote run SHALL NOT resume the
environment, provision resources or start processes: the environment's
state is read as a fact, whatever it is. Connecting to a session MAY
require resuming a stopped environment, and that path SHALL be separate
from the query path, SHALL be authorized on the server before anything is
activated, and SHALL present the possible activation — with its cost made
explicit — for the person's confirmation before it happens. A refused or
unconfirmed connection SHALL activate nothing.

#### Scenario: A stopped environment is queried

- **WHEN** a task's run sits in a stopped environment and the detail is opened
- **THEN** status and history answer, and no resume, provision or spawn is issued — measured at the adapter's boundary

#### Scenario: Connecting resumes with a confirmation

- **WHEN** a person chooses to connect to a session whose environment is stopped
- **THEN** the activation and its possible cost are named, the person confirms, the server authorizes, and only then is the environment resumed

#### Scenario: An unauthorized connection is refused

- **WHEN** a caller without the declared permission attempts the connect path directly
- **THEN** the connection and any resume are refused on the server, and nothing is activated

### Requirement: Resume does not fake session survival

A resumed environment SHALL be asked for the session the run named: the
session exists and is attached to, or it does not and that is what is
said. The system SHALL NOT start another agent or process to fill the
terminal of a lost session, and SHALL NOT present a new session as the
continuation of the original one.

#### Scenario: The original session is gone after a resume

- **WHEN** the environment resumed and the session the run named no longer exists
- **THEN** the state is named as session-absent, the available history is offered as history, and no new agent is started

### Requirement: Disconnected, stopped, absent and finished are four states

The observation SHALL name its state, and the four SHALL NOT collapse
into one another: **disconnected** keeps the last observation without
asserting the process's current state, **environment stopped** is a fact
of the environment, **session absent** is a fact of the session, and
**process finished** is a fact of the process. A disconnection SHALL NOT
mark a run finished, failed or cancelled; errors SHALL be actionable and
SHALL NOT expose credentials or secret URLs in the UI or logs.

#### Scenario: A dropped connection is not a finished process

- **WHEN** the connection to a remote session drops mid-run
- **THEN** the state is disconnected with the last observation, and the process's state is not rewritten by the drop

#### Scenario: An environment removed keeps its history honest

- **WHEN** the environment a run worked in is deleted
- **THEN** the persisted history remains readable, its limits are declared, and nothing is recreated to answer the observation
