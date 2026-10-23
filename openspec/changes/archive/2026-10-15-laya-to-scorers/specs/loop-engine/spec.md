## MODIFIED Requirements

### Requirement: The model server is ensured before the first score of a pass
Before running a state's score, the engine SHALL run `.harness/aio/scorers/laya/laya ensure` in the folder's own
checkout, not on the PBI's branch, when that file exists there, and at most once a pass. A checkout
that has not installed harness 36 has `.harness/aio/laya` instead, and SHALL have that one run
(#759). A pass that runs
no score SHALL NOT run it. An `ensure` that fails SHALL NOT fail or hold anything: it is logged, and the
score answers `unknown` with its reason.

#### Scenario: Two scores in one pass
- **WHEN** two PBIs are in `proposal-review` in the same pass
- **THEN** `laya ensure` runs once, in the folder's checkout, before the first `risk-score`

#### Scenario: laya cannot start
- **WHEN** `laya ensure` exits non-zero and `proposal-review` does not name `unknown`
- **THEN** the PBI is held with `proposal-review produced unknown, which its on does not name: risk-score: no se pudo puntuar: …`

#### Scenario: laya only beside the steps
- **WHEN** the folder's checkout has `.harness/aio/laya` and no `.harness/aio/scorers/laya/laya`, and a pass runs a score
- **THEN** `.harness/aio/laya ensure` runs once, before the first score
