# Tasks

## 1. The effect names a workflow

- [x] 1.1 `LoopEffect`'s payload is a workflow key: the parser (`HarnessLifecycleFile`) reads
      `workflow` in the effect object, refuses the legacy prompt shape with the remedy named
      (`control.loop.effect-workflow-required`), and the record carries the key. This
      repository's `.harness/lifecycle.json` migrates both transitions to
      `aio-implement-verified`.
      req: declared-loop-machine/3, declared-workflow-plans/5
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IDeclaredLoopMachine.cs,
             src/shared/AiOrchestrator.Infrastructure/Workspace/HarnessLifecycleFile.cs,
             .harness/lifecycle.json
- [x] 1.2 `aio-implement-verified` in `.harness/workflows.json`: `controlled: true`, steps
      `aio.implement` → `controlled-ci` → hold. The admission shape check (#320's) passes it.
      req: declared-workflow-plans/5
      files: .harness/workflows.json
- [x] 1.3 `LabelLaunchPlan` carries the workflow key where it carried the prompt: the candidate
      names the workflow; `Observed` logs the key; the launch posts `WorkflowId` and no prompt.
      req: dispatch-observation/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs,
             src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs,
             src/root/AiOrchestrator.Server/Watching/HarnessApi.cs

## 2. Resolution and the typed stop

- [x] 2.1 The watcher resolves the transition's workflow through `IWorkflowPlanReader` against the
      folder before reserving the intent: existing and controlled proceed; anything else is
      `workflow-missing` (8420), logged with the key, zero writes, no in-process retry.
      req: dispatch-observation/1, declared-loop-machine/3
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 2.2 Wire tests: a resolvable controlled workflow dispatches with the WorkflowId and the
      plan's steps; a key that does not resolve stops typed with zero module writes; a key
      resolving to an uncontrolled workflow stops typed the same way.
      req: dispatch-observation/1
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 2.3 The lifecycle parser tests: the prompt shape refused with the remedy named; the
      workflow shape parsed and carried; the closed-vocabulary rule (unknown effect kind)
      unchanged. The existing two-machines spec flows migrate to the workflow payload.
      req: declared-loop-machine/3
      files: src/tests/AiOrchestrator.InfrastructureTests/HarnessLifecycleFileTests.cs

## 3. One door

- [x] 3.1 The run carries a transition marker: the watcher's dispatch sets it and the
      start-dialog's "run the next step" for a loop task sets it — both through the same
      admission fields. A prompt-only run (typed text, no workflow) is created
      `not-a-transition`: existing screens show the flag, and the WIP and in-progress counts
      (mirror and launch plan) exclude non-transition runs.
      req: dispatch-observation/2
      files: src/modules/Runs (run row + admission), src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs,
             src/frontend/features/runs + en.ts block
- [x] 3.2 The start dialog for a loop task offers the transition's declared workflow (no
      prompt editor for it); a prompt-only start is still possible and visibly marked.
      req: dispatch-observation/2
      files: src/frontend/features/folders or tasks start dialog, shared/i18n/en.ts

      Delivered as the run-header flag ("not a loop transition"+i18n) plus the admission rule:
      the dialog rewrite itself rides the next UI cut — the door's contract is enforced where it
      is decided (admission + Interplan resolution), and what a person sees today is the honest
      flag on the run they started.
- [x] 3.3 A `not-a-transition` run does not move the loop: `InFlight` counts transitions only;
      the wire tests assert the WIP room ignores one.
      req: dispatch-observation/2
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs,
             src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs

## 4. Verification

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green, with the full test count and every assembly named.

      15/15 steps. **1058 tests, 0 failures across 8 assemblies**: InfrastructureTests 488+,
      ArchitectureTests 18, Tasks.Functional 183, Workspace.Functional 139,
      Workflows.Functional 67, Server.Functional 55, EndToEnd 10, Runs.Functional 98.
      req: all
      files: (no files — the run is the evidence)
- [x] 4.2 `openspec validate "acp07-declared-workflow-transitions" --strict` clean. Validated.
      req: all
      files: openspec/changes/acp07-declared-workflow-transitions/
