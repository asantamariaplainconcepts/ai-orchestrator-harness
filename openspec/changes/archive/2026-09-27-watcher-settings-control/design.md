# watcher-settings-control — Design

## Context

The label watcher (`src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs`) is a host-owned
`BackgroundService`. It reads its mode once, in `MayObserve()`, from `IConfiguration`
(`LabelWatcher:Mode`, default `disabled`), and when that value is unset or unknown it returns
without logging — silent by construction. The tick already produces every fact this change needs to
surface: `WaveReason` lines for dependencies and seam disputes, `Suppressed` for blocked identities,
`LoopUnreadable` for loop files, `RegistryMissing`/`RegistryUnknown`/`ForeignClaim` for the claim
side, and `WouldHaveDispatched` for the pilot cut. The problem is not that the facts do not exist —
it is that they are log lines and the mode is a string, so the screen could not show them and the
person could not move the string.

The settings screen (`src/frontend/features/settings/SettingsScreen.tsx`) already reads
`/api/preferences` (`remember-preferences`) — typed fields, optional-field PUT, fallbacks equal to
pre-existing behaviour — and that capability is the shape this change follows for persistence:
nothing about the watcher's defaults changes on a machine that never opens the new section.

## Goals and Non-Goals

Goals:

- The operative state (mode, interval, last tick, last tick's refusals) is one HTTP read away and on
  the settings screen.
- Lowering is always available; raising climbs exactly one rung behind a confirmation naming the
  consequence.
- Mode and pilot survive a velopack update, which replaces the bundle directory wholesale.
- A machine that never touches the new section behaves exactly as today.

Non-goals:

- #355's manual-tick endpoint — related, separate, already held as its own run.
- Changing any mode's semantics, the registry protocol (#328/#329), or the wave computation (#346).
- A history of ticks. Last tick only, plus the refusals it produced.

## Decisions

### D1 — Where the mode lives: a watched store in the host, with configuration as fallback

The watcher is a host service, so its state belongs to the host, not to a module — same reasoning
`Program.cs` states for `LabelWatcher` and the attention queue. A small host-owned store
(`Watching/WatcherState.cs` shape: load on demand, hold in memory, write-through to disk) behind a
`BuildingBlocks/World` interface (`IWatcherControl`) registered in `AddWorld()`. The effective mode
is: persisted value if one exists, else `LabelWatcher:Mode` from configuration, else `disabled`.

Disk location: `ApplicationSupport`-shaped next to the SQLite file the host already derives per
checkout (ADR-0003's property). In the installed app that is `~/Library/Application Support/...`,
outside the bundle; the velopack full nupkg replaces `AI Orchestrator.app` entirely, so anything
under `Contents/MacOS/app` is not durable (AC-6 of #381).

Why not user-secrets: the installed app runs with no developer tooling and `dotnet user-secrets`
does not exist there. Why not the `~/.aspire` directory: the installed app is not aspire-managed.

`MayObserve()` re-reads the effective mode per tick (it already runs per tick — `Tick()` calls it;
only `ExecuteAsync`'s timer setup reads once). No restart needed for a mode change; the interval
stays a configuration value.

### D2 — One rung at a time is enforced server-side, not by the UI

The screen offers exactly one lowering button and one raising button. The command handler refuses
any transition that is not exactly one rung in the named direction, or that raises when nothing
holds a mode (first rung from `disabled` is `observe`, never higher), with a 4xx naming the legal
next rung. The UI being cautious is a convenience; the refusal being server-side is the ladder
itself (#348's ceremony lives in the command, not the dropdown).

### D3 — Refusals reach the screen through the host, loopback like everything else

Module data is behind module HTTP endpoints; the watcher's state is host data. The new reads are
host-mapped routes (`/api/watcher`), exactly as `/api/attention` is. The tick keeps a bounded
snapshot of its last outcome (mode seen, at, one row per refusal: kind + task key + reason string
from the existing log vocabulary). Bounded means one tick's worth — the previous snapshot is
replaced, not accumulated, which is what keeps the memory fixed and the screen honest about "last
tick" rather than pretending to a history.

### D4 — Pilot task rides the same store

`PilotTask()` moves from configuration to the store with the same fallback chain. Empty is the
default and means the full cut. The screen edits it as a plain text field with the task key's shape
validated — a `#123`-style key — and clearing it is explicit.

### D5 — Frontend composes the kit; three shared files appended, not edited

One new section on `SettingsScreen.tsx` using existing kit pieces (`Button`, `Toggle`-like rows,
the section pattern already on the screen). `contracts.ts` and `queries.ts` gain the read and the
two commands next to the settings block; `en.ts` gains a `settings.watcher` group. The confirmation
for raising is plain copy from `en.ts` that names the consequence (the AC-4 wording: agents will be
seated on the tasks the tick finds ready). No new shared file, no edits inside a neighbour's block.

## Risks / Trade-offs

- Two AppHosts against one checkout still see one store file — mode changes are visible to both.
  The registry of claims (#328) is what fences double-dispatch, not the mode; the doc comment on the
  store says so.
- The tick snapshot is memory in the host process: a restart clears "last tick" to unknown until the
  next tick. Acceptable — the state's job is to answer "why is nothing launching *now*".

## Migration Plan

Nothing migrates. A machine with a configured `LabelWatcher:Mode` keeps it as the fallback; screen
changes write the store and win from then on. Rollback is deleting the store file.

## Open Questions

- Does #355's manual tick extend this same snapshot when it lands (its trace feeding the same
  read)? Expected yes; it is in the issue's out of scope for a reason — the answer belongs to that
  change.
