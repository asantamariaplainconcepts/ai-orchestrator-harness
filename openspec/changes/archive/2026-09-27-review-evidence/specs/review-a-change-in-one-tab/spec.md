## REMOVED Requirements

### Requirement: The change is five ordered steps, drawn as a rail and counted in a footer
**Reason**: Tests and Evidence are merged into one Evidence step, and steps are no longer marked.
**Migration**: restated below as "The change is four ordered steps, drawn as a rail, and nothing is
marked".

## ADDED Requirements

### Requirement: The change is four ordered steps, drawn as a rail, and nothing is marked

The Change tab SHALL draw an ordered rail of four steps — **Proposal**, **Code**, **Evidence**,
**The app** — one panel for the selected step, and a footer that moves between them.

**The steps are an order to read in, not a checklist.** Selecting a step, moving forward and moving
back SHALL leave the worktree exactly as it was, and there SHALL be no control that marks a step
read, no count of steps left and no progress: the reviewer is trusted to have read what they merge.
Merging SHALL NOT wait on any step.

#### Scenario: Four steps, in order
- **WHEN** the Change tab opens on a worktree
- **THEN** four numbered steps are drawn in order, and nothing on the tab counts them

#### Scenario: The footer moves and stores nothing
- **WHEN** the reader presses forward and then back through the rail
- **THEN** the panel follows the selection and nothing about the worktree has been recorded

#### Scenario: Merge opens on an unread change
- **WHEN** the reader presses Merge change without opening a step
- **THEN** the dialog offers the merge; only the server's own refusals hold it back

### Requirement: The Evidence step reads what the gate gathered beside what the checks said
The Evidence step SHALL draw, in this order: the review evidence `aspire do review-evidence` left in
the worktree's `evidence/` folder; what the declared checks last said and which tests the change
added; and what was attached to the issue. It SHALL judge nothing — a figure's tone says which side
of a conventional line it is on, never whether the change is good enough — and a section that was
not measured SHALL be a sentence with its reason, never a zero.

A worktree with no `evidence/` folder SHALL be told how to gather it, and evidence that describes an
earlier commit than the worktree's head SHALL say so.

#### Scenario: Nothing gathered yet
- **WHEN** the worktree has no `evidence/review-evidence.json`
- **THEN** the step names `aspire do review-evidence` and still shows the checks and attachments

#### Scenario: Evidence from an earlier commit
- **WHEN** the evidence names a commit other than the worktree's head
- **THEN** the step says the figures describe an earlier commit
