## ADDED Requirements

### Requirement: A worktree's task is resolved from its runs first and its branch second

The system SHALL resolve which task a worktree is about from **the most recent run in that
directory** when there is one, and otherwise from a number proposed by **the branch name** and
confirmed by the mirror. Both surfaces that need the answer — the Seam tile and the Issue tab —
SHALL take it from the same place, so the two can never name different tasks for one worktree.

A run SHALL always win. Measured across this repository's own history, 3 of 26 worktrees with runs
carry a task key their branch does not name — a worktree gets reused — and in those cases the branch
would name the wrong issue over data that is already right.

The branch SHALL only **propose**: the candidate is used solely because the mirror holds that task
for this folder's repository, resolved by repository and key together. A branch that names no number,
or names one the mirror does not hold, SHALL resolve nothing. **No branch-naming convention SHALL be
encoded**, because a product used by more than one loop may not impose one loop's naming.

The answer SHALL carry **where it came from**, and a key derived from a branch SHALL be shown as
derived rather than as something observed in that directory.

#### Scenario: A worktree the loop created resolves without a run
- **WHEN** a worktree on branch `change/275` has never had a run and the mirror holds `#275` for this repository
- **THEN** its task is `#275`, and the answer says it was derived from the branch

#### Scenario: A run outranks the branch
- **WHEN** a worktree on branch `change/174` has a run whose task key is `#104`
- **THEN** its task is `#104`, and the answer says it came from a run

#### Scenario: A branch that names no task resolves nothing
- **WHEN** the branch carries no number, or a number the mirror does not hold for this repository
- **THEN** no task is resolved, and the surfaces draw their existing unknown states

## MODIFIED Requirements

### Requirement: Five conditions are drawn, each from a real source
`MergeGate.tsx` SHALL assemble its verdict from five conditions — **Base** (how far the branch is ahead
of and behind the trunk), **Merge** (whether it merges clean), **Checks** (how the folder's declared
checks ended, and where), **Tree** (whether the working copy is clean) and **Seam** (whether the changed
paths stay inside what the task declared). A condition with no source SHALL read as unknown: absent, never
invented and never green.

The Seam tile's task input SHALL be the resolved answer above, so a worktree that has never had a run
is compared against the issue its branch names rather than against nothing. Comparing against the
**wrong** issue SHALL remain worse than comparing against none: the tile keeps its unknown state for
every case the resolution does not settle.

#### Scenario: A condition with no answer is unknown, not green
- **WHEN** the repository has no trunk to compare against, or this git cannot answer a merge dry run
- **THEN** the tile reads unknown and names why, rather than showing a tick

#### Scenario: The seam tile waits for both its inputs
- **WHEN** the scope has just opened and either the task or the diff has not resolved
- **THEN** the Seam tile reads unknown until both are in

#### Scenario: A worktree with no run still compares its seam
- **WHEN** a worktree has no runs and its branch names a task the mirror holds
- **THEN** the Seam tile compares the diff against that task's declaration instead of reading unknown
