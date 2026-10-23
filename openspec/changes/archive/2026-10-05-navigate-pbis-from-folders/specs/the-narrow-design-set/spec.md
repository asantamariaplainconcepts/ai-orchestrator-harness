## ADDED Requirements

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
