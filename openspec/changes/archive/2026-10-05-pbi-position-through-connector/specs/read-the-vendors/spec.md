## ADDED Requirements

### Requirement: The snapshot holds the position the vendor answered, never one deduced
The snapshot SHALL carry, per PBI, the position and hold the vendor's answer gave — the Connector's
translation applied to what the vendor actually said — and SHALL NOT hold a position deduced from the
last run, the last write, or the product's own move. A refresh SHALL take the position from the
vendor's fresh answer, so a change made in a browser and one made through this product are
indistinguishable downstream. The read side SHALL NOT write anything at the vendor. A vendor whose
position could not be read, in a read the rest of which succeeded, SHALL contribute the rest of its
answer with the position as `unknown` and the failure named, rather than having its whole answer
dropped or its position defaulted.

#### Scenario: A remote change arrives on the next read
- **WHEN** a position is changed at the vendor outside this product and the snapshot is emptied
- **THEN** the next read polls the vendor and the position shown is what it answered

#### Scenario: The last run does not decide the position
- **WHEN** a PBI's run finished and the vendor still answers an earlier position
- **THEN** the snapshot holds the vendor's answer, and no code path writes a position implied by the run over it

#### Scenario: A vendor that failed its position read keeps the rest of its answer
- **WHEN** a vendor answers its PBIs but the position part of the read fails
- **THEN** those PBIs arrive with position `unknown` and the failure named, and neither the rest of the read nor the other vendors is affected
