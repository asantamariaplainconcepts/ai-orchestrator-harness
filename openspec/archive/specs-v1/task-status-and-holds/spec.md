# task-status-and-holds Specification

## Purpose
One status per task, derived from the vendor's labels rather than stored, and the one hold a person releases
by hand. The derivation is a precedence and not a search, because an issue can carry two loop labels at once
and a vendor returns labels in whatever order it likes. Reverse-engineered from the shipped code, not
negotiated ahead of it.
## Requirements
### Requirement: A status is derived from the labels by precedence, and holding is orthogonal
This capability is superseded by `pbi-status-and-holds` (#448). Every requirement of
`task-status-and-holds` SHALL live on in `pbi-status-and-holds` with the behaviour unchanged and the
vocabulary renamed from task to PBI — the derivation, the seven states, `status:spec-ready`'s
meaning and the board's rendering, answering at `POST /api/pbis/status` and `POST /api/pbis/hold`.
Nothing new SHALL be added to this capability: it exists only to name its replacement.

#### Scenario: Two loop labels resolve to the furthest along
- **WHEN** a task carries `status:in-progress` and `status:holding`, in either order
- **THEN** the status is in-progress and the task reads as held

#### Scenario: An unrecognised status label changes nothing
- **WHEN** a task carries a `status:` label outside the seven
- **THEN** it renders as an ordinary chip and the derived status is unchanged

#### Scenario: spec-ready derives like the rest
- **WHEN** a task carries `status:spec-ready` alone
- **THEN** the derived status is SpecReady, it wins over ReadyForProposal and loses to
  ReadyForImplementation, and its lane's action is `/aio:propose`

### Requirement: Moving a task writes the label at the vendor, new before old, and leaves the hold alone
The system SHALL expose `POST /api/tasks/status` (`Features/Inbox/UseCases/SetTaskStatus.cs`,
`Access.AnyCaller`) taking the key **in the body rather than in the path**, because `#482` and `AB#12904`
are the vendor's own identifiers and a `#` in a path segment is a fragment delimiter. It SHALL write the new
label **before** removing the old one, SHALL write the label the derivation reads back for every status,
SHALL remove every other loop label, and SHALL leave `status:holding` alone. Moving a task to the status it
already has SHALL write nothing, and a task with no status label dropped on Backlog SHALL write nothing. A
status outside the seven SHALL be refused. A task neither the snapshot nor its vendor has SHALL be a 404, a
vendor that refuses the write SHALL be reported in its own words, a pull request SHALL be written through
the GitHub connector, and a vendor with no reader SHALL be refused with the fix named — configure its scope
and credential.

The body SHALL also be able to name the **repository**, and the task SHALL be resolved by source, repository
and key together. Source and key alone do not identify a task once one scope names two repositories, and this
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

### Requirement: The next read follows the write, not the write itself
Neither the status write nor the hold write SHALL edit the labels this product last read: the vendor is the
record, so a change made in this product and one made in a browser SHALL arrive by the same path. A write that
changed something at the vendor SHALL instead empty the task snapshot, so the next read polls the vendor and
derives the new status from what it answers. The consequence SHALL be visible rather than hidden: the board
draws the move at once and puts the card back if the vendor refuses.

#### Scenario: The labels come back through the next read
- **WHEN** a status is written at the vendor
- **THEN** no label is written into the snapshot, it is emptied, and the next read brings the label back

#### Scenario: Moving a task leaves the hold alone on the next read too
- **WHEN** a held task is moved
- **THEN** the hold survives the write and the following read

### Requirement: Display precedence does not authorize controlled dispatch
Existing status derivation and legacy label-write behavior SHALL remain display/manual
contracts. A controlled evaluator SHALL independently require one recognized phase from a
fresh provider read, and SHALL reject multiple phase labels, unknown phases or unknown Hold
state. Display precedence and the inbox's cached read SHALL NOT be used as authorization.

#### Scenario: A card can render while dispatch is blocked
- **WHEN** the inbox renders the furthest phase but the provider has two phase labels
- **THEN** the card's display does not authorize a controlled transition

### Requirement: Human Hold release and controlled grant are distinct
The existing Hold-release endpoint SHALL NOT issue a controlled grant. Shared controlled
execution SHALL require both absence of Hold and a valid content-bound owner grant; neither
condition SHALL substitute for the other. Coordinator failure handling SHALL apply Hold
through a separately authorized, journaled effect, not by changing the legacy release
endpoint's semantics — and that effect SHALL carry a typed reason from the closed attention
vocabulary, projected as one attention comment on the task's issue. An unrecognized reason
code SHALL display raw with the hold intact.

#### Scenario: A generic Hold release succeeds
- **WHEN** a person clears the label through the existing endpoint
- **THEN** the label changes but no controlled grant or dispatch authority is created

#### Scenario: A valid grant coexists with Hold
- **WHEN** a grant is valid but the provider still reports Hold
- **THEN** controlled dispatch remains blocked

#### Scenario: A hold the coordinator applied says why
- **WHEN** coordinator failure handling applies a hold through its journaled effect
- **THEN** the effect carries a typed reason, and the task's issue carries exactly one
  attention comment naming it

#### Scenario: An unknown reason is information, not permission
- **WHEN** a reader does not recognize a hold's reason code
- **THEN** it displays the code raw and the hold remains

### Requirement: Local ending and provider Hold are observed independently
The system SHALL distinguish a locally held Run, a confirmed shared Hold and a pending or
unknown Hold publication. A local state or successful intent write SHALL NOT claim the
provider changed. A failed dispatch SHALL remain blocked across restart even when Hold
publication fails.

#### Scenario: The provider refuses the Hold
- **WHEN** local execution stops but GitHub refuses or cannot confirm the Hold write
- **THEN** the local failure remains blocking with a visible publication reason

