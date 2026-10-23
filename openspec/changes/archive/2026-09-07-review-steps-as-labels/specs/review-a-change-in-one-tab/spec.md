## MODIFIED Requirements

### Requirement: The change is five ordered steps, drawn as a rail and counted in a footer

The Change tab SHALL draw an ordered rail of five steps — **Proposal**, **Code**, **Tests**,
**Evidence**, **The app** — one panel for the selected step, and a footer that moves between them and
says how many are done out of how many there are.

**Navigating SHALL record nothing, and marking SHALL be a separate act.** Selecting a step, moving
forward and moving back SHALL leave the worktree exactly as it was; a step becomes done only when
somebody marks it, through a control that says so. The screen SHALL NOT claim a step is done that
nobody marked, and a step nobody has marked SHALL read as not looked at.

**A step is done when its `reviewed:<step>` label is on the issue, and not done when it is not.**
The state of each step is the `review-steps-per-worktree` capability's; this requirement is that the
rail **says** it — a tick where the label is present, an ordinal where it is not. There is no
freshness dimension and no stale state, because a label has no commit.

**How many steps are done SHALL be read from one place.** The number beside the tab and the number in
the footer SHALL come from the same source — the task's labels filtered for `reviewed:*` — so the
strip and the footer can never disagree about how much is left.

#### Scenario: Five steps, in order, none of them claimed
- **WHEN** the Change tab opens on a worktree whose issue carries no `reviewed:*` labels
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
- **THEN** the rail still shows all five marks, because a label has no commit dimension and does not
  expire when the branch moves
