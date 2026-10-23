# read-a-run delta

## MODIFIED Requirements

### Requirement: One query answers history, a folder's runs, and what is running now
The system SHALL expose `GET /api/runs` (`Features/Runs/UseCases/ListRuns.cs`, `Access.FiltersToCaller`)
as one query serving a worktree's journey header and sidebar answers filtered by folder and by
working directory, and — filtered to what has not finished — what is running right now. The
unscoped History screen is deleted; the query's unscoped shape remains available to spend
reporting. The rail SHALL match those against its branches by working directory: a run knows the
directory it works in, a branch is a directory, and that join is the liveness. The transcript SHALL
be excluded by a **projection** rather than by omission, because the log is an owned collection
that EF would otherwise fetch with the run, and a history of a hundred runs would carry every line
any agent ever printed to draw a table that shows none of them.

#### Scenario: The history table does not fetch the transcript
- **WHEN** a worktree's screen loads
- **THEN** no transcript lines are read

#### Scenario: The journey header does not fetch the transcript
- **WHEN** a worktree's screen loads
- **THEN** no transcript lines are read

#### Scenario: The rail lights the branch a run is working in
- **WHEN** a run is working in a worktree
- **THEN** the sidebar row for that branch says a run is working in it

## REMOVED Requirements

### Requirement: One run is read in full before the screen subscribes
**Reason**: the full-screen Run screen is deleted with its route; a run is read embedded in a
worktree's screen. `GET /api/runs/{id}` stays — the embedded `RunView` loads exactly this: the
steps rail's data as the transition's record, the bounded transcript tail and the diff, before it
subscribes to the live stream.
**Migration**: the requirement moves to `worktree-is-a-loop`'s embedded surface; the endpoint and
its payload are unchanged.

### Requirement: The screen tracker parses the opcodes the agents actually emit
**Reason**: unchanged in code and kept — but the requirement's scenarios describe the full-screen
replay surface this change deletes. `ScreenFrame` and its tracker are backend behaviour serving the
embedded `RunView` identically; retiring the tracker is explicitly out of scope here and stays a
candidate on its own ticket.
**Migration**: none — no code is deleted; only this spec's placement of the reading surface moves.
