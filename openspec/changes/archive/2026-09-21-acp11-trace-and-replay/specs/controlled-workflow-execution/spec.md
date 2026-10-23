## MODIFIED Requirements

### Requirement: The stored definition and trace explain the Run
The system SHALL expose a controlled Run's stored plan, definition hash and ordered execution evidence through a read endpoint and the Run screen. The view SHALL distinguish execution, validation failure and Hold, and SHALL identify outcomes, reasons, observed commits and exit codes without parsing prose logs.

The Run's dispatch SHALL also carry the decision that created it: the loop facts and the machine
snapshot that decided them, stored with the dispatch at admission — never re-read from the live
declaration afterwards. The read endpoint SHALL expose that decision as evaluated facts: the
recognised state, each guard with the value it held, and the effect that fired. Editing the
declaration SHALL NOT rewrite a finished Run's snapshot, exactly as it does not rewrite its
definition.

#### Scenario: A finished Run is inspected
- **WHEN** the Workflow has changed since a Run ended
- **THEN** inspection still shows that Run's original definition and path

#### Scenario: The Run's authorising decision is inspectable
- **WHEN** a Run the loop started is inspected
- **THEN** the answer names the facts it was decided on, the state they recognised, the guards
  with their held values, and the effect that fired — read from the dispatch's snapshot, not
  from the live declaration

#### Scenario: The declaration changes after the Run ended
- **WHEN** `.harness/lifecycle.json` is edited after a Run ended
- **THEN** the Run's reported decision is unchanged
