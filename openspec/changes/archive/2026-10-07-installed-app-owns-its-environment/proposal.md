## Why

After `sudo installer -pkg … -target /` the installer opened v0.2.16 with root's environment
(`USER=root`, `SHELL=/bin/sh`). The app asks `$SHELL` for the PATH; `/bin/sh -i -l` reads no
`~/.zshrc`, so the server ran without `/opt/homebrew/bin`: no `gh`, an empty inbox, `#355` 404, every
agent "not installed" (#475). Quitting left that server running, and the next window adopted it as a
second window of the install, environment and all. In Settings, the long error that came with it
squeezed the agent's name to a word per line.

## What Changes

- The login shell asked for the PATH is the account's, read from the user database (`dscl` on macOS,
  `/etc/passwd` elsewhere), and `$SHELL` only when no record answers — in the server's
  `LoginShellEnvironment` and in the installed Shell's `Packaged`.
- An installed server on the port with no other window of the install running is an orphan: the new
  window stops it and starts its own, rather than adopting it.
- An agent status longer than a badge is drawn in the card's text column; the badge says
  `unavailable`.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `update-the-installed-app`: the installed app recovers the account's PATH whoever opened it, and
  replaces an orphaned server instead of adopting it.

## Impact

`src/shared/AiOrchestrator.Infrastructure/Agents/LoginShellEnvironment.cs`,
`src/root/AiOrchestrator.Shell/Packaged.cs`, `Program.cs`,
`src/frontend/features/settings/SettingsScreen.tsx`, `en.ts`, and the infrastructure tests. No wire
or migration.
