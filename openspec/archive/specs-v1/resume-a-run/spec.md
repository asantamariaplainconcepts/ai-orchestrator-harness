# resume-a-run Specification

## Purpose
Carries a finished run's conversation into a new one — continuing yesterday's thread instead of explaining
it again. A resume is a **new** run: what happened yesterday still happened, and the history table is an
audit trail rather than a working copy. Reverse-engineered from the shipped code, not negotiated ahead of
it.

## Requirements
### Requirement: A resume is a new run linked back to the old one
The system SHALL expose `POST /api/runs/{id:guid}/resume` (`Features/Runs/UseCases/ResumeRun.cs`,
`Access.AnyCaller`), which SHALL create a **new** run rather than reopening, extending or editing the
finished one. The new run SHALL link back through `ResumedFromRunId` and SHALL keep its own steps,
transcript and cost.

#### Scenario: The old run is untouched
- **WHEN** a finished run is resumed
- **THEN** a new run exists linking back to it, and the original row's transcript, steps and cost are
  unchanged

### Requirement: A resume carries the session the run recorded, or it is refused
A resume SHALL carry forward the vendor session id the finished run recorded, and a run with **no**
recorded session SHALL be refused rather than resumed against a guess. The resume SHALL hand the session
to the vendor through the `Session` seam (`ISession`) rather than directly through the `IAgentRunner`
PTY path, so the session a resume opens is the same surface a fresh run opens.

#### Scenario: A run with no recorded session is refused
- **WHEN** the finished run recorded no vendor session
- **THEN** the resume is refused, naming why

#### Scenario: The recorded session is what is carried
- **WHEN** the finished run recorded a session id
- **THEN** that id is what the resume hands to the vendor, through the `Session` seam
### Requirement: The agent is asked to fork, so accounting stays separate
Where the vendor supports it, the resume SHALL ask the agent to **fork** the conversation rather than
merely continue it — Claude Code receives `--fork-session --session-id <newRunId>` — because a plain
resume would have the agent write into the previous run's session, the two runs would share a transcript,
and the same tokens would be reported twice, once against each. A vendor that cannot fork SHALL share the
record, which is worse but honest. Each vendor SHALL be resumed in its own spelling, an agent this machine
has no adapter for SHALL produce no command, and the command line SHALL start with the binary a person
would type.

#### Scenario: Claude forks into the new run
- **WHEN** a Claude Code run is resumed
- **THEN** the command forks the session under the new run's id

#### Scenario: A vendor with no adapter is not resumed
- **WHEN** the run's agent has no adapter on this machine
- **THEN** no resume command is produced

#### Scenario: A vendor that only continues shares the record
- **WHEN** the vendor cannot fork a session
- **THEN** the resume continues in the recorded session and the shared record is what the product reports
