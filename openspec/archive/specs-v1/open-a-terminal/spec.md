# open-a-terminal Specification

## Purpose
Terminals the server owns, inside the app: a login shell in a folder, the folder's agent in it, or one
of the folder's declared commands. The session belongs to the server rather than to the browser, so a
reload finds it still running and a tab beside the diff is where its output lives. Reverse-engineered
from the shipped code, not negotiated ahead of it.
## Requirements
### Requirement: A terminal opens in a resolved directory, never in a path a caller names
The system SHALL expose `POST /api/folders/{id:guid}/terminals`
(`Features/Folders/UseCases/OpenTerminal.cs`, `workspace:open-on-desktop`), which SHALL resolve the
working directory from the folder and its worktrees rather than honouring an arbitrary path, because
this endpoint starts a process in the directory it is given and a free path would be a shell anywhere
on the machine. A declared command's own directory SHALL be resolved against the checkout it is
started in with `Path.GetFullPath` and SHALL be refused when the result is not inside it — checked
here even though `IDeclaredCommandFile` already normalised it, because `.harness/commands.json` is the
single untrusted input in this product and the thing on the other side of the line is a process.
Symlinks SHALL NOT be followed. The terminal the request opens SHALL be a view over a `Session`
(`ISession`), and the session the launcher holds — not the directory the screen carries — is what a
pane reattaches to. The `?worktree=` parameter SHALL NOT be carried by the route the terminal opens
under; the session identity is the anchor.

#### Scenario: A command's directory is resolved against the working directory
- **WHEN** a declared command naming a relative directory is opened from a worktree
- **THEN** the process starts in that worktree's copy of the directory

#### Scenario: A directory that escapes is refused
- **WHEN** the resolved directory falls outside the checkout it was started in
- **THEN** the terminal is refused rather than opened

#### Scenario: A reopened pane attaches to the session, not a path
- **WHEN** a pane reopens while a session a terminal opened is still live
- **THEN** the session the launcher holds is attached, and the route carries no `?worktree=` parameter
### Requirement: A shell terminal runs the person's own login shell
A shell terminal SHALL run `exec "${SHELL:-/bin/sh}" -l` — `exec`, so the session **is** the shell
rather than a shell inside the one that started it, otherwise Ctrl-D drops the person into an invisible
parent instead of ending the terminal; `$SHELL`, because their prompt, aliases and `PATH` live in the
shell they chose. The fallback SHALL be reached only when the variable is unset.

#### Scenario: Ctrl-D ends the terminal
- **WHEN** a person presses Ctrl-D in a shell terminal
- **THEN** the session ends rather than dropping into a parent shell

### Requirement: A folder's terminals are listed per folder, and every terminal per machine
The system SHALL expose `GET /api/folders/{id:guid}/terminals` (`ListTerminals`, `workspace:read`)
returning what is running and what has finished and not been closed, scoped **per folder rather than
per working directory** — a command run in a worktree is still this folder's terminal, and each session
says which directory it is in, because filtering by directory hid exactly the tab somebody had just
started. The system SHALL also expose `GET /api/terminals` (`ListAllTerminals`, `Access.AnyCaller`)
returning every terminal on this machine across every folder, which is what the global session tray
counts: "what is live" is not scoped to where you are standing, and a folder-scoped read would be one
request per folder to render one tray. The live listing SHALL list each session once, regardless of how
many panes are attached.

#### Scenario: A reopened screen attaches instead of starting a second shell
- **WHEN** a person reloads the folder screen while a terminal is running
- **THEN** the list carries that session and the tab attaches to it

#### Scenario: The attention chip counts the machine
- **WHEN** terminals are running in two different folders
- **THEN** `GET /api/terminals` returns both in one answer

