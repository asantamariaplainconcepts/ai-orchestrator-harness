# choose-an-agent Specification

## Purpose
Which agent CLI a folder starts with, whether that binary answers right now, what models its vendor will
accept, and what it may do without asking. An Agent here is a CLI in a terminal, and every fact about it is
a fact about this machine at this instant. Reverse-engineered from the shipped code, not negotiated ahead of
it.

## Requirements
### Requirement: The agent list carries health, asked per request
The system SHALL expose `GET /api/agents` (`Features/Agents/UseCases/ListAgents.cs`,
`Access.FiltersToCaller`) returning the agents the settings screen shows, each with whether its binary
answers **right now**. Health SHALL be asked per request rather than stored: somebody signs out, somebody
upgrades, and a cached answer is how a run fails three steps in for a reason the settings screen called
fine.

#### Scenario: Health is current, not remembered
- **WHEN** an agent's CLI stops answering and the settings screen is loaded
- **THEN** that agent is reported as not answering

### Requirement: A vendor's model list is a separate request, made when the picker opens
The system SHALL expose `GET /api/agents/{key}/models` (`GetAgentModels.cs`, `Access.FiltersToCaller`) as
its own request rather than a field on the agent list, because the list already costs a process spawn per
agent and this can cost a real network round trip on top — `opencode models` alone measured 3.8 seconds. It
SHALL be asked only for the one agent a person is about to start, when the picker actually opens.

#### Scenario: Loading settings does not price every vendor's model list
- **WHEN** the settings screen loads
- **THEN** no model list is fetched until a picker opens for a specific agent

### Requirement: A folder's default agent can be changed, and an unknown id degrades to no button
The system SHALL expose `PUT /api/folders/{id:guid}/agent` (`Features/Folders/UseCases/SetFolderAgent.cs`,
`workspace:configure`) writing `Folder.DefaultAgentId`, which before it could only be set when the folder
was added. The id SHALL NOT be validated against the agent list, deliberately: agents live in the Runs
module and a cross-module read is what MOD002 refuses. What makes that safe is on the other side — the
folder screen's agent button renders nothing for an agent that is not configured, so an unknown id degrades
to no button rather than to a terminal running something that is not there. Naming an agent for a folder
that is gone SHALL be a not-found.

#### Scenario: A folder's agent is changed after it was added
- **WHEN** a folder added with one agent is pointed at another
- **THEN** the folder screen and the terminal resolve the new one

#### Scenario: An unknown agent id draws nothing
- **WHEN** the stored agent id names an agent this machine has not configured
- **THEN** the folder screen renders no agent button for it

### Requirement: A permission mode is passed in the vendor's own spelling or dropped
The permission mode SHALL be sent in the vendor's own spelling, the default SHALL be the line this product
has always sent, an empty mode SHALL be the default rather than an empty flag, a mode the vendor never named
SHALL be dropped rather than sent, and a vendor with no spelling for it SHALL be left untouched. The modes
offered SHALL be the ones the binary lists.

#### Scenario: An unknown mode is dropped rather than passed through
- **WHEN** a mode arrives that the vendor never named
- **THEN** the agent is started without it

#### Scenario: The offered modes come from the binary
- **WHEN** the picker lists permission modes for an agent
- **THEN** they are the ones that agent's binary lists

### Requirement: A vendor session id is issued only where the vendor takes one
A vendor that takes a session id SHALL be given one on a fresh run; a vendor that issues its own SHALL NOT
be given one it did not issue. On resume each vendor SHALL be spelled its own way — one joins the value to
its flag, one forks into the new run, one forks without naming the new session. A listed session SHALL match
only in the run's directory and window, the newest session in the window SHALL win, a trailing separator
SHALL NOT stop a match, a symlinked parent directory SHALL still match, and anything unreadable SHALL be
unknown rather than a failure.

#### Scenario: OpenCode is not handed an id it did not issue
- **WHEN** a fresh OpenCode run starts
- **THEN** no session id is passed to it

#### Scenario: The newest session in the window wins
- **WHEN** several vendor sessions exist for the run's directory inside its window
- **THEN** the newest is the one attributed to the run
