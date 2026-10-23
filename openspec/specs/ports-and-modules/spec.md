# ports-and-modules Specification

## Purpose
The control plane is a modular monolith with five ports to the outside. Modules depend on the ports and
on the contracts in BuildingBlocks and on nothing else, so a module cannot spawn a process or call a
vendor by accident, and a new environment, agent or vendor is a new adapter rather than a change to the
loop. Reverse-engineered from the shipped code (`src/shared/AiOrchestrator.BuildingBlocks/`,
`src/shared/AiOrchestrator.Infrastructure/Ports.cs`, `src/shared/AiOrchestrator.ArchitectureAnalyzers/`,
`src/tests/AiOrchestrator.ArchitectureTests/`), not negotiated ahead of it.

## Requirements
### Requirement: Five ports, and their adapters register in their own folders
`BuildingBlocks/Ports` SHALL hold exactly five ports: `IPbiProvider`, `IAgentProvider`, `IEnvironment`,
`ISession` and `IScriptRunner`. `AddPorts()` SHALL register their adapters by calling one registration
per folder of `Infrastructure` (`PbiProviders`, `AgentProviders`, `Environments`, `Sessions`,
`Scripts`). What Workspace asks of a checkout beyond them — `ICheckoutProbe`, `IBranchFiles` and
`IDesktopIntegration` in `BuildingBlocks/Files` — SHALL be registered by `AddFiles()`.

#### Scenario: A sixth port
- **WHEN** an interface is added to `BuildingBlocks/Ports`
- **THEN** `PortTests` fails the gate

### Requirement: Loop and Runs meet at a contract, never at each other
The Loop module SHALL reach Runs only through `IRunRequests` and Workspace only through
`IFolderDirectory`, both in BuildingBlocks; neither module SHALL reference the other, read in the
assembly's references and in its project file.

#### Scenario: Loop references Runs
- **WHEN** a project reference from Loop to Runs is added
- **THEN** `PortTests` and `ModuleBoundaryTests` fail

### Requirement: Modules are discovered, own their tables and cross none
The host SHALL discover `AiOrchestrator.Modules.*.dll` at startup, so adding a module needs no host
edit. Each module SHALL own its tables, its migrations with their own history, and its feature slices;
a reference across modules SHALL be a path or an id, never a foreign key. The analyzers MOD001–005 and
CQS001 SHALL fail the build on a public CQS request, a cross-module reference, a public domain entity, a
controller, or an entity returned by an endpoint.

#### Scenario: An endpoint returns an entity
- **WHEN** a use case returns a domain entity from its route
- **THEN** the build fails with MOD005

### Requirement: Every request declares what it requires, and errors have one body
A use case SHALL be one file with its route, request, response, validator and handler, `internal`.
Requests SHALL pass Logging, then Authorization, then Validation, then the handler; a request that
declares no `[Requires]` SHALL be refused. Domain errors SHALL be `ErrorOr<T>` values mapped by
`ApiResults.Problem` and validation failures rendered by `GlobalExceptionHandler`, both as RFC 7807
`application/problem+json`.

#### Scenario: A request with no declaration
- **WHEN** a command type carries no `[Requires]`
- **THEN** the pipeline refuses it before the handler runs

### Requirement: Only the vendor's adapter knows the vendor
No module and no contract in BuildingBlocks SHALL name the `gh` CLI or a `status:` label; `VendorTests`
SHALL read their sources and fail on either, so another vendor with a state field arrives through the
same port without touching the loop.

#### Scenario: A module filters by label
- **WHEN** a module's code contains `status:in-progress`
- **THEN** `VendorTests` fails the gate naming the file