#### Scenario: The tray counts the machine, each session once
- **WHEN** terminals are running in two different folders, and one has two panes attached
- **THEN** `GET /api/terminals` returns both sessions in one answer, and each session appears once
### Requirement: Stopping keeps the tab; closing takes the process group
`POST /api/folders/{id:guid}/terminals/{sessionId:guid}/stop` (`StopTerminal`) SHALL stop what the
terminal is running and **leave the tab standing**, because a stopped command still has everything it
printed on its way down and that is usually the half somebody wanted; the checkout's declared stop line
SHALL run first when one is declared, before the process group is signalled, because killing the group
first takes away the handle a polite shutdown needed. `DELETE /api/folders/{id:guid}/terminals/{sessionId:guid}`
(`CloseTerminal`) SHALL end the session and kill its process group, and SHALL NOT run a command's
declared stop line: closing a tab is not a request to shut an application down, and what a command
detached stays the server's to take away when the server stops.

#### Scenario: A stopped command keeps its output
- **WHEN** a running command is stopped
- **THEN** the process ends, its declared stop line ran first, and the tab remains with its output

#### Scenario: Closing kills the group without the stop line
- **WHEN** a tab is closed
- **THEN** the session ends and its process group is killed, and no declared stop line is run

### Requirement: An image from a clipboard becomes a path a terminal can type
The system SHALL expose `POST /api/terminals/paste` (`PasteImage.cs`, `Access.AnyCaller`) accepting
base64 bytes in a JSON body and answering with a path on this machine's disk, so a terminal can type
that path at the agent running in it. It SHALL go over HTTP rather than through the terminal hub, for
two reasons: `AddSignalR()` is called with no options so the receive limit is the 32 KB default and a
screenshot does not fit, and a hub dispatches nothing so it inherits no authorization from the CQS
pipeline and cannot be where a side effect starts. `IPastedFiles` SHALL take the extension from the
media type, refuse a type it does not paste rather than guessing, refuse an empty paste rather than
writing an empty file, write two pastes in the same second as two files, and name the file after where
it came from and when.

#### Scenario: A screenshot becomes a path
- **WHEN** an image is pasted
- **THEN** the bytes are on disk where the response said they would be, and the name says where it came
  from and when

#### Scenario: An unsupported or empty paste is refused
- **WHEN** the media type is one this does not paste, **THEN** it is refused rather than guessed at
- **WHEN** the paste is empty, **THEN** it is refused rather than written as an empty file

### Requirement: A terminal's title is read from what the agent draws
The terminal layer SHALL read a session's title out of the byte stream, including a title split across
two or many chunks and one terminated by a string terminator, SHALL NOT mistake other escape sequences
for titles, and SHALL NOT let an unterminated title grow without bound. It SHALL derive an agent's state
from what the title says: a spinner glyph means working, an asterisk means it is the person's turn, and
some agents write words instead of drawing. A word inside another word or a path SHALL say nothing, and a
plain shell title SHALL say nothing rather than idle. The summary SHALL be the title without its status
glyph.

#### Scenario: A title split across chunks is still read
- **WHEN** the escape sequence carrying the title arrives across several reads
- **THEN** the title is read once, whole

#### Scenario: A plain shell says nothing
- **WHEN** a shell sets an ordinary title
- **THEN** no agent state is claimed from it

### Requirement: A terminal can be asked to continue a vendor session
`TerminalRequest` SHALL carry an optional vendor session to continue, so a restored terminal resumes
the conversation it was having rather than starting a new one behind the same tab. For an agent
terminal with a session to continue, the line SHALL gain the vendor's own resume arguments
(`AgentDefinition.ResumeArguments`) — pinning a fresh session id and resuming are two different
statements, and the pin mechanism SHALL NOT fire for a resumed terminal. A shell and a command
ignore the field.

#### Scenario: An agent terminal resumed rather than pinned
- **WHEN** an agent terminal is opened carrying a vendor session to continue
- **THEN** the process is spawned with the vendor's resume arguments and without a fresh session pin

#### Scenario: A shell ignores the session to continue
- **WHEN** a shell terminal is opened
- **THEN** the line is the person's login shell and nothing is appended

