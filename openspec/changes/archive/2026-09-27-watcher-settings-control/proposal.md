## Why

The label watcher is invisible. Its mode lives in `LabelWatcher:Mode` configuration, the default is
`disabled`, and when the mode is unset it exits silently — no log line, no indicator, no screen that
names it. The cost of that was measured on 27 September 2026: a whole session spent diagnosing "why
does it not pick up the current tasks", whose answer was one configuration string nothing in the
product surfaces. The same morning, the installed app (velopack, #380) repeated the pattern — with
the server log as the only place the reason (`gh` off the GUI PATH) was stated, and no
`LabelWatcher` state anywhere.

## What Changes

- A read of the watcher's operative state — mode, tick interval, last tick at, last tick's refusal
  reasons — exposed over HTTP and shown as a section on the settings screen.
- The refusal reasons a tick already logs (`WaveReason`, `Suppressed`, `RegistryMissing`,
  `ForeignClaim`, unreadable loop files) become a struct a person can read on the screen, not only a
  log line.
- Lowering the mode one rung at a time (`dispatch-claims → observe-claims → observe → disabled`)
  from the screen, behind a confirmation, with the host log recording who lowered what when.
- Raising the mode one rung at a time — from the rung the configuration already holds. `off →
  dispatch-claims` in one act is impossible by construction: raising climbs at most one rung and the
  screen says what the next rung does in plain words.
- `LabelWatcher:PilotTask` editable from the screen and persisted like a preference: the pilot's
  cut (#348) is the reversible experiment by design.
- Where the state persists, the installed app survives an update: the mode a person sets lives in a
  machine location, never in the bundle's `appsettings.json`, which velopack replaces whole.

## Capabilities

### New Capabilities

- `watcher-settings-control`: read the label watcher's state, lower its mode freely, raise it one
  rung at a time behind a confirmation that names the consequence, edit the pilot task, and persist
  all of it where a velopack update cannot erase it.

### Modified Capabilities

- `dispatch-observation`: the mode values keep their semantics, but the effective mode is no longer
  read once at startup — it is re-read per tick from a persisted store, and an unknown persisted
  value is refused the same way an unknown configuration value is today. The disabled default
  becomes visible (a state the screen shows and a person can raise from) rather than silent.

## Impact

- `src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs` — the mode moves from
  `IConfiguration` read-once to a source that can change while running; the watcher records its
  last tick's outcome where the settings read can reach it.
- A persistence home for the watcher's operative state. The host (`AiOrchestrator.Server`) owns the
  watcher, so the state goes where the host can store machine-local data — the same shape
  `remember-preferences` already established for the editor and the worktree default, extended with
  a home that survives a velopack update (the installed app's `app/` directory is replaced whole;
  `~/Library/Application Support`-shaped storage is not).
- `src/frontend/features/settings/SettingsScreen.tsx` — one new section composed from the kit,
  copy from `en.ts`, following DESIGN.md.
- `contracts.ts`, `queries.ts`, `en.ts` — appended next to the settings screen's existing blocks.
