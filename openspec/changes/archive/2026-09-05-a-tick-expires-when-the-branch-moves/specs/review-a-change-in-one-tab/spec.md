## MODIFIED Requirements

### Requirement: The change is five ordered steps, drawn as a rail and counted in a footer

The Change tab SHALL draw an ordered rail of five steps — **Proposal**, **Code**, **Tests**,
**Evidence**, **The app** — one panel for the selected step, and a footer that moves between them and
says how many are done out of how many there are.

**Navigating SHALL record nothing, and marking SHALL be a separate act.** Selecting a step, moving
forward and moving back SHALL leave the worktree exactly as it was; a step becomes done only when
somebody marks it, through a control that says so. The screen SHALL NOT claim a step is done that
nobody marked, and a step nobody has marked SHALL read as not looked at.

**A step marked on a commit the working directory has since left SHALL NOT be drawn as done.** The rail
SHALL say the mark is there and that it was put on something else, in words distinct from both a
current mark and a blank — including the case where the marked commit is no longer in the repository
at all. The state of each step, and whether it counts, are the `review-steps-per-worktree` capability's;
this requirement is that the rail **says** it rather than rounding it to a tick.

**How many steps are done SHALL be read from one place.** The number beside the tab and the number in
the footer SHALL come from the same source, so the strip and the footer can never disagree about how
much is left, and both SHALL count only the steps marked on the commit the working directory is on now.

#### Scenario: Five steps, in order, none of them claimed
- **WHEN** the Change tab opens on a worktree where nothing has been marked
- **THEN** five numbered steps are drawn in order, none is marked done, and the footer says none of
  five is done

#### Scenario: The footer moves and stores nothing
- **WHEN** the reader presses forward and then back through the rail
- **THEN** the panel follows the selection and nothing about the worktree has been recorded

#### Scenario: One count, two places
- **WHEN** the steps outstanding change
- **THEN** the tab's counter and the footer's progress both change, because they read one source

#### Scenario: A mark the branch has moved past
- **WHEN** every step has been marked and a commit is then made in that working directory
- **THEN** the rail still shows all five marks, says of each that it was put on an earlier commit, and
  the tab's counter and the footer both read five outstanding again
