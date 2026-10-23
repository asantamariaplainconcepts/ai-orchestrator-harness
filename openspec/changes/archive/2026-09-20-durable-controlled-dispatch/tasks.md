## 1. Admission

- [x] 1.1 Reuse canonical Task identity and authoritative Folder scope lookup.
      `ControlTaskIdentity` (shared in BuildingBlocks) flows through `StartRun`'s request,
      and the launch validates the folder scope before any side effect. The four-field
      wire shape (provider, host, repository, key) is the same one the journal uses.
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/StartRun.cs,
      src/shared/AiOrchestrator.BuildingBlocks/World/IRunLauncher.cs
- [x] 1.2 Add Runs-owned dispatch persistence and generated migration.
      `RunDispatch` entity (immutable request JSON + SHA-512 request hash, plan JSON, RunId,
      failure code/reason, pending/launch-intent/process-observed/refusal states) persisted in
      `RunsDbContext` with migration `20260920102711_DurableDispatch` and snapshot.
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Domain/RunDispatch.cs,
      src/modules/Runs/AiOrchestrator.Modules.Runs/Persistence/RunsDbContext.cs,
      src/modules/Runs/AiOrchestrator.Modules.Runs/Persistence/Migrations/20260920102711_DurableDispatch.cs,
      src/modules/Runs/AiOrchestrator.Modules.Runs/Persistence/Migrations/20260920102711_DurableDispatch.Designer.cs,
      src/modules/Runs/AiOrchestrator.Modules.Runs/Persistence/Migrations/RunsDbContextModelSnapshot.cs
- [x] 1.3 Admit optional scoped controlled starts before side effects; deduplicate with database
      uniqueness and immutable payload comparison.
      `StartRun` admits controlled starts when a `task` field is present: the dispatch row is
      committed **before** `EnsureWorktree`/`Launch` run, so a lost response leaves an observable
      `launch-intent` rather than a silent second agent. Concurrent duplicates are arbitrated by
      the unique index; a different payload on the same effect is a `409`, including the
      collection-expression fix that satisfies IDE0300 on the status-order assertion.
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/StartRun.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/DispatchRecoveryTests.cs

## 2. Recovery

- [x] 2.1 Add scoped dispatch observation and explicit pending/launch-intent/process-observed/refusal states.
      `GET /api/runs/dispatches/{effectId}?folderId=…` returns `DispatchObservation` — the dispatch
      row joined to its Run's state, with `uncertain` derived (`pending`/`launch-intent`), scoped,
      and guarded by `Requires(RunsPermissions.Read)` (which in turn removed `runs:read` from the
      orphan list in `PermissionTests`).
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/GetRunDispatch.cs,
      src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/DispatchObservation.cs
- [x] 2.2 Preserve effect identity through launch and record process observation with PID evidence.
      The launch keeps the same `RunId` throughout; `RunSink.RecordAgentProcess` lands the PID in
      the dispatch, and re-reading flips `uncertain` to false once the process is observed.
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/RunSink.cs,
      src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/DispatchObservation.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/DispatchRecoveryTests.cs
- [x] 2.3 Preserve legacy starts and controlled resume refusal.
      A start with no `task` field goes through the legacy path unchanged — asserted by
      `Identity_mismatch_and_uncontrolled_effects_are_refused_but_legacy_start_is_unchanged`.
      A controlled run in an uncertain state refuses `POST /api/runs/{id}/resume` with a `400`
      until a process observation lands.
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/StartRun.cs,
      src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/ResumeRun.cs,
      src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/DispatchRecoveryTests.cs

## 3. Verification

- [x] 3.1 Test concurrency, changed payloads, persistence failures, lost responses and crash gaps
      against real SQLite.
      12 fact/theory tests in `DispatchRecoveryTests` (34 cases over theories), all against the
      real SQLite file the fixture boots: concurrent duplicate commits one binding and one launch;
      changed payloads conflict; failed persistence stops before the next effect; lost response
      and changed configuration recover the original binding; crash gaps remain queryable and
      never replay; setup refusal is durable and does not retry.
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/DispatchRecoveryTests.cs
- [x] 3.2 Test scope authorization, immutable snapshots and legacy behavior.
      Scope binding is authoritative and mismatches do not leak; start and replay require the
      current start permission (theory over ProjectRole, including denial); identity mismatch and
      uncontrolled effects are refused while a legacy start (no task field) is unchanged; the
      snapshot hash makes any request mutation a conflict.
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/DispatchRecoveryTests.cs
- [x] 3.3 Run targeted verification and request the parent's full gate slot; report remaining
      authority gaps.
      Full solution from this worktree: **821 tests and 0 failures across 8 assemblies** —
      Infrastructure 381, Runs functional 98 (34 new here), Workspace 139, Tasks 108, Workflows
      67, Architecture 18, EndToEnd 10. `pnpm install` had to run first in this worktree:
      EndToEnd's initial 10 failures were a missing `node_modules`, not the change. Two
      PermissionTests orphan-list entries removed (`runs:read`, `runs:start`) — both now name
      their permission through real operations (`GetRunDispatch`, controlled `StartRun`), which
      is the list shrinking for the documented reason.
      **Remaining authority gaps, left for the separately reviewed change:** the observation
      route exists but no trusted authority consumes it to decide controlled admission yet, and
      no live check has been run — by design, that is the human step (H6 in ACP01's tasks).
      **Gate from the integration branch** (`andonisan-p0-integration-validation`, all five P0
      slices assembled): `aspire do ci` passed 15/15 steps in 2m48s — restore, build, frontend
      install+build, typecheck, lint, then `dotnet test` over the full solution: 1011 tests,
      0 failures (Architecture 18, Tasks 183, Workspace 139, Workflows 67, Server functional 39,
      Runs 98, Infrastructure 457, EndToEnd 10). One EndToEnd screenshot comparison failed once in
      a `--no-build` re-run outside the gate and passed on repeat — the known 390px flake.
      files: (verification only — all files listed above)
