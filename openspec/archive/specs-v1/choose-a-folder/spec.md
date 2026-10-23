# choose-a-folder Specification

## Purpose
Points the app at the directories on this machine it can run agents in — usually git checkouts, and
not necessarily. The gate is "what is this", not "is this a repository": a folder's commands ask git
nothing since #98, so a directory git has never heard of is still somewhere this product can work.
Reverse-engineered from the shipped code, not negotiated ahead of it.
## Requirements
### Requirement: A directory is added by its path, probed before it is stored
The system SHALL expose `POST /api/folders` (`Features/Folders/UseCases/AddFolder.cs`), open to any
caller — the one folder operation with no folder to hold a permission against, so it declares
`Access.AnyCaller` rather than a permission. The submitted path SHALL be non-empty and at most 1000
characters. The system SHALL canonicalise the submitted path and every stored path before comparing
them, in memory rather than in SQL, so a hand-typed `~/code/portal` and a chooser's
`/Users/me/code/portal` are one folder; a match SHALL be refused with `FolderErrors.AlreadyAdded`.
The path SHALL be probed through `IWorkspaceProbe` **before** the row is written, and a path the
probe cannot read SHALL be refused rather than stored. A directory git cannot describe SHALL still
be stored, with `IsRepository` false and the aggregate's own defaults — no branch, no worktrees,
nothing uncommitted — and only a checkout SHALL record a probe.

Registering a repository SHALL also ask `IRepositoryGuard.EnsureInstalled` to install the trunk
guard on it. A guard failure SHALL NOT refuse the registration: the folder is stored all the same
and the failure's actionable sentence travels in the response, because a repository whose hook
could not be installed is still an inbox with work in it. The chooser path
(`POST /api/folders/choose`) SHALL do the same once the person picks a directory. On every startup
the server SHALL walk the registered repositories and re-run `EnsureInstalled` — idempotent, and
the repair for a hook somebody deleted. A repair failure SHALL be logged, never looped.

#### Scenario: A directory with no git is added
- **WHEN** a path that is a plain directory is submitted to `POST /api/folders`
- **THEN** the folder is created with `IsRepository` false, no probed git fields, and it holds commands

#### Scenario: A path that is not there is refused
- **WHEN** the submitted path does not exist on this machine
- **THEN** the request is refused with the reason and no folder row is written

#### Scenario: The same checkout typed two ways is one folder
- **WHEN** a path is submitted that canonicalises to an already-stored path — a tilde against an
  absolute path, or a trailing slash
- **THEN** the request is refused with `FolderErrors.AlreadyAdded` and no second row is written

#### Scenario: A checkout git cannot describe is refused rather than demoted
- **WHEN** the path is a checkout whose git state cannot be read
- **THEN** the request is refused, rather than the folder being stored as a plain directory

#### Scenario: A repository the guard cannot lock still becomes a folder
- **WHEN** the trunk guard refuses while a repository is being registered
- **THEN** the folder is stored, and the response carries the guard's own sentence about what it could not install

#### Scenario: A deleted hook is back after the next start
- **WHEN** a repository's `.githooks/pre-commit` is deleted and the server starts
- **THEN** the startup walk reinstalls it, and a guard that cannot reinstall logs the failure rather than retrying it

#### Scenario: A plain directory asks the guard nothing
- **WHEN** a folder with `IsRepository` false is registered, or the startup walk reaches it
- **THEN** no guard is installed and no `core.hooksPath` is written, because there is no trunk to protect

### Requirement: The machine's own chooser answers where a folder is
The system SHALL expose `POST /api/folders/choose` (`ChooseFolder.cs`, `Access.AnyCaller`), which
asks this machine for a directory through `IDesktopIntegration` and SHALL hold the request open while
a person decides — no job id and no polling loop, because there is one machine, one person and one
chooser. `IDesktopIntegration` SHALL refuse a second chooser while one is open, by naming the first,
and the seam SHALL own the ceiling on the wait. Cancelling SHALL be a success: `200` with `cancelled`
set and no path. Only a chooser that could not run at all SHALL be a refusal. A prompt containing a
newline SHALL be refused. With no folders yet the chooser SHALL start at the person's home directory,
and otherwise where most of the existing checkouts already are.

