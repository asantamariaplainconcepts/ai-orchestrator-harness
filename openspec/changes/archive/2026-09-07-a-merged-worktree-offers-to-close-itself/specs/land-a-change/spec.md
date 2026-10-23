## ADDED Requirements

### Requirement: A worktree whose pull request was merged offers to close itself, not to merge again

When the pull request on a worktree's branch has been observed as `MERGED`, the Change tab's
primary action SHALL read **"Close worktree"** rather than **"Merge change"**, and the dialog
SHALL say the merge landed and ask for confirmation to close the worktree. The action SHALL call
the same endpoint (`POST /api/folders/{id}/merge`) that the merge button calls, and the server
SHALL skip the merge because the pull request is already merged — the behaviour the `alreadyMerged`
path already provides. The state SHALL be read from the pull request query the Change tab already
has through `useWorktreePullRequest`, and SHALL NOT be a second request or a polled query.

#### Scenario: The button says close when the pull request is merged
- **WHEN** the worktree's pull request state is `MERGED`
- **THEN** the Change tab's primary action reads "Close worktree" and opens the dialog

#### Scenario: The button says merge when the pull request is open
- **WHEN** the worktree's pull request state is `OPEN`
- **THEN** the Change tab's primary action reads "Merge change" as it does today

#### Scenario: The dialog adapts its copy to the close
- **WHEN** the pull request is `MERGED` and the dialog is open
- **THEN** the title and body name the close and say the merge landed, and the confirm button
  reads "Close and remove"

#### Scenario: The endpoint is unchanged
- **WHEN** the "Close worktree" button is pressed
- **THEN** the request is `POST /api/folders/{id}/merge` and the server skips the merge and runs
  the clean-up queue, as the `alreadyMerged` path already does
