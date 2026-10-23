# the-narrow-design-set Specification

## Purpose

The record of what this product's screens look like at the width a phone actually is, and the rule
that keeps it honest.

`docs/design/current/` is what every design issue in this repository argues from, and its narrow half
is what #217 measured three defects against — a merge gate rendering ten elided values in five cells,
a header spending its row on seven icons while the folder's name was cut to two characters, and a
Review tab whose patch had 150px. None of it was red anywhere, because nothing was looking below
1280.

**The width has to be proved, not requested.** Headless Chrome on macOS lays a 390px window out at
500px and scales the result, so a picture taken that way looks right and every number read off it is
about a layout nobody has. So a capture and the assertion `innerWidth == 390` come out of the same
run, and the theme comes from the control the product draws for it rather than from a browser flag
that moves nothing the application reads.

**It finds; it does not fix.** A capture is never compared against a stored image and never asserts
what a panel says: the content of a worktree surface is whichever change is open on the machine. What
is asserted is where things end. A surface the set finds does not fit leaves as its own issue with
the picture that shows it, because a layout fix folded into a retake hides the finding inside a diff
about something else.
## Requirements
### Requirement: The width is proved on the page, not assumed from the request

A run that produces a narrow capture SHALL read `window.innerWidth` from the page it is photographing
and SHALL fail unless it is **390**.

The width SHALL be set through the browser's device-metrics override. Headless Chrome's window sizing
SHALL NOT be used for this set: on macOS, `--headless=new --window-size=390,844` lays the page out at
500×757 and scales the result into a 390px PNG, and a page printing `innerWidth` under it reports 500
under both `--headless` and `--headless=old`. Every number read off such a shot is a number about a
500px layout.

This assertion SHALL come before any assertion about what the page contains, because it is what says
the instrument measures what it claims to.

#### Scenario: The instrument agrees

- **WHEN** the page is opened at the phone viewport and asked its own `innerWidth`
- **THEN** it answers 390, and the capture is taken

#### Scenario: The viewport is not applied

- **WHEN** the page answers any width other than 390 — 500 from a scaled headless window, or 1280
  from a context opened without a viewport
- **THEN** the run fails saying the capture would be a picture of a width nobody has, and no
  measurement below it is reported

### Requirement: The theme is reached through the product's own control, and the switch is read back

A narrow capture in the dark theme SHALL be produced by operating the control the application draws
for it, and SHALL NOT be produced by a browser flag or a page-level override.

Below the width at which the shell folds its header icons into one menu, that control is an item of
the overflow menu and not a button on the header, so a run SHALL open the menu and choose the item.

The theme SHALL be read off the page before the switch and again after it. A capture named for a
theme the page is not in SHALL be a failure and not a file.

#### Scenario: Switching at phone width

- **WHEN** the dark capture is taken at 390px
- **THEN** the overflow menu is opened, the theme item is chosen, and the page is confirmed to carry
  the dark class before the shot is taken

#### Scenario: The page is already in the theme wanted

- **WHEN** the page's theme is read and it is already the one the capture needs
- **THEN** no switch is made, because the starting theme is the browser's colour-scheme preference
  and not the run's to assume

#### Scenario: The control stops working

- **WHEN** the control is operated and the page's theme does not change
- **THEN** the run fails, rather than writing a light picture into a file named `-dark`

### Requirement: A narrow capture asserts where things end, never what they say

A narrow capture SHALL assert that no required control ends past the window's right edge and that the document does not scroll sideways. It SHALL NOT compare against a stored image or assert machine-dependent panel text.

Widths of names supplied by the machine SHALL not be asserted as layout facts. The run SHALL assert that photographed steps draw different screens. Copy-only changes SHALL not fail layout assertions; their new pictures SHALL be artifacts, not implicit documentation commits.

A narrow capture of the Change tab SHALL also assert that the step it photographs is reachable: the
first row of the selected step's panel SHALL be brought into the viewport by scrolling the
application's own scroller, and a panel that no scroll of the page reaches SHALL fail the run.

