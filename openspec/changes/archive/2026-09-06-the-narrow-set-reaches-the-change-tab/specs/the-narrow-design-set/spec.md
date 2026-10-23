## ADDED Requirements

### Requirement: The narrow set covers the Change tab and each of its five steps, in both themes

The narrow design set SHALL hold one committed capture of the `Change` tab at 390×844 in the light
theme and one in the dark theme, and one further pair for each step of the tab that draws a different
panel.

The tab's resting step SHALL be the tab's own file rather than a second file of its own name: the tab
opens on the first step, so a `change-<first step>` file would be byte-identical to `change`, and two
files of one picture are two things to keep in step.

Every capture SHALL be produced by the same run that measures the page, so that a picture and the
numbers taken from it are never of two different renders.

#### Scenario: The files the set holds

- **WHEN** the narrow set is retaken for the `Change` tab and its five steps
- **THEN** ten files exist: `change-390.png`, `change-code-390.png`, `change-tests-390.png`,
  `change-evidence-390.png`, `change-app-390.png`, and a `-dark` counterpart of each

#### Scenario: A step that draws nothing of its own

- **WHEN** a step's panel would be identical to another step's
- **THEN** it does not get a file of its own, and the set says which steps it distinguishes

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

A run that produces narrow captures SHALL assert that no control anybody has to press ends past the
right edge of the window, and that the document does not scroll sideways.

It SHALL NOT compare a capture against a stored image, and SHALL NOT assert the text of any panel:
the content of a worktree surface is whichever change is open on the machine running the suite, which
is a fact about the machine rather than about the layout.

Where a heading's content is a name the machine supplies — a project's directory name, a branch —
its width SHALL NOT be asserted on that surface, because the assertion would measure the name and not
the layout. The measured reason: the heading's box is 172px at this width, and at a worktree scope the
heading is the project's name followed by the branch, which for ordinary pairs needs 200–250px.

A run SHALL assert that the steps it photographed drew different screens, so that a rail that stopped
switching leaves a failure rather than several identical pictures each of which looks like evidence.

#### Scenario: A control off the edge

- **WHEN** a panel puts a button's box past the right edge of the window, outside any container that
  scrolls sideways
- **THEN** the run fails, naming the element, its text and where it ends

#### Scenario: A label is reworded

- **WHEN** a panel's copy changes and its layout does not
- **THEN** nothing fails, and the next retake commits a different picture

#### Scenario: Two steps drawing the same screen

- **WHEN** two of the photographed steps read identically
- **THEN** the run fails, because one of the captures is of the wrong panel

### Requirement: Retaking the narrow set is a step of the implement command

The command that implements an approved change SHALL name retaking the design record as one of its
steps, with the command that does it, so that it happens where the product is already running and the
reviewer sees the picture and the diff together.

A change that moves a screen the set covers SHALL commit the retaken capture with its diff. A capture
of a screen that is no longer there SHALL be treated as worse than no capture, because it argues
against something that does not exist.

#### Scenario: A change moves a covered screen

- **WHEN** an implemented change alters a screen the narrow or wide set covers
- **THEN** the implement step retakes that set and the new files are committed with the change

#### Scenario: A change touches no covered screen

- **WHEN** an implemented change alters nothing the set covers
- **THEN** the step is a no-op and no capture changes

### Requirement: A surface the set finds does not fit leaves as its own issue

Where a retake finds a surface that does not fit at this width, the finding SHALL leave the change
that found it as its own issue, carrying the capture that shows it and the measurement behind it.

The change that found it SHALL NOT alter the layout. A retake is a net; a fix is a product change with
its own reasoning, and folding one into the other hides the finding inside a diff about something
else.

#### Scenario: A panel that does not fit

- **WHEN** a retake finds a panel drawing less than the height its content asks for at 390px
- **THEN** the capture is committed as it is, the measurement is reported, and the layout is left
  alone for the issue that will own it

#### Scenario: A finding that is a fact about the machine

- **WHEN** a measurement's value depends on a directory or branch name the machine supplies
- **THEN** the report says which part of the number is the layout and which is the name, rather than
  filing the whole of it as a defect
