## ADDED Requirements

### Requirement: A requested Workflow must exist
If a Run request names a Workflow that cannot be resolved in its Folder, the system SHALL refuse it before creating a Run or Worktree. It SHALL NOT silently substitute a freeform prompt.

#### Scenario: A removed Workflow is requested
- **WHEN** a caller names an unavailable Workflow
- **THEN** the request is refused with a remedy and no Agent starts

## MODIFIED Requirements

### Requirement: A plan advances one step at a time, and a hold stops it
A run with a workflow SHALL start on its plan's first step, and a run with no plan SHALL do nothing at all. For legacy plans, going idle SHALL end the current step and send the next. For opt-in controlled plans, going idle SHALL only request validation of the current step's correlated result; the controlled execution contract SHALL determine whether another step is permitted. A step that holds SHALL stop the run there and nothing SHALL move after a hold. The last step ending SHALL finish the plan exactly once. An unresolved step this product would run itself SHALL stop the plan rather than being skipped. Stopping a run SHALL end its plan wherever it was, and an ended run SHALL have no step still queued or running.

#### Scenario: A hold parks the run
- **WHEN** the plan reaches a step that holds
- **THEN** the run waits there and no further step is sent

#### Scenario: The plan finishes once
- **WHEN** the last step ends
- **THEN** the plan is finished a single time

#### Scenario: Stopping settles the plan
- **WHEN** a run is stopped mid-plan
- **THEN** its plan ends where it was, with nothing left queued

#### Scenario: Controlled idle is not success
- **WHEN** an Agent in a controlled plan becomes idle
- **THEN** the next step is withheld until the result has been validated
