## MODIFIED Requirements

### Requirement: Every outside reach is an interface in BuildingBlocks, implemented in Infrastructure

The seam SHALL include these active interfaces in `src/shared/AiOrchestrator.BuildingBlocks/World/`:
git and the working copy (`IWorkspaceProbe`, `IWorktreeManager`, `IRepositoryGuard`), harness
adoption (`IHarnessAdopter`), the agent CLIs (`IAgentRunner`, `IAgentModelCatalog`,
`IAgentSessionReader`, `IAgentTranscriptReader`, `IAgentUsageReader`, `IAgentHookListener`,
`IRunLauncher`), vendors (`ITaskSource`, `IPullRequestLookup`), the machine (`ICommandRunner`,
`ITerminals`, `IPastedFiles`, `IDesktopIntegration`), declared files (`IDeclaredCommandFile`,
`IDeclaredConfigFile`, `IDeclaredLoopFile`, `IWorkflowPlanReader`), shared control
(`IControlRegistry`), launch observations (`ILaunchFacts`, `IReviewEvidence`), watcher settings
(`IWatcherControl`) and notifications out (`IWorkspaceNotifier`, `ICheckSink`,
`IAttentionCommentWriter`). Retired contracts for removed modules SHALL not remain in
that inventory. Every outside-world implementation SHALL live in `AiOrchestrator.Infrastructure`
and SHALL be registered in `World.cs` through `AddWorld()`, which is the whole seam: nothing above
it knows which implementation it got, and modules would not compile differently.

`IRepositoryGuard` SHALL be the only write to a repository's hook configuration — writing
`.githooks/pre-commit` and setting `core.hooksPath` — and `IHarnessAdopter` SHALL be the only write
of the harness manifest's files to a checkout. Both SHALL answer results rather than throw, for
`IWorktreeManager`'s reason: every failure is one a person acts on, and an exception would flatten it.

#### Scenario: A module reaches the outside only through an interface
- **WHEN** a module needs git, a process, a vendor or the desktop
- **THEN** it takes the interface from BuildingBlocks, and the implementation is chosen in `World.cs`

#### Scenario: A new seam is one registration
- **WHEN** an implementation is swapped or added
- **THEN** the change is a line in `World.cs` and no module changes

#### Scenario: The trunk guard is one seam, consulted not implemented
- **WHEN** a use case needs a repository's hook installed
- **THEN** it takes `IRepositoryGuard` from BuildingBlocks, and `GitRepositoryGuard` is chosen in `World.cs`
