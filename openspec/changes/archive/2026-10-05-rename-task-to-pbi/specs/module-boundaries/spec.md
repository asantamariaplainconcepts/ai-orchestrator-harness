# module-boundaries Delta

## MODIFIED Requirements

### Requirement: The architecture tests assert the boundary over the whole solution
`AiOrchestrator.ArchitectureTests/ModuleBoundaryTests` SHALL assert that no module references another module,
no module exports a domain entity, no module exports a command or a query, every handler is internal and
sealed, every use case exposes the routes method the host looks for, every request has exactly one handler, and
no module declares a controller.

The module inventory the architecture tests walk SHALL include the PBI module as
`AiOrchestrator.Modules.Pbis` (previously `AiOrchestrator.Modules.Tasks`): the rename of an assembly
is a fact the whole-solution tests must know, while the analyzers themselves stay name-agnostic —
they glob `AiOrchestrator.Modules.*.dll`, and the renamed assembly keeps matching without an edit.

#### Scenario: A use case without its routes method is caught
- **WHEN** a use case is added without the static routes method the host discovers
- **THEN** the architecture test fails, naming the type

#### Scenario: Two handlers for one request is a failure
- **WHEN** a second handler is written for an existing request
- **THEN** the architecture test fails

#### Scenario: The renamed module is walked like any other
- **WHEN** the architecture tests enumerate the solution's modules
- **THEN** `AiOrchestrator.Modules.Pbis` is among them and `AiOrchestrator.Modules.Tasks` is not

#### Scenario: Module discovery survives the rename
- **WHEN** the AppHost boots after the assembly rename
- **THEN** `ModuleRegistration.Discover()` finds the PBI module through the same `AiOrchestrator.Modules.*` glob
