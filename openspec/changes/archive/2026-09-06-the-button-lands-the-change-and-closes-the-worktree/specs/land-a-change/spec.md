## ADDED Requirements

### Requirement: A branch whose OpenSpec change is not archived is not merged from here

The system SHALL refuse to merge a worktree whose working directory holds an **unarchived OpenSpec
change**, and the refusal SHALL **name the command that is missing**. Nothing SHALL be merged, no
environment stopped, no worktree removed and no branch deleted.

An unarchived change is a directory under `openspec/changes/` that is not `archive/`. That reading
SHALL be **the same one the change panel already uses**, not a second implementation of it: a
wholly-untracked change directory is collapsed by git into a single entry, and a guard that missed
that case would wave through exactly the change an agent had just written.

The reason is ordering and it is not negotiable. Archiving folds the change's deltas into
`openspec/specs/` **on the branch**, and the retro entry is written there too, so that the trunk
receives **one commit per change**. Merging first would split the deltas across two commits or lose
them. **The system SHALL NOT archive the change itself** — archiving and writing the retro are
judgement, and the refusal is the opposite of doing them.

More than one unarchived change SHALL refuse just the same, and the refusal SHALL NOT choose between
them.

#### Scenario: An unarchived change refuses the merge and names the command
- **WHEN** the worktree holds a directory under `openspec/changes/` that is not `archive/`
- **THEN** the merge is refused, the sentence names the command that archives it, and nothing is
  merged, stopped, removed or deleted

#### Scenario: An archived change is not mistaken for an unarchived one
- **WHEN** every change in the worktree is under `openspec/changes/archive/`
- **THEN** the guard passes and the queue proceeds

#### Scenario: A change git has not been told about still counts
- **WHEN** the change directory is wholly untracked, so git lists it as one collapsed entry
- **THEN** it is still read as an unarchived change and the merge is still refused

### Requirement: A dirty working copy stops the queue, and "what the branch changed" is not what is read

The system SHALL refuse to merge a worktree whose working copy holds **uncommitted** work, and the
refusal SHALL say **how many files git reported**. Nothing SHALL be merged.

Work that is not committed is work that never lands, so the refusal is the feature: a squash-merge of
the branch would silently leave it behind on a directory that is about to be deleted.

**The reading SHALL be the count of uncommitted files, and SHALL NOT be the branch's diff.** These
are different questions and the second one is nearly always non-empty: a branch's diff is measured
from the merge base so that work an agent has already committed still shows, which is what makes it
the right answer for a Diff tab and the wrong one here. A guard reading it refuses **every branch
worth merging** — measured, by pressing the button against a real repository whose `git status` was
clean.

It SHALL be the same number the change header already states as the working copy's condition, rather
than a second reading in a second vocabulary.

A working directory that git **cannot describe** SHALL be refused rather than read as clean. The
guard exists so that unlanded work is not deleted, and a directory nothing can describe has the least
claim of any to be assumed empty.

#### Scenario: A dirty tree merges nothing
- **WHEN** the worktree's working copy holds uncommitted files
- **THEN** the merge is refused, the sentence says how many git reported, and nothing is merged

#### Scenario: Committed work on a clean tree merges
- **WHEN** the branch has changed many files against the trunk and its working copy is clean
- **THEN** the guard passes and the queue proceeds

#### Scenario: A directory git cannot describe is not a clean one
- **WHEN** git cannot describe the working directory
- **THEN** the merge is refused, and the count is not read as zero

### Requirement: The merge is the pull request's, squashed, and this system writes nothing about the issue

The system SHALL land a change by **squash-merging its pull request**, and SHALL NOT fast-forward and
SHALL NOT create a merge commit. A branch with no pull request, one whose pull request cannot be
asked about, and one whose pull request is closed unmerged SHALL each be refused with the sentence
the vendor or the seam gave.

**The system SHALL make no call about an issue** — not to close it, not to label it, not to comment
on it. The issue closes because the pull request carries `Closes #<n>`, and the vendor acts on that
when the pull request merges. Who closed the issue is therefore checkable, and it is not this system.

The seam that merges SHALL be **separate from the seam that reads** a pull request, because the
reading seam is documented as read-only by construction and this is the most destructive operation on
the screen.

#### Scenario: A branch with no pull request is refused
- **WHEN** the branch has no pull request
- **THEN** the merge is refused and nothing is merged

