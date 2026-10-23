# Tasks: check-the-claim-in-its-worktree

## 1. The watcher asks for the claim's path

- [x] 1.1 Add the worktree-path helper beside `LabelWatcher` (Server, `Watching/`): `change/397` →
      `.worktrees/change-397`, with the comment naming `RunWorkingDirectory` as its twin and why the
      copy exists (Runs' type is internal; a BuildingBlocks seam cannot reach a module).
      req: dispatch-observation/11
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 1.2 In `Dispatch`, hand `facts.OpenSpecChange` the claim's working path — the worktree
      directory when `Directory.Exists` says so, else `candidate.Folder.Path` — and pass the claim's
      branch name (`candidate.WorktreeName`, `change/<n>`) for the ref fallback. Extend the facts'
      fake in `LabelWatcherTests` to record its arguments.
      req: dispatch-observation/11
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs

## 2. The branch fallback in `LaunchFacts`

- [x] 2.1 Extend `ILaunchFacts.OpenSpecChange` with the claim's branch name parameter and update
      the single World implementation `LaunchFacts`: directory enumeration first (unchanged), then,
      when no change was found and the `change/<n>` argument is present, `git ls-tree --name-only
      origin/change/<n> -- openspec/changes/` through `GitCli` — a `tasks.md` under any change
      directory answers `Exists` with the change's name.
      req: dispatch-observation/11
      files: src/shared/AiOrchestrator.BuildingBlocks/World/ILaunchFacts.cs, src/shared/AiOrchestrator.Infrastructure/Agents/LaunchFacts.cs
- [x] 2.2 Write `LaunchFactsTests` (InfrastructureTests, real temp git repo like
      `GitHistoryTests`): the change in the directory answers; the change only on the remote ref
      answers; neither refuses.
      req: dispatch-observation/11
      files: src/tests/AiOrchestrator.InfrastructureTests/LaunchFactsTests.cs

## 3. The wire tests

- [x] 3.1 `LabelWatcherTests`: a `ready-for-implementation` task whose change is in its worktree
      dispatches (fake facts answering `Exists: true` for the worktree path, `false` for the primary
      path, so the test pins which path was asked).
      req: dispatch-observation/11
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 3.2 The genuine refusal keeps its words: no change in directory or ref → the existing
      `no OpenSpec change in openspec/changes/` refusal and its remedy stand.
      req: dispatch-observation/11
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs

## 4. Gate and check

- [x] 4.1 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      and record the full test count in the issue check.
      req: dispatch-observation/11
      files: openspec/changes/check-the-claim-in-its-worktree/tasks.md
- [x] 4.2 Verify against reality per the issue's check: this machine holds #397 at
      `ready-for-implementation` with its propose in `.worktrees/change-397` and pushed to
      `origin/change/397` — after the fix, the watcher's next tick in `dispatch-claims` dispatches
      it instead of refusing; and a task with no change anywhere still refuses.
      req: dispatch-observation/11
      files: openspec/changes/check-the-claim-in-its-worktree/tasks.md
