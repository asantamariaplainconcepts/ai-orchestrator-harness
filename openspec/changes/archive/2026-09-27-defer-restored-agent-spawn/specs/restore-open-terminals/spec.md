## MODIFIED Requirements

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

### Requirement: The restore runs at boot and drops what no longer exists
The restore SHALL run as a hosted service in the Workspace module after the database is up, walk the
persisted rows oldest first, and reopen each through the same path a person opening a terminal
takes — which recomputes the line from the folder rather than replaying a stored command. A restored
agent terminal is registered openable rather than spawned, as its requirement states; a restored
shell terminal is spawned, and a login shell without a reader is harmless. A row whose folder,
worktree or working directory no longer exists SHALL be dropped with a log line rather than restored
into an error. A row that cannot be reopened this boot SHALL be dropped rather than left as a
promise: the terminals a restore half-finished are not a state a screen can finish for it.

#### Scenario: A removed worktree is not restored into
- **WHEN** a row's working directory no longer exists on disk
- **THEN** the row is dropped and the drop is logged

#### Scenario: Restored terminals keep their ids
- **WHEN** a terminal is restored
- **THEN** it carries the id it had before the restart, so `term:<id>` tabs and hub groups survive

#### Scenario: The restore respects the open-session ceiling
- **WHEN** more rows exist than the terminal ceiling allows
- **THEN** the ceiling holds and the rows that did not fit are dropped

#### Scenario: Closing a restored tab nobody opened
- **WHEN** a restored agent terminal's tab is closed before any pane attached to it
- **THEN** the terminal is forgotten, its row is dropped, and no process was ever started for it

## ADDED Requirements

### Requirement: The first attach starts a restored agent terminal
A pane attaching to a restored agent terminal that has not yet been started SHALL cause the process
to start — through the terminals seam, with the resume line the restore registered — and the attach
SHALL wait, briefly and bounded, for the process to have registered, so the pane's size measurement
lands on it and the first paint is at the pane's width rather than a spawn default. Attaching SHALL
be idempotent: a second attach while the process runs gets the replay the first attach's process is
producing, and a second attach racing the first gets the same session once, not two processes. A hub
method SHALL NOT accept anything that reaches a command line; the session id of a terminal already
registered by the restore is the whole of what an attach names.

#### Scenario: A pane attaching to a restored opencode terminal starts it
- **WHEN** a pane attaches over the terminal hub to a restored agent terminal registered by the
  restore and not yet started
- **THEN** the process starts with `--session <the recorded id>` and its output streams to that pane

#### Scenario: Two panes attaching to the same restored terminal start one process
- **WHEN** two panes attach to the same restored agent terminal, whether one after the other or at
  once
- **THEN** one process runs and both panes receive its output
