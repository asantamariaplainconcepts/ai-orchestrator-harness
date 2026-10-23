## MODIFIED Requirements

### Requirement: Controlled execution survives the declaration round trip
A declared Workflow SHALL accept an optional boolean `controlled` field, false when absent. Import,
source comparison, export and plan resolution SHALL preserve it. A controlled plan SHALL refuse skipped
required steps rather than silently omit them.

A transition-target workflow — the one a lifecycle machine's `startRun` names — SHALL exist as a
declared controlled plan at the moment it is resolved, carrying the shape the controlled
admission demands: one agent step first, one-shot gates after it, a final hold. This
repository's transition workflow is `aio-implement-verified` and lives beside the lifecycle file
that names it.

#### Scenario: An imported controlled plan is exported
- **WHEN** a plan declaring controlled execution is imported and exported
- **THEN** the exported plan still declares controlled execution

#### Scenario: A legacy plan is imported
- **WHEN** the declaration omits controlled execution
- **THEN** it retains the existing execution semantics

#### Scenario: A transition's workflow carries the controlled shape
- **WHEN** a lifecycle transition's named workflow is resolved
- **THEN** the plan exists, is controlled, and the admission accepts its shape — one agent step,
  resolved one-shot gates, a final hold
