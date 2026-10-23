# ADR-0009: The app ships as the .NET tool aio, and a person updates it with one command

- **Status:** Accepted
- **Date:** 2026-10-12
- **Deciders:** repository owner
- **Tags:** infra, packaging, architecture
- **Supersedes:** [ADR-0008](0008-the-installed-app-is-one-process.md)

## Context

[ADR-0008](0008-the-installed-app-is-one-process.md) made the installed app one process, the
Server with the frontend inside, with the browser as its window, and kept Velopack to pack it and
update it. On 12 October that install was a 93 MB `Setup.pkg` for Apple Silicon only, unsigned, so
macOS blocked it on opening, and read from the releases of a private repository, so updating needed
`GITHUB_TOKEN` or a `gh` login. The last release was v0.2.18, of 8 October: nothing of v3 to v9 was
in an installer. Velopack cost `Installed/Updates.cs` (191 lines), the detection in
`Installation.cs`, `mac/pack.sh` (85 lines), the `vpk` tool and the packing half of `release`.

#678 made the Server the .NET tool `aio`: `dnx aio` runs it on any machine with .NET 10, and
`dotnet tool install -g aio` keeps it (epic #677).

## Decision

**We will ship the app only as the .NET tool `aio` on nuget.org, and the app will say when there is
a newer one and the command that installs it, without installing anything itself.**

- **One process stays.** ADR-0008's shape holds: the frontend in `wwwroot`, the log in
  `~/.aiorchestrator/server.log`, the login shell's PATH, the preferred port 48117, a second launch
  that only opens the browser, the PWA as its window.
- **Velopack goes**, with `mac/`, its package, `vpk`, and the Setup.pkg that `release` attached.
- **The update check reads nuget.org.** `/api/updates` reads
  `https://api.nuget.org/v3-flatcontainer/aio/index.json`, which needs no credential, once when the
  tool starts and again when the person presses *Check*, and compares its newest stable version with
  its own. A 404 is "no version published yet".
- **Settings gives the command.** For an installed tool, `dotnet tool update -g aio` with a copy
  button. For `dnx aio`, that the next `dnx aio` takes the new version. The app tells the two apart
  by where it runs from: an installed tool sits under the tools folder's `.store`, and `dnx` runs the
  package from the NuGet cache.

## Consequences

- **Positive:** One install on every system .NET 10 runs on, no signing, and an update check with no
  credential. The Server loses the Velopack start-up hook and the install route that stopped it.
- **Negative:** The person runs the update command; the app cannot do it for them while it is the
  process being replaced. A machine needs the .NET 10 runtime first. A local tool
  (`dotnet tool install --local`) also runs from the NuGet cache, so it reads as `dnx`.
- **Neutral:** Publishing to nuget.org is #681. Installs made with the Velopack Setup.pkg stop
  hearing of updates; the person moves to `dotnet tool install -g aio` once.

## Alternatives considered

- **Keep Velopack beside the tool** — rejected: two installers for one app, and the one that needs
  signing and a token is the one nobody could open.
- **Run `dotnet tool update` from Settings** — rejected: the running process holds the files the
  update replaces, and the command is one line a person can paste.
- **Read the version from GitHub releases** — rejected: the repository is private, so it needs a
  credential, and nuget.org is where the package is.

## References

- [ADR-0008](0008-the-installed-app-is-one-process.md) — superseded by this record
- #677 — `dnx aio`; #678 — the .NET tool; #679 — this change; #681 — publishing to nuget.org