#### Scenario: Cancelling is not an error
- **WHEN** a person opens the chooser and dismisses it
- **THEN** the response is 200 with `cancelled` set and no path, and no red state appears beside the form

#### Scenario: A second chooser is refused by naming the first
- **WHEN** a chooser is already open and another `POST /api/folders/choose` arrives
- **THEN** the request is refused with a sentence naming the open chooser

#### Scenario: A machine with no chooser says so
- **WHEN** the machine has no directory chooser available
- **THEN** the response names the way round it — typing the path — rather than failing silently

### Requirement: Every folder is listed with its branches, the checkout first
The system SHALL expose `GET /api/folders` (`ListFolders.cs`, `Access.FiltersToCaller`) returning
every folder with its branches, and SHALL synthesize the repository's own checkout as the **first
branch** of each folder from the folder's probed state rather than storing it. A folder that is not a
repository SHALL get that row too, carrying no branch name and no dirty count. That row is where a
run is joined to a folder by the path they both name, so a folder without one could not be shown as
busy. The list SHALL report the folder's trunk so the create-worktree dialog can offer it.

#### Scenario: The checkout is the first branch of every folder
- **WHEN** a client reads `GET /api/folders`
- **THEN** each folder's branch list begins with its own checkout, synthesized rather than stored

#### Scenario: A plain directory still gets the checkout row
- **WHEN** the folder is not a repository
- **THEN** it carries the checkout row with no branch name and no dirty count, and the rail words the
  slot from its own catalogue rather than rendering a blank one

### Requirement: One folder is read in full for its detail screen
The system SHALL expose `GET /api/folders/{id:guid}` (`GetFolder.cs`, `workspace:read`) returning one
folder's file tree, configured commands and worktrees in a single round trip, because the tabs are
cheap together and a person switches between them faster than a request completes. The file tree
SHALL be read from `IWorkspaceProbe` at request time rather than from a table, and SHALL be the tree
of the working directory the caller scoped to — a worktree is a different checkout on a different
branch, so listing the checkout's files under a worktree's name shows somebody else's work.

#### Scenario: The tree belongs to the working directory asked about
- **WHEN** the caller scopes the read to a worktree
- **THEN** the files returned are that worktree's, not the checkout's

#### Scenario: The tree is read, never stored
- **WHEN** a file is created in the folder outside the app and the folder is read again
- **THEN** the new file is in the tree without any table having been updated

### Requirement: Forgetting a folder touches nothing on disk
The system SHALL expose `DELETE /api/folders/{id:guid}` (`RemoveFolder.cs`,
`workspace:manage-folders`), which SHALL delete the folder row and its worktree rows and SHALL NOT
ask git to delete anything: the checkout, its worktrees and `git worktree list` SHALL be exactly as
they were. Live terminals in that folder SHALL be closed **before** the row goes, because those
sessions are addressed as `/api/folders/{id}/terminals` and a shell nobody can reach is worse than a
shell nobody wanted; a terminal that has already exited SHALL NOT be closed again, and terminals in
other folders SHALL be left running. Runs SHALL be untouched — a run names its folder
by path string, so history keeps every run the checkout ever had. Re-adding the same path SHALL bring
the folder back with its branches already listed.

#### Scenario: The row goes and the directory stays
- **WHEN** a folder is removed
- **THEN** it leaves the list with its worktrees, and nothing on disk is touched

#### Scenario: The folder's terminals are closed first
- **WHEN** a folder with live terminals is removed
- **THEN** those sessions are closed before the row is deleted, the folder's terminals route stops
  answering, and a terminal in another folder keeps running

#### Scenario: Adding the path again restores the branches
- **WHEN** a removed folder's path is added again
- **THEN** the folder returns with its worktrees listed, because the add path probes before it stores

