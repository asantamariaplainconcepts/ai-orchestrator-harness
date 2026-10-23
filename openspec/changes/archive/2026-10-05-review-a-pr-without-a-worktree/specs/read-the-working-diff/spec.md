## MODIFIED Requirements

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
