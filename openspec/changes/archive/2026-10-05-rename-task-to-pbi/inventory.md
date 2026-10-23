# Occurrence inventory — verified 4 Oct 2026, branch `change/448` at `d940fac5` (master `0866c0a9`)

Every `Task`/`task` occurrence in the rename's scope, classified before any file was edited. Four
classes: **[R]** rename (domain vocabulary), **[P]** platform type, keep (`System.Threading.Tasks.Task`
and friends), **[V]** vendor/stored text, keep (configuration keys, stored markers, label values,
vendor wire fields), **[H]** immutable history, keep (ADR and retro names, archived changes,
migrations' generated text).

## 1. Module and assembly — `src/modules/Tasks`

- Folder `src/modules/Tasks/AiOrchestrator.Modules.Tasks/` — 33 `.cs` files; project
  `AiOrchestrator.Modules.Tasks.csproj`; namespace `AiOrchestrator.Modules.Tasks[...]` — **[R]** →
  `src/modules/Pbis/AiOrchestrator.Modules.Pbis/`, `AiOrchestrator.Modules.Pbis[...]`.
  Discovery is `ModuleRegistration.Discover()` globbing `AiOrchestrator.Modules.*.dll`
  (`BuildingBlocks/Modules/ModuleRegistration.cs`) — no code edit needed, matched by the new name.
- Compile-time consumers of the assembly reference (project paths only — no `using` of the module's
  namespaces exists outside it, MOD rules): `src/AiOrchestrator.slnx` (2 entries),
  `AiOrchestrator.Server.csproj:15`, `AiOrchestrator.MigrationService.csproj:17`,
  `AiOrchestrator.ArchitectureTests.csproj:21` — **[R]** paths/names.
- `ArchitectureTests/Modules.cs:30` roster `"Tasks"` — **[R]** → `"Pbis"`.
- `TasksModule` (Name `"Tasks"`), `TasksPermissions` (`tasks:read`, `tasks:sync`,
  `tasks:configure` — in-code permission ids; roles are granted in code, nothing persisted names
  them) — **[R]** → `PbisModule`/`"Pbis"`/`PbisPermissions`/`pbis:*`.
- `Features/Inbox/TaskSnapshot.cs` — the in-memory cache (no row, no migration) — **[R]** →
  `PbiSnapshot`. Configuration key `"TaskSources:CacheSeconds"` (`TtlKey`) — **[V]**, keeps its
  stored spelling.
- Use cases carrying the noun in the class/route name (13 files, 15 route templates — see §4) — **[R]**.
- `TaskInputValidator`/`TaskInputAssessment` assess the stored `### Task input` block — class names
  **[R]**; the parsed JSON member `taskKind` and the `### Task input` marker are the stored
  grammar, version 1 — **[V]**, unchanged.
- `ReceiveWebhook` — vendor webhook surface, no Task noun — unchanged.
- Test folder `src/tests/AiOrchestrator.Modules.Tasks.FunctionalTests` (19 files) — **[R]** →
  `...Pbis.FunctionalTests`, namespaces and `GetProperty("tasks")` JSON reads with it (the response
  member renames with the DTO, §4).

## 2. World seam — `src/shared/AiOrchestrator.BuildingBlocks/World/`

- `ITaskSource.cs` (39 occurrences): `ITaskSource`, `ITaskSourceCatalog`, `SourcedTask`,
  `TaskAssignee`, `TaskLabel`, `TaskComment`, `TaskSourceKind`, `TaskSourceKinds`, method
  `GetTask`, parameters `taskKey`/`taskUrl` — **[R]** per design D2. Doc-comment wording
  ("a task", "per-task call") renames with the types; the rules they state do not.
- `ControlTaskIdentity.cs`: `ControlTaskIdentity(Provider, Host, Repository, Key)` — **[R]** →
  `ControlPbiIdentity`; the four members and their spellings are **[V]** (they feed
  `MergeEffectId.Derive` and the stored registry tuple).
- `IControlRegistry.cs`, `IEffectsJournal.cs`: `ControlTaskIdentity` parameters and the `Task`
  members of `RegistryAcquire`/`RegistryBootstrap`/`RegistryGrant`/… — type name **[R]**; record
  members reviewed against the registry's serialised shape (see §5 — the persisted registry
  serialises the identity tuple, not a property named `Task`; where a member name is itself
  serialised it is **[V]** and stays).
