# ADR-0001: The agent terminal runs in a browser, not a desktop shell

- **Status:** Accepted — amended by [ADR-0004](0004-the-console-desktop-habitat.md), which adopts a webview shell over this same server without changing anything below it; ADR-0004 is superseded by [ADR-0008](0008-the-installed-app-is-one-process.md), which drops the shell and installs the browser app as a PWA
- **Date:** 2026-08-14
- **Deciders:** repository owner; agent scaffolding the product from the Harness design
- **Tags:** frontend, backend, architecture

## Context

This product's central screen is a live agent transcript. Agent CLIs — `claude`, `copilot`,
`opencode` — are TUIs: they detect a terminal, emit ANSI, move the cursor and repaint in place. To
show one faithfully the app needs a pseudo-terminal on one side and a terminal emulator on the
other.

The architecture chosen when the product was scaffolded is a browser SPA talking to a .NET server
over HTTP and a SignalR hub. The reference implementation for this domain, `orca`, is an Electron
desktop app: a Node main process owning `node-pty`, a Chromium renderer running `@xterm/xterm`, and
Electron IPC between them. Reading it raised a fair question — is a browser a handicap for the
terminal specifically, and should the architecture be reconsidered before the PTY work starts?

The facts that bear on it:

- **The renderer is the same either way.** `@xterm/xterm` is a web component. Orca runs it inside
  Chromium; VS Code's terminal is the same library. There is no rendering difference to trade away.
- **.NET has no built-in pseudo-terminal.** It is an open proposal targeting .NET 12
  ([dotnet/runtime#128565](https://github.com/dotnet/runtime/issues/128565)). Two usable libraries
  exist today: Microsoft's [Pty.Net](https://github.com/microsoft/vs-pty.net), which Visual Studio
  ships and which bundles a side-by-side conhost for Windows, and
  [Porta.Pty](https://github.com/tomlm/Porta.Pty), which wraps ConPTY and POSIX behind
  `SpawnAsync` → `ReaderStream` / `WriterStream` / `Resize` / `Kill`. Node's `node-pty` has a far
  larger install base than either.
- **Transport latency is not perceptible.** Electron IPC is in-process; a loopback WebSocket is
  well under a millisecond, against a ~50ms threshold for terminal echo. Orca's own remote mode
  carries this over SSH across real networks.
- **Backpressure costs the same on both.** Orca implements credit-based flow control for its
  *local* IPC path, because the bottleneck is the emulator's parse rate rather than the wire.
- **A browser intercepts keystrokes.** `⌘W`, `⌘T`, `⌘N`, `⌘R`, `F5` and `Ctrl+W` are consumed by
  the browser before the page sees them. This matters for a terminal in a way it does not for the
  rest of the app, because a terminal wants every key.
- **A server-owned PTY outlives its viewer.** Closing a browser tab does not stop a run. Electron
  kills its PTYs when the app quits, which is why orca carries scrollback snapshots, sleeping-agent
  records and per-vendor session resume — a substantial part of its complexity exists to survive
  its own process ending.
- **Client/server is already remote.** Orca's `remote-runtime-terminal-*` family exists because a
  desktop app had to be retrofitted to reach a terminal it does not host.

## Decision

**We will keep the browser SPA and .NET server, and render the agent terminal with `@xterm/xterm`
over a byte-framed hub connection.** The pseudo-terminal will be a .NET library behind the existing
`IAgentRunner` seam.

The renderer is identical in both architectures, so the only things actually being traded are the
PTY library's maturity and the browser's keyboard capture — against a run that survives its viewer
and remote access that costs nothing to build. Two of orca's largest subsystems exist to obtain
properties this architecture has by construction; adopting its architecture would mean building
those subsystems in order to lose those properties.

The decision is also cheap to revise, which is itself part of the reason to take it now: a webview
shell can be wrapped around the same server later without changing a line of backend code, whereas
moving to Electron means rewriting the .NET modular monolith in Node for one library choice.

## Consequences

- **Positive:** Runs survive the tab closing, and reattach on reload — no sleeping-agent machinery
  is needed to get there. The app is reachable from another machine or a phone with no extra work.
  One deployment story rather than a server plus a packaged desktop app. The whole existing backend
  stands.
- **Negative:** The browser eats `⌘W`, `⌘R`, `Ctrl+W` and similar before the terminal sees them, so
  some keystrokes cannot be delivered to the agent. The .NET PTY libraries are less proven than
  `node-pty`, and neither is a Microsoft-supported product in the way `node-pty` is a de-facto
  standard. There is no native file-picker for "Add a folder" — a path is typed. Desktop actions
  (open in editor, reveal in file manager) execute on the *server*, so they only work while server
  and user share a machine; `DesktopFailure.Unsupported` already exists for the case where they do
  not.
- **Neutral:** A byte-framed transport and credit-based flow control have to be built — but orca
  proves those are needed for local IPC too, so they are not a cost of this choice. Server-side
  screen serialization has no .NET equivalent of `@xterm/headless`, so replay-on-reconnect starts
  as a raw ring buffer (backlog F2). Revisit if the keyboard limitation proves material in
  practice; the mitigation is a standalone display mode or a webview shell, not a rewrite.

## Alternatives considered

- **Move to Electron, as orca did** — rejected because it discards the entire .NET modular monolith
  in exchange for one library (`node-pty`), and gives up runs surviving the app and remote access,
  both of which orca then spends significant code re-acquiring.
- **Keep .NET, ship a webview shell now (Photino.NET, Tauri, or Electron hosting only the URL)** —
  rejected *for now*, not on the merits: it fixes the keyboard capture and adds a dock presence
  without touching the backend, but it is strictly additive and can be adopted at any point. Taking
  it before the keyboard limitation has been felt would be paying for a fix to a problem whose size
  is unmeasured.
- **Keep the browser, spawn PTYs from a Node sidecar over a pipe** — rejected as premature. It
  would buy `node-pty`'s maturity at the cost of a second runtime, a second process to supervise
  and a second packaging story. Reconsider only if a .NET PTY library proves unfit in practice.
- **Avoid a PTY entirely: run agents with redirected pipes and render lines** — rejected because
  agent CLIs behave differently without a TTY, and their in-place repainting renders as garbage in
  a line list. This is what the seeded implementation does today, and it is precisely what has to
  change.

## References

- (superseded: the plan moved to GitHub issues) — items 3, 4 and 5, which this decision governs
- (superseded: the backlog moved to GitHub issues) — A2 (PTY), F1 (byte transport), F2 (replay), F3 (flow control),
  I1 (xterm)
- [`IAgentRunner`](../../src/shared/AiOrchestrator.BuildingBlocks/World/IAgentRunner.cs) — the seam
  that makes the PTY library a swappable detail
- [dotnet/runtime#128565](https://github.com/dotnet/runtime/issues/128565) — built-in PTY support,
  proposed for .NET 12
- [microsoft/vs-pty.net](https://github.com/microsoft/vs-pty.net) and
  [tomlm/Porta.Pty](https://github.com/tomlm/Porta.Pty) — the two candidate libraries
- `orca` `src/shared/terminal-stream-protocol.ts`, `terminal-multiplex-flow-control.ts`,
  `renderer/src/runtime/remote-runtime-terminal-*` — the reference for the transport
