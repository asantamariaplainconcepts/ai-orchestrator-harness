# pbi-status-and-holds Specification

## Purpose
TBD - created by archiving change rename-task-to-pbi. Update Purpose after archive.
## Requirements
### Requirement: A status is derived from the labels by precedence, and holding is orthogonal
The module SHALL NOT derive a PBI's status from the vendor's labels: the position — its status in
the loop's declared vocabulary — and whether it is held SHALL come from the Connector's position
read, translated by each adapter behind the seam. The vocabulary SHALL be the loop's declared
positions — Backlog, NeedsRefinement, ReadyForProposal, SpecReady, ProposalReview,
ReadyForImplementation, InProgress, CodeReview, Done, Blocked — plus `unknown` as a first-class
answer, distinct from Backlog, carrying the vendor's reason when it appears. Holding SHALL remain
orthogonal: a held PBI SHALL keep the position the work reached, and holding alone SHALL read as a
hold over the position the read answered. `spec-ready` SHALL still mean the Definition of Ready is
met and the proposal has not landed, its action `/aio:propose`, and the board SHALL render it as
its own lane.

#### Scenario: Two loop labels resolve to the furthest along
- **WHEN** a PBI carries `status:in-progress` and `status:holding`, in either order
- **THEN** the position is in-progress and the PBI reads as held

#### Scenario: An unrecognised status label changes nothing
- **WHEN** a PBI carries a `status:` label outside the loop's vocabulary
- **THEN** it renders as an ordinary chip and the position decided by the recognized labels stays

#### Scenario: spec-ready derives like the rest
- **WHEN** a PBI carries `status:spec-ready` alone
- **THEN** the position is spec-ready, it wins over ready-for-proposal and loses to
  ready-for-implementation, and its lane's action is `/aio:propose`

#### Scenario: No position at the vendor is unknown, not backlog
- **WHEN** a PBI arrives from a vendor that answered no recognized loop state
- **THEN** its status is `unknown` with the vendor's reason, and it is not placed in the backlog lane

### Requirement: A hold is released and never applied from the screen
The system SHALL keep `POST /api/pbis/hold` (`SetPbiHold.cs`, `Access.AnyCaller`) which SHALL only
**clear** a hold: applying one is how the `/aio:*` commands park a PBI, and releasing one is a person
approving what they just read, so `held: true` SHALL be refused rather than written. Releasing SHALL
be made through the Connector — the adapter clears it by its own mechanism, GitHub's being the label
removal — and SHALL change nothing else. A PBI that is not held SHALL write nothing, an unknown
source SHALL be refused, and a vendor that refuses SHALL be reported in its own words.

The body SHALL still be able to name the **repository**, resolved the same way as the position
write: a release is a person saying they have read *this* proposal, and clearing the hold on another
repository's PBI of the same number would let an unread change through.

#### Scenario: Applying a hold is refused
- **WHEN** a request asks to apply a hold
- **THEN** it is refused, and the only vendor write this file makes is the release

#### Scenario: Releasing a hold that is not there writes nothing
- **WHEN** the PBI carries no hold
- **THEN** no vendor write is made

#### Scenario: A release names the repository it means
- **WHEN** two repositories hold the same key and one of them is released
- **THEN** the hold is cleared on that repository's PBI and the other stays held

### Requirement: The next read follows the write, not the write itself
Neither the position write nor the hold write SHALL edit what this product last read: the vendor is
the record, so a change made in this product and one made in a browser SHALL arrive by the same
path. A write that changed something at the vendor SHALL instead empty the PBI snapshot, so the next
read polls the vendor and takes the position from what it answers. The consequence SHALL be visible
rather than hidden: the board draws the move at once and puts the card back if the vendor refuses.

#### Scenario: The labels come back through the next read
- **WHEN** a position is written at the vendor
- **THEN** nothing of it is written into the snapshot, it is emptied, and the next read brings the position back with the vendor's answer

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

### Requirement: Moving a PBI writes the position at the vendor through the Connector
The system SHALL keep `POST /api/pbis/status` (`Features/Inbox/UseCases/SetPbiStatus.cs`,
`Access.AnyCaller`) taking the key **in the body rather than in the path**. The move SHALL be made
as the Connector's position write — one call the adapter translates to its own mechanism — and SHALL
preserve, where the adapter is GitHub: the new label written **before** removing the old one, every
other loop label removed, and `status:holding` left alone. Moving a PBI to the position it already
has SHALL write nothing, a PBI with no position dropped on `backlog` SHALL write nothing, a position
outside the declared vocabulary SHALL be refused, and moving to `unknown` SHALL be refused — it is a
read's answer, not a destination. A PBI neither the snapshot nor its vendor has SHALL be a 404, a
vendor that refuses the write SHALL be reported in its own words, a vendor with no position write
SHALL be refused with the fix named, and a pull request SHALL be written through the GitHub
connector.

The body SHALL still be able to name the **repository**, and the PBI SHALL be resolved by source,
repository and key together, exactly as before.

#### Scenario: The new label lands before the old one leaves
- **WHEN** a PBI moves from ready-for-implementation to in-progress through the GitHub adapter
- **THEN** the vendor receives `status:in-progress` while `status:ready-for-implementation` is still present, and the removal follows

#### Scenario: A hold survives a status move
- **WHEN** a held PBI is moved to another position
- **THEN** `status:holding` is not touched

#### Scenario: The repository disambiguates the write
- **WHEN** two repositories in one scope each carry a PBI with key `#12` and the position of one is moved
- **THEN** only that repository's PBI changes, resolving by source, repository and key together

#### Scenario: A key nobody has is a 404
- **WHEN** the key is neither in the snapshot nor at the vendor
- **THEN** the response is a 404 rather than an error about labels

#### Scenario: A vendor that cannot write the position is refused with a remedy
- **WHEN** the PBI's connector is one whose vendor has no position write
- **THEN** the response is a refusal naming the vendor and the remedy, not a 500

#### Scenario: Moving to unknown is refused
- **WHEN** a request moves a PBI to `unknown`
- **THEN** it is refused before any vendor is called

### Requirement: The list of positions a PBI can move to is threaded through everything that uses one
The position list SHALL be the loop's declared vocabulary — the ten positions plus the hold that
travels beside them — threaded through the wire, the native menu and the vendor's own fields alike,
so that a position added to the declaration is one edit in one place and every consumer follows.
The order SHALL be the loop order, so the board's columns, the native menu and the picker agree
without re-sorting. `unknown` SHALL NOT appear as a destination in any menu: it is a read's answer,
so no picker, menu or drag target SHALL offer it.

#### Scenario: The loop's order is the only order
- **WHEN** a position pull-down is drawn, native or web
- **THEN** its entries are the declared positions in loop order, sourced from the same declaration

#### Scenario: Unknown is not offered as a destination
- **WHEN** a position move is offered anywhere in the UI
- **THEN** `unknown` is not among the choices

