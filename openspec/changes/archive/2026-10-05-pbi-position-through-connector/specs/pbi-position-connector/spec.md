## ADDED Requirements

### Requirement: The position is read through the Connector, and unknown is an answer, not a default
`IPbiSource` SHALL expose a position read — one call per PBI answering the PBI's place in the loop
and whether it is held — implemented by every adapter. The answer SHALL be one of: a known position
with the hold flag, or `unknown` carrying the vendor's own reason. A PBI with no loop state at the
vendor SHALL answer `unknown` ("no position at the vendor"), NOT a default position: a vendor that
was never asked shall not be reported as if it had said `backlog`. A vendor error during the read
SHALL be reported as that error, distinct from both a known position and an unknown answer. The
module above the seam SHALL NOT interpret the vendor's labels, fields or tags to derive the position
it serves.

#### Scenario: A vendor that never spoke loops answers unknown
- **WHEN** a PBI arrives from a vendor whose state carries nothing the adapter recognizes as a loop position
- **THEN** the read answers `unknown` with a reason, and the UI renders it as its own state — not as a `backlog` card

#### Scenario: An unreadable position is not a refusal
- **WHEN** the vendor answers a state value the adapter does not know
- **THEN** the read is `unknown` carrying the raw value in the reason, and not an error, a known position, or a finished PBI

#### Scenario: A vendor failure is neither unknown nor a position
- **WHEN** the vendor refuses or fails the position read
- **THEN** the failure is reported with the vendor's own words, and the PBI is not presented as done or ready to run

### Requirement: The position write is a Connector call each adapter translates
`IPbiSource` SHALL expose a position write — move one PBI to a named loop position — that the
adapter translates to its own mechanism as one operation the adapter owns. Moving to `unknown`
SHALL be refused at the contract, because it is a read's answer rather than a place a PBI can be
moved to. An adapter whose vendor has no position write SHALL refuse with a sentence that names the
remedy rather than fail silently or fake the move.

#### Scenario: A vendor without a position write says so
- **WHEN** a move is attempted through a connector whose vendor does not support writing the position
- **THEN** the refusal names the vendor and the remedy, and arrives as an answer the caller can act on rather than a crash

#### Scenario: A move to unknown is refused
- **WHEN** a caller asks to move a PBI to `unknown`
- **THEN** the request is refused before any vendor is called

### Requirement: GitHub translates the loop's declared vocabulary behind the seam
`GitHubPbiSource` SHALL be the only code that knows `status:kebab` strings. Its translation SHALL
cover every position the loop declares — including `needs-refinement`, `proposal-review` and
`blocked`, which the previous derivation collapsed into `backlog` — and SHALL document the
precedence between them where a PBI carries several: the furthest along SHALL win, and holding SHALL
be read orthogonally from `status:holding` without participating in the precedence. A PBI with no
recognized loop label SHALL answer `unknown` — the vendor never wrote a loop position, and the
adapter SHALL NOT guess one. An unrecognised `status:` label SHALL stay an ordinary chip and change
nothing about the position the recognized labels decided.

#### Scenario: Two loop labels resolve to the furthest along
- **WHEN** a PBI carries `status:in-progress` and `status:holding`, in either order
- **THEN** the position is in-progress, the hold flag is set, and the label order did not decide

#### Scenario: The three statuses the loop declares arrive as themselves
- **WHEN** a PBI carries `status:needs-refinement`, `status:proposal-review` or `status:blocked`
- **THEN** the read answers that position, not `backlog`

#### Scenario: An unrecognised status label changes nothing
- **WHEN** a PBI carries a `status:` label outside the loop's declared vocabulary
- **THEN** it renders as an ordinary chip, and the position is decided by the recognized labels — or answers `unknown` when there are none

#### Scenario: No loop label is unknown
- **WHEN** a GitHub PBI carries no `status:` label at all
- **THEN** the position read answers `unknown` with the reason that the vendor wrote no loop position, and not `backlog`

### Requirement: Azure DevOps implements the contract from its own state, without invented support
`AzureDevOpsPbiSource` SHALL read the work item's state column (`System.State`) and translate it into
the loop's declared vocabulary, mapping each state its process actually uses and answering
`unknown` with the state's raw value whenever a state has no mapping. It SHALL implement the
position write as a write of the state column when a mapping exists for the target position, and
refuse with the remedy when it does not. It SHALL NOT invent fields GitHub uses; the translation is
the adapter's, as GitHub's labels are GitHub's.

#### Scenario: A recognized ADO state answers a position
- **WHEN** a work item's state is one the adapter maps
- **THEN** the position read answers that position and the module never saw a label

#### Scenario: An unmapped state answers unknown with the raw value
- **WHEN** a work item's state belongs to a process the adapter does not map
- **THEN** the read is `unknown` carrying the raw state in the reason, and not a default position

#### Scenario: An unmapped target is a refusal with a remedy
- **WHEN** a move names a position the adapter has no state column value for
- **THEN** the vendor is not written and the refusal names what was missing

### Requirement: Vendors without a position translation answer honestly, both ways
Jira and Linear connectors SHALL answer `unknown` with a reason on the position read and a refusal
naming the remedy on the position write, until their own translation exists. They SHALL NOT be
reported as PBIs in `backlog`, as finished, or as ready to run, and the refusal SHALL be a typed
answer the caller can act on, not an unhandled failure.

#### Scenario: A Jira read that has no translation is unknown
- **WHEN** a PBI's connector is Jira or Linear
- **THEN** the position read answers `unknown` naming that this vendor has no position translation yet

#### Scenario: A Jira move that has no translation is a refusal
- **WHEN** a position move is attempted through the Jira or Linear connector
- **THEN** the move is refused with the remedy, and no label, field or tag is written at the vendor
