# pbi-status-and-holds Delta

## ADDED Requirements

### Requirement: A status is derived from the labels by precedence, and holding is orthogonal
`LoopStatus` SHALL name seven states in loop order — Backlog, ReadyForProposal, SpecReady,
ReadyForImplementation, InProgress, CodeReview, Done — and a PBI's status SHALL be **derived** from
its `status:` labels rather than stored. Where a PBI carries two loop labels the furthest along
SHALL win, whichever order they arrive in, because the vendor's array order is not stable. A PBI
with no `status:` label SHALL be Backlog. Case SHALL NOT decide the status. Holding SHALL NOT be a
member of the enum: it is orthogonal, so a held PBI SHALL keep the status the work reached, and
holding alone SHALL read as a hold over Backlog. A promoted label SHALL leave the chips while the
vendor's own labels stay, and an unrecognised `status:` label SHALL stay an ordinary chip and change
nothing.

`spec-ready` SHALL mean the Definition of Ready is met and the proposal has not landed: the state
between interrogation and implementation, moved by the refine step and gate for the propose step.
The board SHALL render it as its own lane, and its action SHALL be `/aio:propose`.

#### Scenario: Two loop labels resolve to the furthest along
- **WHEN** a PBI carries `status:in-progress` and `status:holding`, in either order
- **THEN** the status is in-progress and the PBI reads as held

#### Scenario: An unrecognised status label changes nothing
- **WHEN** a PBI carries a `status:` label outside the seven
- **THEN** it renders as an ordinary chip and the derived status is unchanged

#### Scenario: spec-ready derives like the rest
- **WHEN** a PBI carries `status:spec-ready` alone
- **THEN** the derived status is SpecReady, it wins over ReadyForProposal and loses to
  ReadyForImplementation, and its lane's action is `/aio:propose`

### Requirement: Moving a PBI writes the label at the vendor, new before old, and leaves the hold alone
The system SHALL expose `POST /api/pbis/status` (`Features/Inbox/UseCases/SetPbiStatus.cs`,
`Access.AnyCaller`) taking the key **in the body rather than in the path**, because `#482` and `AB#12904`
are the vendor's own identifiers and a `#` in a path segment is a fragment delimiter. It SHALL write the
new label **before** removing the old one, SHALL write the label the derivation reads back for every
status, SHALL remove every other loop label, and SHALL leave `status:holding` alone. Moving a PBI to
the status it already has SHALL write nothing, and a PBI with no status label dropped on Backlog SHALL
write nothing. A status outside the seven SHALL be refused. A PBI neither the snapshot nor its vendor
has SHALL be a 404, a vendor that refuses the write SHALL be reported in its own words, a pull request
SHALL be written through the GitHub connector, and a vendor with no reader SHALL be refused with the
fix named — configure its scope and credential.

The body SHALL also be able to name the **repository**, and the PBI SHALL be resolved by source,
repository and key together. Source and key alone do not identify a PBI once one scope names two
repositories — `#12` under two repositories is two PBIs, and a status move SHALL land on the one the
caller named.

#### Scenario: The new label lands before the old one leaves
- **WHEN** a PBI moves from ReadyForImplementation to InProgress
- **THEN** the vendor receives `status:in-progress` while `status:ready-for-implementation` is still
  present, and the removal follows

#### Scenario: A hold survives a status move
- **WHEN** a held PBI is moved to another status
- **THEN** `status:holding` is not touched

#### Scenario: The repository disambiguates the write
- **WHEN** two repositories in one scope each carry a PBI with key `#12` and the status of one is
  moved
- **THEN** only that repository's PBI changes, resolving by source, repository and key together

#### Scenario: A key nobody has is a 404
- **WHEN** the key is neither in the snapshot nor at the vendor
- **THEN** the response is a 404 rather than an error about labels

### Requirement: The list of statuses a PBI can move to is threaded through everything that uses one
The status list SHALL be the loop's declared vocabulary, threaded through the wire, the native menu
and the vendor's own labels alike, so that a status added to the declaration is one edit in one
place and every consumer follows. The order SHALL be the loop order, so the board's columns, the
native menu and the picker agree without re-sorting.

#### Scenario: The loop's order is the only order
- **WHEN** a status pull-down is drawn, native or web
- **THEN** its entries are the seven in loop order, sourced from the same declaration

