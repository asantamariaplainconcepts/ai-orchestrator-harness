# watcher-settings-control — Tasks

## 1. World seam and state store

- [x] 1.1 Add `IWatcherControl` to `BuildingBlocks/World/` (effective mode with the persisted →
  configuration → `disabled` fallback chain, pilot task with the same chain, rung set as the single
  definition both the tick and the commands validate against) and register it in `AddWorld()`,
  appending to this change's own block.
      req: watcher-settings-control/5
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IWatcherControl.cs, src/shared/AiOrchestrator.Infrastructure/World.cs
- [x] 1.2 Implement the host-side store: load on demand, in-memory hold, write-through to disk under
  the ADR-0003-derived application-support location (never inside the velopack-replaced bundle
  directory), with the doc comment saying the registry of claims fences double-dispatch, not the
  mode.
      req: watcher-settings-control/5, watcher-settings-control/1
      files: src/shared/AiOrchestrator.Infrastructure/Watching/WatcherStore.cs
- [x] 1.3 Unit tests for the fallback chain (no store + config → config; store wins; neither →
  `disabled`) and for refusing a persisted mode outside the rung set with the same refusal shape the
  configuration path already produces.
      req: watcher-settings-control/5
      files: src/tests/AiOrchestrator.InfrastructureTests/WatcherStoreTests.cs

## 2. Watcher re-read and snapshot

- [x] 2.1 Change `MayObserve()`/`PilotTask()` in `LabelWatcher.cs` to read the effective mode per
  tick from `IWatcherControl` instead of read-once configuration; interval stays configuration.
      req: dispatch-observation/1
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 2.2 Capture the last tick's outcome in a bounded snapshot (mode seen, at, one row per refusal
  reusing the existing log vocabulary — wave reasons, suppressed, loop unreadable, registry missing,
  foreign claim, would-have-dispatched) replacing, not accumulating, the previous snapshot.
      req: watcher-settings-control/1
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs, src/root/AiOrchestrator.Server/Watching/WatcherSnapshot.cs
- [x] 2.3 Functional tests: a mode lowered between ticks governs the next tick with no restart; a
  persisted unknown value refuses with the controlled-admission refusal and no tick runs.
      req: dispatch-observation/1, dispatch-observation/10
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs

## 3. Host endpoints

- [x] 3.1 `GET /api/watcher` — mode, interval, last tick at, last tick's refusals; host-mapped like
  `/api/attention`, declaring its access.
      req: watcher-settings-control/1
      files: src/root/AiOrchestrator.Server/Watching/WatcherEndpoints.cs, src/root/AiOrchestrator.Server/Program.cs
- [x] 3.2 Lower command (`POST /api/watcher/mode/lower`-shape, one file, `ErrorOr`): refuses
  anything that is not exactly one rung down with a 4xx naming the legal rung; applies without
  restart; logs the lowering.
      req: watcher-settings-control/2
      files: src/root/AiOrchestrator.Server/Watching/WatcherEndpoints.cs
- [x] 3.3 Raise command (`POST /api/watcher/mode/raise`-shape): refuses skips, refuses raising from
  unset above `observe`, applies the same rung rules; logs the raising.
      req: watcher-settings-control/3
      files: src/root/AiOrchestrator.Server/Watching/WatcherEndpoints.cs
- [x] 3.4 Pilot task set/clear command with the task-key shape validated and empty meaning the full
  cut.
      req: watcher-settings-control/4
      files: src/root/AiOrchestrator.Server/Watching/WatcherEndpoints.cs
- [x] 3.5 Functional tests for each refusal path (no rung below, skip refused, first rung is
  `observe`) and for the happy paths.
      req: watcher-settings-control/2, watcher-settings-control/3, watcher-settings-control/4
      files: src/tests/AiOrchestrator.Server.FunctionalTests/WatcherEndpointsTests.cs

## 4. Screen

- [x] 4.1 Append the watcher read and the three commands to `contracts.ts` and `queries.ts` next to
  the settings block; append the `settings.watcher` group to `en.ts` with the confirmation copy that
  names the consequence of raising (`observe-claims` acquires, `dispatch-claims` seats agents on the
  tasks the tick finds ready).
      req: watcher-settings-control/1, watcher-settings-control/3
      files: src/frontend/shared/http/contracts.ts, src/frontend/shared/http/queries.ts, src/frontend/shared/i18n/en.ts
- [x] 4.2 Compose the "Loop watcher" section on `SettingsScreen.tsx` from the kit: state row (mode,
  interval, last tick), refusals list, lower and raise buttons with the confirmation dialog, pilot
  task field. No hardcoded copy, every value a token, per DESIGN.md.
      req: watcher-settings-control/1, watcher-settings-control/2, watcher-settings-control/3, watcher-settings-control/4
      files: src/frontend/features/settings/WatcherSection.tsx, src/frontend/features/settings/SettingsScreen.tsx
- [x] 4.3 Verify against a running app (`aspire start --isolated`): mode shown, refusals listed,
  lowering applies on the next tick, raising asks and names the consequence, pilot edit applies.
      req: watcher-settings-control/1, watcher-settings-control/2, watcher-settings-control/3, watcher-settings-control/4
      files: src/frontend/features/settings/WatcherSection.tsx

## 5. Packaged survival and check

- [x] 5.1 Verify the store path lands outside the bundle on the installed app (pack + install once,
  set a mode, reinstall, mode survives).
      req: watcher-settings-control/5
      files: src/shared/AiOrchestrator.Infrastructure/Watching/WatcherStore.cs
- [x] 5.2 Run the gate: `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`,
  and record the observed counts in the issue's check.
      req: watcher-settings-control/1, watcher-settings-control/2, watcher-settings-control/3, watcher-settings-control/4, watcher-settings-control/5
      files: openspec/changes/watcher-settings-control/tasks.md
