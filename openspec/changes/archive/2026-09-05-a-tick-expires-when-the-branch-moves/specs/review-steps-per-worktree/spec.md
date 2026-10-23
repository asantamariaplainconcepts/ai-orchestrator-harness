## ADDED Requirements

### Requirement: A marked review step is stored against the working directory and the commit it was marked on

Marking a review step SHALL record the step, the working directory it was marked in, the commit that
directory was on at that moment, and when. The record SHALL survive the application restarting.

The record SHALL be keyed by **working directory path**, not by a worktree row and not by a branch. A
worktree removed and recreated at the same path is the same place, and the rows a folder owns are
reconciled against git — a probe that answers wrongly once MUST NOT be able to delete what somebody
marked.

Two working directories of the same project SHALL therefore carry independent marks: marking a step in
one SHALL leave every step of the other exactly as it was.

#### Scenario: A mark outlives the session
- **WHEN** three of the five steps are marked and the application is restarted
- **THEN** those three are still marked in that working directory and the other two are not

#### Scenario: Two worktrees of one project
- **WHEN** a step is marked in one worktree of a project
- **THEN** no step of any other worktree of that project is marked

#### Scenario: A probe does not unmark anything
- **WHEN** the worktree rows of a folder are reconciled against git
- **THEN** every mark recorded for those directories is still there

### Requirement: A mark is read back as one of four answers, and only one of them counts as done

Reading a working directory's review steps SHALL compare each mark's commit against that directory's
current head and SHALL answer, per step, one of exactly four things:

- **done on this commit** — the directory has not moved since the mark;
- **done on an earlier commit** — the directory has moved, and by how far;
- **not comparable** — the marked commit is not in this repository at all, which is what a mark
  recorded before a rebase looks like;
- **never looked at** — nothing has been marked for that step.

**Not comparable SHALL be its own answer.** It SHALL NOT be reported as done, and it SHALL NOT be
reported as never looked at. Rounding it to either states a fact nobody established.

Only **done on this commit** SHALL count as done. A mark whose commit is no longer the head SHALL NOT
be deleted: it is kept and reported as put on something else, because a mark that quietly disappears
and a mark that quietly survives are the same untruth from opposite sides.

The comparison SHALL be made when the state is read and SHALL NOT be stored. A stored freshness is
wrong from the moment anybody commits.

#### Scenario: Marked here
- **WHEN** a step is marked and the directory has not moved
- **THEN** it reads as done on this commit and it counts as done

#### Scenario: The branch moved under a mark
- **WHEN** every step has been marked and one commit is then made in that working directory
- **THEN** all five read as done on an earlier commit, none of them counts as done, and the number
  outstanding is five again

#### Scenario: The marked commit no longer exists
- **WHEN** the branch is rebased so that a marked commit is no longer in the repository
- **THEN** that step reads as not comparable — neither a tick nor a blank — and does not count as done

#### Scenario: Nothing marked
- **WHEN** no step has been marked for a working directory
- **THEN** every step reads as never looked at

### Requirement: Marking is refused where the mark could never mean anything

The product SHALL refuse to record a mark for a step it does not have, and SHALL refuse to record a
mark in a working directory whose current commit cannot be read.

A step key outside the five the product draws SHALL be refused as an unknown step. A working directory
whose head git will not name SHALL be refused as not comparable, because a mark stored without a commit
could only ever read as *not comparable* — storing it would be storing something that can never count.

Both refusals SHALL name what was refused and SHALL leave every existing mark untouched.

A named working directory SHALL be honoured only when it is one of that folder's own, and an unknown
one SHALL be refused as not found rather than as forbidden, so that probing paths teaches nothing.

#### Scenario: A step the product does not have
- **WHEN** a mark is requested for a step key that is not one of the five
- **THEN** it is refused as an unknown step and nothing is recorded

#### Scenario: A directory whose head cannot be read
- **WHEN** a mark is requested in a working directory whose current commit git will not name
- **THEN** it is refused as not comparable and nothing is recorded

#### Scenario: A working directory that is not this folder's
- **WHEN** a mark is requested against a path the folder does not own
- **THEN** it is refused as not found, in the same words as an unknown folder

### Requirement: Unmarking removes the mark, and marking again replaces it

Unmarking a step SHALL remove its record, returning that step to **never looked at**. Marking a step
that is already marked SHALL replace the record rather than add a second: a step has a current mark,
not a history.

#### Scenario: Unmark
- **WHEN** a marked step is unmarked
- **THEN** it reads as never looked at

#### Scenario: Marked twice
- **WHEN** a step is marked, the directory moves on, and the step is marked again
- **THEN** there is one mark for that step and it names the newer commit

### Requirement: The marks say a person looked, and nothing else

The stored marks SHALL NOT be turned into a verdict. The product SHALL derive from them only how many
steps are outstanding.

Marking or unmarking a step SHALL NOT place or remove a hold: a hold is the vendor's and a person
removes it. The marks SHALL NOT be sent anywhere — they are state on this machine, like everything
else the workspace stores.

Recording a mark SHALL require its own permission, distinct from reading the folder. Reading the marks
SHALL require only what reading the folder requires: seeing what has been looked at is seeing the
folder.

#### Scenario: Five marks are not an approval
- **WHEN** all five steps are marked on the current commit
- **THEN** the product says none is outstanding and states nothing about whether the change may land

#### Scenario: A mark leaves the hold alone
- **WHEN** a step is marked in a worktree that is holding
- **THEN** the worktree is still holding

#### Scenario: Reading is not marking
- **WHEN** a caller may read the folder but has not been granted the right to record a review
- **THEN** it can read the marks and cannot record one
