## Context

The product's session model is welded to a directory and to the PBI screen that opened it. Three seams carry execution today, none of them named "Session":

- `IAgentRunner.Start(AgentInvocation)` takes a `WorkingDirectory` and returns a byte stream. The directory IS the place, and there is no `Environment`.
- `ICommandRunner.Start(sessionId, commandLine, workingDirectory)` does the same for a folder's own commands, in a pseudo-terminal to preserve build tools' TTY detection.
- `ITerminals` holds the live sessions in a process-local dictionary (`TerminalSession.Session` xml-doc: *"It does not survive the server"*) and exposes `All()` — the base of the tray today. There is no multi-panel attach/detach primitive: a second pane over the same id is not a first-class citizen, only the hub's replay makes it share one stream.
- `IRemoteAgentSessions` (#434, ACA-only) is the closest precedent: it separates a `Read` door (effect-free) from a `Connect` door (only one that may activate), names four states that do not collapse (`disconnected`/`stopped`/`absent`/`finished`), and answers "unreachable is a fact a screen can name, not a registration error". Its `RemoteSessionQuery(Environment, SessionId)` is already the shape `Session`/`Environment` want.

`IRunLauncher.RunLaunch.RunId` carries the xml-doc *"Also the agent session id — one run is one session, so they are one value"* — `sessionId == runId` is already established for agent runs. `StartRun.cs` already injects `IWorktreeManager worktrees` and calls `worktrees.Ensure(...)`; the missing half is that `StartRun` carries no `Environment`, so a run cannot say where it executes. `Run` already names a remote environment (`202601004134507_RunNamesItsRemoteEnvironment`); this change names the local one the same way.

The chat capability (`chat-with-an-agent`) is specced but its use case (`Features/Folders/UseCases/Chat.cs`, `GetChat`/`OpenChat`) is not on master. The structured record the chat view would consume IS in master: `IAgentTranscriptReader` maps the vendor's record onto `AgentTranscriptRoles` (person, agent, thinking, tool, result). The criterion *"no se inventa chat para una sesión sin registro estructurado"* lines up with the spec's *"Only Claude Code SHALL claim to chat"*.

The frontend tray today is `SessionsBar.tsx` + `sessions.tsx`, a store whose `SessionTab` kinds are `{shell, agent, command, run}` derived from `useAllTerminals()` + `useRuns()`, with `openRun(id, label)` and `startSession({folderId, kind, worktreePath})` still anchored to folder/worktree. The fixed bottom tray with ATTACHED ×N badges, Detach, and Terminal/Chat tabs in `design-import/pbi-redesign.html` (branch `origin/design/pbi-redesign`, `.tray` l.582, `renderTray` l.1528) is new in this change.

Stakeholders: this change sits under epic #447 and is blocked by #452 (worktree-is-a-loop, which establishes that a worktree is the instance of a loop for one issue). `pbi-navigation` (#450/#451/#452) runs in parallel on `change/450`, `change/451`, `change/452` and touches the same `openRun`/`startSession` in `sessions.tsx`. The four PBI-screen files (`RunRedirect.tsx`, `WatchExecutionEntry.tsx`, `PbiDetailPane.tsx`, `JourneyHeader.tsx`) all consume the shared store.

## Goals / Non-Goals

**Goals:**

- A `Session` seam that covers spawn, attach, detach, input, signal and the live-list, with no PTY/process/container type escaping above it.
- An `Environment` seam with a real local implementation; the folder declares the default, a run may override it among the available ones, unavailable is refused.
- Terminal and chat as two views of one session — the raw stream and the structured record — sharing identity and lifecycle.
- A global tray that lists each live session once and allows attach/detach from several panels without ending the run.
- The launcher as the sole owner of the worktree; remove the manual UI and the `?worktree=` navigation dependency.
- The tray's placement validated with a person during implementation, not fixed by this spec.

**Non-Goals:**

- No new cloud adapters (Docker, Kata). ACA stays preview-only and is not surfaced as if working.
- No reviving finished sessions. Restore stays restart-only and out of the public `Session` contract.
- No change to merge guarantees, holds, `effectId`s or the canonical-identity formula (epic #448).
- No child tasks (epic: no vendor brings them).
- No definitive tray placement decision here — it is documented in the epic's 4-oct decision and carried forward to a person.

## Decisions

### Decision 1: `ISession` is the carrier; PTY, process and container are below it

The session is the thing a pane attaches to and a run names. The seam lives in `BuildingBlocks/World/` and returns records a screen can render without ever holding a process handle. The local adapter wraps `PtyAgentRunner` / `ProcessCommandRunner` — the PTY is the first implementation, not the contract.

**Why over the alternative:** the alternative is to widen `ITerminals` with attach/detach/replay primitives and ask every caller to keep treating a "terminal" as the session. That leaves a PA named after its transport, exposes the dictionary's "does not survive the server" to every consumer, and makes a remote session and a local session two different shapes. `IRemoteAgentSessions` already proved the other shape — a state-reading door and an attaching door, neither of which hands a process to the caller — so the local side becomes its mirror.

**Alternatives considered:**

- *Widen `ITerminals`.* Rejected: the name says transport, the dictionary is process-local, and the criterion is explicit that a second pane is a first-class citizen, not a hub replay implementation detail.
- *A new `IPaneManager` above `ITerminals`.* Rejected: it would still funnel through the dictionary and the run's identity would not be the session's.

### Decision 2: `IEnvironment` is separate from `ISession`, and local is real

`Environment` answers "where does this run execute". `Session` answers "what is the interaction". A run is one session on one environment; the environment is named on the run, as it already is for the remote case. The local adapter is `LocalEnvironment` — real, not a stub — and is the folder's default. A run may override it among the available ones; an unavailable environment is refused with an explanation, never simulated.

**Why over the alternative:** pairing the two into one `IRunHost` collapses two questions the epic kept apart — *Session abstracts the interaction, Environment the place of execution*. They change for different reasons (a chat is many sessions; a remote preview is one environment) and the ACA precedent already separated them as `RemoteSessionQuery(Environment, SessionId)`.

**Alternatives considered:**

- *One seam `IRunHost` carrying both.* Rejected by the epic §2 (Session and Environment are two seams).
- *No `Environment` seam; keep the directory in `RunLaunch`.* Rejected: the directory IS the place, hardcoded local, and the criterion names override-and-refuse.

### Decision 3: The agent session id and the run id stay one value

`IRunLauncher.RunLaunch.RunId` is *"also the agent session id — one run is one session"*. This change keeps that identity and extends it to `Session`: a run's session id IS its run id. The `Session` seam is the surface through which the launcher drives the agent, not a second id space.

**Why over the alternative:** minting a separate session id split one fact into two columns and forced a join the launcher already avoids. Keeping one value is the decision the launcher's xml-doc already states.

### Decision 4: Terminal and chat are two views of one session, sourced from the seam and the reader

The terminal view consumes the raw byte stream (as `AgentTerminal.tsx` does today) through the `Session` seam; the chat view consumes the structured record through `IAgentTranscriptReader` / `AgentTranscriptRoles`. Both attach to the same session identity. A session with no structured record offers no chat — the spec's *"Only Claude Code SHALL claim to chat"* is the same line.

**Why over the alternative:** the alternative is a chat that stitches the vendor's tail and the record together, which the `chat-with-an-agent` spec forbids ("Nothing SHALL stitch the two together"). Two views over one identity keeps the record authoritative and avoids inventing a chat for a session that has no record.

### Decision 5: The launcher owns the worktree; the public UI surface and `?worktree=` go

`IWorktreeManager` stays as a registered seam, but its only caller becomes the launcher (`StartRun.cs` already injects it). The `POST /api/folders/{id}/worktrees` (`CreateWorktree.cs`), the remove endpoint and the folder dialog are removed. The `?worktree=` navigation dependency in `RunRedirect.tsx` is removed; routing to a run attaches its session through the tray.

Retiring a worktree is the launcher's job per the loop (the epic: *"retirar no elimina el viaje"*). Removing the manual UI is **BREAKING** against `worktree-lifecycle`, which is why that capability is degraded.

**Why over the alternative:** the alternative is to leave the UI as a "manual override". That leaves two owners of the worktree and a navigation dependency the PBI redesign removes — the folder's screen would still claim the worktree as a destination, and closing it would still feel like closing the journey.

### Decision 6: Restore stays restart-only, out of the public `Session` contract

`ITerminalRestore` serves boot-time restore and `EnsureStarted` (the deferred spawn of a restored agent terminal). Reattaching a live local session may recover its screen as a *detail of the local adapter* — that is `EnsureStarted`/`Running`/`Replay` rephrased over `Session`, not a new public verb. Restore does not revive a finished session.

**Why over the alternative:** the criterion is explicit — restore is not part of the public contract. Exposing it would invite callers to revive sessions that ended, which is the faking the criterion forbids.

### Decision 7: The local adapter is the mirror of the ACA adapter's state vocabulary

The four states `IRemoteAgentSessions` carries (`disconnected`/`stopped`/`absent`/`finished`) become the vocabulary the local `Session`/`Environment` answer in too. A screen names a state whether the environment is local or remote, which is the `observe-a-remote-agent-session` modification.

## Risks / Trade-offs

- **Shared frontend files collide** with `change/450`/`451`/`452`. → Coordinate by the issue and `git branch --list`; append next to this change's own block in `sessions.tsx`/`contracts.ts`/`queries.ts`/`en.ts` and do not reformat a neighbour's. The seam's contracts land before the tray takes over the store.
- **Two new seams in `World.cs`** is a whole-file conflict risk. → Append at the end of this change's own block; do not reorder or reformat lines not added here (AGENTS rule 1).
- **Migration** touches the Runs module snapshot. → One issue owns the module's migrations at a time; this issue claims it and, if it needs one it does not own, says so and waits (AGENTS rule 2).
- **Removing the worktree UI is BREAKING.** → It is marked breaking in the proposal; the `worktree-lifecycle` delta is REMOVED-with-reason, and the launcher's internal ownership is documented.
- **Tray placement deferred to a person** looks like an open question a spec should close. → It is not a gap: the 4-oct epic decision names it as "validated during #453". The spec says *a placement is validated with a person*, not *which placement*. The task carries the validation, the spec does not prescribe the answer.
- **Chat for a non-structured session** could be tempting to fake. → The seam refuses it: no record means no chat view, said plainly.

## Migration Plan

- No data migration is required for `Session`/`Environment` themselves — sessions live in a process-local dictionary as today, and `Environment`'s local value is the folder's default on existing rows. The `Run.Environment` column already exists from `20260927185923_AcaSessionFacts`/`20261004134507_RunNamesItsRemoteEnvironment`; this change writes the local environment into it as the default.
- The `ITerminalRestore` rows continue to drive boot-time restore; their shape does not change in this delivery.
- Rollback is the branch — the change is on `change/453` and the seam contracts are additive; removing the branch restores the old `IAgentRunner`/`ITerminals` paths.
- The manual worktree UI removal is the one breaking surface; rollback restores `CreateWorktree.cs`/`RemoveWorktree.cs` and the `POST`/`DELETE` endpoints.

## Open Questions

- **Tray placement** — validated with a person during implementation (epic 4-oct decision). The spec names that a placement is validated; the task names the validation.
- **Command sessions without a run** — the criterion: *"sesiones de comandos sin run de agente mantienen un cierre explícito coherente; no se les atribuye un PBI ficticio"*. They keep explicit close; they are not assigned a fictitious PBI. The implementation keeps the `TerminalKind.Command` close path explicit.