#### Scenario: The vendor's refusal is passed through
- **WHEN** the vendor refuses the squash-merge
- **THEN** its own sentence is returned and nothing further in the queue runs

#### Scenario: Nothing is written about the issue
- **WHEN** a change lands
- **THEN** the issue is closed by the pull request's own `Closes`, and this system made no call about
  the issue at all

### Requirement: The environment is stopped before the worktree is removed, using the line the folder declared

Before removing a worktree, the system SHALL stop every **live session this product started** in that
directory, and stopping SHALL run **the stop line the folder declared** rather than a signal this
system chose. Nothing new SHALL leave the process to do it: the stop line is already carried on the
session that started the command.

A session in another directory SHALL NOT be touched, and neither SHALL a process somebody started
themselves. Sessions SHALL be stopped **before** the directory is removed, because after the removal
there is no route left that could reach them.

#### Scenario: The declared stop line runs before the directory goes
- **WHEN** a worktree with a live long-running session is merged
- **THEN** that session's declared stop line is run and the session is closed, before the directory is
  removed

#### Scenario: A session in another directory is left alone
- **WHEN** the folder has live sessions in the checkout and in another worktree
- **THEN** only the sessions in the worktree being merged are stopped

### Requirement: The worktree is removed without force, and its local branch is deleted after the merge is real

The worktree SHALL be removed on the path git reports, **without `--force`** and **without a recursive
delete**. Git's refusal SHALL be returned rather than worked around: a worktree still holding work
cannot be removed, and that refusal is the feature.

Only a worktree this product holds a row for SHALL be operated on. The **primary checkout SHALL never
be removed**, and neither SHALL a directory the product does not own.

The **local branch** SHALL be deleted afterwards, from the repository the worktree belongs to, and it
SHALL be deleted with the form that works on a branch git cannot see as merged — a squash-merge
leaves no merge base, so the ordinary form refuses every time. What makes that safe is not the flag:
the branch SHALL only be deleted **after the pull request has been observed merged**, so the commits
are on the trunk in the vendor before the local ref is dropped.

A branch that could not be deleted SHALL NOT fail the operation. The merge is done, the directory is
gone and the row is gone, so there is nothing left to resume — the sentence git gave SHALL be carried
on the successful answer instead of being swallowed or reported as a failure.

#### Scenario: Git refuses to remove the worktree
- **WHEN** git refuses to remove the directory
- **THEN** the refusal is returned, it says the merge is already done, and it is not retried with
  force

#### Scenario: The branch is deleted only after the merge is observed
- **WHEN** a change lands
- **THEN** the local branch is deleted after the pull request has been observed merged, and never
  before

#### Scenario: A branch that would not delete is reported rather than failed
- **WHEN** the merge succeeded, the worktree was removed, and git would not delete the local branch
- **THEN** the answer is a success carrying git's sentence about the branch

### Requirement: A mid-queue failure says which step it stopped at, and a retry does not merge twice

When the queue fails after the merge, the answer SHALL say that **the merge is done** and **which step
it stopped at**. Nothing SHALL retry by itself.

A retry SHALL resume rather than start over, and it SHALL NOT merge a second time. How far the queue
got SHALL be **read from the world rather than stored**: the pull request's own state says whether it
has been merged, so a second attempt on an already-merged pull request SHALL skip the merge and carry
on with the steps that are left. The answer SHALL say out loud that the merge was already done, so
"resumed" is observable and not inferred.

No column, no stored counter and no migration SHALL be introduced to remember the queue's position —
a stored position would be wrong the moment somebody merged by hand.

#### Scenario: Removal fails after the merge and the retry resumes
- **WHEN** the merge succeeds and the worktree cannot be removed
- **THEN** the refusal says the merge is done and names the step
- **AND** a second attempt reads the pull request as already merged, does not merge again, and
  continues from the removal

#### Scenario: An already-merged pull request is not merged again
- **WHEN** the queue runs against a worktree whose pull request is already merged
- **THEN** no merge is attempted and the answer says the merge was already done

### Requirement: Landing a change is authorised as an operation on the repository

The endpoint SHALL require the permission that already governs creating and removing worktrees, and
SHALL be scoped to the folder as its tenant. It SHALL NOT mint a permission of its own: what it does
is what that permission's own description covers — the operations that change the repository.

#### Scenario: The repository permission governs it
- **WHEN** a caller without the worktree-management permission asks to land a change
- **THEN** the request is refused before anything is read
