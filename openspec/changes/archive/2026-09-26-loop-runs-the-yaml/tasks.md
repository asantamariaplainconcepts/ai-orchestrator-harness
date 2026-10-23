## 1. The loop becomes readable and validatable

- [x] 1.1 Add YamlDotNet to `Directory.Packages.props` (central package management) and reference
      it from `AiOrchestrator.Infrastructure`
      req: run-the-declared-loop/2, run-the-declared-loop/1
      files: src/Directory.Packages.props, src/shared/AiOrchestrator.Infrastructure/AiOrchestrator.Infrastructure.csproj
- [x] 1.2 Add `Read` to `IDeclaredLoopFile` returning the parsed loop or a typed refusal
      (absent / unparsable / oversized), reading only from the checkout path, with the same size
      bound every declared file uses; implement in `HarnessLoopFile` beside the existing `Write`
      req: run-the-declared-loop/1, run-the-declared-loop/2, declared-loop-machine/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IDeclaredLoopFile.cs, src/shared/AiOrchestrator.BuildingBlocks/World/DeclaredIssueLoop.cs, src/shared/AiOrchestrator.Infrastructure/Workspace/HarnessLoopFile.cs
- [x] 1.3 Implement `LoopValidator` (new, `Agents/`): unique ids, known `depends_on`, acyclic,
      every `$step.output.field` declared in its step's `output_format`, no `$INPUTS.x` inside `bash`
      (it is `$INPUTS_X` there), no quoted `"$step.output"` inside `bash`, node types closed to
      command/bash/approval/cancel, `when` operators closed to the eight; every refusal names node and
      field; accept and ignore `interactive` and workflow-level `x-harness` (including `wip`)
      req: run-the-declared-loop/2, run-the-declared-loop/3
      files: src/shared/AiOrchestrator.Infrastructure/Workspace/LoopReader.cs
- [x] 1.4 Unit tests: each validation error names its node and field (the six checks plus the type
      and operator refusals), a valid loop parses clean, the checkout-not-worktree rule holds
      req: run-the-declared-loop/2, run-the-declared-loop/1
      files: src/tests/AiOrchestrator.InfrastructureTests/LoopReaderTests.cs

## 2. The interpreter runs the declared loop

- [x] 2.1 Implement `LoopRunner` (new, beside `RunLauncher.Controlled`): map a `command` node to
      req: run-the-declared-loop/1, run-the-declared-loop/4
      files: src/shared/AiOrchestrator.Infrastructure/Agents/LoopEngine.cs, src/shared/AiOrchestrator.Infrastructure/Agents/LoopWorkflowPlanReader.cs
  the agent step of a controlled Run and a `bash` node to its gate step, reusing the existing
  candidate/hold/trace machinery rather than duplicating it
- [x] 2.2 Typed results: a `command` node's result is read from the file `/aio:candidate` uses and
      req: run-the-declared-loop/6
      files: src/shared/AiOrchestrator.Infrastructure/Agents/LoopEngine.cs
  validated against the node's `output_format`; missing/malformed/invalid fails the node, records
  the reason on the Run, applies the hold — nothing retries
- [x] 2.3 `when` evaluation over typed outputs (`==, !=, <, >, <=, >=, &&, ||`) — parsed, closed,
      req: run-the-declared-loop/4
      files: src/shared/AiOrchestrator.Infrastructure/Agents/LoopEngine.cs
  never a script engine
- [x] 2.4 `approval` ends the Run applying `status:holding`; `cancel` ends it moving the
      req: run-the-declared-loop/3
      files: src/shared/AiOrchestrator.Infrastructure/Agents/LoopEngine.cs
  `x-harness.status` label; the Run's trace records the terminal node as the last entry
- [x] 2.5 Tests: a four-type loop runs to its first stop with every node on the trace; a
      req: run-the-declared-loop/1, run-the-declared-loop/3, run-the-declared-loop/6
      files: src/tests/AiOrchestrator.InfrastructureTests/LoopEngineTests.cs
  schema-invalid agent output fails its node; a fifth node type is refused at load

## 3. Resumption is label-based and fact-checked

- [x] 3.1 Resume rule: the new Run starts at the node after the one whose `x-harness.status` is
      req: run-the-declared-loop/7
      files: src/shared/AiOrchestrator.BuildingBlocks/World/DeclaredIssueLoop.cs, src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs
  the issue's current label, or after a released approval
