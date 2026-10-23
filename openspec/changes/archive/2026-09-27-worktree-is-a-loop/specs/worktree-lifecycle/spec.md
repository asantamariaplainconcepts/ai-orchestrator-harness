# worktree-lifecycle delta

## MODIFIED Requirements

### Requirement: The stored worktrees are reconciled against the probe, and unreadable is not empty
Stored worktree rows SHALL be reconciled against a `git worktree list` probe, with git the record
and the rows following it, in that order. A probe that cannot be read SHALL NOT be treated as an
empty set, because empty would remove every row. The worktree's screen and sidebar row answer
"where does this journey stand" as a projection over Runs (see `worktree-is-a-loop`); the row
itself stores no position and no state, which is the standing decision this repository took when
the dead `State` column was removed.

#### Scenario: A worktree made outside the app appears
- **WHEN** `git worktree add` is run in a terminal and the sweep runs
- **THEN** the worktree appears in the folder's list

#### Scenario: A probe failure keeps the rows
- **WHEN** the reconcile probe cannot be read
- **THEN** the stored rows are kept rather than removed, and the failure is reported

#### Scenario: An unreadable folder keeps everything
- **WHEN** the probe fails or throws for a folder
- **THEN** every worktree row survives, the folder is not removed, and its probe time is not advanced

#### Scenario: The sidebar row says what the journey is doing
- **WHEN** a run is working in a worktree
- **THEN** the sidebar row for that branch says so, from the same run-by-working-directory join
  the rail already uses — storing nothing new