- `IRunLauncher.cs` `LaunchFacts.TaskKey`, `IControlledWorkflow.cs` `VerificationManifest.TaskKey`:
  `LaunchFacts` is in-memory only — **[R]** → `PbiKey`. `VerificationManifest` is serialised into
  `run_control_events.VerificationJson` — **[V]**, member keeps the spelling `TaskKey`.
- `LabelLaunchPlan.cs`, `RepositoryIdentity.cs`, `IWorkspaceProbe.cs` (`WatchedTask`): doc comments
  name `GET /api/tasks` and task wording — **[R]** (`WatchedTask` → `WatchedPbi`, `/api/pbis`).
- `System.Threading.Tasks.Task` return types appear throughout the same files (`Task<…>`,
  `Task.WhenAll`, `Task.CompletedTask`) — **[P]**, untouched everywhere, in every layer below too.
- `LoopSnapshot`/`LoopFacts` — no task-named member (**verified against a stored row**, §5).

## 3. Infrastructure implementations — `src/shared/AiOrchestrator.Infrastructure/`

- `TaskSources/` folder, namespace `AiOrchestrator.Infrastructure.TaskSources`: `TaskSourceCatalog`
  (which also names the **[V]** configuration keys `TaskSources:GitHub:Scope`,
  `TaskSources:GitHub:SecretName`, `TaskSources:AzureDevOps:BaseUrl`, `TaskSources:Jira:Site`,
  `TaskSources:Linear:…` — kept) and `GitHubTaskSource`, `AzureDevOpsTaskSource`, `JiraTaskSource`,
  `LinearTaskSource`, `GitHubPullRequestReader` — **[R]** → `PbiSources/`, `PbiSourceCatalog`,
  `GitHubPbiSource`, … .
- `World.cs` — 12 references across registrations — **[R]** in place, append-only order preserved
  (renamed identifiers, no reordering).
- `Agents/RunLauncher.cs`, `RunLauncher.Controlled.cs`, `VerificationManifests.cs` — read
  `LaunchFacts.TaskKey` / write `VerificationManifest.TaskKey` — first renames **[R]**, the
  manifest member **[V]**.
- `Git/GitControlRegistry.cs`, `ControlRegistryDecisions.cs`, `Coordination/HttpEffectsJournal.cs`
  — `ControlTaskIdentity` consumers — **[R]** type name; serialised shapes **[V]**.

## 4. Routes and wire