#### Scenario: A control off the edge
- **WHEN** a required button ends outside the window and outside an intentional horizontal scroller
- **THEN** the run fails with the element, text and measured position

#### Scenario: A label is reworded
- **WHEN** copy changes without changing layout
- **THEN** layout assertions still pass and a routine retake writes the new picture only to artifacts

#### Scenario: Two steps drawing the same screen
- **WHEN** two photographed steps read identically
- **THEN** the run fails because the capture sequence did not show distinct panels

#### Scenario: A step panel below an unscrollable fold
- **WHEN** the change tab is photographed at 390 px and the selected step's first panel row cannot
  be brought into the viewport by scrolling
- **THEN** the run fails naming the step, rather than writing a capture whose subject never reached
  the screen

### Requirement: Retaking the narrow set is a step of the implement command

The implement command SHALL retake the relevant wide and narrow screen sets where the product is available, and SHALL stage relevant captures in ignored artifacts under the `evidence-<n>-<screen>` convention.

Routine retakes SHALL NOT modify or commit tracked design captures. The implement flow SHALL publish those captures through `publish-issue-evidence`; a failure SHALL leave them locally and state the reason rather than inventing remote evidence.

Copying staged captures SHALL be a local operation, not an application start or network request. A change touching no covered screen SHALL not manufacture captures or an empty publication.

#### Scenario: A change moves a covered screen
- **WHEN** implementation alters a covered wide or narrow surface
- **THEN** the relevant sets are retaken into ignored artifacts and relevant captures are staged for issue publication

#### Scenario: A change touches no covered screen
- **WHEN** implementation alters none of the covered surfaces
- **THEN** no capture is generated merely to populate evidence

#### Scenario: The copy follows the convention
- **WHEN** a retake produces relevant captures for issue #n
- **THEN** staged names begin `evidence-<n>-` and the capture operation does not dirty tracked documentation

#### Scenario: The wide half cannot run
- **WHEN** the AppHost does not report a healthy server
- **THEN** the wide capture names that failure and remedy without starting an app or claiming a successful retake

### Requirement: A surface the set finds does not fit leaves as its own issue

A retake finding a surface that does not fit SHALL report the measurement and preserve the capture in ignored artifacts for publication with its finding. It SHALL NOT silently repair unrelated layout or commit a screenshot as a substitute for publishing evidence.

A finding whose number depends on the machine's directory or branch name SHALL identify that dependence rather than call the whole value a layout defect.

#### Scenario: A panel that does not fit
- **WHEN** a retake finds insufficient space for the panel's content at 390px
- **THEN** the capture and measurement are retained for the separately owned finding, without an unrelated layout edit

#### Scenario: A finding that is a fact about the machine
- **WHEN** the value depends on the directory or branch name
- **THEN** the report distinguishes the supplied name from the layout measurement

#### Scenario: The copy lands under the convention
- **WHEN** the retake produced captures and `.harness/aio/evidence stage` takes them into a batch
- **THEN** `.harness/evidence/<batch>/` holds them named `evidence-<n>-<screen>.png` for the issue being implemented, and `git status` stays clean because the directory is ignored

#### Scenario: The app is down and the wide half cannot run
- **WHEN** no AppHost reports the `server` resource healthy
- **THEN** the wide retake degrades as `capture.sh` already does — said, with the remedy — and the command does not start the app itself

### Requirement: The narrow set covers the Change tab and each of its four steps, in both themes

The narrow set SHALL produce a 390x844 light/dark pair for Change and each distinct covered step. The resting step SHALL use Change's capture name rather than duplicate that same screen under another name.

Each capture SHALL come from the same run that proves the page's width and theme. Gate and direct routine runs SHALL both write to ignored capture artifacts; neither SHALL overwrite the tracked design record.

#### Scenario: The files the set holds
- **WHEN** Change and its covered four-step set are retaken
- **THEN** the artifact directory contains `change-390.png`, `change-code-390.png`, `change-evidence-390.png`, `change-app-390.png` and their dark counterparts

