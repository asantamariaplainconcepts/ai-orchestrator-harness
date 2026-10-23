## ADDED Requirements

### Requirement: The live listing is a tray fixed at the bottom of the content

The shell SHALL draw the live sessions as a tray fixed at the bottom of the content column, on every
route, led by a label with the number of sessions it lists, each session once. Pressing a session SHALL
attach its pane over the content and pressing it again SHALL detach it, without ending the run.
The tray SHALL carry no affordance to start a session of its own: a run starts from the PBI's step,
and a manual terminal from the folder's menu.

#### Scenario: The tray is at the bottom

- **WHEN** any screen is open
- **THEN** the session tray is the last row of the content column, below the screen

#### Scenario: Detaching from the tray leaves the run running

- **WHEN** an attached session is pressed again in the tray
- **THEN** its pane closes and the session stays in the live listing

#### Scenario: The tray starts nothing

- **WHEN** the tray is read
- **THEN** it offers no control to start a session or open a terminal
