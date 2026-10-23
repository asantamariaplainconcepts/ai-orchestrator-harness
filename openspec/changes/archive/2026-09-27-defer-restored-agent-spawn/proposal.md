## Why

The restore #368 shipped reopens the tab but not the conversation: `TerminalRestore` spawns
`exec claude --resume <id>` three seconds after boot, into a pseudo-terminal nobody is attached to. A
TUI with no reader exits without writing a transcript, the session finishes, and the person looking at
the restored tab sees an empty buffer — a fresh conversation they did not start, or a dead pane — and
goes hunting for the session by hand in the CLI. The #368 retro entry named the fix and left it
honest: resume-en-boot is not enough; the spawn has to wait for the person. Reported again on the
installed 0.1.3 (#384).

## What Changes

- The restore stops spawning agent terminals at boot. It yields the restored sessions to the terminal
  seam without starting a process: the row says the terminal exists — its id, its folder, its
  directory, its label, its vendor session — and the process is not started until somebody attaches
  to that terminal.
- Attaching to a not-yet-spawned restored agent terminal starts the process with the resume material
  the row carries (`claude --resume <id>`, `copilot --resume=<id>`, `opencode --session <id>`), so the
  conversation returns under the same tab, from the vendor session the row already persisted. A
  restored **shell** keeps its current behaviour — shells are cheap, and a login shell with no reader
  exits harmlessly.
- `TerminalSessions.Open` learns the shape of "a terminal that exists but has not started": the
  existing `ResumeFrom`/`RestoreId` contract is reused, with the spawn deferred rather than the row
  re-read.

## Capabilities

### New Capabilities

_(none)_

### Modified Capabilities

- `restore-open-terminals`: the third requirement ("The restore runs at boot and drops what no longer
  exists") changes — the restore still runs at boot and still drops, but a restored agent terminal is
  served before its process exists and the process starts on first attach, not at boot; the second
  requirement's scenario ("the restore reopens it") keeps its wording about the conversation continuing
  and gains the deferral as the mechanism that makes it true. A new scenario pins that nothing is
  running between boot and attach.

## Impact

- `src/shared/AiOrchestrator.BuildingBlocks/World/ITerminals.cs` — the seam's open path carries a
  deferred spawn for a restored agent terminal (`OpenAsTab` sessions that are listed but not running
  until attached). Shape settled in design.
- `src/shared/AiOrchestrator.Infrastructure/Terminals/TerminalSessions.cs` — a session that exists
  without a process; the spawn on first attach, through the existing `Pin()`/`ResumeArguments` path.
- `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/TerminalRestore.cs` — stops
  calling `terminals.Open` for agent rows; registers the deferred session instead. Shells unchanged.
- `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/TerminalRestoreSink.cs` —
  `Closed` also forgets a deferred session that was never attached (tab closed before first look).
- The SPA's attach path (`FolderTerminal.tsx`, SignalR) needs no new request: attaching to a listed
  session is already what a pane does; the server side turns that attach into the spawn. Verified in
  design against what the SignalR hub actually sends.
- Frontline behavioural surface: restoring a row whose vendor session cannot be resumed already drops
  resume material and restores without it — unchanged.
