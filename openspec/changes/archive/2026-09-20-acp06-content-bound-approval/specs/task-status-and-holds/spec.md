## MODIFIED Requirements

### Requirement: Human Hold release and controlled grant are distinct
The existing Hold-release endpoint SHALL NOT issue a controlled grant. Shared controlled
execution SHALL require both absence of Hold and a valid content-bound owner grant; neither
condition SHALL substitute for the other. Coordinator failure handling SHALL apply Hold
through a separately authorized, journaled effect, not by changing the legacy release
endpoint's semantics — and that effect SHALL carry a typed reason from the closed attention
vocabulary, projected as one attention comment on the task's issue. An unrecognized reason
code SHALL display raw with the hold intact.

#### Scenario: A generic Hold release succeeds
- **WHEN** a person clears the label through the existing endpoint
- **THEN** the label changes but no controlled grant or dispatch authority is created

#### Scenario: A valid grant coexists with Hold
- **WHEN** a grant is valid but the provider still reports Hold
- **THEN** controlled dispatch remains blocked

#### Scenario: A hold the coordinator applied says why
- **WHEN** coordinator failure handling applies a hold through its journaled effect
- **THEN** the effect carries a typed reason, and the task's issue carries exactly one
  attention comment naming it

#### Scenario: An unknown reason is information, not permission
- **WHEN** a reader does not recognize a hold's reason code
- **THEN** it displays the code raw and the hold remains
