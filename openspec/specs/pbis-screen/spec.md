# pbis-screen Specification

## Purpose
The PBIs screen answers what work there is: every PBI of every folder in one list grouped by the
loop's states, with the closed ones behind a filter rather than on another screen, and no board.
Reverse-engineered from the shipped code (`src/frontend/features/pbis/PbisScreen.tsx`), not negotiated
ahead of it.

## Requirements
### Requirement: One list grouped by state, in the loop's order
The screen at `/pbis` SHALL group the PBIs by state: first the states the folders' loops declare, in
their order; then the states no loop declares, alphabetically, marked "The loop does not declare this
state."; then "No state"; then "Closed". Each group SHALL show eight rows and offer the rest. Each row
SHALL show the id, title, folder and vendor, a pause when the PBI is held and a dot when a session of
it is live, and SHALL open the PBI's screen.

#### Scenario: A PBI with no status label
- **WHEN** an open issue carries no `status:*` label
- **THEN** it is listed under "No state", with the hint that a state label puts it in the loop

### Requirement: Filters live in the address
The filters SHALL be the source, the folder, the state (Open, In hold, Done or All, Open by default) and
a search, all kept in the URL. A search that is a number, with or without `#`, SHALL match the id's
start; any other SHALL match the title. Done and All SHALL read the closed PBIs, and the screen SHALL
say that Done lists the latest 100 of each folder.

#### Scenario: Finding a PBI by number
- **WHEN** a person types `#49`
- **THEN** the list shows the PBIs whose id starts with 49

### Requirement: An unread folder is named, not hidden
A folder the vendor could not be read for SHALL be named with the reason, "<repository> was not read:
<why>", and the rest SHALL still be listed. With no folders, the screen SHALL say to add one with the
`+` in the rail.

#### Scenario: No folders yet
- **WHEN** the console has no folder
- **THEN** the screen says "No PBIs yet. Add a folder with the + in the rail."
