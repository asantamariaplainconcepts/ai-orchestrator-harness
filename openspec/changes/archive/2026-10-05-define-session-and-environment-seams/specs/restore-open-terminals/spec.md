## MODIFIED Requirements

### Requirement: Open terminals are recorded so a restart can restore them
The system SHALL record every open shell and agent terminal as a row in the Workspace module's own
database — written through a `BuildingBlocks/World` seam the terminals layer reports across, exactly as
`ICheckSink` reports a declared command's ending — carrying the folder, the working directory, the kind,
the agent id, the vendor session id when one is known, and the label. The row SHALL be written when the
terminal opens and removed when its tab is closed. Only shells and agent terminals SHALL be recorded:
a declared command is a process this product starts for a purpose, and restarting the server is not a
request to run it again unasked. Recording SHALL be best-effort — a row that cannot be written is logged
and never takes the terminal down with it. This restore is for the server restart and SHALL NOT be a
public verb of the `Session` seam — reattaching a live local session is a detail of the local adapter,
not a restore the seam exposes. A finished session SHALL NOT be revived: the row records what was open
at shutdown, and a session that has ended is not brought back.

#### Scenario: A shell terminal open across a restart is restored
- **WHEN** the server restarts while a shell terminal is open in a folder
- **THEN** after the restart the folder's terminal list carries a shell terminal on the same working
  directory, and it is a fresh shell

#### Scenario: A declared command is not restored
- **WHEN** the server restarts while a declared command's terminal is running
- **THEN** that command is not started again and no tab for it appears

#### Scenario: A closed terminal leaves no row
- **WHEN** a terminal's tab is closed and the server later restarts
- **THEN** the closed terminal is not restored

#### Scenario: Recording is best-effort
- **WHEN** the row cannot be written
- **THEN** the terminal is unaffected and the failure is logged

#### Scenario: Restore does not revive a finished session
- **WHEN** a session had ended before the server restarted
- **THEN** the row records it but the session is not brought back as a live session, and its transcript is offered as the record that survived

### Requirement: A restored agent terminal resumes the conversation it was having
An agent terminal SHALL be reopened after a restart carrying the vendor's own resume material —
`claude --resume <id>`, `copilot --resume=<id>`, `opencode --session <id>` — so the conversation
continues under the vendor session recorded when it opened, rather than a new conversation starting
behind the same tab. The process of a restored agent terminal SHALL NOT be started at boot: it SHALL
be registered openable — listed with its id, its folder and its label, its line carrying the resume
arguments — and started when a pane first attaches to it, because an agent's TUI spawned with no
reader exits without writing a transcript, which is a restored tab that looks alive and is not. The
persisted session id SHALL be validated before it reaches a command line: non-empty, at most 512
characters, no control characters, and not starting with a dash — a value that fails validation
SHALL restore the terminal without its resume material rather than refusing the restore, and such
a terminal is spawned at boot like a shell, there being no conversation to wait for.

#### Scenario: A claude conversation continues after a restart
- **WHEN** the server restarts while a Claude Code terminal is open and a pane attaches to the
  restored terminal
- **THEN** the agent process is spawned with `--resume <the recorded session id>` and the conversation
  continues where it left off

#### Scenario: Nothing runs between boot and the first attach
- **WHEN** the server has restarted and restored an agent terminal and no pane has attached to it
- **THEN** no process for that terminal is running, while the terminal is listed and openable

#### Scenario: A session id that failed validation restores without resuming
- **WHEN** the recorded vendor session id is empty, over 512 characters, carries control characters or
  starts with a dash
- **THEN** the terminal reopens in the same directory without a resume target, and the row's resume
  material is dropped
