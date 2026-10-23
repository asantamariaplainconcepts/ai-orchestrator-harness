## 1. Environment

- [x] 1.1 `LoginShellEnvironment.Shell()` reads the account's shell from the user database before `$SHELL`; tests for the `dscl` and `/etc/passwd` readings and for this account.
      req: update-the-installed-app/1
      files: src/shared/AiOrchestrator.Infrastructure/Agents/LoginShellEnvironment.cs, src/tests/AiOrchestrator.InfrastructureTests/LoginShellEnvironmentTests.cs
- [x] 1.2 `Packaged.LoginPath` asks the same recorded shell.
      req: update-the-installed-app/1
      files: src/root/AiOrchestrator.Shell/Packaged.cs

## 2. Orphaned server

- [x] 2.1 `Packaged.ServerIsOrphaned` and `StopOrphanedServer`, matched by executable path inside the bundle; `Program` replaces an orphan instead of adopting it.
      req: update-the-installed-app/2
      files: src/root/AiOrchestrator.Shell/Packaged.cs, src/root/AiOrchestrator.Shell/Program.cs

## 3. Settings

- [x] 3.1 An agent status longer than a badge is drawn in the card's text column, and the badge says `unavailable`.
      files: src/frontend/features/settings/SettingsScreen.tsx, src/frontend/shared/i18n/en.ts

## 4. Verification

- [x] 4.1 Probe on this machine: process discovery and `MainModule` resolve the installed Shell, its server and a checkout's server; `Environment.UserName` is the account with `USER=root` exported.
- [x] 4.2 `aspire do ci` on the branch.
