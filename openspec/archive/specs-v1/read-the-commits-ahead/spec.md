# read-the-commits-ahead Specification

## Purpose
TBD - created by archiving change commits-ahead-of-trunk. Update Purpose after archive.
## Requirements
### Requirement: A worktree's commits ahead of the trunk are read from git per request and never stored

The product SHALL answer, for one working directory, the commits its branch has that the trunk does not — each as short hash, one-line subject, author name and commit date — read from git on every request that displays them and stored nowhere.

A commit appears on somebody's branch the moment they write it, so a stored list is stale from that moment — the same rule the worktree's position follows. The reading is asked for by the request that draws the screen, never by the loop that keeps the rail warm.

#### Scenario: A worktree two commits ahead lists two rows

- **WHEN** a worktree's branch carries two commits the trunk does not
- **THEN** the answer holds two entries, and each carries a short hash, a subject, an author and a date

#### Scenario: A worktree on the trunk lists none and says so

- **WHEN** the branch has no commits the trunk does not have
- **THEN** the answer is an empty list, and the tab draws one sentence rather than a blank pane

#### Scenario: The reading is not part of the poll

- **WHEN** the folder poller runs on its timer
- **THEN** it does not ask for the commits ahead, exactly as it does not ask for the position

### Requirement: No trunk is a sentence, not a zero

The product SHALL answer that there is no trunk to be ahead of, distinctly from answering that there is nothing ahead — a repository with no remote and no conventional trunk has not "caught up", and a screen that renders the two the same says the branch is current when nothing checked.

#### Scenario: A repository with no trunk says so

- **WHEN** the working directory's repository has no remote and no conventional trunk
- **THEN** the screen says there is no trunk to be ahead of, rather than drawing an empty list as up to date

#### Scenario: A directory that is not a repository does not draw the tab

- **WHEN** a folder is not a git checkout
- **THEN** the commits tab is not on its strip at all

### Requirement: The list is capped and says so

The implementation SHALL cap the number of commits it answers, and the screen SHALL say when the list is a prefix rather than the whole set. A branch can be hundreds of commits ahead, and every commit becomes a row in a browser.

#### Scenario: A branch far ahead shows the cap

- **WHEN** a branch carries more commits than the implementation's cap
- **THEN** the answer holds the first of them in git's order and is marked truncated, and the screen says the list stops short rather than ending quietly

### Requirement: The tab names what the commits are ahead of

The answer SHALL carry the trunk name it measured against, so the screen can say what the commits are ahead of rather than implying there is only one thing they could be.

#### Scenario: The trunk name reaches the screen

- **WHEN** the commits ahead of `origin/master` are answered
- **THEN** the answer names `origin/master` as the trunk it measured against

