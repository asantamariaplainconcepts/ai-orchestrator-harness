# live-workspace-updates Specification

## Purpose
Keeps the sidebar honest about what is on disk without anybody pressing anything: a service re-reads
every folder on an interval, and a hub tells the browsers to ask again the moment a worktree appears
or goes. The socket is a signal, never a payload, and it never replaces the poll underneath it.
Reverse-engineered from the shipped code, not negotiated ahead of it.

## Requirements
### Requirement: Every folder is re-read on an interval, and an unreadable folder is skipped
`FolderProbePoller` SHALL re-read every folder from disk on an interval and reconcile it through
`Folder.RecordProbe`, which replaces the worktree set whole rather than merging — the only way a
`git worktree remove` typed in a terminal is ever noticed. A folder that cannot be read SHALL be
**skipped, never reconciled**: `IWorkspaceProbe` answers null rather than a partial state, and null
here means leave it alone, with the stale `ProbedAt` as the record that it was not reached. A folder
that is not a repository SHALL never be probed.

#### Scenario: A worktree removed in a terminal disappears without anybody opening a screen
- **WHEN** a worktree is removed outside the app and the sweep runs
- **THEN** the folder's worktree rows match git again

#### Scenario: An unreadable folder is left exactly as it was
- **WHEN** the probe answers null for a folder — an unplugged drive, a repository mid-rebase
- **THEN** every worktree row survives and `ProbedAt` is not advanced

### Requirement: A change to a folder's worktrees is announced, and nothing else is
`IWorkspaceNotifier` SHALL announce that a folder is not what it was, and SHALL be a seam
(`BuildingBlocks/World/IWorkspaceNotifier.cs`, implemented by `HubWorkspaceNotifier` in
`Infrastructure`) because the browsers it reaches are outside this process and a module may not know a
browser exists. An announcement SHALL be made when a worktree appeared and when one went, including
when the app itself created or removed it. A sweep that finds the same worktrees SHALL say nothing, a
changed dirty count or a renamed branch SHALL NOT be an announcement, a folder that could not be read
SHALL NOT be announced, and a removal git refused SHALL announce nothing. Announcing SHALL be
fire-and-forget: implementations swallow their own failures, and nothing upstream fails because
nobody was listening.

#### Scenario: A worktree appearing or going is announced
- **WHEN** the sweep finds a worktree that appeared, or one that went
- **THEN** the other windows are told, and what a client refetches on that message is the new answer

#### Scenario: Noise is not announced
- **WHEN** the sweep finds the same worktrees, a changed dirty count, or a folder it could not read
- **THEN** nothing is announced

### Requirement: The socket carries a signal, and the poll stays the floor
`workspaceHub.ts` SHALL treat the server's message as "ask again" rather than as the new list, so
there is exactly one way to read a folder and a client that missed a message while its socket was down
converges by doing what it does anyway. The hub SHALL NOT replace the folder query's twenty-second
poll: `withAutomaticReconnect` gives up after about half a minute of failures, and a rail whose only
source of truth is a socket freezes with no sign that it has. The hub SHALL make the one moment
anybody notices immediate rather than up to eighty seconds late — sixty for the server's sweep, twenty
more for the browser to ask.

#### Scenario: The message is not the payload
- **WHEN** the hub delivers a folder-changed message
- **THEN** the client refetches the folder tree and renders that answer

#### Scenario: A dead socket degrades to the poll
- **WHEN** the socket cannot reconnect
- **THEN** the rail keeps updating on its twenty-second poll

### Requirement: One sentence describes a working copy, shared by both readers
`Dirty` SHALL produce what a working copy's subtitle says when nothing is running in it, and SHALL be
shared by `ListFolders` (the rail's branch row) and `GetFolder` (the project screen's heading). The two
describe the same directory at the same moment and are often on screen together, so a drift between
them would not read as two components disagreeing — it would read as one of them being wrong about the
repository with no way to tell which.

#### Scenario: The rail and the project screen agree
- **WHEN** the same worktree is shown in the sidebar and on the project screen
- **THEN** both subtitles are the same sentence, derived rather than stored
