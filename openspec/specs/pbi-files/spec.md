# pbi-files Specification

## Purpose
The Files tab answers what is in the repository on this PBI's branch: the tree with react-arborist and
the chosen file in CodeMirror 6, read and edited, saved to disk and never committed. It is not an IDE;
*Open in editor* is for that. Reverse-engineered from the shipped code
(`src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Files/`,
`src/shared/AiOrchestrator.Infrastructure/Files/GitBranchFiles.cs`,
`src/frontend/features/pbi/tabs/FilesTab.tsx`), not negotiated ahead of it.

## Requirements
### Requirement: The tree is the PBI's branch, wherever it is checked out
`GET /api/files?folderId&pbi&asIs` SHALL list the files of `change/<pbi>`: from the checkout that holds
the branch (the folder itself or one of its worktrees) when there is one, else from `origin/change/<pbi>`
with no checkout, else answer that the branch does not exist. The answer SHALL carry the branch, whether
it exists, the checkout and whether it is a worktree, the files sorted and capped at 50,000, and the
modified ones from `git status`, untracked files included. With `asIs`, it SHALL list the folder's
checkout as it is instead.

#### Scenario: The run ended and its worktree is gone
- **WHEN** the tab opens on a PBI whose branch no checkout holds
- **THEN** it says so and offers *Show the checkout*, which lists the folder as it is, on its own branch

### Requirement: A file is read whole, or said to be too much
`GET /api/files/content?folderId&pbi&path&asIs` SHALL answer a file the branch lists, from disk when a
checkout holds the branch and from git otherwise, up to 2 MiB, saying whether it is binary (a NUL in its
first 8 KB, or not strict UTF-8) or truncated. A path the branch does not list SHALL be 404
`Folder.NoSuchFile`.

#### Scenario: A binary file
- **WHEN** a person chooses a PNG in the tree
- **THEN** the editor says it is binary and Save stays disabled

### Requirement: Save writes to disk and commits nothing
`POST /api/files/content {folderId, pbi, asIs, path, text}` SHALL write the text as UTF-8 to the file in
the checkout that holds the branch and answer the tree after the write, so the header's count of
modified files is current. It SHALL refuse with 409 `Files.Refused` when no checkout holds the branch,
the path is not a file of the branch, or it is a symlink or outside the checkout. The commit SHALL be a
person's, from a terminal, or an agent's in its run.

#### Scenario: Saving an edit
- **WHEN** a person edits `LoopDefinition.cs` on `change/495` and presses Save
- **THEN** the file changes on disk, the header reads `change/495 · 1 modified`, and the tree marks it `M`

### Requirement: Open in editor opens the same file in the person's editor
*Open in editor* SHALL ask `POST /api/folders/{id}/open-in-editor` for the chosen file in the checkout
that holds the PBI's branch, with the editor Settings names. No checkout on the branch SHALL be 409,
and an editor that does not start SHALL say "Choose another editor in Settings."

#### Scenario: Opening a file of the branch
- **WHEN** a person presses Open in editor with a file chosen
- **THEN** that file opens in the configured editor from the worktree of `change/<pbi>`
