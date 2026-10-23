# run-spend Specification

## Purpose
What a window of runs cost, above the History table. A sum over a set containing unknowns is itself partly
unknown and says so: runs whose vendor reported nothing are counted separately rather than folded in as
zero, because a total that silently omits them reads as complete and is not. Reverse-engineered from the
shipped code, not negotiated ahead of it.
## Requirements
### Requirement: A window's spend is aggregated in the database and names its unknowns
The system SHALL expose `GET /api/runs/spend` (`Features/Runs/UseCases/GetSpend.cs`,
`Access.FiltersToCaller`) returning what a window of runs cost, aggregated over rows the product
already owns rather than by re-reading raw transcript events, because tokens are attributed to a
run when the run finishes and the question is therefore a `GROUP BY`. Runs whose vendor reported
nothing SHALL be counted **separately** and reported as unknown rather than as zero. The History
band that consumed it is deleted with the History screen; the query remains spend's owned surface,
scoped where its consumers ask for it.

#### Scenario: Unknowns are reported, not absorbed
- **WHEN** the window contains runs with no reported usage
- **THEN** the query reports their count apart from the total rather than adding zero for each

### Requirement: Usage is read per run from the vendor's own record, keyed and merged
`IAgentUsageReader` SHALL attribute usage to a run from the vendor's record: repeated rows with the same
key SHALL be merged by **maximum** rather than summed, rows with different keys SHALL be summed, a forked
conversation SHALL NOT be counted twice, rows from another directory SHALL be ignored, rows from a
subdirectory of the run's directory SHALL be counted, and rows outside the run's window SHALL be ignored.
Nothing measured SHALL read as null, a missing transcript directory SHALL read as null, and a half-written
line SHALL be skipped rather than fatal.

#### Scenario: A fork is not billed twice
- **WHEN** a run forked a conversation whose rows appear under both sessions
- **THEN** the usage is counted once

#### Scenario: Another directory's rows are not this run's
- **WHEN** the vendor's record holds rows written in a different directory
- **THEN** they are ignored

### Requirement: Cost is priced from the model the vendor reported, and cache tokens are priced
The cost SHALL be priced from the model the vendor reported, resolving a model id by **longest prefix**. An
unknown model SHALL keep its tokens and report **no cost** rather than a wrong one. Cache tokens SHALL be
priced, because they dominate a real run: a cache read costs a tenth of fresh input and a cache write a
quarter more than fresh input. A run costing less than a cent SHALL NOT be rounded to nothing, and nothing
used SHALL cost nothing.

#### Scenario: An unpriced model keeps its tokens
- **WHEN** the reported model has no price
- **THEN** the tokens are kept and no cost is claimed

#### Scenario: A sub-cent run is not zero
- **WHEN** a run cost less than one cent
- **THEN** the reported cost is that amount rather than 0

