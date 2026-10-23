## Context

`TerminalSessions` holds open terminals in a `ConcurrentDictionary<Guid, Session>`; its own docblock says a session lives as long as the server process. `ITerminals.cs` documents the consequence on `TerminalSession.Session`: the vendor session id "does not survive the server… Persisting them is its own item" — that item is this change. Issue #6 grilled this ground before its ship was halted; its findings are the prior art this design starts from:

- `ICheckSink` is the exact precedent for the split: the terminals layer reports endings across a `BuildingBlocks/World` seam, `Features/Folders/CheckSink.cs` in Workspace writes the table. Same shape as `IRunSink`.
- Orca's `SleepingAgentSessionRecord` carries `launchConfig` (how to relaunch, not just the id) and prunes records whose owner disappeared. A row pointing at a deleted folder is a dead row.
- A persisted session id is read back from a table and put on a command line again, so it must be validated before it is appended — a leading dash stops being a string and becomes a flag.

What survives a restart today without this change: structured chats (`AgentChatChain` derives their ids from folder + directory + agent) and runs (a table plus `RunReconciliation`). What does not: shells and agent terminals.

## Goals / Non-Goals

**Goals:**

- A shell terminal reopens on the same folder/worktree directory after a restart, as a fresh shell.
- An agent terminal reopens and **resumes the conversation it was having**, using the vendor session id recorded at open.
- The restore runs on boot without a person's action, and without re-launching declared commands.
- Rows whose folder or directory no longer exists are dropped rather than restored into errors.

**Non-Goals:**

- Reconnecting to live PTYs — the processes die with the server; this restores the *conversations*, not the processes.
- Persisting scrollback — the replay buffer is deliberately memory-bounded and stays so.
- Restoring declared-command terminals — a dev server relaunched unasked at boot is a surprise with a port attached.
- Restoring the *selected* tab or window layout — the tab strip lists server sessions; which one was on top is a person's business and the list answers it.

## Decisions

### D1. One new World seam, shaped like `ICheckSink`

`ITerminalRestore` (name mirrors the verbs the codebase already uses): the terminals layer reports open sessions and their resume material; Workspace writes them. The restore direction goes through the same seam as a read — a use case asks the seam for "what should be restored", or rather: the Workspace module owns a hosted service that reads its own table and calls the same `OpenTerminal` path a person does.

Alternative rejected: a `BackgroundService` inside Infrastructure that walks a Workspace table — crosses the module boundary in the exact direction MOD001 forbids.

### D2. What a row carries: the launch line, not a reconstruction

Persist per session: folder id, working directory, kind (shell/agent only), agent id, the vendor session id (already known at open: pinned or discovered), the label, and the id the terminal had. Restoring reuses `TerminalTarget.Resolve` + the existing `OpenTerminal` handler logic, which recomputes the line (`exec $SHELL -l` / `exec <binary>`) from the folder rather than replaying a stored command string — a stored line would be a `.harness/commands.json`-shaped trust problem for no benefit.

The vendor session id is the only thing that must be remembered rather than recomputed, because it is minted by the vendor.

### D3. Resume through the existing pin mechanism

`TerminalSessions.Open` already pins a session id before spawn (`agents.PinSession`). The restore passes the *previous* vendor session: `TerminalRequest` gains a `ResumeFrom`-shaped field, and for an agent terminal the line becomes `exec <binary> <resume-args>` using `AgentDefinition.ResumeArguments` (the same functions `ResumeCommand` composes from). No fork — the restored conversation continues under the id it already had where the vendor supports that; where the vendor mints its own (OpenCode), the id is passed and the vendor decides.

Validation before the line is built: the persisted id must be non-empty, ≤512 chars, no control characters, and not start with `-` — the rules orca's `normalizeSessionId` learned the hard way.

### D4. Where the reporting hooks in

Report on `Open` (recording) and on `Close` (forgetting) — not on process exit: a finished-but-not-closed terminal is a tab somebody is reading, and restoring *it* is not required for the conversation to continue; the row is removed when the tab is closed, and a finished process's row is restored as a fresh shell only if it was an agent with a session id... it was not — kind shell restores fresh, kind agent resumes. Simplest honest rule: **the row tracks the terminal, and restore reopens it**; a terminal whose process had already exited before the restart reopens and says so itself (fresh shell prompt / resumed conversation).

Best-effort like `ICheckSink`: a report that cannot be written is logged and dropped, never fatal to the terminal.

### D5. The restore is a hosted service in Workspace

`TerminalRestore` : `BackgroundService` in the Workspace module (precedent: `RunReconciliation`, `RunLogRetention` in Runs). On start, read rows, resolve each against the folder table (drop rows whose folder or working directory is gone), and call the terminal-open path with the resume material. Sequential, logged, one line per restored terminal. Order by `StartedAt` — the same order tabs are restored in.

### D6. One table, Workspace schema, next to `check_results`

`terminal_sessions` (Workspace schema): `Id` (the terminal's own Guid, kept so the hub group and tab id are stable across a restart — `term:<id>` survives), `FolderId`, `WorkingDirectory`, `Kind`, `AgentId`, `VendorSessionId`, `Label`, `StartedAt`. Unique on `Id`; rows deleted on close. No cross-module foreign keys; `FolderId` is a path-shaped reference like `CheckResult`'s.

## Risks / Trade-offs

- [A persisted vendor session id is replayed onto a command line] → validate at restore (`D3` rules) and drop the row's resume material rather than the whole terminal when it fails: restore as a fresh session in that directory.
- [Restoring many agent terminals at boot costs real processes] → the existing `MaxSessions` ceilings already guard this; the restore respects them and leaves the rest as rows, restorable... no: rows not restorable this boot are dropped with a log line — a half-restored state that pretends it will finish later is worse.
- [Server restarts while a restore is mid-flight] → the restore is idempotent per row: opening a terminal reports the row again (upsert by `Id`), so a re-run converges.
- [Worktree removed between shutdown and boot] → `TerminalTarget.Resolve` already refuses unknown directory paths; the restore drops the row and logs it.

## Migration Plan

One EF migration in Workspace (new table only, no data to backfill — no rows exist yet). Rollback is "drop the table"; nothing else reads it.

## Open Questions

None blocking. The tab-strip behavior was verified during refinement: `useTerminals` lists server sessions and the strip filters by scope, so restored terminals appear without frontend work.
