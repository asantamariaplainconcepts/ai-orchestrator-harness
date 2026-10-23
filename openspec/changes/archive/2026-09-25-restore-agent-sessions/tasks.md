## 1. The seam and the row

- [x] 1.1 Add `ITerminalRestore` to `BuildingBlocks/World/` (report open, report closed) and register the Workspace implementation in `World.cs`, appended at the end of the block
      req: restore-open-terminals/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/ITerminalRestore.cs, src/modules/Workspace/AiOrchestrator.Modules.Workspace/WorkspaceModule.cs
- [x] 1.2 Add the `TerminalSessionRow` entity, `WorkspaceDbContext` mapping and the Workspace migration for the `terminal_sessions` table
      req: restore-open-terminals/1
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Domain/TerminalSessionRow.cs, src/modules/Workspace/AiOrchestrator.Modules.Workspace/Persistence/WorkspaceDbContext.cs, src/modules/Workspace/AiOrchestrator.Modules.Workspace/Persistence/Migrations/20260925163019_RestoreTerminalSessions.cs

## 2. The terminals layer reports

- [x] 2.1 `TerminalSessions` reports every opened shell and agent terminal through the sink (folder, directory, kind, agent id, vendor session id when known, label, terminal id) and forgets the row on `Close`; best-effort with a log line, never fatal
      req: restore-open-terminals/1
      files: src/shared/AiOrchestrator.Infrastructure/Terminals/TerminalSessions.cs
- [x] 2.2 `TerminalRequest` gains the vendor session to continue; `TerminalSessions.Open` appends the resume arguments instead of pinning a fresh session when one is carried
      req: open-a-terminal/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/ITerminals.cs, src/shared/AiOrchestrator.BuildingBlocks/World/IAgentRunner.cs, src/shared/AiOrchestrator.Infrastructure/Agents/PtyAgentRunner.cs, src/shared/AiOrchestrator.Infrastructure/Terminals/TerminalSessions.cs

## 3. The restore

- [x] 3.1 `OpenTerminal` accepts the session to continue (from a trusted caller only — the restore, not the wire) and composes the resume line for an agent terminal
      req: open-a-terminal/1
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/OpenTerminal.cs
- [x] 3.2 `TerminalRestore` hosted service in the Workspace module: walk the rows oldest first on boot, validate the session id (non-empty, ≤512, no control characters, not dash-leading), resolve each against the folder (drop and log what no longer exists), reopen through the terminal path, respect the session ceiling, drop what did not fit
      req: restore-open-terminals/1, restore-open-terminals/2, restore-open-terminals/3
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/TerminalRestore.cs
- [x] 3.3 Register the hosted service in `WorkspaceModule`
      req: restore-open-terminals/3
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/WorkspaceModule.cs

## 4. Verified

- [x] 4.1 FunctionalTests: a recorded row restores as a terminal with the resume line; a dash-leading or oversized session id restores without resume; a row for a gone directory is dropped; a declared command leaves no row
      req: restore-open-terminals/1, restore-open-terminals/2, restore-open-terminals/3
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/TerminalRestoreTests.cs
- [x] 4.2 `aspire do ci` passes against the running product, and a manual restart of the AppHost with a claude terminal open restores the conversation
      req: restore-open-terminals/1, restore-open-terminals/2, restore-open-terminals/3
      files: src/tests/AiOrchestrator.SharedFunctionalTests/ApiServiceFixtureBase.cs
