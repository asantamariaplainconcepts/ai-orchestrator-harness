# app-shell Specification

## Purpose
Around the four screens sit the rail, which gets a person to every PBI the loop touches, and the session
tray, which shows what is live right now. Both are always there, and both send a person to the PBI's
screen rather than explain where it is. Reverse-engineered from the shipped code
(`src/frontend/app/Layout.tsx`, `Rail.tsx`, `Tray.tsx`, `routes.tsx`), not negotiated ahead of it.

## Requirements
### Requirement: Four screens, and the PBI under PBIs
The routes SHALL be `/pbis`, `/loop`, `/settings`, `/folders/:folderId` and
`/pbis/:folderId/:pbiId/:tab`, each loaded on its own, and `/` SHALL lead to `/pbis`. The first screen's gzip size SHALL stay within the 200 kB the build
budgets, and the build SHALL fail above it.

#### Scenario: Opening the app
- **WHEN** a person opens the root of the console
- **THEN** the PBIs screen is shown

### Requirement: The rail leads to the PBIs, the holds and the folders
The rail SHALL offer PBIs with the count of open ones, Loop with "<n> need you" counting the held ones
and those whose loop run is stale,
and the folders under "Folders · <n>" with a `+` that adds one. Each folder's name SHALL link to its
page, and a chevron beside it SHALL show or hide its PBIs. Each folder SHALL list its PBIs that
carry a state, each with a pause when held and a dot when a session of it is live, or "No PBI in the
loop here."; its `…` menu SHALL offer *Terminal* and *Explore with agent* and nothing else. The footer
SHALL be Settings and the theme. Below 900 pixels the rail SHALL become a drawer.

#### Scenario: A hold appears in the rail
- **WHEN** the engine holds a PBI
- **THEN** the Loop entry counts it in "need you" and the PBI's row under its folder carries a pause

#### Scenario: A stale run appears in the rail
- **WHEN** a PBI's loop run has been silent past `Runs:StaleAfter`
- **THEN** the Loop entry counts it in "need you" without a pause, and stops counting it once the run writes

### Requirement: The tray holds the live sessions and nothing else
The tray SHALL list the live sessions under "Sessions · <n>", each once, as "<agent> · #<pbi>
<command>" or "<agent> · <folder> <command>", with its age or how long it has been silent. A click SHALL
open its terminal over the screen and another SHALL close it without stopping the run. A session opened
by hand SHALL open in the tray once it appears. Ended sessions SHALL NOT be listed: "A session dies with
its run; what it wrote is in the Journey."

#### Scenario: Closing a terminal
- **WHEN** a person closes a run's terminal from the tray
- **THEN** the terminal hides, the chip stays, and the run keeps running

### Requirement: A session is ended from the tray after one line of confirmation
Each chip SHALL carry *End <name>* beside it and the terminal open over the screen *End session*.
Either SHALL ask in one line, and only the dialog's *End session* SHALL send `POST /api/runs/{runId}/stop`,
the call of *Stop run*; Cancel, the scrim and Escape SHALL send nothing. The line SHALL say what dies:
for a folder session, nothing is kept; for a manual one on a PBI, its recording stays in the Journey;
for a loop run, the run fails and the loop holds the PBI on its next pass. On success the chip SHALL
leave the tray; a refusal SHALL stay in the dialog.

#### Scenario: Ending a folder terminal
- **WHEN** a person presses *End* beside a folder terminal's chip and confirms
- **THEN** the console sends `POST /api/runs/{runId}/stop` once and the chip leaves the tray

#### Scenario: Ending a loop run warns of the hold
- **WHEN** a person presses *End* beside a loop run's chip
- **THEN** the dialog says the loop holds the PBI on its next pass, and nothing is sent until they confirm

### Requirement: Both themes, chosen by the person or the system
The theme SHALL follow the system until a person chooses one from the rail's footer, and the choice
SHALL be remembered in the browser. The theme SHALL be one class on one element, and no component SHALL
branch on it.

#### Scenario: Switching to dark
- **WHEN** a person presses the theme control in light
- **THEN** every screen redraws in the dark tokens and stays dark on the next visit
