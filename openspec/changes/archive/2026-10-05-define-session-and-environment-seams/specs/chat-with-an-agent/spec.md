## MODIFIED Requirements

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
