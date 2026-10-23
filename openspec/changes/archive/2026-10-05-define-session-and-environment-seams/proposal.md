## Why

A run's session is welded to its worktree and to the PBI screen that opened it. The seams that exist today (`IAgentRunner`, `ICommandRunner`, `ITerminals`) are hardwired to a local PTY in a directory the caller names, the tray derives its tabs from the PBI's folder, and navigating away or closing the PBI panel can close the run's view. The product sells a journey, and a journey that ends whenever its screen closes is not the one the epic (#447) describes: Session abstracts the interaction, Environment the place, terminal and chat are two views of one session, and the tray is global so attach and detach do not depend on the PBI's screen.

## What Changes

- Add a `Session` seam in `BuildingBlocks/World` covering spawn, attach, detach, input, signal and a listing of live sessions, **without exposing PTY, process, or container types to the modules above it**. The local PTY (`PtyAgentRunner`, `ProcessCommandRunner`) becomes the first implementation, wrapped behind the seam.
- Add an `Environment` seam, separate from `Session`, with a **real local implementation** (`LocalEnvironment`). The folder declares its default environment and a run may override it among the available ones; an environment that is not available is refused with an explanation, never simulated.
- Add the `Environment` column to `RunLaunch` / `StartRun` so a run names where it executes, alongside the worktree the launcher owns. The `Run` row already carries its remote environment (`20261004134507_RunNamesItsRemoteEnvironment`); this names the local one the same way.
- Add a **global session tray** that lists each live session once and allows attach/detach from several panels. Navigating or detaching does not end the run. The tray's placement is **validated with a person during this change** (the epic's 4 oct. decision names it), not fixed here in the spec.
- Make the agent run's terminal and chat **two views of one session**: the terminal view consumes the raw stream (PTY/TUI, as `AgentTerminal.tsx` does today), the chat view consumes the structured record (`IAgentTranscriptReader` / `AgentTranscriptRoles`). A session with no structured record offers no chat — it is not invented.
- On run end or cancel, the session ends and its resource is freed, and it leaves the live list; transcript, cost, result, commits and PR are preserved (already in the `Run` aggregate and `GetRunTranscript.cs`).
- `Restore` is **removed from Session's public contract** and never revives a finished session. Reattaching a live local session may recover its screen as a detail of the local adapter; `ITerminalRestore` stays restart-only, as today.
- The **launcher owns the worktree**: create/reuse/retire it according to the loop. Retire the manual worktree UI (`POST /api/folders/{id}/worktrees`) and the `?worktree=` navigation dependency, preserving the journey and not deleting pending work when a view closes.
- **BREAKING**: the worktree-lifecycle capability drops from a first-class, UI-backed capability to an internal mechanism of the launcher. `IWorktreeManager` stays as the seam the launcher uses internally; its public HTTP surface and the folder dialog that calls it are removed.
- Document the future Docker/Kata extensions and ACA-preview separation. This delivery does not surface unimplemented environments as if they worked.

## Capabilities

### New Capabilities

- `run-a-session`: the `Session` seam and its local implementation. Spawning, attaching, detaching, input, signal and listing live sessions, with terminal and chat as two views. A session ends with its run and its transcript survives it.
- `choose-an-environment`: the `Environment` seam and its local implementation. The folder declares the default environment, a run may override it among the available ones, and an unavailable environment is refused with an explanation rather than simulated.

### Modified Capabilities

- `open-a-terminal`: the terminal becomes a view over a `Session` rather than a one-shot terminal the PBI's folder owns. Listing, attach and detach flow through the global session tray; the directory it resolves is the session's, not a `?worktree=` the screen carries.
- `chat-with-an-agent`: the chat becomes the structured-record view of the same session the terminal shows, sourced from `IAgentTranscriptReader`. A session with no structured record offers no chat. The folder/per-directory chat use case (`Chat.cs`, `GetChat`/`OpenChat` — specced, not on master) moves onto the session identity.
- `restore-open-terminals`: narrowed. Restore stays restart-only and out of Session's public contract; a restored agent terminal is registered openable and started on first attach, as today. Reattaching a live local session may recover its screen as a detail of the local adapter — this is not part of the public contract.
- `observe-a-remote-agent-session`: the four-state observation (`disconnected`/`stopped`/`absent`/`finished`) becomes the same vocabulary the local `Session` and `Environment` answer in, so a screen names a state whether the environment is local or remote.
- `resume-a-run`: a resume still creates a new run and carries the recorded session; it now does so through the `Session` seam rather than directly through the PTY runner.
- `read-a-finished-transcript`: unchanged in content; the transcript is what survives a finished session, sourced through the same `IAgentTranscriptReader` the chat view uses for a live session.
- `worktree-lifecycle`: **degraded** from a first-class, UI-backed capability to the launcher's internal mechanics. `IWorktreeManager` remains as the seam the launcher owns internally; its public HTTP surface and the folder dialog are removed. Git stays the record; the rows follow it.

## Impact

- **Seams in `World/`**: two new interfaces (`ISession`, `IEnvironment`) registered in `AddWorld()` in `World.cs`, appended at the end of this change's block per the AGENTS collision rule. Local adapters wrap the existing `PtyAgentRunner` / `ProcessCommandRunner` / `ITerminals`.
- **Runs module**: `StartRun.cs` gains the `Environment` parameter; `RunLaunch` gains the `Environment` column. The launcher continues to inject `IWorktreeManager` as the internal owner of worktrees; the public `POST /api/folders/{id}/worktrees` and `CreateWorktree.cs` / `RemoveWorktree.cs` are removed.
- **Workspace module**: the manual worktree UI surface goes; `ITerminalRestore` stays restart-only and reshapes around the session identity rather than the folder/tab.
- **Frontend shared files** (`contracts.ts`, `queries.ts`, `en.ts`, `sessions.tsx`): the global tray and the `Session`/`Environment` contracts land here. These are the three-plus-one shared files the AGENTS collision rule names; this change appends next to its own screen's block and does not reformat a neighbour's. Coordination with `change/450`, `change/451`, `change/452` (all touch `openRun`/`startSession`) is by the issue and `git branch --list`, not by a plan written into a file.
- **Migration**: the `Run` row already names a remote environment; this change adds the local environment as the same column's first-class value (local is the default, ACA was the first instance). Any new migration belongs to the Runs module and this issue owns the module's migrations while the change is open.
- **ADRs**: no accepted ADR is rewritten. ADR-0001 (the agent terminal runs in a browser) and ADR-0002 (a finished run is read from a record) are extended, not superseded: the session belongs to the server across screens, and the transcript the chat view reads is the same record ADR-0002 established. A new ADR recording the Session/Environment split may follow.
- **Out of scope**: no new cloud adapters (Docker, Kata); ACA stays preview-only. Merge guarantees, holds and `effectId`s untouched. No child tasks. The tray's placement is validated with a person during this change, not settled by the spec.
