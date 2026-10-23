## 1. Inventory, before any rename

- [x] 1.1 Build the occurrence inventory the specs promise: every `Task`/`task` occurrence under `src/modules/Tasks`, `src/modules/Runs`, `src/modules/Workspace`, `src/modules/Coordination`, `src/shared/AiOrchestrator.BuildingBlocks/`, `src/shared/AiOrchestrator.Infrastructure/`, `src/root/AiOrchestrator.Server/` and `src/frontend/`, each classified as domain vocabulary / platform type (`System.Threading.Tasks.Task`) / vendor-owned text / immutable history (ADR and retro names, stored `### Task input` marker, `status:` label values, `TaskSources:` configuration keys)
      req: the-world-seam/1, module-boundaries/1
      files: openspec/changes/rename-task-to-pbi/inventory.md
- [x] 1.2 Confirm the persisted surface against a real database copy: `PRAGMA table_info` on `Runs` and the dispatch tables (expect `TaskKey` columns unchanged), and read one `DecisionSnapshot` row to confirm the JSON carries no task-named member
      files: openspec/changes/rename-task-to-pbi/inventory.md
- [x] 1.3 Record the effectId invariant: capture `MergeEffectId.Derive`'s input tuple spelling and the hash it produces for one existing registered operation, as the baseline the post-rename test must reproduce
      files: openspec/changes/rename-task-to-pbi/inventory.md

## 2. Module and assembly rename

- [x] 2.1 Move `src/modules/Tasks` to `src/modules/Pbis` with `AiOrchestrator.Modules.Pbis.csproj` and the namespace rename, updating `src/AiOrchestrator.slnx`, `AiOrchestrator.Server.csproj`, `AiOrchestrator.MigrationService.csproj` and the folder `src/tests/AiOrchestrator.Modules.Tasks.FunctionalTests` → `src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests`
      req: module-boundaries/1
      files: src/modules/Pbis/AiOrchestrator.Modules.Pbis/AiOrchestrator.Modules.Pbis.csproj, src/AiOrchestrator.slnx, src/root/AiOrchestrator.Server/AiOrchestrator.Server.csproj, src/root/AiOrchestrator.MigrationService/AiOrchestrator.MigrationService.csproj, src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/AiOrchestrator.Modules.Pbis.FunctionalTests.csproj
- [x] 2.2 Verify `ModuleRegistration.Discover()` still finds the renamed assembly through the `AiOrchestrator.Modules.*.dll` glob, and that the ArchitectureTests module roster names `AiOrchestrator.Modules.Pbis`
      req: module-boundaries/1
      files: src/tests/AiOrchestrator.ArchitectureTests/Modules.cs, src/tests/AiOrchestrator.ArchitectureTests/AiOrchestrator.ArchitectureTests.csproj

## 3. World seam rename

- [x] 3.1 Rename the seam types in `BuildingBlocks/World/` per design D2 (`ITaskSource`→`IPbiSource`, `ITaskSourceCatalog`→`IPbiSourceCatalog`, `SourcedTask`→`SourcedPbi`, `TaskAssignee`/`TaskLabel`/`TaskComment`→PBI spellings, `ControlTaskIdentity`→`ControlPbiIdentity`, `TaskSnapshot`→`PbiSnapshot`, `TaskSourceKind`→`PbiSourceKind`), keeping every method signature's shape and every doc-comment rule; parameters `taskKey`/`taskUrl` become `key`/`url`
      req: the-world-seam/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IPbiSource.cs, src/shared/AiOrchestrator.BuildingBlocks/World/ControlPbiIdentity.cs
- [x] 3.2 Update the implementations in `AiOrchestrator.Infrastructure` and the registrations in `World.cs` in place — append-only order preserved, no reordering of lines this change did not touch
      req: the-world-seam/1
      files: src/shared/AiOrchestrator.Infrastructure/PbiSources/GitHubPbiSource.cs, src/shared/AiOrchestrator.Infrastructure/PbiSources/PbiSourceCatalog.cs, src/shared/AiOrchestrator.Infrastructure/PbiSources/AzureDevOpsPbiSource.cs, src/shared/AiOrchestrator.Infrastructure/PbiSources/JiraPbiSource.cs, src/shared/AiOrchestrator.Infrastructure/PbiSources/LinearPbiSource.cs, src/shared/AiOrchestrator.Infrastructure/World.cs
