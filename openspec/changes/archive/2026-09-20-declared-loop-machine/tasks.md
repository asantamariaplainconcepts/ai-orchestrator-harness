# Tasks

Ordered so the switch is the last thing removed and the first thing that can come back. Steps 1 and
2 land inert: nothing calls the reader until task 3.1.

## 1. The seam and its reader

- [x] 1.1 Add `IDeclaredLoopMachine` to `BuildingBlocks/World` with the record types for a declared
      machine — named machines, states with their recognising predicates, transitions with guards and
      one effect — and document the closed vocabulary on the interface, citing `IDeclaredCommandFile`
      as the doctrine it inherits.
      req: declared-loop-machine/1, declared-loop-machine/3, declared-loop-machine/4
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IDeclaredLoopMachine.cs
- [x] 1.2 Add its reader in `Infrastructure/Workspace`, beside `HarnessConfigFile` and with the same
      shape: size bound, comment-tolerant parser, malformed answered as null. Register it in
      `World.cs` as the fourth declared file. Verify unit tests for a missing file, an unparsable
      file, an oversized file, and a well-formed one.
      req: declared-loop-machine/2
      files: src/shared/AiOrchestrator.Infrastructure/Workspace/HarnessLifecycleFile.cs, src/shared/AiOrchestrator.Infrastructure/World.cs, src/tests/AiOrchestrator.InfrastructureTests/HarnessLifecycleFileTests.cs
- [x] 1.3 Refuse anything outside the vocabulary — an unknown predicate, effect or prompt token — with
      the offending term named in the refusal rather than treated as false. Verify a unit test per
      category, and one that a declaration from a hypothetical newer product is refused rather than
      partly honoured.
      req: declared-loop-machine/3
      files: src/shared/AiOrchestrator.Infrastructure/Workspace/HarnessLifecycleFile.cs, src/tests/AiOrchestrator.InfrastructureTests/HarnessLifecycleFileTests.cs
- [x] 1.4 Verify a unit test that a file declaring several machines over one set of states parses as
      several machines, and that selecting one by name yields only its transitions.
      req: declared-loop-machine/4
      files: src/tests/AiOrchestrator.InfrastructureTests/HarnessLifecycleFileTests.cs

## 2. This repository's own declaration

- [x] 2.1 Write `.harness/lifecycle.json` describing the loop this repository runs today — the states
      of `docs/ai-orchestator/loop-lifecycle.html` that are reachable with the effects this change
      supports, and the guards already enforced (`folder`, `room`, `not held`, `no live run`).
      req: declared-loop-machine/1, declared-loop-machine/4, issue-lifecycle/2
      files: .harness/lifecycle.json
- [x] 2.2 Verify, as a test over the real file, that it parses and that the machine it declares
      reproduces the decisions the current `CommandFor` switch makes for every status in `LoopStatus`
      plus the held and unheld cases. This is the test that says the translation was faithful.
      req: declared-loop-machine/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LoopInterpreter.cs, src/tests/AiOrchestrator.InfrastructureTests/HarnessLifecycleFileTests.cs

## 3. The interpreter

- [x] 3.1 Change `LabelLaunchPlan.For` to take the declared machine and decide by interpreting it.
      Keep it a pure function with no I/O, so it stays testable without a socket.
      req: declared-loop-machine/1, issue-lifecycle/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs, src/tests/AiOrchestrator.InfrastructureTests/LabelLaunchPlanTests.cs
- [x] 3.2 Have `LabelWatcher` read the machine per Folder once per tick, beside the tunables it
      already reads, and pass it to the plan. A Folder whose machine is absent or unreadable starts
      nothing, and the reason is logged with the file path — distinguishable from "declares a machine
      that matches nothing".
      req: declared-loop-machine/2
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs, src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 3.3 Verify a functional test that two different declarations over one set of tasks produce
      different posted runs, driving the real watcher through its HTTP client as
      `LabelWatcherTests` already does.
      req: declared-loop-machine/1
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 3.4 Verify a functional test that a Folder with no declaration, and one with a malformed
      declaration, each start nothing.
      req: declared-loop-machine/2
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 3.5 Remove `CommandFor` and any other compiled knowledge of which status starts which command.
      Nothing in `src/` may name `ready-for-proposal` or `ready-for-implementation` as a launch rule
      after this task; grep for both and say what is left and why.
      Left: `ListTasks.cs`'s `LoopStatus -> kebab-case` mapping (`"ready-for-proposal"`,
      `"ready-for-implementation"`), which spells a status for the wire the way the screens read it —
      it decides nothing about what starts, only how a value already decided elsewhere is written.
      The declared file's own header comment also names both, as documentation, not code.
      req: declared-loop-machine/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs

## 4. Verification

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green, with the full test count and every assembly named.

      15/15 steps, 5m15s. **1035 tests, 0 failures across 8 assemblies**: InfrastructureTests 479,
      ArchitectureTests 18, Modules.Tasks.FunctionalTests 183, Modules.Workspace.FunctionalTests 139,
      Modules.Workflows.FunctionalTests 67, Server.FunctionalTests 41, Modules.Runs.FunctionalTests
      98, EndToEndTests 10.

      Two earlier runs each failed one `PhoneTests` case (a different one each time) with
      `System.TimeoutException` waiting on `Locator("header")` — resource contention from running the
      whole solution's tests together, not this change: it touches no frontend code, and all 5
      `PhoneTests` pass in isolation every time (`dotnet test` on `EndToEndTests` alone, run
      separately). Recorded here rather than silently retried past.
      req: declared-loop-machine/1, declared-loop-machine/2, declared-loop-machine/3, declared-loop-machine/4, issue-lifecycle/1, issue-lifecycle/2
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IDeclaredLoopMachine.cs, src/shared/AiOrchestrator.BuildingBlocks/World/LoopInterpreter.cs, src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs, src/shared/AiOrchestrator.Infrastructure/Workspace/HarnessLifecycleFile.cs, src/shared/AiOrchestrator.Infrastructure/World.cs, src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs, .harness/lifecycle.json
- [x] 4.2 `openspec validate "declared-loop-machine" --strict` clean.
      req: declared-loop-machine/1
      files: openspec/changes/declared-loop-machine/
- [x] 4.3 Behaviour on this repository is identical before and after: with `.harness/lifecycle.json`
      in place, the watcher's decisions for the current inbox match what the switch produced. Record
      the comparison, not an adjective.

      `gh issue list --state open` against every `status:` label in this repository's real inbox on
      2026-09-20, `CommandFor`'s rule applied by hand (it no longer exists in code to call) against
      the declared machine's actual decision:

      | Issue | status | held | old `CommandFor` | new declared machine |
      | --- | --- | --- | --- | --- |
      | 319 | ready-for-proposal | false | `propose` | `/aio:propose #319` |
      | 317 | ready-for-implementation | false | `implement` | `/aio:implement #317` |
      | 278 | ready-for-proposal | false | `propose` | `/aio:propose #278` |
      | 271 | ready-for-proposal | false | `propose` | `/aio:propose #271` |
      | 267 | needs-refinement | false | none | none |
      | 262 | code-review | true | none | none |
      | 321, 261, 260, 259, 254, 253, 252, 250, 248, 247, 246, 244, 243, 239 | backlog | false | none | none |

      Every status the real inbox carries today — `backlog`, `ready-for-proposal`,
      `ready-for-implementation`, `needs-refinement`, `code-review` — is one of the nine
      `DeclaredStagedMachineTests` already asserts against this exact `CommandFor` table, unheld and
      held. No divergence.
      req: declared-loop-machine/1
      files: openspec/changes/declared-loop-machine/tasks.md
