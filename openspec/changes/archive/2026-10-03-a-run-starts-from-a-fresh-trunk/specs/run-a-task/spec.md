## ADDED Requirements

### Requirement: A run's worktree is prepared from the fetched trunk, never from the checkout's HEAD
Where the Folder's repository has an `origin`, ensuring a run's worktree SHALL prepare it the same way
every time, before the run starts:

- A blank or `HEAD` base SHALL mean the remote's default branch, asked of the remote rather than read
  from the clone-time `origin/HEAD`; a named base SHALL be used as named. Either SHALL be fetched
  first, and a fetch that fails SHALL refuse the start.
- A new worktree SHALL be made from `origin/<branch>` when the branch was already pushed, from the
  local branch when one exists, and otherwise from the fetched base.
- The worktree SHALL then be brought forward to what was pushed of its branch and merged up to the
  base. It SHALL NOT be rebased, because the branch is the claim and is already pushed.
- Uncommitted files, a local branch that diverged from what was pushed, and a merge conflict SHALL
  each refuse the start with a sentence naming what to settle, and a conflicted merge SHALL be
  aborted so the worktree is left as it was.

What the main checkout has checked out SHALL NOT decide where a run begins. A repository with no
`origin` SHALL branch from the base it was given, as before. The Create worktree dialog is not a run
and is not changed by this.

#### Scenario: A stale checkout does not leak into a run
- **WHEN** the checkout stands on an unpushed commit on another branch and the remote's trunk has moved
- **THEN** a new run's worktree starts at the remote trunk's new tip, without the unpushed commit

#### Scenario: The second run of a task catches up with the trunk
- **WHEN** a worktree holds committed work and the trunk moved since it was made
- **THEN** the next run's start merges the trunk in and keeps the work

#### Scenario: A conflict with the trunk stops the run
- **WHEN** the worktree's branch and the moved trunk changed the same lines
- **THEN** the start is refused naming the conflicted files, and the worktree's HEAD and tree are
  unchanged

#### Scenario: Uncommitted files stop the run
- **WHEN** the worktree has uncommitted files
- **THEN** the start is refused saying how many

#### Scenario: Work pushed from another machine is resumed
- **WHEN** `change/<n>` exists at the remote and not locally
- **THEN** the worktree starts at the pushed tip rather than at the trunk

#### Scenario: No remote to fetch from
- **WHEN** `origin` cannot be fetched
- **THEN** the start is refused and no worktree is made
