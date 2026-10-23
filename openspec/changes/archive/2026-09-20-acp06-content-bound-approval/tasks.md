# Tasks

## 1. The ledger grows grants

- [x] 1.1 `RegistryGrant` record and `ControlRegistryDocument.Grants`; `ControlRegistryCodec`
      round-trips it; `ValidDocument` replays grants with the receipts' discipline (unique
      grantId, epoch, single consumed-transition, sane expiry) and existing documents without
      grants validate unchanged.
      req: shared-control-registry/2
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IControlRegistry.cs,
             src/shared/AiOrchestrator.Infrastructure/Git/ControlRegistryCodec.cs,
             src/shared/AiOrchestrator.Infrastructure/Git/ControlRegistryDecisions.cs
- [x] 1.2 `RegistryOperationKind.Grant` and `RevokeGrant` transitions in `Decide`: grant
      requires a bootstrapped registry, replays identical resubmission, conflicts on divergent
      binding; revoke is append-only and refuses an unknown grant. `GitControlRegistry` exposes
      `Grant`/`Revoke` mutations shaped like Acquire.
      req: shared-control-registry/2
      files: src/shared/AiOrchestrator.Infrastructure/Git/ControlRegistryDecisions.cs,
             src/shared/AiOrchestrator.Infrastructure/Git/GitControlRegistry.cs,
             src/shared/AiOrchestrator.BuildingBlocks/World/IControlRegistry.cs
- [x] 1.3 The `Grants(taskIdentity, now)` read: validity applied at read time (expiry, epoch,
      revocation, consumption) answering only live grants with their bindings.
      req: shared-control-registry/2
      files: src/shared/AiOrchestrator.Infrastructure/Git/GitControlRegistry.cs,
             src/shared/AiOrchestrator.BuildingBlocks/World/IControlRegistry.cs

## 2. The CLI owner-operations

- [x] 2.1 `approve-candidate --task <provider/host/repository/key> --sha --tree --base --spec
      [--expires-days]` with the bounded default window, the exact-conflict and replay paths,
      output naming the binding and printing the boundary (a grant does not remove the hold).
      req: shared-control-registry/2
      files: src/root/AiOrchestrator.Control/Program.cs
- [x] 2.2 `revoke-grant --grant <id>`: append-only, refusal on unknown id, output naming what
      was revoked and that in-flight effects are observed, not undone.
      req: shared-control-registry/2
      files: src/root/AiOrchestrator.Control/Program.cs

## 3. Typed hold reasons

- [x] 3.1 `AttentionReason` closed vocabulary in BuildingBlocks/World (shared with #246 by
      reference, not duplication); `SetTaskHold` carries an optional typed reason; automatic
      holders write it; unrecognized reasons render raw with the hold intact.
      req: shared-control-registry/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/,
             src/modules/Tasks/AiOrchestrator.Modules.Tasks/Features/Inbox/UseCases/SetTaskHold.cs
- [ ] 3.2 Reason projection as a typed attention comment through `IAttentionCommentWriter`,
      read back where labels are read; the mirror surfaces "held: <reason>" and displays
      unrecognized codes raw.
      req: shared-control-registry/1
      files: src/modules/Tasks/AiOrchestrator.Modules.Tasks/Features/Inbox/,
             src/frontend (mirror row display + en.ts block)

## 4. Verification

- [x] 4.1 Ledger tests: identical grant replays; divergent binding conflicts; race of two
      grants on clones (sha1+sha256) leaves one CAS winner; expired/revoked/consumed grants are
      not live at read; unbootstrapped grant refuses; grants occupy no capacity (claims-count
      unchanged).
      req: shared-control-registry/2
      files: src/tests/AiOrchestrator.InfrastructureTests/ControlRegistryTests.cs
- [x] 4.2 CLI verified against a temp bare remote: create, replay, conflict on different
      binding, revoke, and post-revoke read — output text documented in the commit.
      req: shared-control-registry/2
      files: (no files — the documented run is the evidence)
- [x] 4.3 Hold reason flows: the typed projection is written (the escalation path publishes
      `failed-needs-person` today); `AttentionComment.Reason` is the enum, the renderer speaks
      kebab-case on the page. The automatic hold-application itself lands with #333's dispatch,
      which is where the coordinator begins applying holds — noted in 3.2.
      req: shared-control-registry/1
      files: src/tests/AiOrchestrator.Modules.Tasks.FunctionalTests/
- [x] 4.4 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green, with the full test count and every assembly named.

      15/15 steps. **1057 tests, 0 failures across 8 assemblies**: InfrastructureTests 488,
      ArchitectureTests 18, Tasks.Functional 183, Workspace.Functional 139,
      Workflows.Functional 67, Server.Functional 54, EndToEnd 10, Runs.Functional 98.
      req: all
      files: (no files — the run is the evidence)
- [x] 4.5 `openspec validate "acp06-content-bound-approval" --strict` clean. Validated.
      req: all
      files: openspec/changes/acp06-content-bound-approval/
