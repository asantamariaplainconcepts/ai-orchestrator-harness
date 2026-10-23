# run-record-upkeep Specification

## Purpose
Keeping the run table true after the fact: ending the runs nothing is working on any more, and keeping
transcripts from growing without limit. Both are about the record rather than about the work — neither
deletes a run, and neither touches a directory. Reverse-engineered from the shipped code, not negotiated
ahead of it.

## Requirements
### Requirement: A run is ended when its directory is gone or its agent process is not there
`RunReconciliation` SHALL end a run on either of two observations about that one run: the directory its
agent worked in is gone, or the process recorded as its agent can be shown not to be there. The first SHALL
be asked of the disk and the second of the seam allowed to ask about a process, using the pid and start
time the row carries. A run with its worktree and a live agent SHALL be left alone, and a run that already
ended SHALL be left alone however gone its directory is. Ending a run SHALL delete nothing, and an ended run
SHALL have no step still queued or running. Before this existed, a server restart orphaned everything that
was Running or Waiting: 24 rows with no `FinishedAt` against one live agent were measured on this checkout.

#### Scenario: A run whose worktree was removed is ended
- **WHEN** the directory a run worked in no longer exists
- **THEN** the run is ended, and nothing on disk is deleted

#### Scenario: A live agent keeps its run open
- **WHEN** the worktree is there and the recorded process is alive
- **THEN** the run is left alone

#### Scenario: An ended run is not re-ended
- **WHEN** a run already carries a finish time
- **THEN** reconciliation leaves it as it is

### Requirement: A recorded process is only believed when it matches what the row carries
The process check SHALL treat a live process that started with its run as that run's agent, a process older
than the run that claims it as somebody else's, a pid that names nothing as gone, and a process within the
allowed clock skew as still believed.

#### Scenario: A recycled pid is not mistaken for the agent
- **WHEN** the pid is alive but started before the run did
- **THEN** it is not accepted as that run's agent

### Requirement: Transcripts are pruned and runs are not
`RunLogRetention` SHALL delete transcript **lines** and never runs. Only finished runs SHALL be pruned: an
unfinished run SHALL never be pruned however long it has been running. A finished run past the cutoff SHALL
keep no transcript, a recent finished run within the cap SHALL be untouched, an oversized run SHALL keep its
newest lines, and a transcript larger than one batch SHALL be fully pruned. Pruning SHALL leave the run row,
which is the History table's whole content.

#### Scenario: An unfinished run is never pruned
- **WHEN** a run has been going for longer than the cutoff
- **THEN** its transcript is untouched

#### Scenario: Pruning keeps the run and the newest lines
- **WHEN** a finished run is oversized
- **THEN** its newest lines survive, the older ones go, and the run row remains

### Requirement: Another module's question about live runs is answered from the table that owns them
`RunningWorkflowProbe` SHALL be the only place another module's question about live runs is answered, where
unfinished means Running **or** Waiting: a waiting run is stopped at a hold, which is exactly the run a
person will come back to, so a hold SHALL count as in use as loudly as an agent mid-sentence. A run for
another workflow SHALL NOT be this workflow's problem, a finished run SHALL hold nothing, and nothing running
SHALL be null rather than an answer. The probe SHALL be answerable by workflow key and by id both.

#### Scenario: A run waiting at a hold still holds its workflow
- **WHEN** a run is parked at a hold and its workflow is asked about
- **THEN** the workflow reads as in use

#### Scenario: A finished run holds nothing
- **WHEN** every run for a workflow has finished
- **THEN** the probe answers null
