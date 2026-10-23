# Update from the shell's settings, the Orbion model

## Why

Updating the installed app today is download-the-pkg-again-by-hand: the releases carry the
installers (#442) and nothing reads them. A person running 0.2.4 has to learn 0.2.5 exists, open
GitHub, download and re-install — the machinery exists end to end except for the last metre,
which is the one a person actually touches. Orbion solved this exact problem with a shape worth
copying whole: a settings section that reads local facts on open, checks the feed on demand,
downloads quietly, asks once, and restarts the app into the new version.

## What Changes

- The message channel between the webview and the shell, built once and generally: `WebMessages`
  (an id, a method, a body) in the shell, `window.external.sendMessage` routed in `Program.cs`,
  and a `shared/shell.ts` on the page that returns promises hanging off request ids. Updates is
  the first method through it, not a second pipe.
- `Updates.cs` in the shell, following Orbion's model line by line where it is Orbion-shaped and
  this repo where it is not: the feed is this repository's releases, the credential is
  `GITHUB_TOKEN` or `gh auth token` (the repository is private — an anonymous feed read is a
  404 that looks like "no releases"), and applying stops this install's own server first, because
  a half-replaced install talking to its old API is the bug report nobody can act on.
- Three methods — `status` (local facts, nothing leaves the machine), `check` (the feed read and
  the quiet download, off the UI thread), `install` (the question and the handover) — and a
  settings section that draws every answer as itself: up to date, available, error with its
  sentence, and "not an install" for a checkout's dev shell.
- Nothing on a timer: status on open, check on a press. A modal in front of somebody
  mid-sentence is rude no matter how new the version is.

## Capabilities

### New Capabilities

- `update-the-installed-app`: the installed app updates itself — feed read, quiet download,
  one question, restart — from the settings screen.

## Impact

- The Shell: `WebMessages.cs`, `Updates.cs`, both new beside `Packaged.cs`; `Program.cs` gains
  the message routing and the launch-time offer.
- The SPA: `shared/shell.ts`, the Settings section, `en.ts` copy.
- The feed is the vendor's releases page, read with the person's own credential; no new
  service, no new storage, no migration.
- Signed packages are not implied: Velopack replaces an app's files without Gatekeeper's opinion
  on the update itself — the signature question stays its own change.
