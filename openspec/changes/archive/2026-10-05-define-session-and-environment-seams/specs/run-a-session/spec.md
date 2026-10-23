## ADDED Requirements

### Requirement: A session is spawned without exposing the process or the place to the caller
The system SHALL expose a seam `ISession` in `BuildingBlocks/World/` that spawns an agent or a command session and returns a record a screen can render, and SHALL NOT expose the pseudo-terminal, the process, the operating system or the container it runs in to any module above the seam. The local PTY (`PtyAgentRunner`, `ProcessCommandRunner`) is the first implementation, wrapped behind the seam — never the contract.

#### Scenario: A caller never holds a process handle
- **WHEN** a use case spawns a session
- **THEN** it receives a session record and an id, and no type from `System.Diagnostics` or `PTY` reaches a module above `BuildingBlocks/World`

#### Scenario: A local PTY is the first implementation
- **WHEN** a session is spawned and the environment is local
- **THEN** a pseudo-terminal backs it, and replacing the backing does not change the seam's contract

### Requirement: A session id is the run id, and one run is one session
A session the seam spawns for an agent run SHALL carry the run's own id as its session id — `sessionId == runId` — because one run is one session and they are one value. The seam is the surface through which the launcher drives the agent; it does not mint a second id space beside the run's.

#### Scenario: A run's session carries the run's id
- **WHEN** a run is started
- **THEN** the session the launcher opens carries the run's id, and attaching to it names that id

### Requirement: A session supports attach, detach and a listing of live sessions
The seam SHALL expose attach and detach against a session id — attaching more than one pane to the same live session, detaching without ending it — and SHALL expose a listing of every live session, each once. Navigation, attaching a second pane or detaching a pane SHALL NOT end the run or the session.

#### Scenario: Two panes attach to one session
- **WHEN** a second pane attaches to a session a first pane is already on
- **THEN** both panes receive the session's output, and only one session exists

#### Scenario: Detaching a pane does not end the run
- **WHEN** a pane detaches from a live session
- **THEN** the session continues, the run continues, and the session remains in the live listing

#### Scenario: The live listing lists each session once
- **WHEN** the tray asks for the live sessions
- **THEN** each session appears exactly once, regardless of how many panes are attached

### Requirement: Input and signal go through the seam, not the process
The seam SHALL accept input (a message or keystrokes) against a session id and SHALL accept a terminate signal against a session id, and SHALL NOT give the caller a handle to signal the process directly. A message SHALL be delivered as one message (a bracketed paste and a return), and a keystroke SHALL be passed through as given — the same split `AgentInput` draws, because routing a keystroke through the message path turns `Esc` into a paste of `Esc` followed by Enter, which at a menu confirms whatever is highlighted.

#### Scenario: A message is delivered as one message
- **WHEN** a multi-line message is sent to a session
- **THEN** it arrives as a single message with its newlines intact as content

#### Scenario: A keystroke is passed through, not pasted
- **WHEN** a keystroke is sent to a session
- **THEN** it is written as given, without paste markers or a trailing carriage return

#### Scenario: A session id that has ended is not an error
- **WHEN** input or a signal is sent to a session that has ended
- **THEN** the seam answers false rather than throwing

### Requirement: Terminal and chat are two views of one session
A live session SHALL carry two views the panes can choose between: a terminal view over the raw byte stream, and a chat view over the structured record. The terminal view SHALL receive the byte stream the pseudo-terminal produces, as `AgentTerminal.tsx` does today; the chat view SHALL receive the structured record `IAgentTranscriptReader` builds from the vendor's own file, mapping every block onto its `AgentTranscriptRoles` role. The two views SHALL share the session's identity and its lifecycle.

#### Scenario: A pane switches from terminal to chat
- **WHEN** a pane on a live session switches its view from terminal to chat
- **THEN** it receives the structured record for the same session, and the session is not affected

#### Scenario: A session with no structured record offers no chat
- **WHEN** a session's vendor kept no structured record
- **THEN** the chat view is refused with a sentence the pane can show, and the terminal view remains

### Requirement: A session ends with its run and its transcript survives it
When a run finishes or is cancelled, its session SHALL end and the resource behind it SHALL be freed, and the session SHALL leave the live listing. Transcript, cost, result, commits and the pull request SHALL remain consultable — they live on the `Run` row and its transcript endpoint (`GET /api/runs/{id}/transcript`, `IAgentTranscriptReader`), not on the live session.

#### Scenario: A finished run frees its session
- **WHEN** a run finishes
- **THEN** the session is no longer in the live listing, and the process behind it is gone

#### Scenario: The transcript survives the session
- **WHEN** a run's session has ended
- **THEN** the transcript, cost, commits and pull request are still readable through the run, and the chat view reads the record rather than a live stream

### Requirement: Restore is not part of the public contract and does not revive a finished session
The seam SHALL NOT expose restore as a public verb, and SHALL NOT revive a session that has ended. Reattaching a live local session MAY recover its screen as a detail of the local adapter — the same thing `EnsureStarted`, `Running` and `Replay` do today, rephrased over the session — but that is not a public contract verb and a finished session is not brought back.

#### Scenario: A finished session is not revived
- **WHEN** a session has ended and a pane attaches to its id
- **THEN** the seam answers that the session is not live, and no new process is started behind it

#### Scenario: Reattaching a live local session may recover its screen
- **WHEN** a pane reattaches to a live local session
- **THEN** the local adapter may recover the screen it had, through a detail of the adapter rather than a public verb on the seam