13 `/api/tasks…` route templates in the module (1 inbox list, 1 detail, status, hold, sync,
input/validate, comments, labels ×2, assignees ×2, evidence, attachments/bytes) **[R]** →
`/api/pbis…`, with the response member `Tasks` → `Pbis`. The PR-review surface under
`/api/tasks/pull-requests…` (module routes and SPA paths) renames with the family — **[R]**.
External consumers: `AiOrchestrator.Server/Attention/AttentionQueue.cs`,
`Watching/HarnessApi.cs`, `LabelWatcher.cs` — **[R]** (path, DTO member `TaskKey` → `Key` on the
attention row and run body). Run DTOs' `taskKey` → `key` (3 members in `contracts.ts`); `taskTitle`
stays (not in the change's scope — the trait a run copies at start). `AiOrchestrator.Control`
CLI's `--task` flag — local vocabulary, **[R]** → `--pbi`. Webhooks keep `/api/webhooks/{vendor}`
— **[V]**, the vendor names them. `.harness/` and `.ai/commands/` call no `/api/tasks` route —
verified by grep, nothing to move.

## 5. Persistence and identity (verified against a real database copy)

Real database: main checkout `.harness/data/aiorchestrator.db` (154,890,240 bytes; `runs` 39 rows,
`run_dispatches` 1, `coordination_dispatch_intents` 1). `PRAGMA table_info` on it:

- `runs.TaskKey` NOT NULL (+ `TaskTitle`), index `IX_runs_TaskKey`; `run_dispatches.TaskKey`
  NOT NULL — **[V]**: entity property renames to `PbiKey` with `.HasColumnName("TaskKey")` and the
  index keeps its database name — no migration, no data motion. `TaskTitle` columns stay `TaskTitle`.
- `coordination_dispatch_intents` columns: `Provider, ApiHost, Repository, Key, Operation` — no
  task noun at all, nothing to do.
- One `DecisionSnapshot` row read: `{"facts":{"status":…,"held":…,"folder":…,"room":…,"liveRun":…},
  "machine":{…}}` — no task-named member (**[V]** confirmed; `LoopFacts` serialisation untouched).
- `MergeEffectId.Derive` baseline (task 1.3): the registered operation on disk is
  `Provider=github, Host=api.github.com, Repository=asantamariaplainconcepts/ai-orchestrator-harness,
  Key=355, FolderId=01A010C9-32EA-7ADF-8C28-D32D570807D7, Operation=implement`, and its stored
  effectId is **`065c57c3-8425-5f5d-ab21-920c77197ddd`** (dispatch `run_dispatches.EffectId`,
  state `process-observed`; the same operation is journalled as coordination intent `065C57C3-
  8425-5F5D-AB21-920C77197DDD`). The derivation input tuple is
  `$"{Provider}\n{Host}\n{Repository}\n{Key}\n{folderId}\n{operation}"` over SHA-256 folded into a
  Guid — none of those names moves; task 5.2 turns this row into the regression assertion.
- Migrations and their `*.Designer.cs` + `RunsDbContextModelSnapshot.cs`: generated history of the
  persisted schema — **[H]**, only `Run`/`RunDispatch` mappings and the snapshot regenerate through
  task 5.1's mapping change (empty diff against the stored schema, or an explicit no-op migration
  only if the tooling requires it — the inventory expects none).

## 6. Frontend — `src/frontend/`

- SPA routes `/tasks`, `/tasks/pull-requests`, `/tasks/pull-requests/:owner/:repo/:number`
  (`routes.tsx`) — **[R]** → `/pbis…` with redirects from the old paths (`/workflows` and
  `/history` redirects retarget). `Features/tasks/` folder (11 files, `TasksScreen`, `TaskWalk`,
  `AttentionQueuePanel`, the PR-review set) — **[R]** → `features/pbis/`, `PbisScreen`, `PbiWalk`;
  PR-review and watch files carry no Task noun in their names. `features/folders/TaskDetailPane.tsx`
  and `TaskLoopCommand.tsx`, `shared/tasks/` — **[R]**.
- `en.ts`: 121 `tasks.*` keys + `nav.tasks` = 122 — **[R]** → `pbis.*`, `nav.pbis`, grouped by
  screen exactly as today; user-facing copy strings ("Tasks", "task sources") rename with the key.
- `contracts.ts` (13 `/api/tasks` refs, 3 `taskKey` members) and `queries.ts` (`["tasks", …]`
  query keys, `useTask*` hooks, `/api/tasks` fetches) — **[R]**; `tasksDone`/`tasksTotal` in the
  change-tab DTO are the OpenSpec `tasks.md` counts — **[V]** (they count a change's task list,
  the folder capability's own word, not the inbox noun).
- `AppShell.tsx` heading match `/tasks` and cross-references — **[R]**.

## 7. Docs, forms, specs

- README vocabulary (Task among the eight words, lines 4, 101, 107), ARCHITECTURE (module tree,
  `TaskSnapshot` prose, lines 17, 61, 80, 89, 91, 121), DESIGN (locked vocabulary line 29, copy
  line 67), `.github/PULL_REQUEST_TEMPLATE.md` ("Task URL/identity" headings) and
  `.github/ISSUE_TEMPLATE/controlled-task.yml` (description, seam example values) — **[R]**.
- Kept in the issue form: the `### Task input` marker and the textarea label "Task input" that
  existing bodies and archived issues carry — **[V]**; the JSON grammar's `taskKind` member —
  **[V]**.
- `status:` label values (`status:holding` etc.) — the vendor's own — **[V]**.
- Accepted ADRs (`MirroredTask.FolderPath` in ADR-0005), `docs/process/retro-log.md`, archived
  changes under `openspec/changes/archive/`, `design-import/` prototypes — **[H]**.
- AGENTS.md is not in the change's doc scope (the vocabulary table lives in README).
