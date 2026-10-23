# the-world-seam Specification

## Purpose
Every reach outside this process goes through an interface in `BuildingBlocks/World/`, registered in one file.
A module cannot spawn a process, open a socket or read somebody's home directory, and that is the point:
nothing above the seam knows which implementation it got. Reverse-engineered from the shipped code, not
negotiated ahead of it.
## Requirements
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

### Requirement: The seam's implementations are singletons because they hold process-lifetime state
The registrations SHALL be singletons where they hold process-lifetime state — a shared git runner, the table
of live agent sessions, the window a watching browser is acknowledging — so that state is the process's and not
a request's.

#### Scenario: Live sessions survive the request that made them
- **WHEN** a terminal or an agent session is opened by one request
- **THEN** a later request finds it, because the seam holding it is a singleton

### Requirement: A seam answers null for "cannot say", never a partial truth
`IWorkspaceProbe` SHALL answer null rather than a partial state, so a caller reconciling whole sets can tell
"nothing there" from "could not read". A notification seam SHALL be fire-and-forget: implementations swallow
their own failures, and nothing upstream fails because nobody was listening.

#### Scenario: Unreadable is null, not empty
- **WHEN** git cannot describe a directory
- **THEN** the probe answers null and the caller leaves the stored rows alone

#### Scenario: A failed announcement does not fail the work
- **WHEN** the notifier cannot reach any browser
- **THEN** the reconciliation that called it still succeeds

### Requirement: The commits ahead of the trunk are a separate read, asked where it is displayed

`IWorkspaceProbe` SHALL expose the commits a working directory's branch has that the trunk does not, as a method separate from `Read` and `Position`, answering null rather than a partial list — not a repository, no trunk, or a git that could not be asked are all "cannot say", never an empty list read as "nothing ahead".

A separate call for the same reason `Position` is one: `Read` is what the poller calls on a timer for every folder, and this is a git process per working copy that belongs to the request that displays it, not to the loop that keeps the rail warm.

#### Scenario: The poller's read is untouched

- **WHEN** the poller reads a folder's state on its timer
- **THEN** it calls `Read` and no commit list is read for it

#### Scenario: Unsayable is null, not empty

- **WHEN** the directory is not a repository, or its repository has no trunk, or git refuses
- **THEN** the probe answers null for the commit list, and the caller leaves the screen's sentence alone rather than drawing an empty list as "nothing ahead"

### Requirement: The vendor position is part of the seam
The seam for reading and moving a PBI's place in the loop SHALL be members of `IPbiSource` in
`src/shared/AiOrchestrator.BuildingBlocks/World/` — the position read and the position write — with
their answer types beside the interface, and every implementation SHALL live in
`AiOrchestrator.Infrastructure/PbiSources/` registered through `World.cs`'s `AddWorld()`. No module
SHALL know a vendor's loop vocabulary: `status:kebab` strings, `System.State` values and each
vendor's field names SHALL live only inside their adapter. A new vendor is a new adapter, not a module
change, and the seam's rule that nothing above it knows which implementation it got SHALL hold for
the position exactly as it holds for every other read and write.

#### Scenario: The module asks for a position, not for labels
- **WHEN** a use case needs where a PBI stands
- **THEN** it calls the position read on `IPbiSource`, and there is no code above the seam that maps a label or a field to a position

#### Scenario: A new vendor's positions are one adapter
- **WHEN** a fifth vendor is added
- **THEN** it implements the position read and write like the rest of the interface, and no module, use case or screen changes to teach it the loop

