# read-a-run Delta

## MODIFIED Requirements

### Requirement: One query answers history, a folder's runs, and what is running now

The system SHALL expose `GET /api/runs` (`Features/Runs/UseCases/ListRuns.cs`,
`Access.FiltersToCaller`) as one query serving a worktree's journey header and sidebar answers
filtered by folder and by working directory, and — filtered to what has not finished — what is running
right now. The unscoped History screen is deleted; the query's unscoped shape remains available to
spend reporting. The rail SHALL match those against its branches by working directory: a run knows
the directory it works in, a branch is a directory, and that join is the liveness. The transcript SHALL
be excluded by a **projection** rather than by omission, because the log is an owned collection that
EF would otherwise fetch with the run, and a history of a hundred runs would carry every line any
agent ever printed to draw a table that shows none of them.

A run row the PBI detail matches to its PBI SHALL join by the PBI's **full identity** — the source,
repository and key — and not by key alone: with two repositories in one scope, `#12` is two PBIs, and
first-match by key would offer the other repository's run as this PBI's execution. A run row carries
its folder's path rather than a repository, so the join resolves the run's folder to the repository it
names and compares that against the repository the PBI's own address names, the way every folder
question on the screen is already answered. Where the PBI cannot be attributed to a repository at all,
the entry SHALL say so rather than offering a run whose repository is unknown as if it were confirmed.
The run DTOs' identifier for the PBI a run belongs to SHALL be spelled `key`, the context carried by
the DTO it sits in rather than repeated in the field's name.

#### Scenario: The history table does not fetch the transcript

- **WHEN** a worktree's screen loads
- **THEN** no transcript lines are read

#### Scenario: The journey header does not fetch the transcript

- **WHEN** a worktree's screen loads
- **THEN** no transcript lines are read

#### Scenario: The rail lights the branch a run is working in

- **WHEN** a run is working in a worktree
- **THEN** the sidebar row for that branch says a run is working in it

#### Scenario: A run joins its task by identity

- **WHEN** two repositories in one scope each carry a PBI with the same key and only one has a run
- **THEN** the PBI detail of the repository with the run offers it, and the other PBI's detail does not

#### Scenario: A run whose task names no repository is not a confirmed match

- **WHEN** a PBI cannot be attributed to a repository
- **THEN** the execution entry names the absence of an attribution, rather than offering any run as confirmed by identity
