## Why

The terminals open inside the app live in a `ConcurrentDictionary` in the server process — `TerminalSessions` says so in its own docblock — so a restart or a crash drops every one of them: no tabs, no shells, no agent conversations, even though the vendor session id of every agent terminal is already known at spawn and the vendor's own CLI can continue the conversation from it. A person who restarts the app comes back to an empty screen for no reason the vendors impose: `claude --resume <id>` is one flag away.

## What Changes

- A restore seam beside the existing ones in `BuildingBlocks/World`: the terminals layer reports what it holds so Workspace can write it down, and a restore reads it back and reopens the terminals. Same split as `ICheckSink`, for the same reason: the layer that owns processes knows nothing about schemas, the module that owns the schema must not spawn anything.
- `TerminalSessions` reports every open shell and agent terminal as it opens and closes, through the new sink. Commands are deliberately not restored — a declared command is a process this product starts for a purpose, and restarting the app is not a request to run it again unasked.
- `TerminalRequest` learns to continue a vendor session: the agent's resume arguments are appended by the existing pin mechanism, so a restored agent terminal resumes the conversation it was having (`claude --resume <id>`, `copilot --resume=<id>`, `opencode --session <id>`) rather than starting a new one.
- A restore passes in Workspace on boot, after the database is up: a hosted service in the Workspace module walks the persisted rows and calls the same `OpenTerminal` path a person does, dropping rows whose folder or directory no longer exists.
- The tab strip needs no frontend work: `useTerminals` already lists server-side sessions, and the restored terminals appear in the list the same as any other. (Kept in scope only if the restore does not; verified during implementation.)

## Capabilities

### New Capabilities
- `restore-open-terminals`: terminals survive a server restart — recorded as rows by the Workspace module from reports across the World seam, and restored by reopening them against the vendor's own resume material where there is one.

### Modified Capabilities
- `open-a-terminal`: `TerminalRequest` carries the vendor session to continue, so a restored agent terminal resumes its conversation instead of starting a new one behind the same tab.

## Impact

- `src/shared/AiOrchestrator.BuildingBlocks/World/` — one new interface (`ITerminalRestore` sink, name settled in design).
- `src/shared/AiOrchestrator.Infrastructure/Terminals/TerminalSessions.cs` — report open/close; accept a resume target.
- `src/modules/Workspace/AiOrchestrator.Modules.Workspace/` — new entity + table + migration, restore hosted service, one query use case for the restore to read.
- `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/OpenTerminal.cs` — accept and carry the session to continue.
- No schema crosses a module boundary; the restore reads only Workspace's own table and calls the World seam.
