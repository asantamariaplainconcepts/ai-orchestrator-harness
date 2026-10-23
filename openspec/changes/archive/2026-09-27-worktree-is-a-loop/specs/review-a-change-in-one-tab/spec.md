# review-a-change-in-one-tab delta

## MODIFIED Requirements

### Requirement: A worktree's strip is five fixed tabs and the first of them is the change

A worktree SHALL draw its fixed tabs — **Change** and **Files** — under a journey header that
answers where the loop stands (see `worktree-is-a-loop`). There SHALL be no tab called Issue, no
tab called Review and no tab called Checks in a worktree's strip: what those three answered is a
step of the change, and reading three tabs to assemble one judgement is the thing this replaces.

**A checkout's strip SHALL keep Review and Checks.** They are withdrawn from the worktree only. A
checkout somebody edits by hand has a real diff, and a check's result belongs to the directory it
ran in — so the tabs are rescoped and not removed, and no strip other than the worktree's changes.

The tab a worktree rests on SHALL be Change, and it SHALL be a tab that strip actually offers: a
resting place the strip does not contain sends the screen straight back to it.

#### Scenario: The worktree strip under the journey header
- **WHEN** the folder screen is scoped to a worktree of a repository
- **THEN** the fixed tabs are Change and Files under the journey header, and none of them is Issue,
  Review or Checks

#### Scenario: The worktree strip
- **WHEN** the folder screen is scoped to a worktree of a repository
- **THEN** the fixed tabs are Change and Files under the journey header, and none of them is Issue,
  Review or Checks

#### Scenario: The checkout keeps what the worktree gave up
- **WHEN** the same folder is read on its checkout
- **THEN** Review and Checks are still on the strip, with the diff and the checks they always drew

#### Scenario: A link to a tab this scope no longer has
- **WHEN** a link names a tab the scope does not offer, including one this change removed
- **THEN** the screen resolves to Change rather than to a blank panel
