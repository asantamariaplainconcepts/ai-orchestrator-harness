## ADDED Requirements

### Requirement: A terminal can be asked to continue a vendor session
`TerminalRequest` SHALL carry an optional vendor session to continue, so a restored terminal resumes
the conversation it was having rather than starting a new one behind the same tab. For an agent
terminal with a session to continue, the line SHALL gain the vendor's own resume arguments
(`AgentDefinition.ResumeArguments`) — pinning a fresh session id and resuming are two different
statements, and the pin mechanism SHALL NOT fire for a resumed terminal. A shell and a command
ignore the field.

#### Scenario: An agent terminal resumed rather than pinned
- **WHEN** an agent terminal is opened carrying a vendor session to continue
- **THEN** the process is spawned with the vendor's resume arguments and without a fresh session pin

#### Scenario: A shell ignores the session to continue
- **WHEN** a shell terminal is opened
- **THEN** the line is the person's login shell and nothing is appended
