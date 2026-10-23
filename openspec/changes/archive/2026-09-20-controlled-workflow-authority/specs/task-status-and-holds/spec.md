## ADDED Requirements

### Requirement: Display precedence does not authorize controlled dispatch
Existing status derivation and legacy label-write behavior SHALL remain display/manual
contracts. A controlled evaluator SHALL independently require one recognized phase from a
fresh provider read, and SHALL reject multiple phase labels, unknown phases or unknown Hold
state. Display precedence and mirror defaults SHALL NOT be used as authorization.

#### Scenario: A card can render while dispatch is blocked
- **WHEN** the mirror renders the furthest phase but the provider has two phase labels
- **THEN** the card's display does not authorize a controlled transition

### Requirement: Human Hold release and controlled grant are distinct
The existing Hold-release endpoint SHALL NOT issue a controlled grant. Shared controlled
execution SHALL require both absence of Hold and a valid content-bound owner grant; neither
condition SHALL substitute for the other. Coordinator failure handling SHALL apply Hold
through a separately authorized, journaled effect, not by changing the legacy release
endpoint's semantics.

#### Scenario: A generic Hold release succeeds
- **WHEN** a person clears the label through the existing endpoint
- **THEN** the label changes but no controlled grant or dispatch authority is created

#### Scenario: A valid grant coexists with Hold
- **WHEN** a grant is valid but the provider still reports Hold
- **THEN** controlled dispatch remains blocked

### Requirement: Local ending and provider Hold are observed independently
The system SHALL distinguish a locally held Run, a confirmed shared Hold and a pending or
unknown Hold publication. A local state or successful intent write SHALL NOT claim the
provider changed. A failed dispatch SHALL remain blocked across restart even when Hold
publication fails.

#### Scenario: The provider refuses the Hold
- **WHEN** local execution stops but GitHub refuses or cannot confirm the Hold write
- **THEN** the local failure remains blocking with a visible publication reason
