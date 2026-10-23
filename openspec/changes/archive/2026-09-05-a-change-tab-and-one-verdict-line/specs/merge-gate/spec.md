## MODIFIED Requirements

### Requirement: The same facts are stated in one line, and an unknown is stated as unknown

The change header SHALL state, as **one mono line of facts joined by `·`**, everything the five
condition tiles stated — **Base** (how far the branch is ahead of and behind the trunk), **Merge**
(whether it merges clean), **Checks** (how the folder's declared checks ended, and where), **Tree**
(whether the working copy is clean) and **Seam** (whether the changed paths stay inside what the task
declared) — and one fact they did not: **the pull request** the branch has, its number and its state.
No fact SHALL be dropped for width.

A fact with no source SHALL be **stated as absent, in its own words**, never invented, never rounded
to zero and never drawn as a tick. The line SHALL keep the distinctions the sources keep: null counts
are "not compared" and not `0 ahead, 0 behind`; a pull request nobody could ask about is a different
sentence from a branch that has none; and the Seam SHALL keep all three of its readings — a
declaration compared, no task or no declaration to compare, and a body the mirror only holds part of.

The Seam fact's task input SHALL be the resolved answer below, so a worktree that has never had a run
is compared against the issue its branch names rather than against nothing. Comparing against the
**wrong** issue SHALL remain worse than comparing against none: the fact keeps its unknown state for
every case the resolution does not settle.

**Nothing in the line SHALL be drawn as a success.** Colour SHALL mark only what is failing or worth
reading first, so there is no green in the row for a reader to mistake for a verdict, and the words
SHALL carry the fact on their own.

#### Scenario: A condition with no answer is unknown, not green
- **WHEN** the repository has no trunk to compare against, or this git cannot answer a merge dry run
- **THEN** the line names each as not compared and not asked, rather than showing a tick

#### Scenario: The seam tile waits for both its inputs
- **WHEN** the scope has just opened and either the task or the diff has not resolved
- **THEN** the seam fact reads unknown until both are in

#### Scenario: A worktree with no run still compares its seam
- **WHEN** a worktree has no runs and its branch names a task the mirror holds
- **THEN** the seam fact compares the diff against that task's declaration instead of reading unknown

#### Scenario: A cut body is not a missing declaration
- **WHEN** the mirror holds only the front of the issue and the seam section falls past the cut
- **THEN** the line says the body could not be read whole, which is a different sentence from
  "declares no seam"

#### Scenario: A worktree with no pull request, no trunk and a dirty tree
- **WHEN** the branch has no pull request, the repository has no trunk, and the working copy is dirty
- **THEN** the line states three unknowns in their own words and one dirty tree, and no fact in it is
  a tick

## ADDED Requirements

### Requirement: The screen states the facts and the person's own checklist is what blocks

The screen SHALL NOT word a verdict over the facts. There SHALL be no "Ready to merge", no "Cannot
merge yet" and no sentence in which the product judges, from what it can see, whether a change may
land — the facts SHALL be stated and the reading SHALL be the reader's.

What is outstanding SHALL be **the person's own review checklist**: the count of ordered steps they
have not yet marked, said as a number beside the tab and again in the header. A machine's opinion of
five conditions SHALL NOT stand in for it.

**Every action SHALL stay available whatever the facts say.** A person overriding a stale check with
a reason is normal work, and a disabled button with no path is how a tool gets worked around. No
action on this screen SHALL be disabled by a fact.

#### Scenario: A failing check does not disable anything
- **WHEN** a declared check has failed, or the tree is dirty, or nothing has run here at all
- **THEN** the facts say so, no action is disabled, and no sentence tells the reader whether to merge

#### Scenario: The outstanding count is the person's, not the machine's
- **WHEN** every fact in the line is ordinary and no step has been marked
- **THEN** the screen says how many review steps are left rather than that the change is ready

## REMOVED Requirements

### Requirement: The verdict is worded by what is worst, and never disables an action
**Reason**: Half of it was the product's opinion and half was a rule about actions, and only the
second half survives. The wording rule — a red condition reads "Cannot merge yet", an amber one "Not
ready to merge", all-unknown "Nothing has checked this", otherwise "Ready to merge" — described a
machine judging, from five conditions, whether a person may land a change, on a screen that then
asked that person to assemble the real reading from three other tabs. What blocks is now the
reviewer's own checklist, so there is no verdict left to word: `verdict()` and its four sentences are
deleted rather than softened.

**Migration**: The requirement above replaces it and carries the half that survives unchanged — no
action is disabled by a fact, for the same reason it never was. A reader who looked for "Ready to
merge" now looks at the fact line and at how many review steps are left; nothing that was drawn has
been withdrawn without a replacement in the same place.

## RENAMED Requirements

- FROM: `### Requirement: Five conditions are drawn, each from a real source`
- TO: `### Requirement: The same facts are stated in one line, and an unknown is stated as unknown`
