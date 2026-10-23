# run-spend delta

## MODIFIED Requirements

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
