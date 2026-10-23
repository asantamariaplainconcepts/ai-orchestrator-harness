# the-world-seam Delta

## MODIFIED Requirements

### Requirement: Every outside reach is an interface in BuildingBlocks, implemented in Infrastructure
The seam SHALL be the set of interfaces in `src/shared/AiOrchestrator.BuildingBlocks/World/` — git and
the working copy (`IWorkspaceProbe`, `IWorktreeManager`, `IRepositoryGuard`), the harness adoption
(`IHarnessAdopter`), the agent CLIs (`IAgentRunner`, `IAgentChat`,
`IAgentModelCatalog`, `IAgentSessionReader`, `IAgentTranscriptReader`, `IAgentUsageReader`,
`IAgentHookListener`, `IRunLauncher`), the vendors (`IPbiSource`, `IPullRequestLookup`), the machine
(`ICommandRunner`, `ITerminals`, `IPastedFiles`, `IDesktopIntegration`), the declared files
(`IDeclaredCommandFile`, `IDeclaredWorkflowFile`, `IWorkflowPlanReader`), and the notifications out
(`IWorkspaceNotifier`, `ICheckSink`, `IRunningWorkflowProbe`). Every implementation SHALL live in
`AiOrchestrator.Infrastructure` and SHALL be registered in `World.cs` through `AddWorld()`, which is
**the whole seam**: nothing above it knows which implementation it got, and modules would not compile
differently.

`IRepositoryGuard` SHALL be the only write to a repository's hook configuration — writing
`.githooks/pre-commit` and setting `core.hooksPath` — and `IHarnessAdopter` SHALL be the only write of
the harness manifest's files to a checkout. Both SHALL answer results rather than throw, for
`IWorktreeManager`'s reason: every failure is one a person acts on, and an exception would flatten it.

The rename from `ITaskSource` to `IPbiSource` SHALL be a name change in place: the interface's shape,
its doc comments' rules and every implementation choice stay, `World.cs` keeps its append-only order,
and no implementation moves between files.

#### Scenario: A module reaches the outside only through an interface
- **WHEN** a module needs git, a process, a vendor or the desktop
- **THEN** it takes the interface from BuildingBlocks, and the implementation is chosen in `World.cs`

#### Scenario: A new seam is one registration
- **WHEN** an implementation is swapped or added
- **THEN** the change is a line in `World.cs` and no module changes

#### Scenario: The trunk guard is one seam, consulted not implemented
- **WHEN** a use case needs a repository's hook installed
- **THEN** it takes `IRepositoryGuard` from BuildingBlocks, and `GitRepositoryGuard` is chosen in
  `World.cs`

#### Scenario: The vendor seam is named what it reads
- **WHEN** code or docs name the interface every module reads the vendor through
- **THEN** it says `IPbiSource`, and `World.cs` registers it where `ITaskSource` stood
