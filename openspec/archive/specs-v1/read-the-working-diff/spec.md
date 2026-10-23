# read-the-working-diff Specification

## Purpose
What has changed in a working directory, read from git on request. The change belongs to the directory,
not to the run that happened to be typing in it — a snapshot stored on a run was empty for every real
run and would in any case be wrong while the agent is still working. Reverse-engineered from the shipped
code, not negotiated ahead of it.
## Requirements
### Requirement: The changed-file list is read from git, never stored on a run

The system SHALL expose `GET /api/folders/{id:guid}/diff` (`Features/Folders/UseCases/GetDiff.cs`,
`workspace:read`) returning the changed-file list the Diff tab draws down its left side, read from git at
request time. A run SHALL NOT carry a stored diff: nothing ever wrote the field it used to have, and a
snapshot is wrong while an agent is still typing. A run's Diff tab SHALL be this answer about the
worktree the run worked in.

**The working diff is the reading used before a pull request exists**, to see what a person is typing
before it is pushed. Once a pull request is published — open, merged, or closed without merge — the
reviewed code SHALL come from the remote reader's pinned comparison (`review-a-remote-pull-request`),
and the working diff SHALL NOT be offered as a parallel source of reviewed code. The two readings
answer different questions at different times: the local diff is the live worktree before it is a PR;
the remote comparison is the reviewed PR.

#### Scenario: The tab is the directory's answer, not the run's
- **WHEN** a run's Diff tab is opened while the agent is still working
- **THEN** the list is read from that worktree's git state at that moment

#### Scenario: The working diff is the before-PR reading
- **WHEN** a worktree has unpushed work and no pull request exists for its branch
- **THEN** the working diff is read from git, and a reviewer sees what is being typed before it is pushed

#### Scenario: A published PR is not reviewed from the working diff
- **WHEN** a pull request is published for the worktree's branch
- **THEN** the reviewed code comes from the pinned remote comparison, and the working diff is not offered as a second source of review alongside it

### Requirement: A patch is fetched per file
The system SHALL expose `GET /api/folders/{id:guid}/patch` (`GetPatch.cs`, `workspace:read`) returning
one file's patch, per file rather than for the whole change: fetching every patch to render a list of
filenames would read the entire working tree to show a column of numbers, and a run that touched fifty
files would be unusable.

#### Scenario: Picking a file fetches that file's patch
- **WHEN** a person selects a file in the Diff tab
- **THEN** that file's patch is fetched, and no other file's is

#### Scenario: The list costs no patches
- **WHEN** the changed-file list is drawn
- **THEN** no patch has been fetched yet

### Requirement: The Code step lists files in a sidebar beside the patch, not as a vertical column

The Code step of the Change tab SHALL list changed files in a sidebar on the left (fixed width,
`flex: none`) and render the selected file's patch in a panel on the right, the same structure the
Review tab uses. Selecting a file in the sidebar SHALL change the patch panel's contents without
remounting the sidebar.

Below a width the panel SHALL stack the sidebar as a strip across the top with the patch below it,
the same fallback the Review tab applies. Both layouts SHALL keep the claims breadcrumb on each
file row in the sidebar.

**This is a layout change, not a data change.** The Code step already reads `useFolderDiff`,
`useAnnotations` and `usePatch`; the sidebar reads the same data and renders it in a different
structure. No new query is introduced by this requirement.

#### Scenario: A 25-file change reads as a sidebar

- **WHEN** the Code step opens on a worktree with 25 changed files
- **THEN** the file list is rendered in a sidebar on the left, and the first file's patch is in a
  panel on the right, without any other file's patch pushing it down

#### Scenario: Selecting a file changes the panel without remounting the sidebar

- **WHEN** the reader clicks a file in the sidebar
- **THEN** the patch panel updates to that file's patch, and the sidebar's scroll position and
  selection highlight do not reset

#### Scenario: Narrow width stacks the sidebar as a strip

- **WHEN** the panel's width is below the breakpoint
- **THEN** the file list is rendered as a strip across the top with its own scroll, and the patch
  panel takes the full width below it

#### Scenario: Claims stay on every sidebar row

- **WHEN** a file was annotated with `req: 1, 2` in `tasks.md`
- **THEN** its row in the sidebar carries the claims breadcrumb, as it does today

