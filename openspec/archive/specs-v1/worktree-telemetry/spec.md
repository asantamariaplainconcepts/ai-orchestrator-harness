# worktree-telemetry Specification

## Purpose
What a worktree cost, what its agent was actually spawned with, and where to pick its conversation up outside
this app. A rollup rather than a new measurement: every number is already on the wire per run, and no screen
added them up. Reverse-engineered from the shipped code, not negotiated ahead of it.

## Requirements
### Requirement: The tab totals what the runs of one working directory already reported
`TelemetryTab` SHALL sum, over the runs of the working directory in scope, what each run already carries — its
cost, its input, output, cache-read and cache-write tokens, and the two stamps on each step — and SHALL present
them as totals, because the Runs tab shows three runs' costs side by side in a list and a list is not a total.
Where nothing was measured the tab SHALL say so rather than show zeros, and the clock SHALL be summed across
the steps that ran.

#### Scenario: Three runs become one total
- **WHEN** a worktree has three finished runs with reported costs
- **THEN** the tab shows their sum, broken down by token kind

#### Scenario: Nothing measured says nothing, not zero
- **WHEN** no run in the directory reported usage
- **THEN** the breakdown says there is nothing to break down

### Requirement: The command line shown is the one that was actually spawned
The tab SHALL read the agent's command line out of the run's own log rather than rebuilding it, so what is
shown is what was spawned at the time and not what the agent catalogue would build today.

#### Scenario: An old run shows its own command line
- **WHEN** the agent's flags have changed since a run happened
- **THEN** the tab shows the line that run was started with

### Requirement: The session id and its resume line are for leaving the app
The tab SHALL show the vendor session id and the resume line a developer would type in their own terminal, and
SHALL NOT be a second Resume button — the Runs tab already has one. This surface is for leaving the app, not
for staying in it.

#### Scenario: A developer picks the conversation up in their own terminal
- **WHEN** a run recorded a vendor session
- **THEN** the tab shows the id and the exact resume line for that vendor
