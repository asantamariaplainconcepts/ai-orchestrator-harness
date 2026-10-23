# ADR-0010: aio starts at login as a user service, and Settings updates and restarts it

- **Status:** Accepted
- **Date:** 2026-10-13
- **Deciders:** repository owner
- **Tags:** infra, packaging, architecture
- **Amends:** [ADR-0009](0009-the-app-ships-as-the-dotnet-tool-aio.md)

## Context

[ADR-0009](0009-the-app-ships-as-the-dotnet-tool-aio.md) ships the app as the .NET tool `aio` and
leaves the update to the person: Settings shows `dotnet tool update -g aio`, and its alternatives
reject running that command from the app because the running process holds the files it replaces.
In practice `aio` was started from a terminal that stayed busy while it ran, and an update was four
steps by hand: stop the process, run the command, run `aio` again, open the browser.

On 13 October, going from 0.3.1 to 0.3.2 with 0.3.1 still running, the new launch printed
`Already running at http://127.0.0.1:48117; opening the browser there.` and exited. The old version
kept answering, without saying it was another version, until it was killed by hand (#703).

A .NET tool on macOS and Linux lives under `.store/aio/<version>/`. An update writes a new version
folder and removes the old one; a process already running keeps the files it opened. So the claim
in ADR-0009 holds on Windows, where an open file cannot be replaced, and not on the two systems the
service is for.

## Decision

**We will register the installed tool as a service of the person's session, let Settings run the
update and restart into it, and make a newer launch replace an older one on the port.**

- **The service.** `aio service install|uninstall|status`, and Settings' *Start at login*, write a
  LaunchAgent in `~/Library/LaunchAgents/` on macOS or a `systemd --user` unit on Linux that runs the
  tool's command (`<tools folder>/aio`, the shim that runs whichever version is installed) at login,
  with no terminal and no browser. Its output goes to `~/.aiorchestrator/server.log`. Windows answers
  that it is not supported yet and exits 2. Only the installed tool registers: `dnx` and a checkout
  have no command that outlives them.
- **It restarts what failed, not what stopped.** launchd's `KeepAlive` is `{SuccessfulExit: false}`
  and systemd's `Restart=on-failure`. A crash or a `kill -9` is started again; *Stop console* and a
  stop asked by a newer version, both clean exits, are not.
- **Update and restart.** For an installed tool with a newer version on nuget.org, Settings runs
  `dotnet tool update` where the tool is installed (`-g`, or `--tool-path` for another folder), shows
  its output, and on success restarts: under the service it exits with 75 (EX_TEMPFAIL), which
  launchd and systemd answer by starting the new version; by hand it launches the new command
  detached and stops. The command stays in Settings below the button.
- **A newer launch takes over.** A launch that finds another version on the port says which
  (`aio 0.3.1 is running at …; this is 0.3.2.`), asks it to stop through `POST /api/app/stop`, waits
  for the port and starts. That route exists only in the installed tool and answers only loopback
  requests without an `Origin`, so no web page can press it. A version from before it existed is
  asked through `/api/console/stop`. The same version still only opens the browser.

## Consequences

- **Positive:** An update is one button, and a running old version can no longer hide a new one. The
  app starts at login with no terminal. Every piece is a file and a command the person can read:
  the plist or the unit, and `aio service status`.
- **Negative:** The process runs the command that replaces it, which ADR-0009 avoided; it works on
  macOS and Linux and not on Windows, which has no service either. A plain `kill` is a clean exit
  and stays stopped, so `KeepAlive` is only seen with a crash or `kill -9`. The service carries
  `HOME`, `DOTNET_ROOT` and an explicit `ASPNETCORE_URLS` from the shell that registered it; a .NET
  moved afterwards needs `aio service install` again.
- **Neutral:** Turned off from Settings by the service's own process, only the file goes: unloading
  would end the process that answered, and without the file nothing starts it at the next login.
  `AIO_SERVICE_LABEL` names another label, so a test install sits beside the person's own.

## Alternatives considered

- **`KeepAlive` true, `Restart=always`** — rejected: *Stop console* would start again at once, and an
  old version asked to stop by a newer one would come back and ask the newer one to stop.
- **Keep the update by hand, as ADR-0009 decided** — rejected: its reason, the open files, does not
  hold on macOS and Linux, and the four steps are what #703 measured going wrong.
- **A `.app` or a signed installer that updates itself** — rejected for the reasons of ADR-0009: the
  package stays the .NET tool, and nothing is signed.
- **Opening the browser when the service starts at login** — rejected: nobody asked for a window at
  login, and after an update's restart the page that pressed the button is already open.

## References

- [ADR-0009](0009-the-app-ships-as-the-dotnet-tool-aio.md) — amended by this record
- #703 — this change; #679 — the update check; #548 — *Stop console*
- `src/root/AiOrchestrator.Server/Installed/LoginService.cs`, `SelfUpdate.cs`, `Takeover.cs`
