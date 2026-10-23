## MODIFIED Requirements

### Requirement: The change header names the issue and states its facts without judging them

The Change tab's header SHALL name the issue the worktree is about — its
number and its title — and beneath it SHALL state the change's facts as one
mono line. The facts and the wording of an unknown are the `merge-gate`
capability's; this requirement is that the header is **where** they are
said and that the header says nothing else about readiness.

The header SHALL carry the screen's actions in **one place**, and no fact
SHALL disable one.

The issue SHALL be resolved the way every other surface on this screen
resolves it — from the most recent run in that directory, and otherwise
from the number the branch proposes and the task read
confirms — so the header and the facts beneath it can never name different
issues for one worktree.

**The header SHALL paint with cached data before `useTask` and `useWorktreePullRequest` resolve.**
The rail's marks SHALL read as empty while the task is pending, and the
merge button SHALL read as not-merged while the pull request is pending.
When either resolves, the marks and the button SHALL update without
remounting the header.

**The step panel SHALL NOT be replaced by a full-panel spinner while its
query is pending.** Each step panel SHALL draw its own layout skeleton —
the heading bar, muted placeholders for rows — so the header, the rail and
the footer stay visible while the panel's data arrives.

**When the change's pull request is closed — merged, or closed without
merge — the steps the strip offers SHALL draw from the remote reader's
pinned comparison** (`review-a-remote-pull-request`) rather than from the
working directory: the Code, Proposal and Evidence steps read the
comparison's files, the change's OpenSpec artefacts as the review carries
them, and the remote evidence with its provenance — and none of them
completes a missing piece with a local read. **Merged and closed without
merge are named as the different states they are**, and an open pull
request keeps the reading it has today, local unpushed work included.

#### Scenario: The header over a resolved issue

- **WHEN** the worktree's task resolves
- **THEN** the header names that issue's number and title, with the facts as one line beneath it

#### Scenario: The header over a worktree whose issue does not resolve

- **WHEN** no run and no branch resolves a task for this worktree
- **THEN** the header says the issue is not known and still states every fact it can, rather than
  drawing nothing

#### Scenario: Nothing in the header is a verdict

- **WHEN** a check has failed and the tree is dirty
- **THEN** the header states both and offers every action it always offered

#### Scenario: The header paints before the task resolves

- **WHEN** the Change tab mounts and `useTask` is still pending
- **THEN** the header paints with the worktree's branch, the diff counts from the cache, and an empty
  set of step marks — and fills in the marks when `useTask` resolves, without remounting

#### Scenario: The step panel draws a skeleton while pending

- **WHEN** the selected step's query is still pending
- **THEN** the panel draws its layout skeleton rather than a full-panel spinner, and the header, rail
  and footer above it stay visible

#### Scenario: Switching tabs does not blank the screen

- **WHEN** the reader switches from another tab to Change, or between steps inside Change
- **THEN** the previous content stays visible while the new content mounts, and there SHALL be no
  blank gap between the two

#### Scenario: The closed result's steps read remotely

- **WHEN** the change's pull request has merged
- **THEN** the Code, Proposal and Evidence steps draw from the remote comparison, and the state of the worktree changes nothing they show

#### Scenario: Closed without merge is named as its own state

- **WHEN** the change's pull request is closed without merging
- **THEN** the steps say so, and neither a merged comparison nor a local diff is offered in its place
