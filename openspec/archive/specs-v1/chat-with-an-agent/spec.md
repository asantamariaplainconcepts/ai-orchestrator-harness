# chat-with-an-agent Specification

## Purpose
A conversation with the folder's agent as a tab, spoken in structured JSON rather than typed at a
terminal frame: one conversation per directory, messages sent as messages, and the agent's permission
questions answered as questions with an id. It is the road a run's terminal cannot offer, not a patch on
it. Reverse-engineered from the shipped code, not negotiated ahead of it.

## Requirements
### Requirement: The tab draws itself from one read
The system SHALL expose `GET /api/folders/{id:guid}/chat` (`Features/Folders/UseCases/Chat.cs`,
`GetChat`, `workspace:read`) returning whether this folder's agent speaks here at all, the conversation
so far, and the session when one is live — one read rather than three, because a tab that learned "no
session" before it learned "this agent has no adapter" would draw an empty conversation and an enabled
composer over a box that cannot send. The conversation SHALL have exactly one source: while a process is
alive its own tail is the whole of it, and when nothing is alive the record is read directly. Nothing
SHALL stitch the two together. Only Claude Code SHALL claim to chat; the other vendors SHALL report that
they do not. The chat SHALL be the structured-record view of the same `Session` the terminal view holds —
sourced from `IAgentTranscriptReader` and `AgentTranscriptRoles` — so the two views share the session's
identity and its lifecycle. A session whose vendor kept no structured record SHALL be answered as "no
chat for this session," and a chat SHALL NOT be invented for it.

#### Scenario: A folder whose agent has no adapter says so
- **WHEN** the folder's default agent is one with no chat adapter
- **THEN** the tab is told the agent does not speak here, and the composer is not enabled

#### Scenario: A resumed conversation is not a fragment
- **WHEN** a conversation is reopened and its process is alive
- **THEN** the tail read from the process already carries what the record it continued held

#### Scenario: A session with no structured record offers no chat
- **WHEN** a live session's vendor kept no structured record
- **THEN** the chat view is refused with a sentence the pane can show, and the terminal view remains the only one
### Requirement: One conversation per directory, opened idempotently over HTTP
`POST /api/folders/{id:guid}/chat` (`OpenChat`, `workspace:chat`) SHALL open the conversation or hand
back the one already open in that directory, so pressing the tab twice cannot leave two agents talking in
one folder — a second conversation about the same change is two runs, or an issue arguing why not. It
SHALL be an HTTP endpoint rather than a hub call, which is the split every hub in this product observes:
a hub dispatches nothing, inherits no authorization from the CQS pipeline, and starting a process is not
something to do without one.

#### Scenario: Pressing the tab twice is one conversation
- **WHEN** open is called twice for the same directory
- **THEN** the same session is returned and only one agent process exists

### Requirement: A conversation is derived per directory and continued link by link
The chat chain SHALL derive the same session id from the same directory every time, SHALL treat two
spellings of one directory as one chain, and SHALL keep a worktree's conversation separate from its
checkout's. Two folders over one directory SHALL NOT share a chain. Every link SHALL be a well-formed
UUID. A fresh directory SHALL open the first link, an existing conversation SHALL be continued into
the next link, the latest link SHALL be the one holding the conversation, the chain SHALL stop at the first
gap, and a directory nobody has talked in SHALL have no record. Continuing SHALL fork the conversation
under the next link, and the lookup SHALL be narrowed by modification time to keep a scan off a tree of
thousands of files while never deciding which file — 90 days back, wide enough that "the conversation is
still there on Monday" is true.

#### Scenario: A worktree is a different conversation from its checkout
- **WHEN** a conversation is opened in a worktree and in its checkout
- **THEN** they are two chains, each deriving its own session id

#### Scenario: The chain stops at the first gap
- **WHEN** a link in the chain has no record on disk
- **THEN** the walk stops there rather than skipping to an older link
### Requirement: A message is sent as one message, carrying its own settings
`POST /api/folders/{id:guid}/chat/{sessionId:guid}/messages` (`SayToChat`, `workspace:chat`) SHALL send
what the person typed as **one message** — not a paste and a keypress, which is what steering a run is
— so four pasted lines arrive as one message with no terminal frame and no highlighted option to press by
accident. The model and the other two settings SHALL ride on the message rather than on the session,
because a run chooses once and a conversation is many: switching the model between two messages SHALL be
respected, and the earlier message SHALL keep saying what it was sent with. A line with nothing in it
SHALL send nothing, and a trailing terminator SHALL NOT be pasted.

#### Scenario: Four lines are one message
- **WHEN** a multi-line message is sent
- **THEN** it arrives as a single message with its newlines intact as content

#### Scenario: The model can change between messages
- **WHEN** a second message names a different model
- **THEN** it is sent with that model and the first message still records the one it used

### Requirement: The agent's permission question is answered as a question, by a person
`POST /api/folders/{id:guid}/chat/{sessionId:guid}/answer` (`AnswerChat`, `workspace:chat`) SHALL answer
what the agent asked to be allowed to do, by id. This is the step that is impossible against a run, and
the difference is the transport rather than the policy: `SteerRun` keeps refusing while the agent is
blocked because there the question is a menu drawn in terminal cells and a paste would press whatever is
highlighted. An answer SHALL say allow or deny and SHALL only say why on a deny. The app SHALL NOT answer
for anybody: a person presses Allow or Deny. The handshake SHALL open the control channel, asking Claude
Code for the stream and the permission channel together, and a line that will not parse SHALL be skipped
rather than fatal.

#### Scenario: A permission question is answered by id
- **WHEN** the agent asks to be allowed to do something and the person presses Allow
- **THEN** an allow is sent for that question's id, with no reason attached

#### Scenario: A deny carries the reason
- **WHEN** the person presses Deny
- **THEN** the answer says deny and carries why

#### Scenario: Unparseable output does not end the conversation
- **WHEN** a line of the agent's stream cannot be parsed
- **THEN** it is skipped and the conversation continues

### Requirement: Every block of the stream becomes the role the screen reads
The chat reader SHALL map the vendor's stream onto the roles the screen renders: every block becomes its
role, a tool result arrives under the user envelope, a result ends the turn, a failed turn still hands the
turn back, and the init event says which session this is.

#### Scenario: A failed turn does not hang the composer
- **WHEN** the agent's turn ends in a failure
- **THEN** the turn is handed back and the person can type again

### Requirement: Closing ends the process and keeps the record
`DELETE /api/folders/{id:guid}/chat/{sessionId:guid}` (`CloseChat`, `workspace:chat`) SHALL end the
conversation's process and SHALL leave the record, which is the vendor's own file, so reopening the tab
continues from it. This is "stop the process", not "forget what was said" — the same distinction
`ITerminals` draws between stopping and closing.

#### Scenario: Reopening continues the conversation
- **WHEN** a conversation is closed and the tab is opened again
- **THEN** the previous exchange is still there and the next message continues it
