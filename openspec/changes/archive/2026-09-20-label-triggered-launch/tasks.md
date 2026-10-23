# Tasks

## ACP01 hardening

This expanded Seam is an implementation proposal pending issue-owner review. It does not
complete #313, activate controlled dispatch, or authorize the live check.

- [x] H1 Add the Coordination-owned SQLite journal and initial migration, wired into both hosts.
      `src/modules/Coordination/AiOrchestrator.Modules.Coordination` with
      `CoordinationDbContext` (SQLite, `StoreDateTimeOffsetsAsBinary`, migration
      `20260920102432_InitialDispatchJournal`), registered in `World.cs` and wired into the
      Server host through `AiOrchestrator.Server.csproj`, `MigrationService.csproj`,
      `ArchitectureTests.csproj` and `src/AiOrchestrator.slnx`.
      req: dispatch-observation/3
      files: src/modules/Coordination/AiOrchestrator.Modules.Coordination/Persistence/CoordinationDbContext.cs,
      src/modules/Coordination/AiOrchestrator.Modules.Coordination/Persistence/Migrations/,
      src/shared/AiOrchestrator.Infrastructure/World.cs,
      src/AiOrchestrator.slnx
- [x] H2 Add permission-scoped reservation, outcome and read APIs with durable idempotency.
      `ReserveDispatchIntent` (POST /api/coordination/dispatch-intents), `RecordDispatchOutcome`
      (PUT …/{effectId}/outcome) and list-by-folder read route, each `IUseCase` one-file with its
      `Requires(CoordinationPermissions.WriteJournal)` declaration, `ErrorOr` through
      `ApiResults.Problem` (RFC 7807), folder-scope validated, and reservation idempotency
      arbitrated by the database unique index (`SqliteExtendedErrorCode 1555/2067` replay path).
      The replay returns `JournalRecord.From(existing)` — the same row, not a new dispatch.
      req: dispatch-observation/3, dispatch-observation/4
      files: src/modules/Coordination/AiOrchestrator.Modules.Coordination/Features/Journal/ReserveDispatchIntent.cs,
      src/modules/Coordination/AiOrchestrator.Modules.Coordination/Features/Journal/RecordDispatchOutcome.cs,
      src/modules/Coordination/AiOrchestrator.Modules.Coordination/CoordinationPermissions.cs,
      src/tests/AiOrchestrator.Server.FunctionalTests/JournalApiTests.cs
- [x] H3 Replace automatic legacy dispatch with observation-only HTTP journal consumption.
      `LabelWatcher` reads intents per folder via the host's own API (`api.Intents(folder)`), adds
      every journal identity to a blocked set before evaluating eligibility, and a suppressed task
      logs `Suppressed` rather than launching. The tick observes candidates (`Observed`) without
      launching any run; the reservation API exists but the watcher consumes it read-only.
      A journal that cannot be read, or that returns something that is not a valid journal,
      fails the whole tick rather than being treated as empty — an unavailable journal is not
      an empty journal.
      req: dispatch-observation/5
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs,
      src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] H4 Exercise persistence, concurrent reservation, restart suppression, bounds and scope via HTTP.
      39/39 functional tests over the HTTP wire (`AiOrchestrator.Server.FunctionalTests`,
      `JournalApiTests`): persistence across host restart per dispatch state, database-arbitrated
      concurrent reservation, host/port/repository identity normalization, replay-idempotency
      (reservation and outcome), history-protection vs rewrite and unknown-clearing, concurrent
      outcome conflicts, scope-binding and cross-folder isolation, permission-denied-before-
      validation, oversized-input bounds, exact failure-bound limits.
      **Fix found while landing:** the first response carries the in-memory `DateTimeOffset`
      (full precision) while a replay re-reads the row through EF's
      `DateTimeOffsetToBinaryConverter`, which EF documents as truncating beyond 0.1 ms — so an
      exact string comparison of `createdAt`/`updatedAt` between first response and replay fails
      by the encoded tick residue. The idempotency contract is that the replay is the same
      *moment*, so the assertions now compare within storage resolution (≤ 1 tick × 100 ns × 10).
      18/18 ArchitectureTests (Coordination added to `Modules.cs`); Server.FunctionalTests build
      clean after `dotnet format`.
      req: dispatch-observation/3, dispatch-observation/4, dispatch-observation/5
      files: src/tests/AiOrchestrator.Server.FunctionalTests/JournalApiTests.cs