- [x] 3.3 Keep `System.Threading.Tasks.Task` untouched: where a renamed file mixes the platform type with the domain vocabulary, the inventory line (task 1.1) decides each occurrence
      req: the-world-seam/1
      files: openspec/changes/rename-task-to-pbi/inventory.md

## 4. Routes and wire

- [x] 4.1 Move the 15 `/api/tasks…` routes to `/api/pbis…` (`ListTasks`→`ListPbis`, `GetTask`→`GetPbi`, `SetTaskStatus`→`SetPbiStatus`, `SetTaskHold`→`SetPbiHold`, `SyncTasks`→`SyncPbis`, `GetTaskComments`→`GetPbiComments`, `GetTaskLabels`/`SetTaskLabels`, `GetTaskAssignees`/`SetTaskAssignees`, `GetTaskEvidence`→`GetPbiEvidence`, `GetTaskAttachmentBytes`→`GetPbiAttachmentBytes`, `ValidateTaskInput`→`ValidatePbiInput`), no `/api/tasks` fallback
      req: pbi-inbox/1, pbi-inbox/2, pbi-inbox/3, pbi-details/1, pbi-details/2, pbi-details/3, pbi-details/4, pbi-status-and-holds/1, pbi-status-and-holds/2, pbi-status-and-holds/3, controlled-pbi-input/4
      files: src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/ListPbis.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/GetPbi.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/SetPbiStatus.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/SetPbiHold.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/SyncPbis.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/GetPbiComments.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/GetPbiLabels.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/SetPbiLabels.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/GetPbiAssignees.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/SetPbiAssignees.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/GetPbiEvidence.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/GetPbiAttachmentBytes.cs, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Inbox/UseCases/ValidatePbiInput.cs
- [x] 4.2 Rename the run DTOs' `taskKey` to `key` in `contracts` and everywhere the Runs module answers it, keeping the `{ source, repository, key }` full-identity join
      req: read-a-run/1, attention-queue/1
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/ListRuns.cs, src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/GetRun.cs, src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/StartRun.cs, src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/GetAttention.cs
- [x] 4.3 Move the host consumers: `AttentionQueue.cs` and `HarnessApi.cs` read `GET /api/pbis`
      req: attention-queue/1
      files: src/root/AiOrchestrator.Server/Attention/AttentionQueue.cs, src/root/AiOrchestrator.Server/Watching/HarnessApi.cs, src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs

## 5. Persistence and identity invariants

- [x] 5.1 Rename the EF properties (`TaskKey`→`PbiKey`) with `.HasColumnName("TaskKey")` where a mapping exists, leaving every column name on disk unchanged; add a migration only if task 1.2's inventory finds a live need (expected: none — record the finding either way)
      req: shared-control-registry/1
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Domain/Run.cs, src/modules/Runs/AiOrchestrator.Modules.Runs/Domain/RunDispatch.cs, src/modules/Runs/AiOrchestrator.Modules.Runs/Persistence/RunsDbContext.cs
      Finding: no migration needed. `PRAGMA table_info` on the real database copy (task 1.2) shows `runs.TaskKey` and `run_dispatches.TaskKey` still on disk, now mapped through `PbiKey` with `HasColumnName("TaskKey")` and the index keeps `HasDatabaseName("IX_runs_TaskKey")`; the snapshot and every `*.Designer.cs` are untouched, so `dotnet ef` has nothing to add.
- [x] 5.2 Add the regression test for the effectId: derive the effectId for the operation captured in task 1.3 through the renamed `ControlPbiIdentity` and assert it is the same UUID
      req: shared-control-registry/1
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/MergeEffectIdTests.cs
- [x] 5.3 Confirm `LoopFacts` serialisation is untouched: the members carry no task noun, and a stored `DecisionSnapshot` row parses before and after identically
      files: openspec/changes/rename-task-to-pbi/inventory.md

## 6. Frontend

- [x] 6.1 Move the SPA routes (`/tasks`→`/pbis`, `/tasks/pull-requests`→`/pbis/pull-requests`) with redirects from the old paths, and the screen folder `features/tasks/`→`features/pbis/`
      req: app-shell-and-navigation/1
      files: src/frontend/app/routes.tsx, src/frontend/features/pbis/PbisScreen.tsx, src/frontend/features/pbis/PbiWalk.tsx, src/frontend/features/pbis/PullRequestReview.tsx, src/frontend/features/pbis/PullRequestEntry.tsx, src/frontend/features/pbis/pullRequest.ts
