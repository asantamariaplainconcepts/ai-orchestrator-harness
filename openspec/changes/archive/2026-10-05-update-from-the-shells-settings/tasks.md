## 1. The channel

- [x] 1.1 `WebMessages.cs` in the Shell: request (id/method/body) parse, reply/failure shapes, camelCase — Orbion's shape, our namespace.
      req: update-the-installed-app/1
      files: src/root/AiOrchestrator.Shell/WebMessages.cs
- [x] 1.2 One routing point in `Program.cs`: `RegisterWebMessageReceivedHandler` → parse → method table; unknown methods answer errors, never silence.
      req: update-the-installed-app/1
      files: src/root/AiOrchestrator.Shell/Program.cs
- [x] 1.3 `shared/shell.ts` on the page: `call(method, body)` returning a promise resolved by the reply's id; `noShell()` answering true in a plain browser.
      req: update-the-installed-app/1
      files: src/frontend/shared/shell.ts

## 2. Updates in the shell

- [x] 2.1 `Updates.cs`: `status` (local UpdateManager, no credential, nothing leaves the machine), `check` (feed read with `GITHUB_TOKEN` or `gh auth token`, quiet `DownloadUpdates`), `install` (the native question on the UI thread, No keeps the package, Yes applies).
      req: update-the-installed-app/2
      files: src/root/AiOrchestrator.Shell/Updates.cs
- [x] 2.2 `Apply` stops the install's own server before the swap — matched on the executable path `Program.cs` started — then `ApplyUpdatesAndRestart`.
      req: update-the-installed-app/3
      files: src/root/AiOrchestrator.Shell/Updates.cs, src/root/AiOrchestrator.Shell/Program.cs
- [x] 2.3 The launch-time `Offer`: one quiet check after the window shows, one question at most, nothing on a timer.
      req: update-the-installed-app/2
      files: src/root/AiOrchestrator.Shell/Updates.cs, src/root/AiOrchestrator.Shell/Program.cs

## 3. The settings section

- [x] 3.1 `UpdatesSection` in SettingsScreen: status on open (local facts, `staleTime: Infinity`), Check and Install buttons with every state drawn as itself — up to date, available (version named), error (sentence), declined (No answered, package still there), not-an-install (no shell / dev mode).
      req: update-the-installed-app/1, update-the-installed-app/2
      files: src/frontend/features/settings/UpdateSection.tsx, src/frontend/features/settings/SettingsScreen.tsx
- [x] 3.2 `en.ts` copy for all states, the settings block.
      req: update-the-installed-app/1, update-the-installed-app/2
      files: src/frontend/shared/i18n/en.ts

## 4. Verification

- [ ] 4.1 Installed app, real release behind: status shows the installed version; Check names the newer one; Install asks, Yes restarts into it and `/api/version` says the new number after.
- [ ] 4.2 No keeps the package: re-opening settings offers Install without a Check.
- [ ] 4.3 The old server does not survive the swap — no port fight, the new version answers.
- [ ] 4.4 Dev shell and plain browser: "not an install", no broken controls.
- [ ] 4.5 Gate over the correct tree; `openspec validate update-from-the-shells-settings --strict`.
