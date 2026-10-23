## MODIFIED Requirements

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
