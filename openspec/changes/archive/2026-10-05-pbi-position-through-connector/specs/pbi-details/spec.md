## ADDED Requirements

### Requirement: The detail shows the position the Connector translated, and unknown is its own state
The detail of a PBI SHALL show the position and hold exactly as the Connector's read answered,
without mapping, deriving or re-parsing anything in the UI. `unknown` SHALL render as its own
state — with the reason the vendor's answer carried — distinct from every declared position and
from `backlog`; a PBI whose position is unknown SHALL NOT be shown as done or ready to run, and no
write, menu or drag target SHALL be hidden behind it silently distinguishing the state. An error
reading the position SHALL render with the vendor's own words, and a hold SHALL render orthogonal
to the position, exactly as the read answered.

#### Scenario: Unknown renders as itself
- **WHEN** the Connector answered `unknown` with a reason
- **THEN** the detail names it as unknown with that reason, in its own visual state, and the PBI sits in no lane

#### Scenario: A vendor error is not an unknown position
- **WHEN** the position read failed with the vendor's words
- **THEN** the detail shows the error as an error, not an unknown badge and not a position

#### Scenario: Held is shown beside the position
- **WHEN** a held PBI in code review is opened
- **THEN** the detail shows the code review position with the hold beside it, one not overriding the other
