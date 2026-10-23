## MODIFIED Requirements

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
