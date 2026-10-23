# the-world-seam

## ADDED Requirements

### Requirement: The commits ahead of the trunk are a separate read, asked where it is displayed

`IWorkspaceProbe` SHALL expose the commits a working directory's branch has that the trunk does not, as a method separate from `Read` and `Position`, answering null rather than a partial list — not a repository, no trunk, or a git that could not be asked are all "cannot say", never an empty list read as "nothing ahead".

A separate call for the same reason `Position` is one: `Read` is what the poller calls on a timer for every folder, and this is a git process per working copy that belongs to the request that displays it, not to the loop that keeps the rail warm.

#### Scenario: The poller's read is untouched

- **WHEN** the poller reads a folder's state on its timer
- **THEN** it calls `Read` and no commit list is read for it

#### Scenario: Unsayable is null, not empty

- **WHEN** the directory is not a repository, or its repository has no trunk, or git refuses
- **THEN** the probe answers null for the commit list, and the caller leaves the screen's sentence alone rather than drawing an empty list as "nothing ahead"
