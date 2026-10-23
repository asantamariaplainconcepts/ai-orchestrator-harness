## ADDED Requirements

### Requirement: Removing a branch checks that nothing is lost
`.harness/scripts/cleanup`, which `/aio:sync` runs by hand and the app runs to remove a worktree and
its branch, SHALL delete the local branch only when every commit on it is on some remote ref, or a
merged pull request has exactly the branch's head. Otherwise it SHALL keep the branch and say how many
commits exist only here. Deleting the branch SHALL also drop the claim recorded in
`refs/aio/claims/<issue>`. A worktree or branch already gone SHALL count as removed, and a worktree
holding uncommitted work SHALL NOT be removed without `--force`.

#### Scenario: A squash-merged branch is dropped
- **WHEN** the merge script deleted `change/<n>` at origin and the pull request merged with the
  branch's head
- **THEN** cleanup deletes the local branch and its claim record

#### Scenario: Work that was never pushed is kept
- **WHEN** the branch carries a commit on no remote and in no merged pull request
- **THEN** the branch stays, and the answer counts the commits that would have been lost
