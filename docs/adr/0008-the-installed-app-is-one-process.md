# ADR-0008: The installed app is one process, and its window is the browser's

- **Status:** Superseded by [ADR-0009](0009-the-app-ships-as-the-dotnet-tool-aio.md) (2026-10-12)
- **Date:** 2026-10-09
- **Deciders:** repository owner
- **Tags:** frontend, infra, architecture, packaging
- **Supersedes:** [ADR-0004](0004-the-console-desktop-habitat.md)

## Context

[ADR-0004](0004-the-console-desktop-habitat.md) put the console in a Photino window
(`src/root/AiOrchestrator.Shell`) and named Electron as the direction for when distribution
started. Distribution then started without Electron: #444 added Velopack to the Shell, which
packs a Setup.pkg, writes a release feed and updates the app from it.

The installed app was then two processes, a window that owned a server, and most of the Shell's
1,355 lines were about that pair rather than about a window:

- **The orphan server (#475).** A quit that never reached the window's stopper left the server
  running, and the next window adopted it with whatever environment it had been started with.
- **The PATH of the installed app (#476).** The Shell asked the login shell for a PATH and handed
  it to the server, in a copy of the code the server already had in `LoginShellEnvironment`,
  because the Shell could not reference Infrastructure.
- **The message bridge.** The settings screen reached the updater through Photino's string pipe
  (`WebMessages.cs` and `shared/shell.ts`), a wire format of its own.
- **The migrator.** The Shell ran the MigrationService before the server, because the AppHost
  that orders them in the dev loop does not exist in an install.

`ARCHITECTURE.md#how-it-installs` defines v2 as one process: the server with the
frontend inside, which opens the browser at its own address, installed on the desktop as a PWA.

## Decision

**We will ship the Server alone, with the frontend in its `wwwroot`, and keep Velopack inside it.**

- **One process.** `mac/pack.sh` publishes `AiOrchestrator.Server` self-contained and packs it
  with `vpk` as the main executable. Started, the installed Server writes its output to
  `~/.aiorchestrator/server.log`, adopts the login shell's PATH (the #476 fix, now through
  Infrastructure's own `LoginShellEnvironment`), serves from its own folder, migrates its database,
  listens on loopback and opens the default browser there. A second launch finds the first on the
  port, opens the browser at it and exits.
- **The window is the browser's.** The SPA already had a `manifest.webmanifest` with
  `display: standalone`; it gains a service worker with no cache and no fetch handler. Chrome and
  Edge offer to install it with its own window and icon.
- **Velopack keeps its three calls.** The Server checks once at start-up and downloads quietly;
  the settings screen shows what is installed and what waits, and installs it through
  `/api/v2/updates` (`GET`, `POST check`, `POST install`). Install stops the server and Velopack
  restarts it on the new version, which opens the browser again. The feed is unchanged: this
  repository's GitHub releases, read with `GITHUB_TOKEN` or the person's `gh` login.
- **Photino goes**, with the bridge, the orphan handling and the Shell's copy of the PATH code.

**The port is preferred, not fixed.** The installed app asks for 48117 first, because a PWA is
installed per origin and the same port keeps the installed window, its icon and its storage
launch after launch. When another program holds it, the app takes a free port and says so in the
log, where the Shell refused to start. `ASPNETCORE_URLS` still overrides both.

**A downloaded update is not applied on start-up.** Velopack does that by default, and on the
packed app a second launch applied it under the server that was already running. Installing is
the person's press in settings, which stops the server first.

## Consequences

- **Positive:** The installed app is one process, so there is nothing to orphan and nothing to
  bridge. The updater is a v2 route in the generated contract rather than a second wire format.
  The PATH fix is the one the adapters already use. The phone over a tunnel keeps working: the
  server is the same server, on loopback.
- **Negative:** Closing the browser window does not stop the server; it keeps running until the
  machine logs out or somebody quits it, and there is no menu to quit it from. Starting at login
  is not in this change. The window has the browser's limits that ADR-0001 named: shortcuts the
  browser takes before the terminal sees them. Installing an update took 27 to 36 s on the
  packed app, most of it the host's 30 s shutdown timeout.
- **Neutral:** The bundle id stays `dev.aiorchestrator.shell`, so an install made by the Photino
  Shell updates in place. Signing and notarisation are still not done. The MigrationService stays
  for the AppHost; the installed app migrates in-process.

## Alternatives considered

- **Keep the Photino window around the server** — rejected: every problem above came from having
  two processes, and the window added nothing the installed PWA does not.
- **Electron, the direction ADR-0004 named** — rejected: it would add a bundled Chromium and a
  second runtime to get a window the browser already gives.
- **A fixed port, refusing to start when it is taken** — rejected: a fallback port keeps the app
  usable, at the cost of that launch's PWA being a different origin.
- **Apply downloaded updates on start-up (Velopack's default)** — rejected after measuring it on
  the packed app, as above.

## References

- [ADR-0004](0004-the-console-desktop-habitat.md) — superseded by this record
- [ADR-0001](0001-the-agent-terminal-runs-in-a-browser.md) — the browser habitat
- [`ARCHITECTURE.md#how-it-installs`](../../ARCHITECTURE.md#how-it-installs)
- #517 — this change; #444 — Velopack; #475, #476 — the orphan server and the PATH
