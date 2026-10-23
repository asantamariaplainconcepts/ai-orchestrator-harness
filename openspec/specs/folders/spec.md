# folders Specification

## Purpose
A folder is a local checkout where steps may run. It is an internal piece: a person adds it from the
rail and sets its defaults on its page (folder-page), and the engine walks the ones whose remote names a repository it
can read. Reverse-engineered from the shipped code
(`src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/`, `Persistence/WorkspaceSeed.cs`),
not negotiated ahead of it.

## Requirements
### Requirement: A folder is added by path, and git says what it is
`POST /api/folders {path}` SHALL expand `~`, trim a trailing slash and resolve symlinks, refuse a path
already added with 409 `Folder.AlreadyAdded`, and ask git for the checkout's branch and `origin`. A path
that is not a directory SHALL be 400 `Folder.NoSuchDirectory`; one with a `.git` that git cannot describe
400 `Folder.NotDescribable`; any other directory SHALL be added as a plain directory, with no branch and
no remote. The folder's default agent SHALL be `claude-code` unless one is given.

#### Scenario: Adding a checkout
- **WHEN** a person adds `~/src/repo`, a clone of `github.com/owner/repo` on `main`
- **THEN** the folder is stored with branch `main` and its remote, and its PBIs are read on the next list

### Requirement: The add folder dialog browses this machine's directories
`GET /api/directories?path=<path>` SHALL list the subdirectories of a directory on this machine, the
home directory when no path is given: the path with `~` expanded, its parent (null at the root), and
each subdirectory by name with its absolute path and whether it has a `.git`, a directory or a file.
It SHALL list directories only, leave out those whose name starts with a dot, and read no file. A path
that is not a directory SHALL be 404 `Directory.NotFound`; a relative one 400 `Directory.NotAbsolute`;
one the system will not let the server read 403 `Directory.Denied`, saying on macOS where the
permission is given, so a denied directory is never an empty list. The add folder dialog SHALL show it
below the path field as the current path and its subdirectories: a click opens a directory, a click on
a repository puts its path in the field, and a typed path that exists moves the browser there.

#### Scenario: Browsing to a checkout
- **WHEN** a person opens *Add folder*, clicks `projects` and then `clients` under their home, and clicks the repository `portal`
- **THEN** the field holds `~/projects/clients/portal` as an absolute path, and *Add folder* adds it

#### Scenario: A directory macOS has not allowed
- **WHEN** the person opens `~/Documents` before allowing the app `aio` was started from to read it
- **THEN** the browser shows that it cannot be read and where in System Settings to allow it, not an empty list

### Requirement: The first boot seeds this checkout
The migration step SHALL add, as a folder, the checkout the running binary sits in, found by walking up
to the first `.git`, with its branch and remote from git, only when the database had no Workspace
migration applied before it ran. Run from outside a git checkout, it SHALL seed nothing. A folder
removed after that SHALL NOT come back on a later start.

#### Scenario: A fresh clone
- **WHEN** the app starts for the first time from a clone of this repository
- **THEN** the clone is the one folder, with its remote, and its open issues are the PBIs

#### Scenario: Removing the seeded folder
- **WHEN** a person removes the seeded checkout in Settings and the app starts again
- **THEN** there is no folder, and the checkout is added only by adding it again

### Requirement: The engine walks the folders on GitHub
`IFolderDirectory` SHALL list, ordered by path, the folders that are repositories with an `origin` on
github.com, one per repository (the first by path), each with its path, the vendor `github`,
`owner/name`, and its default agent and model.

#### Scenario: Two clones of one repository
- **WHEN** two folders are clones of `owner/repo`
- **THEN** the engine walks only the first by path, so no PBI is worked twice

### Requirement: A folder carries its default agent and model, and can be removed
`PUT /api/folders/{id}/defaults {agent, model?}` SHALL set both together, the model empty meaning the
agent's own default; they SHALL preselect the *Explore with agent* dialog, and the loop SHALL run them
in a state of its YAML that names no agent. `DELETE /api/folders/{id}` SHALL forget the folder and nothing
else: nothing on disk is touched, and its runs are kept, because they name it by path.

#### Scenario: Removing a folder
- **WHEN** a person removes a folder from its page
- **THEN** it leaves the rail and the engine's walk, and its checkout and its runs' records stay where they are
