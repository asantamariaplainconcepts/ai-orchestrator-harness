## Context

`TerminalRestore` (Workspace module) walks the persisted `terminal_sessions` rows at boot and calls
`ITerminals.Open` with `ResumeFrom` — which spawns `exec claude --resume <id>` into a pseudo-terminal
nobody is attached to. The #368 retro recorded the measured consequence: the TUI exits without a
reader, the session finishes with an empty replay buffer, and the tab that comes back is a corpse.
The vendor session id is already persisted, the resume line is already correct — the only wrong thing
is **when** the process starts.

Who is involved:

- `TerminalRestore` (Workspace) — the boot walk. Spawns through `ITerminals` today.
- `TerminalSessions` (Infrastructure, behind the `ITerminals` seam) — `Open` pins/plies the resume
  line, starts the PTY in `Pump`, keeps the replay buffer, reports the row through
  `ITerminalRestore.Opened`.
- `TerminalHub.Attach` (Infrastructure) — what a pane calls over SignalR when it opens a tab. Today a
  pure read: join group, replay buffer, announce closed. Its own docblock states the constraint:
  *"a hub … cannot be the place a process gets spawned"* — authorization lives in the CQS pipeline a
  hub does not carry.
- The hub exists in Infrastructure, next to `TerminalSessions` — same assembly, no module boundary
  crossed by having them talk.

## Goals / Non-Goals

**Goals:**

- After a restart, opening a restored agent tab resumes the recorded conversation (`claude --resume
  <id>`, `copilot --resume=<id>`, `opencode --session <id>`), rather than showing a dead pane.
- Between boot and first attach, no restored agent process is running.
- The SPA's attach path needs no new request and no new copy.

**Non-Goals:**

- No reconnect to PTY processes alive before the restart — they died with the server (#368's
  decision, unchanged).
- No scrollback restore for plain shells — the shell line stays `exec $SHELL -l`, spawned at boot as
  today.
- No change to `ITerminalRestore`'s shape — the row already carries everything the deferral needs.
- No frontend rework beyond what verification demands.

## Decisions

### 1 — A session that is listed but not started, born behind the `ITerminals` seam

`TerminalRestore` registers the restored agent terminal with the seam **without spawning**. The
`Session` inside `TerminalSessions` already separates "what was asked" (`TerminalRequest`) from
"whether the process lives" (`Alive`, `ExitCode`); the deferral adds one more state a session can be
in: `Alive: true` (it is openable), process not yet started, replay buffer empty.

- `ITerminals.Open` keeps its signature and its contract for every existing caller — a person's open
  still spawns immediately. The deferral rides the existing `ResumeFrom`/`RestoreId` pair: a request
  that carries `ResumeFrom` is the restore's, and the restore's is the one whose spawn waits for an
  attach.
- `TerminalSession` reports the state the SPA already reads: `Alive: true` while unspawned (the tab
  renders as a normal live agent tab), `Command` the resume line it will run. No new contract field
  in the first cut; if verification shows the pane needs to know "not started yet" (e.g. to avoid a
  flash of "exited"), the field is added then, not speculatively.

**Alternative rejected — spawn at boot and keep a reader alive**: keeps a drain task per restored
agent from boot, burns file handles for tabs nobody may ever open, and the reader would still have to
hand off to the real pane's flow-control (`RunFlow` acks) mid-stream. Deferral deletes the whole
class of problem.

**Alternative rejected — the SPA opens the terminal on first view** (call `POST /terminals` with the
resume id): moves orchestration into the browser, invents a second open path for the same tab id,
and hands the wire the vendor session id it never needed to see. The server owns sessions (ADR-0001's
property); the browser owning the restore would invert it.

### 2 — First attach starts the process

`TerminalHub.Attach` is the moment a person opens the tab, and the hub already reaches the seam
(`ITerminals`). For a session that is registered-but-unspawned, `Attach` asks the seam to start it:

- `TerminalSessions` starts the process exactly as `Open` starts one today — `Pump`, `Listen`, the
  resume line already pinned into `Request.Command` — then proceeds with replay and flow watch. The
  chunks the process prints after the start arrive to the attaching client as normal
  `TerminalChunk`s; the replay buffer was empty, correctly, because nothing had printed yet.
- **The hub's constraint is honoured, not bent**: the hub is still a dispatcher, not a spawner — it
  calls one method on the seam it already calls, and the seam (which the open path also goes through)
  does the spawning. The spawn is the seam's act on behalf of a session this process already owns.
  The authorization question is identical to today's: the restore is this process reopening what
  this process held (`TerminalRestore`'s own docblock says why a caller-less spawn here carries no
  CQS question), and `Attach` carries no new authority — a person who can see the tab list is a
  person who could have opened that terminal.
- Two panes racing (two windows attaching to the same restored tab) is one `Session.Guard` — the
  start is idempotent under the session's own lock; second attacher gets the replay the first one's
  `Pump` is producing.

**Alternative rejected — a timer**: spawn N seconds after attach, or after boot with a delay, only
re-learns #368's lesson with a smaller constant.

**Alternative rejected — spawn on the first Type/Resize**: the pane attaches before the person
types; the TUI's first paint is the signal "it is alive", and delaying that to a keystroke reads as
a dead tab.

### 3 — Closing a never-attached restored tab forgets the row

`TerminalSessions.Close` already forgets the session and reports `Closed` to the sink. For a
registered-but-unspawned session, closing is simpler — there is no process, so nothing to kill — and
`TerminalRestoreSink.Closed` drops the row as today. No new code path beyond the branch's absence of
a process; the case is named because it is the one the old flow could not produce.

### 4 — Shells and rows without a valid resume id keep today's behaviour

- A restored **shell** spawns at boot as it does today. The empty-PTY problem does not apply: a
  login shell prints a prompt, idles, and — critically — survives without a reader (`Pump` is the
  reader). Keeping it is one less branch.
- A row whose `VendorSession` fails `Resume()` validation restores without resume material — the
  existing requirement's scenario — and spawns at boot like a shell would, since there is no
  conversation to lose.

## Risks / Trade-offs

- **[The tab looks alive before attach and says nothing]** → The pane attaching *is* the fix: the
  first thing that happens after `Attach` is the spawn and then the TUI's first paint. Between boot
  and attach the tab strip is honest — the terminal is openable — and nothing is running.
- **[A hub spawning a process is one step from a hub that spawns arbitrary processes]** → The start
  only ever targets a session id already registered by the restore, with a line the seam built from
  `TerminalRestore`'s recomputation; there is no hub input that reaches a command line. If a future
  change wants more from the hub, that is its own change.
- **[Server stops with unspawned restored tabs]** → The rows are still there; the next boot
  re-registers them. Same idempotence the interrupted restore already has.
- **[Session ceiling counts unspawned sessions]** → They count toward `MaxSessions` the moment they
  register; the restore's existing drop-on-ceiling behaviour covers them unchanged.
- **[Functional tests stub the seam]** → The existing `TerminalRestoreTests` trust boundary holds;
  new coverage pins that `Attach` on a deferred session starts it — an Infrastructure-side test
  against the real `TerminalSessions`, which is the layer being changed.

## Migration Plan

No data migration. Deploy is the build; rollback is reverting it — the rows are unchanged, and the
previous behaviour (boot-time spawn) is what `RestoreId`+`ResumeFrom` already meant. Verify on this
machine with the dev shell first, then the installed app (restart, open tab, conversation returns).

## Open Questions

- Whether the pane needs a "restored, waiting to start" cosmetic before first paint (Decision 1's
  escalation clause). Verification decides; the default is silence.
