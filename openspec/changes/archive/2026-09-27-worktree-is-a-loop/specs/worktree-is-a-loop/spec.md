# worktree-is-a-loop Specification

## ADDED Requirements

### Requirement: A worktree's screen answers where the journey stands
A worktree scoped to a repository SHALL draw, above its tabs, a journey header that answers three
questions in one place: where this worktree's loop stands (the folder's declared machine with the
position marked), what the agent is doing now (the live transition's activity, or the hold it waits
on), and what it has already done (the transitions walked, each stamped with its run's facts — the
command its plan resolved to, its duration, and its cost).

The machine SHALL be read from the declared loop the folder's checkout carries, drawn as one
horizontal strip of its nodes. Clicking the live transition SHALL open that run's terminal in the
pane beside the strip, without navigating away from the worktree.

The position SHALL be projected from the runs in that working directory, never stored: the latest
run the loop dispatched (`IsTransition`) names the node, its dispatch naming the transition. A
worktree whose position cannot be read — no transitioned run, hand-started runs only — SHALL say
"no transitioned run" rather than guessing from labels.

#### Scenario: Entering a worktree mid-implement
- **WHEN** a worktree's latest transitioned run is executing the implement node
- **THEN** the header shows the machine with implement marked live, the agent activity beside it,
  and clicking it joins that run's terminal

#### Scenario: A worktree waiting on a hold
- **WHEN** the last transitioned run ended at an approval and no run is live
- **THEN** the header marks the approval node as what the journey waits on, naming the hold

#### Scenario: A worktree with only hand-started runs
- **WHEN** a worktree holds runs but none was dispatched by the loop
- **THEN** the header draws the machine with no position mark and says no transitioned run, and
  the runs list remains reachable as runs, drawn dimmed as off-loop rather than as the journey

#### Scenario: The machine is the checkout's declaration
- **WHEN** a worktree's branch carries a different loop than its checkout
- **THEN** the machine drawn is the checkout's, for the reason the executor reads it there: the
  workbranch carries the loop as a fact of the trunk it branched from

### Requirement: What a step shows names the transition that produced it
Each step panel of the change SHALL carry a provenance line naming the run that produced what it
shows — the transition's command, its duration, and its cost — resolved the way the issue is
resolved today: the most recent run in that directory. A step nothing produced SHALL say so rather
than drawing nothing.

#### Scenario: The proposal step names its author
- **WHEN** the proposal step shows a change proposal written by the propose transition
- **THEN** the step names that run's command, duration and cost in one line

#### Scenario: A step with no producing run
- **WHEN** a worktree holds no run that produced what a step would show
- **THEN** the step says nothing produced it rather than drawing an empty provenance