- [ ] H5 Integrate trusted authority and controlled Runs admission in a separately reviewed change.
- [ ] H6 Perform the separately authorized live check; no live actions in this slice.

## 1. Folder resolution, ported from the browser

- [x] 1.1 Ported as `RepositoryIdentity.Of`/`.Same`, citing `src/frontend/shared/repo.ts` and saying
      why the existing `VendorRepository` is not it: that one drops the host (which would conflate a
      github.com repository with an Enterprise one of the same name) and parses with `Uri`, which
      cannot read `git@github.com:owner/name.git` — the form git reports about half of these folders'
      remotes in. 12 unit tests over the frontend's own listed cases.
      req: dispatch-observation/6
      files: src/shared/AiOrchestrator.BuildingBlocks/World/RepositoryIdentity.cs,
      src/tests/AiOrchestrator.InfrastructureTests/RepositoryIdentityTests.cs
- [x] 1.2 `RepositoryIdentity.FolderFor`, keeping `folderFor`'s two clauses and its first-match-wins
      rule. Takes its two fields as accessors rather than an interface: the records it compares are
      owned by two different modules, and a shared type for them would be the cross-module coupling
      this whole path exists to avoid. 4 unit tests, including both cases named here.
      req: dispatch-observation/3
      files: src/shared/AiOrchestrator.BuildingBlocks/World/RepositoryIdentity.cs,
      src/tests/AiOrchestrator.InfrastructureTests/RepositoryIdentityTests.cs

## 2. Reading the WIP tunables

- [x] 2.1 `IDeclaredConfigFile`/`HarnessConfigFile`, registered in `World.cs` beside the other two
      declared files. 11 unit tests: missing, malformed, no-limit, out-of-bounds, oversized, absent
      and blank labels, plus this repository's own file verbatim.
      **Not over `IWorkspaceProbe.Contents`, and `design.md` was wrong to say that is the prior art.**
      `.harness/commands.json` is not read that way: `HarnessCommandFile` opens the file directly with
      a size bound, behind an `IDeclared*File` seam in BuildingBlocks. This mirrors the real prior art
      instead — same bound, same comment-tolerant parser, same reading of a malformed file as null.
      A limit out of bounds is refused rather than clamped, and null never means "no limit".
      req: dispatch-observation/2
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IDeclaredConfigFile.cs,
      src/shared/AiOrchestrator.Infrastructure/Workspace/HarnessConfigFile.cs,
      src/shared/AiOrchestrator.Infrastructure/World.cs,
      src/tests/AiOrchestrator.InfrastructureTests/HarnessConfigFileTests.cs
- [x] 2.2 `LabelLaunchPlan.InFlight`. Counted **per folder**, not per machine: the limit is declared
      in a folder's own file, so how busy one repository is cannot be a statement about another.
      A tunables file spells labels the way GitHub does (`status:in-progress`) and a task row spells
      the status the way the screens do (`in-progress`) — the prefix comes off before they are
      compared, or the count is always zero and the watcher looks switched off.
      req: dispatch-observation/2
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs,
      src/tests/AiOrchestrator.InfrastructureTests/LabelLaunchPlanTests.cs

## 3. The watcher (observation-only since the control-plane hardening)

- [x] 3.1 `LabelWatcher`, shaped after `TaskPoller`: one gate so two ticks cannot overlap, a delay
      before the first pass rather than after it, `LabelWatcher:PollSeconds` with a 2-minute default
      and 0 to switch it off, and a tick that is never allowed to throw. Settles `design.md`'s open
      question by staying in `AiOrchestrator.Server` rather than taking a project of its own.
      Verified by composing the real host: `ValidateOnBuild`/`ValidateScopes` are on, so a hosted
      singleton wired wrongly fails every screen at startup rather than one loop on its first tick.
      req: dispatch-observation/1
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs,
      src/root/AiOrchestrator.Server/Program.cs,
      src/tests/AiOrchestrator.Server.FunctionalTests/WatcherHostTests.cs,
      src/tests/AiOrchestrator.Server.FunctionalTests/AiOrchestrator.Server.FunctionalTests.csproj,
      src/AiOrchestrator.slnx
