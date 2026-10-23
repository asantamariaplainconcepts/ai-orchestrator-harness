## Why

The Pbis module interprets the vendor's labels to know where a PBI stands: `ListPbis.Handler` keeps
the label↔status table, the precedence rule and the hold check in the core, so every vendor is forced
to speak GitHub's `status:kebab` labels. Azure DevOps, Jira and Linear fall back to `Backlog` by
default rather than by decision, the eight labels this repository actually uses lose three of them
(`needs-refinement`, `proposal-review`, `blocked`) because the enum knows seven, and a PBI with no
loop label is shown as a legitimate `Backlog` rather than an unknown position. #448 already made the
vendor the product's vocabulary (`IPbiSource`, `PbiSources/`); the position is the last field the
core still derives by reading the vendor's raw labels.

## What Changes

- **Position becomes a Connector contract.** `IPbiSource` grows a position read (`GetPosition`, an
  explicit answer: a known position, or unknown with a reason) and a position write (`MoveTo`), plus
  the hold as part of that answer. The label↔position table moves out of the module into
  `GitHubPbiSource`; the core asks for *position*, never for labels.
- **Unknown is a state, not a fallback.** A vendor that cannot say where a PBI stands — no loop
  field, unrecognised value, ambiguous label set — answers `Unknown` with the vendor's own reason,
  distinct from `Backlog`. The wire carries it and the UI renders it as its own answer, never as a
  lane a card can be dropped on.
- **The loop's real vocabulary arrives whole.** ADO's `System.State` is promoted to the position
  read; the GitHub translation keeps today's seven plus the three the loop declares that the enum
  drops, so `needs-refinement`, `proposal-review` and `blocked` stop collapsing into `backlog`.
- **Moves translate behind the seam.** `SetPbiStatus`/`SetPbiHold` keep their routes, permissions,
  admission and hold rules, but the write they make is `IPbiSource.MoveTo`, which each adapter
  translates to its own mechanism (GitHub: label write, new before old; ADO: `System.State`;
  Jira/Linear: the position write answers unsupported until their own translation exists).
- **Vendors without a position say so honestly.** Jira and Linear answer `Unsupported` on the move
  and `Unknown` on the read, reported as itself — not a silent `Backlog` and not an error.
- **The watcher's consumption is decoupled, not moved.** `GetPbi`'s answer and the board keep
  carrying the wire position the declared loop spells; the label watcher keeps consuming it. What
  changes is the origin: the value comes from the Connector's translation, not from the core
  re-parsing labels.
- **Cache stays a cache.** A refresh reads the position from the vendor's answer; the last-known
  position in the snapshot is never written back over a fresh read, and the read side never writes
  to the vendor.
- **The permission gap is recorded, not inherited silently.** The position writes keep today's
  `Access.AnyCaller` and the known gap is stated in the design as a recorded risk with its own
  follow-up — fixing it needs task-to-folder attribution this change does not build.

## Capabilities

### New Capabilities

- `pbi-position-connector`: the Connector's position contract — how `IPbiSource` reads (position +
  hold, with unknown as an explicit answer) and writes (move to a position, hold release) a PBI's
  place in the loop, and what each vendor adapter must translate and what it may honestly refuse.

### Modified Capabilities

- `pbi-status-and-holds`: the derivation requirement is replaced by the Connector's position read;
  the label-write requirements become the position-write contract with GitHub's translation
  preserved as the adapter's mechanism; the closed seven-state wire opens to the declared loop's
  vocabulary plus `unknown`.
- `pbi-details`: the detail panel consumes the translated position and hold from the Connector
  rather than a status string the core derived, and represents unknown distinctly.
- `read-the-vendors`: the snapshot carries the position the vendor answered, never one deduced
  from the last run, and the position read participates in the same shared-read lifetime as the
  rest of the poll.
- `the-world-seam`: the seam entry for vendors gains the position members — the interfaces in
  `BuildingBlocks/World/` are where the contract lives and `World.cs` registers it.

## Impact

- `src/shared/AiOrchestrator.BuildingBlocks/World/IPbiSource.cs` — position read/write members, the
  position answer type, and the hold as part of it.
- `src/shared/AiOrchestrator.Infrastructure/PbiSources/` — `GitHubPbiSource` receives the label
  translation (table, precedence, the three missing states); `AzureDevOpsPbiSource` its
  `System.State` mapping; `JiraPbiSource`/`LinearPbiSource` honest refusals.
- `src/modules/Pbis/AiOrchestrator.Modules.Pbis/` — `ListPbis`, `GetPbi`, `SetPbiStatus`,
  `SetPbiHold` stop interpreting labels; the `LoopStatus` table and helpers are deleted or reduced
  to a wire projection.
- `src/frontend/` — `contracts.ts` (`PbiStatus` union opens), `PbisScreen.tsx` (board lanes unknown
  rendering, no `status:{status}` literal), `en.ts` copy for the new answers.
- `git` — worktree `.worktrees/change-449`, branch `change/449`. No migration: nothing persists the
  position that was not already persisted as labels at the vendor.
