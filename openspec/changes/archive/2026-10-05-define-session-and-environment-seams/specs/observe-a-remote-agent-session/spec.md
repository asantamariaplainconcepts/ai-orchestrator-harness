## MODIFIED Requirements

### Requirement: The task observes its run's own session, not a shell beside it

The system SHALL open, from the task detail for a run it dispatched to a
remote environment, the **agent session that run created** — its process
and terminal — and SHALL NOT offer an independent shell in the same
environment as the observation. The session SHALL be resolved from the
run's own metadata — the environment it was dispatched to and the session
its process established — joined to the task by full identity, and two
repositories carrying the same key SHALL NOT cross sessions. The session,
local or remote, SHALL be observed through the same `Session` seam and
shall name its state in the same four-word vocabulary whether the
environment is local or remote, so a screen names a state rather than an
implementation.

#### Scenario: The session the run created

- **WHEN** a run for a task works in a remote environment and the task's execution entry is opened
- **THEN** the terminal shown is the session that run's process established, and no new shell is offered in its place

#### Scenario: Two repositories, several sessions

- **WHEN** two repositories carry the same key and each has a run with its own session
- **THEN** each task's detail opens only its own run's session

#### Scenario: Remote without a local checkout

- **WHEN** a task's runs all worked in remote environments and no local folder or worktree exists
- **THEN** the task detail still opens the run's session, and review and execution remain independent entries

#### Scenario: A local session is named in the same vocabulary

- **WHEN** a run works in the local environment and a pane observes it
- **THEN** the session's state is named in the same four words a remote session is named in, and no implementation detail distinguishes the two screens

### Requirement: Disconnected, stopped, absent and finished are four states

The observation SHALL name its state, and the four SHALL NOT collapse
into one another: **disconnected** keeps the last observation without
asserting the process's current state, **environment stopped** is a fact
of the environment, **session absent** is a fact of the session, and
**process finished** is a fact of the process. A disconnection SHALL NOT
mark a run finished, failed or cancelled; errors SHALL be actionable and
SHALL NOT expose credentials or secret URLs in the UI or logs. The local
`Session` and `Environment` seams SHALL answer in this same vocabulary, so
that the four states are the screen's words whether the environment is
local or remote.

#### Scenario: A dropped connection is not a finished process

- **WHEN** the connection to a remote session drops mid-run
- **THEN** the state is disconnected with the last observation, and the process's state is not rewritten by the drop

#### Scenario: An environment removed keeps its history honest

- **WHEN** the environment a run worked in is deleted
- **THEN** the persisted history remains readable, its limits are declared, and nothing is recreated to answer the observation
