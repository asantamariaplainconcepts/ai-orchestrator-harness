## MODIFIED Requirements

### Requirement: The detail opens the journey as its centre, with or without a PR or a worktree

The PBI detail SHALL open the journey — the loop's spine of stages and the runs grouped under them —
as its resting tab, with the issue body and thread on the Issue tab beside it rather than above it,
and the launch form SHALL NOT dominate the heading of the screen. Launching an agent SHALL be an
action offered by the step the loop is currently on or the next permitted one, not a form that owns
the top of the screen; the full launch form MAY remain reachable as a closed disclosure at the foot
of the Journey tab, and SHALL NOT be rendered for a held PBI.

The journey SHALL be drawn the same way before and after a PR exists: the spine and its runs are
the PBI's own facts, and the PR is a reference in the heading that opens the published object, not
a gate that restructures the detail.

#### Scenario: The detail opens on the journey

- **WHEN** a PBI is opened in the detail
- **THEN** the Journey tab is selected, its spine is the centre of the screen, and the launch form does not dominate the heading

#### Scenario: The journey is the same before and after a PR

- **WHEN** a PBI with no PR and one with an open PR are opened
- **THEN** both draw the journey's spine as the centre, and the PR is a reference rather than a restructure of the detail

#### Scenario: Launch is an action of the step

- **WHEN** the loop is on a stage that permits launching an agent
- **THEN** the launch action is offered in the heading as the step's action, and the full form is folded at the foot of the Journey tab

#### Scenario: A held PBI offers no launch form

- **WHEN** a held PBI is opened
- **THEN** the heading offers releasing the hold, and no launch form is rendered anywhere on the screen

## ADDED Requirements

### Requirement: A PBI has a screen of its own, with five tabs

The system SHALL give a PBI a screen of its own on the route `/pbis/:key`, with the folder the PBI
belongs to in the `folder` query, and SHALL read the PBI by key and repository (`GET
/api/pbis/{key}`) so a PBI the vendor has closed opens too. Its heading SHALL carry the key and
title, the position the Connector translated, the hold when there is one, and the PR when one is
joined. Its tabs SHALL be **Journey**, **Issue**, **Proposal**, **Code review** and **Evidence**, in
that order, with Journey selected on arrival. While the PBI's position is `code-review` the Code
review tab SHALL carry a mark saying so, and SHALL NOT be selected in Journey's place.

The Issue tab SHALL draw the issue's body, its assignees, its labels and its thread as the vendor
answers them. Before a PR is joined, the Proposal, Code review and Evidence tabs SHALL say where the
PBI stands in its journey and SHALL NOT offer a local working diff in its place.

A key that names no PBI of that repository SHALL be drawn as absence, a failed read as an error in
the server's words, and a folder that resolves no repository as unattributed — never as another
PBI that shares the key.

#### Scenario: A PBI opens on its own route

- **WHEN** `/pbis/%23412?folder=/repo` is requested
- **THEN** the PBI `#412` of that folder's repository is drawn with its heading and five tabs, Journey selected

#### Scenario: A closed PBI opens

- **WHEN** the PBI was closed at the vendor and is no longer in the inbox
- **THEN** the screen reads it by key and repository and draws it, with its journey

#### Scenario: Code review is marked at code review

- **WHEN** a PBI at `code-review` is opened
- **THEN** the Code review tab carries the code-review mark, and Journey is the selected tab

#### Scenario: No PR yet

- **WHEN** a PBI with no joined PR is opened on the Code review tab
- **THEN** the tab says where the PBI stands and offers no local diff

### Requirement: The heading offers one action, the current step's

The PBI screen's heading SHALL offer at most one action, chosen in this order: **release the hold**
when the PBI is held; **stop the run** when a run of this PBI, joined by key and repository, has not
finished; otherwise **the loop's next command** for the PBI's position, when the position is one a
command starts. A position no command starts SHALL offer no action rather than a disabled one.

#### Scenario: Held

- **WHEN** a held PBI with no live run is opened
- **THEN** the heading offers releasing the hold, and nothing else

#### Scenario: A run is live

- **WHEN** a PBI that is not held has a run that has not finished
- **THEN** the heading offers stopping that run

#### Scenario: Ready for the next command

- **WHEN** a PBI at `ready-for-implementation`, not held and with no live run, is opened
- **THEN** the heading offers the loop's implement command for it

### Requirement: The PR of an issue-shaped PBI is joined from the inbox

For a PBI that is an issue, the screen SHALL find its PR among the pull-request PBIs of the same
repository already in the inbox, as the one whose body closes the issue by GitHub's closing keywords
(`close`, `closes`, `closed`, `fix`, `fixes`, `fixed`, `resolve`, `resolves`, `resolved`, followed by
`#<number>`), case-insensitively and as a whole word. Where several match, the lowest PR number SHALL
be used and the heading SHALL say how many matched. Where none matches, the screen SHALL say no PR was
found rather than report an error. A PBI that is itself a pull request SHALL be its own PR. No
endpoint SHALL be added for the join.

#### Scenario: An issue with a closing PR

- **WHEN** issue `#412` is opened and the inbox holds PR `#422` of the same repository whose body says `Closes #412`
- **THEN** the heading carries PR `#422` and the review tabs read it

#### Scenario: A PR of another repository is not joined

- **WHEN** a PR of another repository says `Closes #412`
- **THEN** it is not joined to this repository's `#412`

#### Scenario: No closing PR

- **WHEN** no PR in the inbox closes the issue
- **THEN** the heading says no PR was found, and nothing is drawn as an error
