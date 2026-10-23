# task-status-and-holds Delta

## MODIFIED Requirements

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
