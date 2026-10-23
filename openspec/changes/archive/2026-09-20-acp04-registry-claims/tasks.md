# Tasks

## 1. Bootstrap owner-command

- [x] 1.1 New console project `src/root/AiOrchestrator.Control` composing the same `AddWorld()`
      registrations as the server; the solution file gains it; `aspire do ci` builds it with the
      rest of the solution.
      req: shared-control-registry/1
      files: src/root/AiOrchestrator.Control/, src/AiOrchestrator.slnx
- [x] 1.2 The bootstrap command's argument parsing and the typed results (`created`,
      `conflict(observedRevision)`, `unknown(reason)`), each named and printed for a person to read,
      with the ref-protection precondition printed on success.
      req: shared-control-registry/1
      files: src/root/AiOrchestrator.Control/
- [x] 1.3 Unit-then-functional coverage against bare remotes (sha1 and sha256, like
      `ControlRegistryTests`): first run creates the ref with the declared pin and capacity; second
      run with the same pin conflicts naming the observed revision; the ref is byte-identical after
      the refused run.
      req: shared-control-registry/1
      files: src/tests/AiOrchestrator.InfrastructureTests/

## 2. Attention comment seam

- [x] 2.1 `IAttentionCommentWriter` in `BuildingBlocks/World` with the typed `AttentionComment`
      record (reason code, owner, generation, last known effect, remedy); registered in `AddWorld()`
      at the end of its own block; `World.cs` untouched except that append.
      req: shared-control-registry/2
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IAttentionCommentWriter.cs,
             src/shared/AiOrchestrator.Infrastructure/World.cs
- [x] 2.2 Infrastructure implementation posting a GitHub issue comment from the task's URL, using
      `ISecretResolver` by name at the moment of use, with the identity check `LabelWatcher` already
      applies; the renderer maps the typed record and nothing else — no prose formatting lives in
      the watcher.
      req: shared-control-registry/2
      files: src/shared/AiOrchestrator.Infrastructure/TaskSources/
- [x] 2.3 `escalationWindowMinutes` (default 30) read through the declared-config seam, per folder,
      with the same null-stops-everything rule every other tunable there carries.
      req: shared-control-registry/2
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IDeclaredConfigFile.cs or its reader
- [x] 2.4 A functional test over a wire: blocked claim fixture older than the window publishes
      exactly one comment with reason and remedy; the same claim on the next tick publishes zero;
      an unknown API response publishes nothing, logs the reason, and retries never.
      req: shared-control-registry/2
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs

## 3. `observe-claims` mode

- [x] 3.1 `MayObserve` accepts `observe-claims`; unknown values keep refusing with the
      controlled-admission error; the mode triple is one closed vocabulary and its test covers all
      legal and one illegal value.
      req: dispatch-observation/1
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 3.2 The tick's acquisition: read-then-acquire against the folder's registry location through
      `IControlRegistry`, one per eligible candidate, `expectedRevisionOid` from the tick's own
      read; `Acquired` and `Conflict` each logged with what a person needs (owner/generation, or
      the observed revision); zero in-process CAS retries.
      req: dispatch-observation/1
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 3.3 Idempotence across ticks: the second tick facing its own live claim for the same identity
      acquires nothing new and logs nothing new for it; a conflicting claim from another owner
      routes to the escalation path from task group 2 rather than a retry.
      req: dispatch-observation/1, shared-control-registry/2
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 3.4 Wire tests in each mode over toy folders with a real bare-remote registry: all requests
      before acquisition are GETs; no module write API is ever called; a folder without a control
      ref suppresses its candidates with the reason logged while other folders proceed.
      req: dispatch-observation/1, shared-control-registry/1
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs

## 4. Verification

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green, with the full test count and every assembly named.

      15/15 steps. **1046 tests, 0 failures across 8 assemblies**: InfrastructureTests 482,
      ArchitectureTests 18, Tasks.Functional 183, Workspace.Functional 139,
      Workflows.Functional 67, Server.Functional 49, EndToEnd 10, Runs.Functional 98.
      The first two runs failed PhoneTests timeouts under load — root cause found live, not
      assumed: an AppHost from the main checkout had been running for five days; stopping it
      made the next run green with zero code changes. Recorded in the retro.
      req: shared-control-registry/1-2, dispatch-observation/1
      files: (no files — the run is the evidence)
- [x] 4.2 `openspec validate "acp04-registry-claims" --strict` clean.
      req: all
      files: openspec/changes/acp04-registry-claims/
- [x] 4.3 Live run of the CLI against this repository's remote is deliberately excluded; the
      command's evidence is the bare-remote coverage in 1.3 plus one documented dry print
      (`--remote` pointing at a temp bare) showing the precondition text.

      Executed live against a temp bare remote: first run bootstrapped `refs/heads/
      harness-control-v1` at `c5ffca1` and printed the ADR-0006 protection precondition;
      second run refused with `control.registry.already-bootstrapped (observed revision
      c5ffca…)`. Output captured in the implementation commit.
      req: shared-control-registry/1
      files: (no files — the documented output is the evidence)
