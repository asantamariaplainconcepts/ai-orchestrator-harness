## ADDED Requirements

### Requirement: A recorded decision can be replayed without effects
A Run whose dispatch carries a decision snapshot SHALL be re-evaluated against any candidate
machine — the stored one or one supplied in the request — by a simulation that launches zero
processes, writes nothing to any provider, and reads nothing but the stored snapshot and the
supplied machine. The answer SHALL be a decision diff: what the stored machine decided, what the
candidate would decide, and the guards whose values differ. A fact the replay cannot re-derive
from the snapshot (a live-run observation) SHALL be named as not re-evaluable with its reason, not
silently fixed. A Run without a snapshot SHALL answer `not reproducible` with its reason — never
an empty diff, never a simulation over invented facts.

#### Scenario: Same inputs reproduce the same decision
- **WHEN** the stored machine is replayed against the stored facts
- **THEN** the answer is the decision that was recorded, and the diff is empty

#### Scenario: A candidate machine shows its differences without cost
- **WHEN** a candidate declaration is replayed against a recorded decision
- **THEN** the answer names every transition that would change, no process is launched, and no
  provider is written

#### Scenario: A Run with no snapshot is said, not simulated
- **WHEN** the Run predates decision snapshots or was started by hand
- **THEN** the answer is `not reproducible` naming the reason, never an empty graph

#### Scenario: A live fact the snapshot cannot re-derive
- **WHEN** the replay reaches a fact that was observed of the running world
- **THEN** the answer names it as not re-evaluable with its reason, rather than assuming either
  value
