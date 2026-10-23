## Why

Issue **#295**, from epic **#288**. The fifth and last step of the change tab, and the one that asks
whether the change was *used* rather than read.

The loop stops for a person and hands them a diff. The application of that branch can be started —
`Start the app` is declared in `.harness/commands.json`, with its `--isolated`, its exact `.csproj`
and its stop line — and nothing offers it. You go to the Commands tab of the right worktree and
press it, knowing it is there.

**The seam that fixes it has been cut and unused since 19 August.**
`WorkflowPlanStep.RunsHere` is documented as *"whether **this product** executes the step rather than
handing it to the agent"*; `ImportWorkflows` already files a step whose `run` matches a
`.harness/commands.json` key as `WorkflowStepKind.FolderCommand`; and `RunPlan.cs:85` already
recognises one — to return `PlanAction.Unsupported` and stop the run. The retro for #148 wrote it
down as *"dead weight today"*.

**Six measurements, taken on this branch with an AppHost live, not assumed.**

| | |
| --- | --- |
| `aspire start` detaches | AppHost `28818` ← `aspire run --non-interactive … --isolated` at **ppid 1**, its own process group. The call that started it returned **exit 0 in 6–14 s** |
| a second `start` on a live app | *"Stopping previous instance … Running instance stopped successfully"*, 9 s, and **exactly one** AppHost after |
| two checkouts | **two** AppHosts, on different ports, neither touching the other — `--isolated` bounds life per checkout |
| the address | **different on every start**: dashboard `61961 → 62526 → 63358 → 63891`, frontend `62523 → 63356 → 63889`. Never `localhost:5000` |
| the ending the product would store | `exit 0` and the last line printed. **`aspire stop` changes neither** |
| the gate with the app live | see `tasks.md` §7 |

## What Changes

- **A plan step whose `run` names a folder command is executed by this product.** `RunPlan` gains
  `PlanAction.RunHere` and stops returning `Unsupported` for a step it can resolve. The process
  ending is the step ending: **zero advances the plan, non-zero stops it naming the step and its
  code, and nothing retries.**
- **One new port in `BuildingBlocks/World/`**, in the shape `IRunSink` and `ICheckSink` established:
  Workspace answers what a command key runs, because since #98 nothing below the modules can resolve
  one. Registered in `WorkspaceModule.cs`, so **`World.cs` gains no line**.
- **The command line is resolved when the run starts**, in `StartRun`, where `<n>` is already
  substituted — so a run executes the plan as it was when it started.
- **A step naming a key the folder cannot run is refused at import, not at run time.** Two cases, one
  new kind and the reason on the step: a key the folder does not declare, and a key it declares
  **long-running**, which has no ending for a step to wait on.
- **The change tab's fifth panel** shows the verification command the folder declares, a `Run`
  button that opens a terminal, and a toggle that makes it a step of a workflow's plan — written to
  the rows *and* out to `.harness/workflows.json`, so the next run of that workflow starts the app
  by itself.
- **The folder declares which command is the verification one**, in `.harness/commands.json`, beside
  the four flags an entry already carries. No column and no migration.
- **It never claims the application is up**, and that is measured rather than preferred — see below.
- **`.harness/commands.json`'s first sentence is rewritten.** *"Nothing here runs by itself"* stops
  being true the moment a plan step executes one.

### The one decision the canvas and #286 answer differently, settled with a measurement

The canvas draws `Up on http://localhost:5000 · started 20s ago` with an `Open app` link. **#286
decided in writing not to assert it. The measurement agrees, and it is not close.**

`aspire start` exits 0 in 6–14 s having handed the AppHost to a supervisor at **ppid 1** — there is
no session left to read. The port it landed on was **different on all four starts** and was never
`5000`, so nothing can be remembered. And the only facts the product would hold about the ending —
exit code `0`, the last line printed — are **unchanged by `aspire stop`**: a tick drawn from them
would still read "up" with the application down.

So drawing "up" would require re-probing on every render: **cross-origin from the browser**, or a
new `World` seam from the server that this change has no need for otherwise. **The link the command
printed already proves it when clicked.** The panel says what was declared and what was pressed, and
nothing about what is listening. This is also the reversible direction: a later slice can add a
claim; withdrawing one people have already seen is worse.

## Capabilities

### New Capabilities

- `the-verification-command`: what a project declares as the command that shows the change working,
  how a plan step executes it, and what the change tab offers.

### Modified Capabilities

- None. Everything here is an ADDED requirement on a new capability.

## Impact

- **`BuildingBlocks/World/`** — `IDeclaredCommandLookup.cs` (new), one field on `LaunchStep`, one
  field on `FolderCommandDefinition`.
- **`RunPlan.cs`**, **`RunLauncher.cs`**, **`StartRun.cs`**, **`WorkflowPlanReader.cs`**,
  **`ImportWorkflows.cs`**.
- **Workspace** — `DeclaredCommandLookup.cs`, one line in `WorkspaceModule.cs`.
- **Workflows** — `WorkflowStepForCommand.cs`, one route pair.
- **`ChangeTab.tsx`** — one line, the `"app"` slot, plus the panel it names.
- **Not touched**: `World.cs`, any migration, any `.ai/commands/aio/` prompt, the rail, the footer,
  the tab counter, `stepsDone`, `FolderDetailScreen.tsx`.
