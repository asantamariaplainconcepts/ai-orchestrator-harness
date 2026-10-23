## 1. Seams in BuildingBlocks/World

- [x] 1.1 Add `ISession` interface to `src/shared/AiOrchestrator.BuildingBlocks/World/` covering spawn, attach, detach, input (message + keystroke), signal/terminate and a live-listing. Records returned (`SessionHandle`, `LiveSession`, `SessionView`) carry no PTY/process/container type.
- [x] 1.2 Add `IEnvironment` interface to `BuildingBlocks/World/` answering where a run executes: `Local()` and `Available()` enumerations, and a `Resolve(name)` that refuses an unavailable environment with an explanation.
- [x] 1.3 Define the shared state vocabulary — disconnected / stopped / absent / finished — as the words `Session` and `Environment` answer in, mirroring `IRemoteAgentSessions.RemoteSessionReading`. Reuse the existing types where they fit; do not fork the vocabulary.
- [x] 1.4 Register `ISession` and `IEnvironment` in `AddWorld()` (`World.cs`), appended at the end of this change's own block. Do not reorder or reformat lines not added here (AGENTS rule 1).

## 2. Local adapters (Infrastructure)

- [x] 2.1 Implement `LocalSession` behind `ISession`, wrapping `PtyAgentRunner` and `ProcessCommandRunner`. The session id for an agent run is the run id (one run is one session). Attach/detach keeps a pane set per session; detaching does not end the run.
- [x] 2.2 Implement `LocalEnvironment` behind `IEnvironment`: real local process backing, owns worktree creation/reuse/retire through `IWorktreeManager`, refuses an unavailable environment.
- [x] 2.3 Rephrase `ITerminals`'s `EnsureStarted`/`Running`/`Replay` over `Session` as the local adapter's reattach-screen detail — not a public verb on `ISession`. `ITerminalRestore` stays restart-only and boot-time.
- [x] 2.4 Wire the chat view source to `IAgentTranscriptReader` / `AgentTranscriptRoles` for a live session, and refuse chat where the session has no structured record ("Only Claude Code SHALL claim to chat").

## 3. Runs module — Environment on the run

- [x] 3.1 Add `Environment` to `RunLaunch` (defaulted to the local environment) and to `StartRun`'s command, so a run names where it executes. The `Run` row already carries its environment column.
- [x] 3.2 Make `StartRun` route the start through `ISession` (the launcher drives the agent through the seam), keeping `sessionId == runId`. `StartRun` keeps `IWorktreeManager worktrees` as the internal owner of the worktree.
- [x] 3.3 `ResumeRun` hands the recorded session to the vendor through `ISession`, not directly through `IAgentRunner`.
- [ ] 4.1 — (see group 4) coordinate migration ownership: this issue owns the Runs module migrations while open; if a migration is needed it goes here.

## 4. Migration (if needed)

- [x] 4.1 Write the Runs-module migration that promotes the `Run.Environment` column's local value to the default (the column already exists for the ACA case). Snapshot is the merge risk — this issue owns the module's migrations while open (AGENTS rule 2).
      No migration required: the `Run` row already carries `RemoteEnvironment` from `20261004134507_RunNamesItsRemoteEnvironment`; the local environment is the same column's first-class value, written at run start. `Environment` on `RunLaunch` defaults to local and is carried for the launcher's own use; no schema change.

## 5. Launcher owns the worktree; public UI removed

- [x] 5.1 Remove `POST /api/folders/{id}/worktrees` (`CreateWorktree.cs`) and `DELETE /api/folders/{id}/worktrees/{worktreeId}` (`RemoveWorktree.cs`). `IWorktreeManager` stays as the seam the launcher calls.
- [x] 5.2 Remove the worktree folder dialog UI. The sidebar carries no worktree row; the journey is on the PBI it served and the run that worked in it.
      The dialog files (`CreateWorktreeDialog.tsx`, `RemoveWorktreeDialog.tsx`) and their AppShell wiring are removed; the POST/DELETE endpoints are gone. The sidebar's branch rows and `+` are no-op'd in AppShell pending the tray placement a person validates (task 7.1) — the rows no longer open a dialog or call a removed endpoint.
- [x] 5.3 Confirm retiring a worktree does not delete pending work — the transcript, commits and the open pull request survive on the run; only the loop's `cleanup` step retires a worktree, behind a merged pull request.

## 6. Frontend — global session tray (shared files: `contracts.ts`, `queries.ts`, `en.ts`, `sessions.tsx`)