### Requirement: A hold is released and never applied from the screen
The system SHALL expose `POST /api/pbis/hold` (`SetPbiHold.cs`, `Access.AnyCaller`) which SHALL only
**clear** a hold: applying one is how the `/aio:*` commands park a PBI, and releasing one is a person
approving what they just read, so `held: true` SHALL be refused rather than written. Releasing SHALL
remove the label once and change nothing else, a PBI that is not held SHALL write nothing, an unknown
source SHALL be refused, and a vendor that refuses SHALL be reported in its own words.

The body SHALL also be able to name the **repository**, resolved the same way as the status write: a
release is a person saying they have read *this* proposal, and clearing the hold on another
repository's PBI of the same number would let an unread change through.

#### Scenario: Applying a hold is refused
- **WHEN** a request asks to apply a hold
- **THEN** it is refused, and the only vendor call this file makes is the release

#### Scenario: Releasing a hold that is not there writes nothing
- **WHEN** the PBI carries no hold label
- **THEN** no vendor write is made

#### Scenario: A release names the repository it means
- **WHEN** two repositories hold the same key and one of them is released
- **THEN** the hold is cleared on that repository's PBI and the other stays held

### Requirement: The next read follows the write, not the write itself
Neither the status write nor the hold write SHALL edit the labels this product last read: the vendor
is the record, so a change made in this product and one made in a browser SHALL arrive by the same
path. A write that changed something at the vendor SHALL instead empty the PBI snapshot, so the next
read polls the vendor and derives the new status from what it answers. The consequence SHALL be
visible rather than hidden: the board draws the move at once and puts the card back if the vendor
refuses.

#### Scenario: The labels come back through the next read
- **WHEN** a status is written at the vendor
- **THEN** no label is written into the snapshot, it is emptied, and the next read brings the label
  back

#### Scenario: Moving a PBI leaves the hold alone on the next read too
- **WHEN** a held PBI is moved
- **THEN** the hold survives the write and the following read

### Requirement: Display precedence does not authorize controlled dispatch
Existing status derivation and legacy label-write behavior SHALL remain display/manual contracts. A
controlled evaluator SHALL independently require one recognized phase from a fresh provider read,
and SHALL reject multiple phase labels, unknown phases or unknown Hold state. Display precedence and
the inbox's cached read SHALL NOT be used as authorization.

#### Scenario: A card can render while dispatch is blocked
- **WHEN** the inbox renders the furthest phase but the provider has two phase labels
- **THEN** the card's display does not authorize a controlled transition

### Requirement: Human Hold release and controlled grant are distinct
The existing Hold-release endpoint SHALL NOT issue a controlled grant. Shared controlled execution
SHALL require both absence of Hold and a valid content-bound owner grant; neither condition SHALL
substitute for the other. Coordinator failure handling SHALL apply Hold through a separately
authorized, journaled effect, not by changing the legacy release endpoint's semantics — and that
effect SHALL carry a typed reason from the closed attention vocabulary, projected as one attention
comment on the PBI's issue. An unrecognized reason code SHALL display raw with the hold intact.

#### Scenario: A generic Hold release succeeds
- **WHEN** a person clears the label through the existing endpoint
- **THEN** the label changes but no controlled grant or dispatch authority is created

#### Scenario: A valid grant coexists with Hold
- **WHEN** a grant is valid but the provider still reports Hold
- **THEN** controlled dispatch remains blocked

#### Scenario: A hold the coordinator applied says why
- **WHEN** coordinator failure handling applies a hold through its journaled effect
- **THEN** the effect carries a typed reason, and the PBI's issue carries exactly one attention
  comment naming it

#### Scenario: An unknown reason is information, not permission
- **WHEN** a reader does not recognize a hold's reason code
- **THEN** it displays the code raw and the hold remains

### Requirement: Local ending and provider Hold are observed independently
The system SHALL distinguish a locally held Run, a confirmed shared Hold and a pending or unknown
Hold publication. A local state or successful intent write SHALL NOT claim the provider changed. A
failed dispatch SHALL remain blocked across restart even when Hold publication fails.

#### Scenario: The provider refuses the Hold
- **WHEN** local execution stops but GitHub refuses or cannot confirm the Hold write
- **THEN** the local failure remains blocking with a visible publication reason
