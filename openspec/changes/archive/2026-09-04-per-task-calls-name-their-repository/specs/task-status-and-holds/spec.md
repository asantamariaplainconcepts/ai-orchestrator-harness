## MODIFIED Requirements

### Requirement: Moving a task writes the label at the vendor, new before old, and leaves the hold alone
The system SHALL expose `POST /api/tasks/status` (`Features/Inbox/UseCases/SetTaskStatus.cs`,
`Access.AnyCaller`) taking the key **in the body rather than in the path**, because `#482` and `AB#12904`
are the vendor's own identifiers and a `#` in a path segment is a fragment delimiter. It SHALL write the new
label **before** removing the old one, SHALL write the label the derivation reads back for every status,
SHALL remove every other loop label, and SHALL leave `status:holding` alone. Moving a task to the status it
already has SHALL write nothing, and a task with no status label dropped on Backlog SHALL write nothing. A
status outside the six SHALL be refused. A task that is not in the inbox SHALL be a 404 naming the sync, a
vendor that refuses the write SHALL be reported in its own words, a pull request SHALL be written through
the GitHub connector, and a vendor with no reader SHALL be refused with the fix named.

The body SHALL also be able to name the **repository**, and the row SHALL be resolved by source, repository
and key together. Source and key alone do not identify a task once two repositories are mirrored, and this
is a write: moving the wrong row relabels somebody else's issue in the loop's own vocabulary, which then
reads as a status somebody set on purpose.

#### Scenario: The new label lands before the old one goes
- **WHEN** a task is moved from one status to another
- **THEN** the new label is written first, then the others removed, and the hold is untouched

#### Scenario: A move to where it already is writes nothing
- **WHEN** a task is dropped on the column it is already in
- **THEN** no vendor write is made

#### Scenario: A vendor refusal is quoted
- **WHEN** the vendor refuses the label write
- **THEN** the response carries the vendor's own words

#### Scenario: A move names the repository it means
- **WHEN** two repositories hold the same key and one of them is moved
- **THEN** that repository's issue is relabelled and the other is untouched

### Requirement: A hold is released and never applied from the screen
The system SHALL expose `POST /api/tasks/hold` (`SetTaskHold.cs`, `Access.AnyCaller`) which SHALL only
**clear** a hold: applying one is how the `/aio:*` commands park a task, and releasing one is a person
approving what they just read, so `held: true` SHALL be refused rather than written. Releasing SHALL remove
the label once and change nothing else, a task that is not held SHALL write nothing, an unknown source SHALL
be refused, and a vendor that refuses SHALL be reported in its own words.

The body SHALL also be able to name the **repository**, resolved the same way as the status write: a
release is a person saying they have read *this* proposal, and clearing the hold on another repository's
issue of the same number would let an unread change through.

#### Scenario: Applying a hold is refused
- **WHEN** a request asks to apply a hold
- **THEN** it is refused, and the only vendor call this file makes is the release

#### Scenario: Releasing a hold that is not there writes nothing
- **WHEN** the task carries no hold label
- **THEN** no vendor write is made

#### Scenario: A release names the repository it means
- **WHEN** two repositories hold the same key and one of them is released
- **THEN** the hold is cleared on that repository's issue and the other stays held
