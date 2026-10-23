## MODIFIED Requirements

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
