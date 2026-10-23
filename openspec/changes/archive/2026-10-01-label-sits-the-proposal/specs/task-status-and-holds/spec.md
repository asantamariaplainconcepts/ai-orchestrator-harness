# task-status-and-holds Delta

## MODIFIED Requirements

### Requirement: A status is derived from the labels by precedence, and holding is orthogonal
`LoopStatus` SHALL name seven states in loop order — Backlog, ReadyForProposal, SpecReady,
ReadyForImplementation, InProgress, CodeReview, Done — and a task's status SHALL be **derived** from
its `status:` labels rather than stored. Where a task carries two loop labels the furthest along
SHALL win, whichever order they arrive in, because the vendor's array order is not stable. A task
with no `status:` label SHALL be Backlog. Case SHALL NOT decide the status. Holding SHALL NOT be a
member of the enum: it is orthogonal, so a held task SHALL keep the status the work reached, and
holding alone SHALL read as a hold over Backlog. A promoted label SHALL leave the chips while the
vendor's own labels stay, and an unrecognised `status:` label SHALL stay an ordinary chip and change
nothing.

`spec-ready` SHALL mean the Definition of Ready is met and the proposal has not landed: the state
between interrogation and implementation, moved by the refine step and gate for the propose step.
The board SHALL render it as its own lane, and its action SHALL be `/aio:propose`.

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
