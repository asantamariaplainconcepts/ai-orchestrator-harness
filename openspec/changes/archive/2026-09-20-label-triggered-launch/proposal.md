# Proposal

## Why

Manual dispatch works on one machine watched by one person: whoever sees `status:ready-for-proposal`
types `/aio:propose`. It breaks the moment no person is watching, and it cannot be made shared
safely, because nothing between the label and `POST /api/runs` records *whose* dispatch this is,
*what* it decided, or what to do when the decision survives a restart. Issue #313 asked for the
automatic launch; the agent control-plane study showed that jumping straight there is the one
mistake that stops being local when shared — two worktrees can each believe they own the launch,
and neither can prove the other happened. Epic #321's ACP-01 therefore splits it: **make the
decision durable and observable first, dispatch authority last**.

## What Changes

- A new **Coordination module** owns a SQLite dispatch journal: one row per effect id, holding the
  folder scope, canonical Task identity, operation (`propose` or `implement`) and a single
  `pending → started/failed/refused/unknown` transition. Reservation is idempotent — replaying an
  effect id returns the same row — and transitions are write-once with protected history. The
  journal is exposed over HTTP with `coordination:read` / `coordination:write-journal` permissions,
  and **never readable or writable from outside its folder scope**.
- The **watcher becomes observation-only**: behind `LabelWatcher:Mode` (default `disabled`, `observe`
  to enable), each tick logs the `/aio:propose` or `/aio:implement` prompt a person could type,
  reads the journal through the folder-scoped API, and **suppresses** any task with a pending
  journal entry. An unknown mode is refused with the reason. It never calls `POST /api/runs`, never
  applies labels, never writes holds.
- **Suppression survives restarts** because it is read from the journal rather than remembered in
  memory: an intent reserved before a crash still suppresses the task after the host comes back.
- **Folder/WIP/Vendor rules keep working** (from #313, section 1-2 of tasks): server-side Folder
  resolution from task URL against Folder remote URLs (institutional-grade repository identity),
  per-folder WIP room counting, held tasks blocked, unreachable folders start nothing, malformed
  configs suppress rather than free the folder.

## Capabilities

### New Capabilities
- `dispatch-observation`: an opt-in, observation-only watcher that advises the next manual-loop
  prompt, and a durable, idempotent, folder-scoped dispatch journal whose pending entries suppress
  the advice. Nothing in it starts a Run.

### Modified Capabilities
(none)

## Impact

- **New**: `src/modules/Coordination/` module (DbContext, migration `InitialDispatchJournal`, three
  journal use cases, permissions) registered in `World.cs`; mode key + tick loop in `LabelWatcher`;
  `Intents(folder)` folder-scoped read in `HarnessApi`; `IDeclaredConfigFile` seam for
  `.harness/config.json`.
- **Touched, read-only**: `GET /api/tasks`, `GET /api/folders`, journal list API — consumed over
  HTTP the same way the frontend consumes them; no cross-module reference.
- **Not touched**: `POST /api/runs` (only corroborated by controlled admission in ACP-05), `status:holding`
  semantics, the release-only hold control, `/aio:sync`, the merge path.
- **Deliberately not built** (open H5/H6): trusted-authority consumption of the journal and the live
  check — separate, reviewable steps in epic #321's P1 wave.
