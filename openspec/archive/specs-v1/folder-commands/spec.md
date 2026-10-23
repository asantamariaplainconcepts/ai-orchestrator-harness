# folder-commands Specification

## Purpose
The commands a folder offers — build, test, a dev server — held as rows against the folder rather than
read live from a file on a branch. `.harness/commands.json` is a seed a person imports, not a source
the product follows: #98 made the row the record after #94 had made the file one, because a file on a
branch changes underneath you on every `git switch`. Reverse-engineered from the shipped code, not
negotiated ahead of it.

## Requirements
### Requirement: A command belongs to the folder, and adding one writes no file
The system SHALL expose `POST /api/folders/{id:guid}/commands` (`AddCommand.cs`,
`workspace:configure`), which SHALL write a row hanging off the folder and SHALL NOT write, append to
or read `.harness/commands.json`. A folder's commands SHALL be the same answer from every working
directory of that folder, so the request SHALL carry no worktree path. The command's working
directory SHALL be bounded on the way in — normalised here and checked again where it is resolved —
even though the request comes from the app's own dialog, because the thing on the other side of it is
a process. A repeated name SHALL get a key of its own.

#### Scenario: Adding a command writes no file
- **WHEN** a command is added through the dialog
- **THEN** a row is written and the folder's `.harness/commands.json` is untouched

#### Scenario: The same commands from every working directory
- **WHEN** the folder's commands are read from the checkout and from one of its worktrees
- **THEN** both answers are the same list

#### Scenario: A working directory that would escape is bounded
- **WHEN** the submitted working directory climbs out of the checkout
- **THEN** it is bounded on the way in rather than stored as given

### Requirement: A command is edited by key, and a running one is not touched
The system SHALL expose `PUT /api/folders/{id:guid}/commands/{key}` (`EditCommand.cs`,
`workspace:configure`) changing six fields while the **key** and the **stop line** stay: the key is
how a command is addressed by the tab strip, the session running it and the row saying when it last
ran, and the stop line has never been in the dialog, so an edit that dropped it would leave a
container running after the server goes down. Nothing SHALL happen to a session already running that
command — the process holds the line the row used to hold, and this writes a declaration read at the
next start — and the dialog SHALL say so when there is a live session to say it about. A command the
folder does not hold SHALL be refused, for edit and for removal alike.

#### Scenario: An edit keeps the key and the stop line
- **WHEN** a command's name and line are changed
- **THEN** its key and stop line are unchanged, and a session already running it keeps running

#### Scenario: A key the folder does not hold is refused
- **WHEN** an edit or a removal names a key this folder does not hold
- **THEN** the request is refused

### Requirement: Removing a command stops nothing
The system SHALL expose `DELETE /api/folders/{id:guid}/commands/{key}` (`RemoveCommand.cs`,
`workspace:configure`), which SHALL remove the row and SHALL NOT reach for a terminal: the process
outlives the declaration and the screen says so. Removing a worktree is the other case and does close
its terminals, because there the directory the process stands in is about to stop existing.

#### Scenario: The row goes and the process stays
- **WHEN** a running command's row is removed
- **THEN** the command leaves the folder's list and the running session is not closed

### Requirement: The declared file is a seed a person imports, and it only adds
The system SHALL expose `POST /api/folders/{id:guid}/commands/import` (`ImportCommands.cs`,
`workspace:configure`) as the **only** reader of `.harness/commands.json` in the product — nothing on
a schedule, nothing on a probe, nothing on the query that draws the screen. The import SHALL read the
file from the **checkout** and never from a worktree, because the file is on a branch. It SHALL only
add: a key the folder already holds SHALL be left exactly as it is, neither updated nor replaced, and
a key the seed no longer lists SHALL NOT be removed — otherwise a person's tuned command would be
undone with no diff to notice it in, and the file would become the authoritative live source #98
removed. Importing from a folder with no file SHALL be refused with the reason.

#### Scenario: A file on disk changes nothing by itself
- **WHEN** `.harness/commands.json` exists in the checkout and nobody has pressed import
- **THEN** the folder's command list is unchanged

#### Scenario: Importing twice leaves the first import alone
- **WHEN** the seed is imported a second time after a command was edited in the dialog
- **THEN** the existing keys are left as they are, including their stop lines and check flags

### Requirement: The declared file is read defensively and bounded
`IDeclaredCommandFile` (`Infrastructure`, `HarnessCommandFile`) SHALL read the file in the order it
lists commands, accept comments and trailing commas because the file is reviewed by people, and treat
a malformed or oversized file as declaring **nothing** rather than as an empty set. A repeated key
SHALL keep the first entry, a missing name SHALL fall back to the key, a command with no key SHALL be
addressed by its name, newlines SHALL be flattened out of names and command lines, an entry with
nothing to run SHALL be skipped without losing the others, and the number of commands and every field
length SHALL be bounded. An explicitly empty list SHALL be a real answer. A declared working directory
that escapes the checkout SHALL be read as the checkout.

#### Scenario: A malformed file declares nothing
- **WHEN** the file cannot be parsed, **THEN** nothing is declared, and this is distinguishable from a
  file that declares an empty list

#### Scenario: The file's four extra fields are carried
- **WHEN** an entry declares where it runs, whether it ends, what stops it, and whether its exit code
  counts
- **THEN** all four are read, and a long-running command cannot declare itself a check

### Requirement: A command runs in the working directory it was started in
Running a command SHALL hand `ICommandRunner` what the folder holds, resolving the command's own
directory **against the working directory it is started in**, so one declaration works in the
checkout and in every worktree of it. Stopping a command SHALL end it and keep its tab. One folder
SHALL NOT be able to stop another folder's command.

#### Scenario: The directory is resolved against the working directory
- **WHEN** a command declaring a relative directory is run from a worktree
- **THEN** it runs in that worktree's copy of the directory

#### Scenario: One folder cannot stop another's command
- **WHEN** a stop names a session belonging to another folder
- **THEN** the request is refused