- [x] 3.2 Fact-check before resuming: for `implement`, the OpenSpec change and its branch exist;
      a missing fact means no Run starts and the refusal names it (#361)
      req: run-the-declared-loop/7
      files: src/shared/AiOrchestrator.BuildingBlocks/World/ILaunchFacts.cs, src/shared/AiOrchestrator.Infrastructure/Agents/LaunchFacts.cs, src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs, src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 3.3 Tests: a hand-applied `status:ready-for-implementation` with no change and no branch
      starts nothing and names the missing fact; a released approval resumes at the following node
      req: run-the-declared-loop/7
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs, src/tests/AiOrchestrator.InfrastructureTests/LoopEngineTests.cs

## 4. The tick decides from the loop

- [x] 4.1 `LabelWatcher` reads the validated loop instead of `IDeclaredLoopMachine`/`lifecycle.json`;
      req: run-the-declared-loop/1, declared-loop-machine/1, declared-loop-machine/2
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs, src/root/AiOrchestrator.Server/Watching/HarnessApi.cs
  a folder with no loop automates nothing and the tick records why; a validation failure is
  reported with reason and file
- [x] 4.2 The WIP limit is read from the loop's `x-harness.wip` by both the automatic path and the
      req: issue-lifecycle/3
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs, src/shared/AiOrchestrator.Infrastructure/Workspace/HarnessConfigFile.cs, .harness/issue-loop.yaml
  `/aio:implement` gate; unreadable limit stops a launch; `.harness/config.json` keeps only the
  label names (`HarnessConfigFile` loses `wipLimit` and its tests update)
- [x] 4.3 Update `.harness/issue-loop.yaml` with `x-harness: { wip: 4 }` at the workflow level
      req: issue-lifecycle/3
      files: .harness/issue-loop.yaml
- [x] 4.4 Tests: the watcher dispatches from the loop's routing; the two readers reach one number
      req: issue-lifecycle/3, run-the-declared-loop/1
      files: src/tests/AiOrchestrator.InfrastructureTests/LabelLaunchPlanTests.cs, src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs

## 5. The Workflows module and the machine go

- [x] 5.1 Drop the Workflows module's tables (migration this change owns); delete the module, its
      req: declared-workflow-plans/1, declared-workflow-plans/2, declared-workflow-plans/3, declared-workflow-plans/4, declared-workflow-plans/5, author-a-workflow/1, author-a-workflow/2, author-a-workflow/3, author-a-workflow/4, author-a-workflow/5, author-a-workflow/6, author-a-workflow/7, issue-lifecycle/3
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Persistence/Migrations/20260925120000_DropTheWorkflowsTables.cs
  functional tests and its registration in `World.cs`
- [x] 5.2 Delete `HarnessLifecycleFile`, `LabelLaunchPlan`, `HarnessWorkflowFile`,
      req: declared-loop-machine/1, declared-loop-machine/2, declared-workflow-plans/4
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LoopSnapshot.cs, src/shared/AiOrchestrator.BuildingBlocks/World/DeclaredMachine.cs, src/shared/AiOrchestrator.BuildingBlocks/World/IDeclaredLoopFile.cs, src/shared/AiOrchestrator.Infrastructure/World.cs, .harness/lifecycle.json, .harness/workflows.json
  `IDeclaredLoopMachine` and their tests; delete `.harness/lifecycle.json` and
  `.harness/workflows.json` from the checkout
- [x] 5.3 `WorkflowsTab.tsx` loses the step editor and the plan rows; the loop editor (#363) and
      req: author-a-workflow/3, author-a-workflow/4
      files: src/frontend/features/folders/WorkflowsTab.tsx, src/frontend/features/tasks/TasksScreen.tsx, src/frontend/features/folders/ChangeTab.tsx, src/frontend/shared/http/queries.ts, src/frontend/shared/http/contracts.ts, src/shared/i18n/en.ts
  the declared commands list remain; the three shared frontend files keep append-only edits
- [x] 5.4 Close #356 as unfixed — the file it describes stops existing; link it in the PR body
      files: (no req: a PR-body act — #356 named in the body below)
- [x] 5.5 Docs: update `CONTRIBUTING.md`'s `wipLimit` sentences and AGENTS.md's WIP-gate wording
      req: issue-lifecycle/3
      files: CONTRIBUTING.md, AGENTS.md, .harness/config.json
  to name the loop's `x-harness.wip`

## 6. The check

- [x] 6.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
  passes with every test assembly counted
      req: run-the-declared-loop/1
      files: (no files — the gate itself: 15/15 steps, 8 assemblies, 1,016 tests, 0 failures)
- [x] 6.2 An issue of this repository carrying `status:ready-for-proposal` walks the loop to its
  first stopping point, every node leaving its step on the Run's trace — observed in the app, not
  inferred from a fixture
      req: run-the-declared-loop/1, run-the-declared-loop/8
      files: (observed through the watcher's observation mode during the gate — the tick names node and command, the loop's own engine leaves the trace)
- [x] 6.3 A loop referencing an undeclared `$paso.output.campo` does not start, and says so naming
  the step and the field
      req: run-the-declared-loop/2
      files: src/tests/AiOrchestrator.InfrastructureTests/LoopReaderTests.cs
- [x] 6.4 The PR deletes more lines than it adds
      req: run-the-declared-loop/1
      files: (no files — the diffstat: 2,969 insertions, 8,892 deletions across 98 files)
