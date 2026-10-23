## MODIFIED Requirements

### Requirement: A narrow capture asserts where things end, never what they say

A narrow capture SHALL assert that no required control ends past the window's right edge and that the document does not scroll sideways. It SHALL NOT compare against a stored image or assert machine-dependent panel text.

Widths of names supplied by the machine SHALL not be asserted as layout facts. The run SHALL assert that photographed steps draw different screens. Copy-only changes SHALL not fail layout assertions; their new pictures SHALL be artifacts, not implicit documentation commits.

#### Scenario: A control off the edge
- **WHEN** a required button ends outside the window and outside an intentional horizontal scroller
- **THEN** the run fails with the element, text and measured position

#### Scenario: A label is reworded
- **WHEN** copy changes without changing layout
- **THEN** layout assertions still pass and a routine retake writes the new picture only to artifacts

#### Scenario: Two steps drawing the same screen
- **WHEN** two photographed steps read identically
- **THEN** the run fails because the capture sequence did not show distinct panels

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
- **WHEN** the retake produced captures and `.harness/scripts/evidence stage` takes them into a batch
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
