## ADDED Requirements

### Requirement: Controlled plan shape is explicit
Version 1 SHALL accept one Agent step followed by resolved one-shot gate commands and a final Hold. It SHALL refuse other shapes before writing a Run. Each step SHALL have a recorded 1800-second deadline. Before checking the candidate or starting a gate, the harness SHALL stop and join the Agent process.

#### Scenario: An unresolved gate is refused
- **WHEN** a controlled plan contains an unresolved or long-running command
- **THEN** no Run or Agent starts

### Requirement: Controlled Agent steps require a correlated result
A controlled Agent step SHALL advance only after its result identifies the current Run, step and challenge. Accepted outcomes SHALL be candidate, needs_input, blocked and failed. Missing, malformed, oversized, unknown or stale results SHALL stop automatic dispatch with a recorded reason. Idle alone SHALL NOT prove success.

#### Scenario: Idle without a result
- **WHEN** an Agent becomes idle without a valid result
- **THEN** no next step executes and the Run records why it is held

#### Scenario: A stale result is rejected
- **WHEN** the result belongs to another Run or step
- **THEN** it cannot advance the current plan

### Requirement: Gates verify the current clean candidate
A candidate SHALL name the independently observed HEAD of a clean working tree. A controlled shell step SHALL execute only against that candidate and SHALL require an observed zero exit code, unchanged HEAD and clean tree at completion. Failure or unknown evidence SHALL NOT count as a pass.

#### Scenario: A command changes the candidate
- **WHEN** a shell command exits zero but HEAD or the working tree changed
- **THEN** validation fails and the next step is not dispatched

### Requirement: Control evidence precedes dispatch
The system SHALL persist the resolved plan and ordered immutable observations and decisions for each controlled Run. A failure to persist required evidence SHALL stop dispatch. A duplicate completion SHALL NOT repeat an effect. Legacy transcript retention SHALL NOT delete control evidence.

#### Scenario: Evidence cannot be saved
- **WHEN** persistence fails before a transition
- **THEN** no next command is sent and the failure is reported

### Requirement: A Hold or interruption cannot silently downgrade control
A controlled Hold SHALL stop automatic execution. An interrupted controlled Run SHALL retain its prior evidence and SHALL NOT automatically replay its steps. Generic conversational resume SHALL refuse a controlled Run unless it can preserve the execution contract.

A controlled Hold SHALL end the Run as `held`, with a finished timestamp and no connected Agent. Legacy `waiting` SHALL retain its live meaning.

#### Scenario: A held Agent reports idle again
- **WHEN** another idle signal arrives after the controlled plan held
- **THEN** no step is dispatched

### Requirement: The stored definition and trace explain the Run
The system SHALL expose a controlled Run's stored plan, definition hash and ordered execution evidence through a read endpoint and the Run screen. The view SHALL distinguish execution, validation failure and Hold, and SHALL identify outcomes, reasons, observed commits and exit codes without parsing prose logs.

#### Scenario: A finished Run is inspected
- **WHEN** the Workflow has changed since a Run ended
- **THEN** inspection still shows that Run's original definition and path
