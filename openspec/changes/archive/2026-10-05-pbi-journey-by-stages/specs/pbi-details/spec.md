## ADDED Requirements

### Requirement: The detail opens the journey as its centre, with or without a PR or a worktree

The PBI detail SHALL open the journey — the loop's spine of stages and the runs grouped under them —
as the centre of the screen, with the issue body and thread drawn beside or beneath it rather than
above it, and the launch form SHALL NOT dominate the heading of the screen. Launching an agent SHALL
be an action offered by the step the loop is currently on or the next permitted one, not a form
that owns the top of the pane.

The journey SHALL be drawn the same way before and after a PR exists: the spine and its runs are
the PBI's own facts, and the PR is a reference on the journey that opens the published object, not a
gate that restructures the detail. The Review, Proposal and Evidence tabs of the detail are owned by
other changes and SHALL NOT be introduced here.

#### Scenario: The detail opens on the journey

- **WHEN** a PBI is opened in the detail
- **THEN** the journey's spine is the centre of the screen, the issue body and thread are beneath or beside it, and the launch form does not dominate the heading

#### Scenario: The journey is the same before and after a PR

- **WHEN** a PBI with no PR and one with an open PR are opened
- **THEN** both draw the journey's spine as the centre, and the PR is a reference rather than a restructure of the detail

#### Scenario: Launch is an action of the step

- **WHEN** the loop is on a stage that permits launching an agent
- **THEN** the launch action is offered by that stage on the spine, not by a form at the top of the pane

### Requirement: The journey survives the worktree, reading what was persisted

The journey SHALL remain consultable when the PBI's worktree no longer exists: the spine and its
runs SHALL be drawn from the runs persisted by identity and the loop declaration read from the
checkout, and the parts that died with the worktree — the working path and the loop command button —
SHALL NOT be drawn. What was not persisted — the live frame of a run whose process is gone — SHALL
be named unrecoverable rather than invented.

The closed journey SHALL NOT be a different screen from the open one: the same spine reading serves
both, because the runs are read by identity and the loop declaration is the trunk's fact. A PBI whose
worktree is gone and one whose worktree is live SHALL answer the journey through the same path, and
the distinction between "the vendor has no data" and "there is no history" SHALL be named.

#### Scenario: The journey is read after the worktree is removed

- **WHEN** a PBI whose worktree has been removed is opened
- **THEN** the spine and its runs are drawn from the persisted facts, the working path and the loop command button are not drawn, and the live frame of a gone process is named unrecoverable

#### Scenario: The closed and the open journey are the same path

- **WHEN** a PBI with a live worktree and one whose worktree is gone are opened
- **THEN** both answer the journey through the same spine reading, and neither is a different screen

#### Scenario: No vendor data is named as no history

- **WHEN** a PBI's vendor has no position and no runs
- **THEN** the spine names the absence of data from the vendor, distinct from the absence of persisted history

### Requirement: The PR and evidence references open the published object and the issue

Where the journey shows a PR reference or an evidence reference, opening it SHALL navigate to the
published object at the vendor (the PR) or to the issue's evidence respectively, not to a local
working path. A reference that resolves no destination SHALL be named rather than offered as a link
to nothing.

#### Scenario: A PR reference opens the published PR

- **WHEN** the journey shows a PR for a PBI and the reference is opened
- **THEN** it opens the PR at the vendor, not a local path

#### Scenario: An evidence reference opens the issue's evidence

- **WHEN** the journey shows an evidence reference and it is opened
- **THEN** it opens the issue's evidence rather than a local directory

#### Scenario: A reference that resolves no destination is named

- **WHEN** a PR or evidence reference resolves no destination
- **THEN** it is named as absent rather than offered as a link to nothing

### Requirement: Tasks hijas appear only when a vendor integration provides them

The journey SHALL NOT synthesise tasks hijas from runs, and SHALL NOT create speculative storage for
them. Tasks hijas SHALL be shown only when a vendor integration actually provides them through the
seam, and a vendor that provides none SHALL draw nothing for them rather than a fabricated list.

#### Scenario: A vendor with no tasks hijas draws nothing

- **WHEN** a PBI's vendor provides no tasks hijas
- **THEN** the journey draws no tasks hijas, and none are synthesised from runs

#### Scenario: A vendor with tasks hijas shows them as provided

- **WHEN** a PBI's vendor provides tasks hijas through the seam
- **THEN** the journey shows them as the vendor provided them, with no speculative storage created
