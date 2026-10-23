# module-boundaries Specification

## Purpose
Nothing crosses a module boundary except a `.Contracts` type, and the build is what says so. Five Roslyn
analyzers and one CQS rule fail the compile; architecture tests catch what an analyzer structurally cannot.
Paths, not foreign keys. Reverse-engineered from the shipped code, not negotiated ahead of it.
## Requirements
### Requirement: Six analyzers fail the build on a crossed boundary
`src/shared/AiOrchestrator.ArchitectureAnalyzers` SHALL report, as build errors on module projects:
**MOD001** a CQS request type declared public rather than internal; **MOD002** a cross-module reference to an
implementation assembly rather than to that module's `.Contracts`; **MOD003** a domain entity deriving from
`Aggregate`/`BaseEntity` declared public; **MOD004** a type deriving from an MVC controller base inside a
module; **MOD005** a public signature exposing a domain entity as a parameter or return type; and **CQS001** a
handler implementing `IAppCommandHandler`/`IAppQueryHandler` that is not `internal sealed`. Each diagnostic
SHALL name the offending type and the remedy in its message.

#### Scenario: A public command fails the compile
- **WHEN** a module declares `public sealed record Command : ICommand<…>`
- **THEN** MOD001 is reported, naming the type and telling the author to make it internal

#### Scenario: A direct cross-module reference fails the compile
- **WHEN** a module references another module's implementation assembly
- **THEN** MOD002 is reported, naming the module and pointing at its `.Contracts`

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

### Requirement: The tests assert their own coverage before they assert the rules
`SurfaceTests` SHALL assert that every module is present to be checked, that every module contributes requests
and handlers, that the permission constants are found where they live, and that some requests name a permission
rather than an access value — so a suite that silently stopped seeing a module fails instead of passing.

#### Scenario: A module missing from the scan is a failure, not a pass
- **WHEN** a module's assembly is not loaded by the test's discovery
- **THEN** the surface test fails rather than the boundary tests passing vacuously

