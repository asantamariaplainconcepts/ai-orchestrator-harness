## Decisions

- **The user database, not the environment.** `Environment.UserName` comes from the effective uid
  (measured: `andoniplain` with `USER=root` exported), so `dscl . -read /Users/<name> UserShell`
  names the account's shell whoever started the process. A recorded shell that does not exist falls
  through to `$SHELL`, then to `/bin/zsh`.
- **Orphan by absence of an owner.** The Shell already tells its own server from a stranger by the
  version endpoint; whether it is owned is answered by whether another `AiOrchestrator.Shell` of this
  bundle is running. Processes are matched by executable path inside this `.app`, so a checkout's
  `AiOrchestrator.Server` of the same name is never touched (measured: `MainModule` resolves both).
- **The badge stays a word.** Statuses longer than 32 characters are sentences; the card draws them
  where text wraps.

## Risks / Trade-offs

- [Two windows started at the same instant both see no other window] → each stops and starts the
  server once; the port check of the second start answers `Ourselves` again, and one server remains.