#### Scenario: Inside the gate
- **WHEN** `aspire do ci` runs the narrow suite
- **THEN** captures land in the configured ignored artifact destination and `docs/design/current/` is untouched

#### Scenario: The suite is run directly
- **WHEN** the narrow suite runs without a capture destination override
- **THEN** it defaults to ignored artifacts and `docs/design/current/` remains untouched

### Requirement: The PBI journey's spine is a covered narrow surface

The PBI journey's spine SHALL be a covered narrow surface: a 390×844 light/dark pair SHALL be produced
where the product is available, asserting that no required control ends past the window's right edge
and that the document does not scroll sideways at the phone width. The spine draws vertically at 390px,
its stages stack, and a run card under a stage SHALL be brought into the viewport by scrolling the
application's own scroller rather than the document.

A narrow capture of the journey SHALL assert that the current stage's first panel row is reachable
by scrolling, the way the Change tab's covered step is asserted. The capture SHALL NOT compare
against a stored image or assert machine-dependent panel text.

#### Scenario: The journey at 390px in both themes

- **WHEN** the PBI journey is retaken at the phone width
- **THEN** a 390×844 light pair and a 390×844 dark pair are produced, each from the same run that proves the page's width and theme

#### Scenario: A control off the edge at 390px

- **WHEN** a required control on the journey ends outside the window at 390px
- **THEN** the run fails with the element, text and measured position

#### Scenario: A stage panel below an unscrollable fold

- **WHEN** the journey is photographed at 390px and the current stage's first panel row cannot be brought into the viewport by scrolling
- **THEN** the run fails naming the stage, rather than writing a capture whose subject never reached the screen

### Requirement: The rail with PBI rows and the History screen are covered narrow surfaces
The narrow set SHALL cover the rail rendering a folder's PBI rows, the global History screen, and the
PBI voyage reached from a row, at 390×844 in both themes. Each covered surface SHALL assert that no
required control ends past the window's right edge and the document does not scroll sideways, and the
voyage's reached surface SHALL assert that its first panel row is reachable by scrolling the
application's own scroller — the same rule the Change tab's step panel follows. A `Live` filter, a
hold badge and a live marker on a rail row SHALL each fit at 390px without clipping, and the History
screen's journey rows and their controls SHALL fit without horizontal overflow.

#### Scenario: The rail fits at phone width
- **WHEN** the rail with PBI rows is rendered at 390px
- **THEN** no required control ends past the right edge and the document does not scroll sideways

#### Scenario: History fits at phone width
- **WHEN** the History screen is rendered at 390px
- **THEN** its journey rows and controls fit without horizontal overflow

#### Scenario: The voyage reached from a row is reachable by scroll
- **WHEN** a PBI's voyage is opened from a rail row at 390px
- **THEN** the first row of the current stage's panel is brought into the viewport by scrolling the
application's own scroller

#### Scenario: Hold and live badges fit at phone width
- **WHEN** a rail row carries a hold badge and a live marker at 390px
- **THEN** both fit beside the key and title without clipping the row

### Requirement: Narrow captures of the rail, History and the voyage are retaken where the product is available
The implement command SHALL retake the covered narrow surfaces this change adds — the rail with PBI
rows, History, and the voyage reached from a row — where the AppHost reports a healthy server, and
SHALL stage the captures in ignored artifacts under the `evidence-<n>-<screen>` convention. A capture
SHALL come from the same run that proves the page's width and theme, and the tracked design record
SHALL NOT be modified by the retake, the same rule every covered surface follows. A change touching
no covered surface SHALL not manufacture captures.

#### Scenario: A change to the rail retakes its surface
- **WHEN** implementation alters the rail's PBI rows
- **THEN** a 390×844 light/dark pair is retaken into ignored artifacts and staged under the
`evidence-<n>-` convention

#### Scenario: A change to History retakes its surface
- **WHEN** implementation alters the History screen
- **THEN** a 390×844 light/dark pair is retaken into ignored artifacts

#### Scenario: The tracked design record is untouched
- **WHEN** the retake runs inside the gate
- **THEN** `docs/design/current/` is untouched and captures land in the configured ignored
destination

