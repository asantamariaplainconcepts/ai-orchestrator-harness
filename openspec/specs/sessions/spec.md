# sessions Specification

## Purpose
A session is a live terminal: the one a loop run's agent works in, or one a person opened by hand on a
folder or a PBI. The console attaches to it over a WebSocket wherever it runs, lists the live ones in
the tray, and lets it die with its run. Reverse-engineered from the shipped code
(`src/modules/Runs/AiOrchestrator.Modules.Runs/Features/PbiRuns/`,
`src/shared/AiOrchestrator.Infrastructure/Sessions/PtySession.cs`), not negotiated ahead of it.

## Requirements
### Requirement: The tray lists the live sessions, each once
`GET /api/sessions` SHALL answer only the live sessions: the runs that are live in this process with a
started session, ordered by when they started, then the sessions opened on a folder. Each SHALL carry
its run id, repository, PBI and state when it has them, whether it is manual, its provider and command,
when it started, when it last wrote, whether it is stale, and its folder's path. A session that ended
SHALL NOT be listed: what it wrote is the run's recording.

#### Scenario: A run ends and leaves the tray
- **WHEN** a loop run's agent exits
- **THEN** the next `GET /api/sessions` no longer lists it, and its recording is in the Journey

### Requirement: A live terminal is a WebSocket anybody can attach to
`GET /api/sessions/{runId}/attach` SHALL accept a WebSocket, send what the session has written so far
(up to 256 KiB) and then its output as binary frames, pass binary frames from the browser to the
session as keystrokes, and read a text frame `{"columns", "rows"}` as a resize, ignored below 40×10 and
capped at 500×200. A request that is not a WebSocket SHALL be 400, and a run that is not live 404
`Session.NotLive`. Closing the browser SHALL detach the viewer and leave the run running; a viewer that
falls behind by 4,096 chunks SHALL be dropped rather than slow the run.

#### Scenario: Closing the terminal does not stop the run
- **WHEN** a person closes a run's terminal in the tray
- **THEN** the WebSocket closes with "Detached." and the run goes on writing

### Requirement: A touch screen gets the keys its keyboard lacks
On a coarse primary pointer, a terminal SHALL show under it a row of Esc, Tab, Ctrl, the four arrows
and Enter, each sending the bytes a physical press of that key sends, on the same WebSocket as the
keyboard. Ctrl SHALL latch until the next key: an arrow then carries the Ctrl modifier (`ESC[1;5A`), and
a letter typed on the soft keyboard is sent as its control byte. With a fine pointer the row SHALL NOT
be shown.

#### Scenario: Interrupting an agent from a phone
- **WHEN** a person on a phone presses Ctrl in the row and types c
- **THEN** the session receives `\x03`, and the latch clears

### Requirement: A session opened on a PBI is a manual run of its current state
`POST /api/pbi/sessions {repository, id, provider?, model?, command?}` SHALL read the PBI's state at the
vendor now, prepare a place on `change/<id>` in the folder's environment, and start a manual run there:
a shell when no provider is given, or the agent with its model and the optional command. The run SHALL
be recorded and listed in the PBI's Journey like any other, and SHALL NOT be seen by the engine: it
does not count against the WIP limit, its end record is not parsed, and it moves nothing.

#### Scenario: Explore with agent on a held PBI
- **WHEN** a person opens Claude Code on a PBI held in `code-review`
- **THEN** a manual run of `code-review` appears in the tray and in the Journey, and the PBI stays held

### Requirement: A session opened on a folder leaves nothing behind
`POST /api/folders/sessions {path, provider?, model?, command?}` SHALL open the shell or the agent in
the folder's own checkout, with no worktree, no row and no recording. A path that is not a directory
SHALL be 404 `Session.NoFolder`; an unknown provider 404 `Session.NoAgent`; a CLI that is not installed
409 `Session.AgentMissing`; a process that did not start 409 `Session.NotStarted`. When it exits, nothing
SHALL remain of it.

#### Scenario: Terminal on a folder
- **WHEN** a person chooses Terminal from a folder's menu in the rail
- **THEN** a shell opens in that checkout, appears in the tray, and when it exits nothing of it is kept
