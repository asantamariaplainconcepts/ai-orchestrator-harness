## ADDED Requirements

### Requirement: Inbox rows open the PBI screen, and Split draws the same body

A row of the inbox's Table and Board layouts SHALL navigate to the PBI's own screen. The Split
layout SHALL draw, beside the list, the same body the PBI screen draws for the selected PBI, so the
inbox carries no second reading of a PBI.

#### Scenario: A Board card opens the PBI screen

- **WHEN** a card of the Board layout is opened
- **THEN** the PBI screen for that key and folder is drawn

#### Scenario: Split draws the PBI body

- **WHEN** a row is selected in the Split layout
- **THEN** the pane beside the list draws the PBI screen's tabs for it, not a separate panel

### Requirement: Needs a person draws one row per blocked PBI

The inbox's *Needs a person* panel SHALL draw one row per PBI identity and cause — provider, host,
repository, key and cause together — however many attention rows name it; the newest run's row
SHALL stand for the group.

#### Scenario: Two failed runs of one PBI

- **WHEN** the attention queue holds two `run-failed` rows for `#185` of one repository
- **THEN** the panel draws `#185` once