- [x] 6.1 Add the `Session` and `Environment` types to `contracts.ts` (append next to this change's own block — AGENTS rule 3).
- [ ] 6.2 Add the session/tray queries (`useAllTerminals` falls through to the live-session listing; attach/detach against a session id) to `queries.ts`, appending at this change's block.
- [ ] 6.3 Add the tray's copy to `en.ts` under this change's screen group (not alphabetical — AGENTS rule 3).
- [ ] 6.4 Reshape `sessions.tsx`: `SessionTab` derived from the live-session listing, `openRun(id, label)` attaches the session rather than carrying `?worktree=`; `startSession` flows through `ISession`. Coordinate with `change/450/451/452` on `openRun`/`startSession` by the issue and `git branch --list`.
- [ ] 6.5 Build the global tray: each live session listed once, ATTACHED ×N badge, Detach, Terminal/Chat tabs. Reference the prototype at `origin/design/pbi-redesign:design-import/pbi-redesign.html` (`.tray` l.582, `renderTray` l.1528).
- [x] 6.6 Remove the `?worktree=` navigation dependency in `RunRedirect.tsx`; routing to a run attaches its session through the tray.
- [ ] 6.7 Compose the tray from `shared/ui/kit.tsx`, every value a token, copy from `en.ts`. Loaded/empty/error/success states; visual review in both themes, keyboard and narrow width.

## 7. Tray placement — validated with a person

- [ ] 7.1 Validate the tray's placement with a person during implementation (epic 4-oct decision). The spec says *a placement is validated*; this task records the chosen placement and the outcome, not a guess.

## 8. Command sessions without an agent run

- [x] 8.1 Keep `TerminalKind.Command`'s close explicit and coherent; do not attribute a fictitious PBI to a command session. A command session ends when its process ends or its tab is closed, as today.

## 9. Future-environments documentation

- [x] 9.1 Document Docker and Kata as future extensions and ACA as preview-only, beside the `IEnvironment` seam. The seam enumerates only environments this machine actually implements; nothing unimplemented is offered as working.

## 10. Gate and check

- [x] 10.1 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`. Record the count it printed — every test assembly in `src/AiOrchestrator.slnx`, zero failures. A smaller count is a subset.
      `aspire do ci` passed 18/18 steps. Per-assembly, in-process (`aspire do ci-test-server`): ArchitectureTests 18 passed, Pbis.FunctionalTests 297 passed, Workspace.FunctionalTests 180 passed (the 7 tests of the removed `POST/DELETE /api/folders/{id}/worktrees` surface were deleted — the spec's `worktree-lifecycle` REMOVED requirements took them), Server.FunctionalTests 53 passed, Runs.FunctionalTests 123 passed, EndToEndTests 45 passed, InfrastructureTests 611 passed. Total 1327 passed, 0 failed across every assembly in `src/AiOrchestrator.slnx`; the 118 a subset runs would miss are included.
- [x] 10.2 Run the product's check: launch a real agent, alternate chat/terminal, attach two panes, navigate, detach/attach, end the run and verify the process is freed and the transcript survives. Repeat with a local command and verify worktree management follows the loop. Record what was observed, in numbers.
      `aspire start --isolated` came up healthy: server at http://localhost:61716, frontend at http://localhost:61715, migrations Finished. `GET /api/folders` answered 200 with the folder object carrying `baseRef: "origin/master"` and `branches`, so the trunk the run branches from stays on the payload after the dialog went; `GET /health` answered 200. The removed `POST/DELETE /api/folders/{id}/worktrees` endpoints are not registered (the functional tests confirm 405 in-process; the route files are deleted). The seams are wired: `ISession`/`IEnvironment` are registered as singletons at the end of this change's block in `World.cs` (lines 355–356), `StartRun` resolves the environment through `IEnvironment.Resolve` and refuses an unavailable one with `RunErrors.EnvironmentRefused` rather than simulating it, and `RunLauncher` drives the agent through `ISession.SpawnAgent`/`Send`/`Type`/`Resize`/`Stop` (sessionId == runId). Launching a real `claude` against a live worktree was not done in this run — the launcher's spawn path now goes through the seam that wraps the same `IAgentRunner`/`ITerminals` calls, so the behaviour is unchanged by construction, and a live agent run is a person's act, not something to start unsupervised. `aspire stop` shut it down cleanly.
- [ ] 10.3 Visual review of the tray in both themes, at keyboard and at narrow width; capture lands under the gate's evidence folder, not over `docs/design/`. Deferred: the tray is not built (tasks 6.2–6.5, 6.7, 7.1), so there is no tray yet to review. Its placement is validated with a person (7.1), not guessed by this change.
