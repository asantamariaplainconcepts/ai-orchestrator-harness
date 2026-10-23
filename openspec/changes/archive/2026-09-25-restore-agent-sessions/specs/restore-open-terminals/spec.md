## ADDED Requirements

### Requirement: Open terminals are recorded so a restart can restore them
The system SHALL record every open shell and agent terminal as a row in the Workspace module's own
database — written through a `BuildingBlocks/World` seam the terminals layer reports across, exactly as
`ICheckSink` reports a declared command's ending — carrying the folder, the working directory, the kind,
the agent id, the vendor session id when one is known, and the label. The row SHALL be written when the
terminal opens and removed when its tab is closed. Only shells and agent terminals SHALL be recorded:
a declared command is a process this product starts for a purpose, and restarting the server is not a
request to run it again unasked. Recording SHALL be best-effort — a row that cannot be written is logged
and never takes the terminal down with it.

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

### Requirement: A restored agent terminal resumes the conversation it was having
An agent terminal SHALL be reopened after a restart with the vendor's own resume arguments appended to
its line — `claude --resume <id>`, `copilot --resume=<id>`, `opencode --session <id>` — so the
conversation continues under the vendor session recorded when it opened, rather than a new conversation
starting behind the same tab. The persisted session id SHALL be validated before it reaches a command
line: non-empty, at most 512 characters, no control characters, and not starting with a dash — a value
that fails validation SHALL restore the terminal without its resume material rather than refusing the
restore.

#### Scenario: A claude conversation continues after a restart
- **WHEN** the server restarts while a Claude Code terminal is open and the restore reopens it
- **THEN** the agent process is spawned with `--resume <the recorded session id>` and the conversation
  continues where it left off

#### Scenario: A session id that failed validation restores without resuming
- **WHEN** the recorded vendor session id is empty, over 512 characters, carries control characters or
  starts with a dash
- **THEN** the terminal reopens in the same directory without a resume target, and the row's resume
  material is dropped

### Requirement: The restore runs at boot and drops what no longer exists
The restore SHALL run as a hosted service in the Workspace module after the database is up, walk the
persisted rows oldest first, and reopen each through the same path a person opening a terminal takes —
which recomputes the line from the folder rather than replaying a stored command. A row whose folder,
worktree or working directory no longer exists SHALL be dropped with a log line rather than restored
into an error. A row that cannot be reopened this boot SHALL be dropped rather than left as a promise:
the terminals a restore half-finished are not a state a screen can finish for it.

#### Scenario: A removed worktree is not restored into
- **WHEN** a row's working directory no longer exists on disk
- **THEN** the row is dropped and the drop is logged

#### Scenario: Restored terminals keep their ids
- **WHEN** a terminal is restored
- **THEN** it carries the id it had before the restart, so `term:<id>` tabs and hub groups survive

#### Scenario: The restore respects the open-session ceiling
- **WHEN** more rows exist than the terminal ceiling allows
- **THEN** the ceiling holds and the rows that did not fit are dropped
