# mirrored-body-completeness Delta

## MODIFIED Requirements

### Requirement: The reader keeps the whole body in the ordinary case
The shared trim every vendor reader runs a body through (`SourcedText`) SHALL cut at **20,000**
characters. A body at or under the limit SHALL be kept whole, normalised only as it is today — CRLF
folded to LF, leading and trailing whitespace removed — with nothing appended.

The limit SHALL remain a limit: this is a number, not the removal of one, and a body over it is still
cut rather than kept entire.

#### Scenario: A body this repository actually writes is kept whole
- **WHEN** an issue body of 18,348 characters — the longest of this repository's 60 most recent — is
  read
- **THEN** the body handed over is the whole 18,348 characters and carries no ellipsis

#### Scenario: A body over the new limit is still cut
- **WHEN** a body of 25,000 characters is read
- **THEN** the body handed over is 20,000 characters of it plus an ellipsis, and no more

#### Scenario: The cut never splits a surrogate pair
- **WHEN** the character at the cut offset is the high half of a surrogate pair — an emoji straddling
  it
- **THEN** the cut steps back off the high surrogate, so the text handed over is valid Unicode

### Requirement: A trim that cut says so, and the fact travels with the body
The trim SHALL answer both the text and **whether it cut**, rather than the text alone. Every vendor
reader SHALL carry that answer into the PBI it hands over (`SourcedPbi.BodyTruncated`), beside the
body.

A body that was not cut SHALL report false. The fact SHALL be replaced on every read along with the
body it describes, because it is a fact about the body and not about the PBI.

#### Scenario: Truncation is recorded
- **WHEN** a body over the limit is read
- **THEN** the PBI handed over says its body was truncated

#### Scenario: A whole body is recorded as whole
- **WHEN** a body under the limit is read
- **THEN** the PBI handed over says its body was not truncated

#### Scenario: A body that shrinks stops being truncated
- **WHEN** a PBI whose body was truncated is read again after somebody shortened the issue
- **THEN** the PBI says its body is no longer truncated
