## ADDED Requirements

### Requirement: Controlled dispatch has a durable identity before effects
The system SHALL accept optional effectId, folderId and canonical Task identity for local authorized controlled StartRun. It SHALL commit the immutable canonical request, fingerprint, full Task identity, WorkflowPlan snapshot and reserved RunId before Worktree creation or Agent launch. EffectId and RunId SHALL have database uniqueness.

#### Scenario: Concurrent identical requests
- **WHEN** concurrent requests submit the same effect and command
- **THEN** one durable binding and at most one Run and launch are created
- **AND** all other callers observe that binding without executing effects

#### Scenario: Payload changes
- **WHEN** an authorized caller reuses an effect in the same scope with changed Folder path, Task identity or launch input
- **THEN** the request returns 409 and performs no effects

#### Scenario: Disk refuses admission
- **WHEN** admission persistence fails
- **THEN** no Worktree is ensured and no Agent is launched

### Requirement: Dispatch recovery observes uncertainty rather than replaying
The system SHALL expose a permission-scoped query by effectId. Pending admission, launch intent, process observation and refusal SHALL be distinct. Launch returning SHALL NOT count as process observation. A Run and launch intent SHALL commit together before launch. Recovery and duplicate delivery SHALL never replay an Agent or Worktree effect.

#### Scenario: Response lost
- **WHEN** the caller loses the start response
- **THEN** querying effectId returns the original Run binding and current observation

#### Scenario: Crash during pending admission or launch intent
- **WHEN** admission was persisted but execution cannot be proved
- **THEN** recovery reports uncertainty and duplicate delivery does not start an Agent

#### Scenario: Refused after admission
- **WHEN** Worktree setup or controlled plan resolution is refused after admission
- **THEN** the original binding retains the refusal code and reason and cannot be retried

### Requirement: Idempotency is not execution authority
Admission SHALL require the current caller's Runs start permission on an authoritatively matched Folder. Recovery SHALL require Runs read permission and match saved Folder scope. Full Task identity SHALL agree with the TaskKey passed to the Run. The primitive SHALL NOT grant coordinator authority, mutate Task/Hold state, enable automated shared dispatch, or bypass controlled resume restrictions.

#### Scenario: Wrong scope
- **WHEN** a caller authorized on a different Folder queries or reuses an effect
- **THEN** it receives 404 without the original payload or conflict detail

#### Scenario: Permission denied
- **WHEN** the caller lacks the applicable Folder permission
- **THEN** the operation is refused before dispatch lookup or side effects

#### Scenario: Legacy start
- **WHEN** the request has no effect envelope
- **THEN** existing manual start behavior is preserved

#### Scenario: Configuration changed after admission
- **WHEN** an exact duplicate arrives after Workflow configuration changes
- **THEN** it returns the stored binding without rereading or executing a new plan
