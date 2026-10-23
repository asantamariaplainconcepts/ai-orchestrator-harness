## Why

The product's centre of navigation is becoming the PBI and its journey through the loop (#447), and the
vocabulary still says "Task" everywhere: the module, the seams, 13 routes under `/api/tasks`, the wire
DTOs, the SPA routes and 121 i18n keys. A rename that ships in halves leaves two words for one thing —
the hold the vocabulary locks in README exists precisely to stop that. One change, delivered at once,
keeps the word, the API and its consumers saying the same thing, while identity, history and the
effectIds already recorded stay exactly as they are.

## What Changes

- **BREAKING** — the HTTP surface moves from `/api/tasks…` to `/api/pbis…` (`/api/pbis/{key}/comments`,
  `/labels`, `/assignees`, `/evidence`, `/attachments/bytes`, `/status`, `/hold`, `/sync`,
  `/input/validate`), and the wire spells the identifier `key` — the route `/api/pbis/{key}` already
  carries the PBI context, so `pbiKey` would say it twice. `MirroredTask` never becomes public
  vocabulary: the vendor cache is internal plumbing without a name in the wire.
- **BREAKING** — the assembly and its namespace become `AiOrchestrator.Modules.Pbis`
  (`src/modules/Tasks/…` → `src/modules/Pbis/…`, tests, `slnx`, the `Server` and `MigrationService`
  references), keeping module discovery (`AiOrchestrator.Modules.*.dll`) working untouched. The types
  inside rename with the vocabulary: the public domain terms in `BuildingBlocks/World` that name the
  vendor's work item (`ITaskSource`, `SourcedTask`, `ControlTaskIdentity`, …) become the PBI
  spellings. `System.Threading.Tasks.Task` is a platform type and is not touched.
- The SPA moves with it: routes `/tasks`, `/tasks/pull-requests`; `nav.tasks`; the 121 `tasks.*` i18n
  keys; `contracts.ts` (`taskKey` on run DTOs); `features/tasks/`; the host consumers in
  `AttentionQueue.cs` and `HarnessApi.cs` that read `/api/tasks` from outside the modules.
- **Task stays in the vocabulary for one thing**: the children of a PBI, when a real vendor provides
  them (an Azure DevOps PBI→tasks hierarchy). No vendor does today, so nothing in the code names a
  task after this change.
- Identity, history and effects are preserved by construction: `MergeEffectId.Derive` hashes
  `{Provider, Host, Repository, Key, folderId, operation}` and none of those names moves; the `TaskKey`
  columns in Runs and the `DecisionSnapshot` JSON keep their stored spelling, read through properties
  whose names change only in code. Migrations are added only where the inventory this change owns
  finds a real need; no historical data is discarded and no cache is rebuilt unless it is demonstrably
  recoverable from the vendor.
- README, ARCHITECTURE, DESIGN, the OpenSpec capability names and the frontend contracts change in the
  same delivery. Accepted ADRs and the retro log are never rewritten.

## Capabilities

The rename is delivered as **new capabilities that supersede the task-named ones**, because the
capability names themselves say what the product calls the thing: a `task-inbox` requirement that
answers at `/api/pbis` is a spec whose name lies. The superseded capabilities say so in one line and
point at their replacement.

### New Capabilities
- `pbi-inbox`: the inbox — one list of everything waiting to become a run, filtered server-side, read
  through the in-memory snapshot. Supersedes `task-inbox`.
- `pbi-details`: the detail reads and writes — comments, labels, assignees — at the vendor, first.
  Supersedes `task-details`.
- `pbi-status-and-holds`: the derived status, its seven states and the orthogonal hold. Supersedes
  `task-status-and-holds`.
- `run-a-pbi`: starting a run against a PBI — branch derived, worktree ensured, agent checked before
  the row exists. Supersedes `run-a-task`.
- `controlled-pbi-input`: the opt-in structured-body grammar and its assessment. Supersedes
  `controlled-task-input`.
- `read-the-vendors`: the vendor read and the shared snapshot it caches — unchanged requirements,
  renamed vocabulary only: **modified in place**, because the capability says what it does rather than
  what it reads, and every requirement's Task wording moves to PBI in the same delivery.

### Modified Capabilities
- `read-the-vendors` — same requirements, the vocabulary each one carries: `TaskSnapshot`,
  `ITaskSource.Poll()`, "a task each" become the PBI spellings, as the rename reaches the one
  in-memory cache that does not migrate.
- `the-world-seam` — the seam's roster: `ITaskSource`, `ITaskSourceCatalog` and
  `ControlTaskIdentity` are named as the vendor seam, and the rename is what they become. No new
  seam, no implementation move; the requirement's list of interface names changes.
- `app-shell-and-navigation` — the SPA's routes: `/tasks` becomes `/pbis` as the landing screen, and
  the redirects that followed the old screens follow the new names — a bookmark to `/tasks` went
  nowhere wrong, and it still SHALL NOT.
- `read-a-run` — the run↔work-item join's wording: "task detail", "task key alone" and the
  full-identity join (`source, repository and key`) keep their requirement and change their noun.
- `attention-queue` — the composed queue reads the inbox (`GET /api/pbis`) and its rows are "one per
  blocked PBI"; the drain rule and the host composition keep their requirement and change their noun.
- `mirrored-body-completeness` — the historical name is kept on purpose (a capability that documents
  its own history does not erase it); the requirements' "task" wording moves to PBI and the body the
  capability governs is now handed over as the PBI the vendor read.
- `module-boundaries` — the module list the analyzers police: the Tasks module is now the Pbis module,
  and the requirement's roster names it.
- `shared-control-registry` — the identity the registry serialises is the PBI's (`provider, host,
  repository, key`), and the requirement's wording follows the rename; the stored shape does not.

The `task-*` capabilities are not modified: they are superseded by the `pbi-*` ones above and their
spec bodies are replaced by a superseded marker.

## Impact

- **Backend**: `src/modules/Tasks` (assembly, namespace, every use case carrying `Task` in route or
  type name), `src/modules/Runs` (join wording, `taskKey` wire spelling on run DTOs, `TaskKey`
  persisted columns kept as-is), `src/modules/Workspace` (`MergeGrantAdmission`,
  `ControlTaskIdentity` consumers), `src/modules/Coordination` (journal wording), the host
  (`AiOrchestrator.Server/Attention/AttentionQueue.cs`, `Watching/HarnessApi.cs`), the seam
  (`BuildingBlocks/World/ITaskSource.cs`, `ControlTaskIdentity.cs`, `IControlRegistry.cs`,
  `IEffectsJournal.cs`, `LoopSnapshot.cs`) and their implementations in `Infrastructure`
  (`World.cs` registrations among them — the repo's append-only rule applies: rename in place, never
  reorder).
- **Frontend**: `routes.tsx`, `AppShell.tsx`, `features/tasks/`, `contracts.ts`, `queries.ts`, 121
  `tasks.*` keys + `nav.tasks` in `en.ts`.
- **Docs**: README (vocabulary table), ARCHITECTURE, DESIGN, the capability specs folded by this
  change's archive. Not rewritten: accepted ADRs, retro log, `design-import/` prototypes.
- **Storage**: `TaskKey` columns in `Runs` (three) and the dispatch tables keep their names —
  renaming them is a table rebuild that history does not need; a migration is added only where the
  inventory in design.md finds a live need.
- **Invisible by design**: `MergeEffectId.Derive` inputs, the `DecisionSnapshot` JSON member names,
  the `status:` label space the vendor carries, and `System.Threading.Tasks.Task` — the platform type
  and the vendor's own identifiers are not this product's vocabulary to rename.