- [x] 6.2 Rename the i18n block: 121 `tasks.*` keys → `pbis.*`, `nav.tasks`→`nav.pbis`, in `en.ts` grouped by screen as it is today; all user-facing copy keeps flowing through i18n
      req: app-shell-and-navigation/1
      files: src/frontend/shared/i18n/en.ts
- [x] 6.3 Update `contracts.ts` and `queries.ts` to the `/api/pbis` surface and the `key` field name; `AppShell.tsx` cross-references follow
      req: pbi-inbox/1, read-a-run/1
      files: src/frontend/shared/http/contracts.ts, src/frontend/shared/http/queries.ts, src/frontend/app/AppShell.tsx, src/frontend/app/Sidebar.tsx, src/frontend/app/RunRedirect.tsx, src/frontend/shared/repo.ts, src/frontend/features/folders/pbi.ts, src/frontend/features/folders/PbiDetailPane.tsx, src/frontend/features/folders/PbiLoopCommand.tsx

## 7. Docs and specs

- [x] 7.1 Fold the renamed capability specs by archive: this change's deltas already carry the supersede; confirm `openspec/specs/` ends with `pbi-inbox`, `pbi-details`, `pbi-status-and-holds`, `run-a-pbi`, `controlled-pbi-input` and the one-line superseded markers on the five task-named capabilities
      files: openspec/specs/task-inbox/spec.md, openspec/specs/task-details/spec.md, openspec/specs/task-status-and-holds/spec.md, openspec/specs/run-a-task/spec.md, openspec/specs/controlled-task-input/spec.md
      Note: this fold is what `/aio:sync`'s archive step executes on this branch; the deltas in this change already carry the supersede markers, so the fold is mechanical at merge time.
- [x] 7.2 Update the vocabulary in README (the eight words become PBI, and Task is reserved for vendor children), ARCHITECTURE.md and DESIGN.md; leave accepted ADRs and the retro log untouched
      files: README.md, ARCHITECTURE.md, DESIGN.md
- [x] 7.3 Update the issue form and PR template so new issues reference PBI identity, keeping the `### Task input` block marker in existing forms' history
      files: .github/PULL_REQUEST_TEMPLATE.md, .github/ISSUE_TEMPLATE/controlled-task.yml

## 8. The check #448 asks for

- [x] 8.1 Copy a database from an existing run, upgrade it, compare row counts and identity checksums for Runs, dispatch and journal before/after; open a pre-rename PBI and its run history in the UI; confirm two repositories with the same key do not share runs; repeat an already-registered operation and confirm no new effect. Record the numbers observed in the issue's Check section
      files: openspec/changes/rename-task-to-pbi/inventory.md, src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/MergeEffectIdTests.cs
      Observed: copy of the live `.harness/data/aiorchestrator.db` (runs 39, run_dispatches 1, coordination_dispatch_intents 1, run_steps 53, run_log_lines 20984) upgraded with this branch's MigrationService: 0 rows differ in the 29 shared `runs` columns, dispatch and journal tables byte-identical (sha256 equal); the only schema diff is `RemoteEnvironment` (#434's migration, already on master, not this change). `PRAGMA table_info`: `TaskKey` keeps its stored name on both tables. The collision case is real in the data: `#185` carries its own run in `ai-orchestator-new` (`01a01ad2…`) and another in `ds3d/ds-connect` (`01a0800d…`), and they do not join — asserted by the passing E2E `A_run_of_another_repository_with_the_same_key_is_not_offered`. Repeating the registered operation derives the same effectId `065c57c3-8425-5f5d-ab21-920c77197ddd` (MergeEffectIdTests, 2/2).
- [x] 8.2 Run the gate: `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` — every test assembly in `src/AiOrchestrator.slnx`, zero failures; say the command ran, not "CI is green"
      Observed: `aspire do ci` ran at HEAD `86c820bb` and passed — 18/18 steps, total 2m41s. Test step counted every assembly in `src/AiOrchestrator.slnx`: ArchitectureTests 18, Modules.Pbis.FunctionalTests 281, Modules.Workspace.FunctionalTests 187, Modules.Runs.FunctionalTests 123, Server.FunctionalTests 53, EndToEndTests 45, InfrastructureTests 603 — 1310 tests, 0 failures, 0 skipped.
