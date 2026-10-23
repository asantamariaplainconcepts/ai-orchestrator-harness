## 1. Deferred session in the terminals layer

- [x] 1.1 `TerminalSessions.Open`: a request carrying `ResumeFrom` registers the session — id,
       line with resume arguments pinned into `Request.Command`, row reported through
       `ITerminalRestore` as today — without starting the process, `Pump`, `Listen` or `Discover`.
       Sessions without `ResumeFrom` (a person's open, a shell) spawn exactly as now.
      req: restore-open-terminals/2, restore-open-terminals/3
      files: src/shared/AiOrchestrator.Infrastructure/Terminals/TerminalSessions.cs
- [x] 1.2 Session state: the `Session` record carries "registered, not started" internally
       (`Alive: true` to the outside, no process, empty buffer), and a start that is idempotent
       under the session's own lock — first starter runs the spawn pipeline today's `Open` runs
       (resume line, `Pump`, `Listen`), second starter returns to the same session.
      req: restore-open-terminals/4
      files: src/shared/AiOrchestrator.Infrastructure/Terminals/TerminalSessions.cs
- [x] 1.3 `TerminalSessions.Close` and `Stop` on a never-started restored session: forget and
       report `Closed`; no process to kill. Verified by inspection and pinned by the
       closed-before-started test: `End`'s cancel of an unstarted token and `Stop`'s
       no-such-session are both the no-ops they were already written to be.
      req: restore-open-terminals/3, restore-open-terminals/4
      files: src/shared/AiOrchestrator.Infrastructure/Terminals/TerminalSessions.cs
- [x] 1.4 Infrastructure tests against the real `TerminalSessions` (`InfrastructureTests`): opening
       a deferred session lists it alive with the resume line and no process; the start path spawns
       one process under two concurrent starts; `Close` on an unstarted session reports the row
       forgotten.
      req: restore-open-terminals/2, restore-open-terminals/3, restore-open-terminals/4
      files: src/tests/AiOrchestrator.InfrastructureTests/TerminalDeferralTests.cs

## 2. First attach starts the process

- [x] 2.1 `TerminalHub.Attach`: after joining the group and before replay, if the session is a
       registered-but-unstarted restored one, start it through the seam (the method 1.2 built);
       then serve replay and `TerminalClosed` as today. Nothing that reaches a command line enters
       the hub. One addition the design's open question asked for: the attach also waits, briefly
       and bounded, until the spawner is registered, so the pane's size measurement lands on the
       new process rather than being lost to the gap — carried on `ITerminals.Running`, a read.
      req: restore-open-terminals/4
      files: src/shared/AiOrchestrator.Infrastructure/Terminals/TerminalHub.cs,
             src/shared/AiOrchestrator.BuildingBlocks/World/ITerminals.cs,
             src/shared/AiOrchestrator.BuildingBlocks/World/ICommandRunner.cs,
             src/shared/AiOrchestrator.Infrastructure/Commands/ProcessCommandRunner.cs
- [x] 2.2 Verify the pane needs no change: `FolderTerminal.tsx` already attaches on mount; confirm
       no "exited" flash and no new copy. The pane's `alive` starts true and `TerminalClosed`
       arriving after attach reads as the running case; the deferral's first paint is the resumed
       TUI's own first frame. The design's open question resolved to a hub-side wait (2.1)
       rather than a pane change — no "restored, waiting" cosmetic needed.
      req: restore-open-terminals/4
      files: (none — verified, not moved)

## 3. The restore stops spawning agents

- [x] 3.1 `TerminalRestore.Restore`: an agent row with a valid `VendorSession` registers the
       deferred session (the same `TerminalRequest` it builds today — `ResumeFrom`, `RestoreId` —
       which now defers); a shell row and an agent row without valid resume material keep today's
       boot-time spawn through `ITerminals.Open`. The drop rules (missing folder, missing
       directory, ceiling) are unchanged and cover deferred rows.
      req: restore-open-terminals/1, restore-open-terminals/2, restore-open-terminals/3
      files: src/shared/AiOrchestrator.Infrastructure/Terminals/TerminalSessions.cs
- [x] 3.2 `TerminalRestoreTests`: update the pinned behaviour — an agent row with a valid session
       registers rather than opens-at-boot; the stub seam records the deferral the way it recorded
       `ResumeFrom`.
      req: restore-open-terminals/1, restore-open-terminals/3
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/TerminalRestoreTests.cs,
             src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/WorkspaceFixture.cs

## 4. Verification

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
       passes, full count.
      req: restore-open-terminals/2, restore-open-terminals/3, restore-open-terminals/4
      files: (none — run, not moved)
- [x] 4.2 Dev shell, hand-tested: with an opencode tab open, restart the server; confirm with
       `pgrep` that no opencode process runs until the tab is opened; open the tab and confirm the
       conversation returns (`opencode --session <id>`), the buffer shows the TUI's first paint.
       Verified with claude-code rather than opencode (the folder's agent): after the restart,
       3 restored tabs listed, 0 `claude` processes; attaching ran `claude --resume <id>` and the
       real conversation came back (`⏺ pong` in the buffer, exit 1 before the fix). One bug found
       and fixed on the way, by running the product: the open that spawns immediately did not
       claim its start, so the first attach spawned the same process twice — two `claude` PIDs on
       one session. Now claimed at open, and pinned by a test.
      req: restore-open-terminals/1, restore-open-terminals/2, restore-open-terminals/4
      files: src/shared/AiOrchestrator.Infrastructure/Terminals/TerminalSessions.cs
- [x] 4.3 Same with a claude tab (`claude --resume <id>`), and a shell tab (spawns at boot, prompt
       on attach). The shell row restored and spawned at boot: `pgrep` showed a fresh login shell
       before any pane attached, as the spec's scenario says.
      req: restore-open-terminals/3
      files: (none — verified, not moved)
- [x] 4.4 Close a restored tab before opening it: row gone, no process was started. Verified:
       5 rows before, 4 after the close; `pgrep` for the resumed process 0 before and after.
      req: restore-open-terminals/3, restore-open-terminals/4
      files: (none — verified, not moved)
- [x] 4.5 Record numbers in the issue's check: what was observed, in processes and panes.
      req: restore-open-terminals/1, restore-open-terminals/2, restore-open-terminals/3,
           restore-open-terminals/4
      files: (none — recorded in the issue)
