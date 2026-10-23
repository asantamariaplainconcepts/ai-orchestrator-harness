# Tasks

## 1. The tick's reconcile pass

- [x] 1.1 `HarnessApi` gains the typed POSTs the dispatch path needs (journal reserve, journal
      outcome, runs start) and the runs-by-effect query, each shaped by what the endpoints already
      read — no schema invented twice.
      req: dispatch-observation/2, durable-controlled-dispatch/1
      files: src/root/AiOrchestrator.Server/Watching/HarnessApi.cs
- [x] 1.2 The reconcile pass at the top of every dispatch-mode tick: intents `pending`/`unknown`
      past the window resolve by querying Runs by effectId; `started` settles the intent; nothing
      else resubmits, ever. Escalates once per effectId through `IAttentionCommentWriter`.
      req: dispatch-observation/2, durable-controlled-dispatch/1
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 1.3 `ListDispatchIntents` (or its read side) grows the stale filter — state in
      (`pending`,`unknown`), updated before now-minus-window — so the tick asks, not scans.
      req: dispatch-observation/2
      files: src/modules/Coordination/AiOrchestrator.Modules.Coordination/Features/Journal/ListDispatchIntents.cs

## 2. `dispatch-claims`

- [x] 2.1 The mode vocabulary: `MayObserve` and `ClaimsMode` grow `dispatch-claims`; the
      `LabelLaunchPlan` candidate set for dispatch filters to `ready-for-implementation` with
      `held: false` — a `ready-for-proposal` task logs `Observed` every mode and is never
      dispatched.
      req: dispatch-observation/1
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 2.2 The dispatch journey per candidate: reserve journal intent (operation `implement`) →
      acquire claim → `POST /api/runs` with effectId/FolderId/Task. Refused claim or conflicted
      intent: typed log, stop for that task this tick. Response 201/200 with RunId: record
      `started`. Anything else: record `unknown` by observation-later, never blind-resubmit.
      req: dispatch-observation/1
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 2.3 The escalation window declared in `.harness/config.json`
      (`escalationWindowMinutes`, default 30) read through the existing tunables seam, with the
      null-stops-everything rule the other numbers carry.
      req: dispatch-observation/2
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IDeclaredConfigFile.cs,
             src/shared/AiOrchestrator.Infrastructure/Workspace/HarnessConfigFile.cs

## 3. Verification

- [x] 3.1 Wire tests per mode: `observe` GET-only as today; `observe-claims` acquires without
      writes; `dispatch-claims` reserves, claims, posts one start for a `ready-for-implementation`
      task, and writes nothing for `ready-for-proposal`.
      req: dispatch-observation/1
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 3.2 Reconcile tests: a `pending` intent whose Run exists settles to `started` — one Run,
      no replay; a `pending` intent past the window without a Run escalates once (second tick
      comments nothing) and blocks; a `failed` intent is never resubmitted.
      req: dispatch-observation/2, durable-controlled-dispatch/1
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 3.3 End-to-end against a bare registry and the real endpoints: claim acquired, intent
      reserved, Run started with the effectId, second tick facing its own live claim and intent
      does nothing new.
      req: dispatch-observation/1
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 3.4 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green, with the full test count and every assembly named.

      15/15 steps. **1051 tests, 0 failures across 8 assemblies**: InfrastructureTests 482,
      ArchitectureTests 18, Tasks.Functional 183, Workspace.Functional 139,
      Workflows.Functional 67, Server.Functional 54, EndToEnd 10, Runs.Functional 98.
      req: all
      files: (no files — the run is the evidence)
- [x] 3.5 `openspec validate "acp05-durable-dispatch" --strict` clean. Validated.
      req: all
      files: openspec/changes/acp05-durable-dispatch/
