# read-a-finished-transcript Specification

## Purpose
What a finished run actually said, read from the record its agent kept rather than from the bytes its
terminal produced. An agent CLI's TUI overwrites its viewport instead of scrolling it, so what scrolled off
the agent's screen was never in the byte stream as history and no transform of the stored lines recovers it
(ADR-0002). Reverse-engineered from the shipped code, not negotiated ahead of it.

## Requirements
### Requirement: The transcript is its own endpoint, for finished runs
The system SHALL expose `GET /api/runs/{id:guid}/transcript`
(`Features/Runs/UseCases/GetRunTranscript.cs`, `Access.FiltersToCaller`) beside the run detail rather than
inside it, because it reaches out of the process to a vendor's file or CLI and only a finished run has
anything to answer with — folding it into the detail would make every open of a running run pay for a read
that cannot succeed. The measurement behind ADR-0002 SHALL stand as the reason: 856 absolute cursor moves
and no line feeds across 90.0% and 99.2% of two real transcripts.

#### Scenario: Opening a live run does not pay for the read
- **WHEN** the Run screen loads a run that is still going
- **THEN** the detail is returned without reaching for the vendor's record

### Requirement: The vendor's record is folded into the conversation the screen reads
`IAgentTranscriptReader` SHALL turn the vendor's record into the roles the screen renders: rows that are
not the conversation SHALL be left out, every block SHALL become its role, a tool call SHALL be folded to
its name and arguments, a tool result SHALL keep one line and say how many there were, a failed tool SHALL
say so, and a subagent's own conversation SHALL NOT be interleaved. Text parts SHALL become the
conversation with the bookkeeping dropped, and one tool part SHALL become a call and a result. This same
reader is the source of the chat view of a live session — the structured-record view the `Session` seam
holds beside the raw-stream terminal view — so the chat a person reads while a run is live and the
transcript they read after it ends come from the one reader, not two.

#### Scenario: A tool result is summarised rather than pasted
- **WHEN** a tool returned many lines
- **THEN** the transcript keeps one line and says how many there were

#### Scenario: A subagent stays out of the thread
- **WHEN** the record contains a subagent's conversation
- **THEN** it is not interleaved into the run's own

#### Scenario: A live chat and a finished transcript read the same record
- **WHEN** a run's chat view is open while the run is live, and the run later finishes
- **THEN** both the live chat view and the finished-transcript read come from `IAgentTranscriptReader` over the same record
### Requirement: An inherited conversation is counted, not replayed
A resumed or forked run SHALL say how many turns it inherited, including a turn that ended moments before
the run began. A run that continued nothing SHALL inherit nothing, and a record entirely older than the
run SHALL mark nothing.

#### Scenario: A forked session says what it inherited
- **WHEN** a run was forked from a previous session
- **THEN** the transcript states the number of inherited turns

#### Scenario: An unrelated record marks nothing
- **WHEN** the record predates the run entirely
- **THEN** no turns are claimed as inherited

### Requirement: A record that is missing or broken reads as nothing, never as a failure
A run with no record SHALL read as null, a record with no conversation in it SHALL read as null, output that
is not an export SHALL read as null, and a half-written line SHALL be skipped rather than fatal.

#### Scenario: A half-written line does not lose the transcript
- **WHEN** the last line of the vendor's file is incomplete
- **THEN** it is skipped and the rest is returned

#### Scenario: No record is null, not an error
- **WHEN** the vendor kept no record for the run
- **THEN** the answer is null and the screen says there is nothing to show
