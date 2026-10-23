## ADDED Requirements

### Requirement: The mirror holds the whole body in the ordinary case
The shared trim every vendor reader runs a body through SHALL cut at **20,000** characters, which is
what the `Body` column already admits (`HasMaxLength(20000)`). A body at or under the limit SHALL be
stored whole, normalised only as it is today — CRLF folded to LF, leading and trailing whitespace
removed — with nothing appended.

The limit SHALL remain a limit: this is a number, not the removal of one, and a body over it is still
cut rather than stored entire.

#### Scenario: A body this repository actually writes is stored whole
- **WHEN** an issue body of 18,348 characters — the longest of this repository's 60 most recent — is
  mirrored
- **THEN** the stored body is the whole 18,348 characters and carries no ellipsis

#### Scenario: A body over the new limit is still cut
- **WHEN** a body of 25,000 characters is mirrored
- **THEN** the stored body is 20,000 characters of it plus an ellipsis, and no more

#### Scenario: The cut never splits a surrogate pair
- **WHEN** the character at the cut offset is the high half of a surrogate pair — an emoji straddling
  it
- **THEN** the cut steps back off the high surrogate, so the stored text is valid Unicode and
  Postgres accepts it

### Requirement: A trim that cut says so, and the fact is stored
The trim SHALL answer both the text and **whether it cut**, rather than the text alone. Every vendor
reader SHALL carry that answer into the task it hands over, and the mirror SHALL store it beside the
body.

A body that was not cut SHALL report false. The fact SHALL be overwritten on every sync along with
the body it describes, because it is a fact about the body and not about the row.

#### Scenario: Truncation is recorded
- **WHEN** a body over the limit is mirrored
- **THEN** the stored row says its body was truncated

#### Scenario: A whole body is recorded as whole
- **WHEN** a body under the limit is mirrored
- **THEN** the stored row says its body was not truncated

#### Scenario: A body that shrinks stops being truncated
- **WHEN** a task whose body was truncated is re-synced after somebody shortened the issue
- **THEN** the row says its body is no longer truncated

### Requirement: The truncation fact reaches every reader of the body
Both reads that carry a body — the inbox list and the single-task read — SHALL carry the truncation
fact beside it. On the wire it SHALL be **additive and optional**: its absence SHALL mean "not
truncated", so a reader written before it existed behaves exactly as it did.

#### Scenario: A reader that ignores the field is unaffected
- **WHEN** a client reads a task and does not look at the truncation field
- **THEN** it sees the same body, the same shape and the same behaviour as before this change

#### Scenario: Absence means whole
- **WHEN** the field is absent from a response
- **THEN** every reader treats the body as whole

### Requirement: A reader handed an incomplete body says so, and does not answer as if it were whole
Every parser that looks for a section inside a mirrored body SHALL have **three** answers, not two:
the value it found, "there is no such section", and **"this could not be read"**. The third SHALL be
one named shape shared by every such parser rather than a separate flag per parser, so a screen and a
later parser draw one distinction rather than several.

A parser SHALL answer "could not be read" when the body it was handed was truncated **and** either
the section is absent, or the section it found runs to the end of the text — a section starting near
the cut is a section only part of which arrived.

A parser SHALL answer with the value it found when the body was truncated and the section was found
whole, ending at a later heading. Truncation SHALL NOT change what a parser does with the text it
does have.

#### Scenario: A missing section in a cut body is not a missing section
- **WHEN** a body was truncated and it has no seam section in the text that arrived
- **THEN** the seam parser answers "could not be read", not "declares no seam"

#### Scenario: A missing section in a whole body is still a missing section
- **WHEN** a body was not truncated and it has no check section
- **THEN** the check parser answers "there is no such section", exactly as before

#### Scenario: A section that survived the cut is still read
- **WHEN** a body was truncated and its seam section sits early in the text, ending at a later
  heading
- **THEN** the seam parser answers with the paths it declares

#### Scenario: A section that runs into the cut could not be read
- **WHEN** a body was truncated and its check section is the last thing in the text that arrived
- **THEN** the check parser answers "could not be read" rather than listing the part that fitted

### Requirement: The screens draw the third state as unknown, never as an absence
The merge gate's Seam tile SHALL draw its existing **Unknown** state when the seam could not be read,
with its own reason naming the mirror's cut — the same three-state shape the tile already has for
"nothing was compared". It SHALL NOT read as "declares no seam".

The Issue tab's check list SHALL say the body could not be read whole. It SHALL NOT say the issue has
no check, and SHALL NOT draw a partial list as if it were the list.

Every line of copy SHALL come from the i18n table, as all user-facing copy in this product does.

#### Scenario: The Seam tile is unknown, not accusatory
- **WHEN** the resolved task's body was truncated and no seam section arrived
- **THEN** the Seam tile reads Unknown and its reason says the mirror holds only part of the issue

#### Scenario: The check list says what is wrong
- **WHEN** the task's body was truncated and no check section arrived
- **THEN** the list says the body could not be read whole, and no rows are drawn
