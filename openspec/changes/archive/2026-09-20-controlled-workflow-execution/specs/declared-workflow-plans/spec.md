## ADDED Requirements

### Requirement: Controlled execution survives the declaration round trip
A declared Workflow SHALL accept an optional boolean `controlled` field, false when absent. Import, source comparison, export and plan resolution SHALL preserve it. A controlled plan SHALL refuse skipped required steps rather than silently omit them.

#### Scenario: An imported controlled plan is exported
- **WHEN** a plan declaring controlled execution is imported and exported
- **THEN** the exported plan still declares controlled execution

#### Scenario: A legacy plan is imported
- **WHEN** the declaration omits controlled execution
- **THEN** it retains the existing execution semantics