- [x] 3.2 `LabelLaunchPlan.For`, a pure function so the rule can be tested without a socket. 20 unit
      tests: both eligible statuses, all seven that start nothing, a held task under **either** status,
      and a status still carrying its vendor prefix.
      **The hold blocks both commands, not only the one requirement 1 names.** Requirement 4 says a
      held task is not reconsidered until the hold clears, unqualified — and a `ready-for-proposal`
      task held by its own failed launch would otherwise relaunch every tick, which is the exact loop
      the hold was applied to break.
      req: dispatch-observation/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs,
      src/tests/AiOrchestrator.InfrastructureTests/LabelLaunchPlanTests.cs
- [x] 3.3 `HarnessApi` makes three read-only calls against this host's own API — the loopback
      `design.md` chose, because no module may reference another and nothing here publishes a
      `.Contracts` assembly: `GET /api/tasks`, `GET /api/folders` and the folder-scoped
      `GET /api/coordination/dispatch-intents`. Nothing else survives on it — the write calls the
      legacy watcher made (`POST /api/runs`, `POST /api/tasks/{key}/labels`) are gone, which is the
      observable signature of observation-only. Verified at the wire: the substitute is the
      `HttpMessageHandler`, so the test asserts every request is a `GET` rather than that the loop
      called something.
      The task's row carries no repository field, so it is derived from the URL with the existing
      `VendorRepository`.
      req: dispatch-observation/1, dispatch-observation/3
      files: src/root/AiOrchestrator.Server/Watching/HarnessApi.cs,
      src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 3.4 Both holds-back, and nothing written to the vendor for either. **Room is spent as it is
      planned**, not checked against a count read once: three eligible tasks and one free slot advise
      one candidate, where checking each against the same starting number would advise three.
      Nothing is remembered about a task held back — the retry is the absence of memory, not a queue,
      which is also what lets a folder added later release a previously unresolvable task.
      A folder whose tunables cannot be read starts nothing: **null is not "no limit"**, and the
      opposite reading is the one mistake here that stopping the loop does not undo.
      req: dispatch-observation/2, dispatch-observation/3
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs,
      src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 3.5 The `GET /api/runs?live=true` read the legacy watcher made, and its skip set, are gone
      with the dispatch they existed to protect. What replaced them is the journal read: a task that
      already has a Run carries a `started` journal entry, which suppresses it exactly as a `pending`
      one does — the same rule, read from the durable record instead of a live count. Two readings of
      "already going" would eventually disagree, and the one that disagreed would put a second agent in
      a worktree that already has one; one record cannot disagree with itself.
      req: dispatch-observation/5
- [x] 3.6 The failure hold the legacy watcher applied — `POST /api/tasks/{key}/labels` with the
      Folder's hold label — is gone with the launching it existed to protect. Its job is done
      earlier and better by the journal: a failed intent records `failed` with its code and reason
      durably, in the record a trusted authority will read, rather than a label a person has to notice
      and remove by hand. Nothing here writes to any vendor in any mode.
      req: dispatch-observation/4, dispatch-observation/5
      files: (this task removed code; the covering tests are the LabelWatcherTests journal-suppression cases)


- [x] 4.1 Green from the `change/313` worktree: **15/15 steps, 1m 13s, 789 tests and 0 failures across
      8 assemblies** — Infrastructure 373, Workspace 139, Tasks 108, Workflows 66, Runs 64, Architecture
      18, Server.FunctionalTests 11, EndToEnd 10. 62 of those are new here.
      **Finding, unrelated to this change:** every gate run rewrites the ten `docs/design/current/*-390*.png`
      captures, byte-identical across runs and across two different worktrees on two branches. That is
      deterministic drift between what master has committed and what the code renders today, not a
      render flake. Not committed here — this change touches no UI. Worth its own issue.
      **Re-verified on the P0 integration branch after the control-plane hardening: 15/15 steps,
      1011 tests and 0 failures across 8 assemblies**, with all five P0 slices assembled.
      files: (no files changed by this task — verification only)
- [x] 4.2 Clean.
      files: (no files changed by this task — verification only)
- [ ] 4.3 **Not run, and deliberately left for a person.** With observation-only in place the live
      check costs nothing and writes nowhere: set `LabelWatcher:Mode` to `observe` on a real host with
      real folders and read the log — expect `Observed` candidates for the repo's ready tasks and
      `Suppressed` for any journaled one. The dispatch live check (H6) remains unperformed and
      separately authorized, because it spends money and its failure mode is a run nobody asked for.
