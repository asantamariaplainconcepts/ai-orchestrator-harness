## Context

After #448 the vendor seam is `IPbiSource` in `src/shared/AiOrchestrator.BuildingBlocks/World/` with
four adapters in `src/shared/AiOrchestrator.Infrastructure/PbiSources/`. But the **position** of a
PBI — where it stands in the loop, and whether it is held — never crossed the seam: `ListPbis.Handler`
(src/modules/Pbis/…/Features/Inbox/UseCases/ListPbis.cs) holds the label↔status dictionary, the
`status:holding` const, the precedence-by-enum-order rule and the `StatusOf`/`LabelFor`/`Name`/`Parse`
helpers. Every write (`SetPbiStatus`, `SetPbiHold`) resolves the label in the core and hands the
string to `ApplyLabel`. Consequences today:

- ADO, Jira and Linear PBIs with no `status:*` label render as `backlog` by default — the vendor
  never got to say. ADO already reads `System.State` (AzureDevOpsPbiSource.cs:270–303) and throws it
  away for anything but open/closed.
- This repository declares **eight** status labels (`.harness/issue-loop.yaml`, line 155:
  `needs-refinement`; plus `proposal-review` and `blocked`): three of them fall through to `backlog`.
- The wire (`PbiStatus` in `src/frontend/shared/http/contracts.ts:389`) is a closed union of seven,
  and `PbisScreen.tsx:2257` renders the literal `status:{status}` — the frontend knows labels too.
- `GetPbi` exposes `string Status` pre-translated; the watcher (`LabelWatcher.cs:264`,
  `HarnessApi.Tasks()`) re-strips a `status:` prefix from it — a string contract no vendor signed.

Product decisions for the epic #447 are already taken and are inputs here, not open questions: the
vendor is the authority of the position; the Connector translates labels ↔ vendor fields;
the cache is an internal detail; the position is a Connector contract (`IPbiSource`), not a module
concept; no interrogating the person.

## Goals / Non-Goals

**Goals:**

- The module asks the Connector for a PBI's position and hold; it stops knowing any `status:*` string.
- `GitHubPbiSource` owns the label translation with a documented precedence that preserves today's
  agreed behaviour, extended to the loop's full declared vocabulary.
- `AzureDevOpsPbiSource` implements the same contract from `System.State`.
- Jira and Linear answer "unsupported to move / unknown position" honestly.
- `SetPbiStatus`/`SetPbiHold` keep their HTTP routes, permission declarations, admission rules,
  hold semantics and repository disambiguation — only what is behind the vendor call changes.
- Unknown, unconfigured and vendor-error are three different answers on the wire and in the UI.
- A remote change shows up on the next read; the snapshot never becomes the position's authority.

**Non-Goals:**

- No change to the watcher's dispatch mechanics (`LabelLaunchPlan`, `DeclaredMachine`) — it consumes
  the translated position instead of a re-parsed label string, nothing more (issue #449 body:
  «No pide mover los mecanismos del watcher»).
- No journal or Coordination grant semantics change (the Seam says so).
- No task-to-folder attribution model — the permission gap is recorded, not fixed here.
- No Jira/Linear position translation: their refusal is the honest answer, not fake support.
- No migration of persisted state — the position lives at the vendor; the snapshot is in-memory.

## Decisions

### D1 — Position lives on `IPbiSource` as read + write, not as label pass-through

New members on `IPbiSource` (and a small answer type in the same file):

```
Task<PbiPositionRead> GetPosition(string key, string? url, CancellationToken ct = default);
Task MoveTo(string key, string? url, LoopPosition to, CancellationToken ct = default);
```

`PbiPositionRead` carries `LoopPosition Position`, `bool Hold`, and — when the vendor cannot say —
`Position = Unknown` plus `string? Reason` in the vendor's own words. `LoopPosition` is a closed
enum in BuildingBlocks: `Backlog, NeedsRefinement, ReadyForProposal, SpecReady, ProposalReview,
ReadyForImplementation, InProgress, CodeReview, Done, Blocked, Unknown`. It spells the loop's
**declared** vocabulary, not GitHub's label set.

*Alternative considered:* extend `SourcedPbi` with a position and keep writes on `ApplyLabel`.
Rejected: the read must be able to answer "unknown, this is why" separately from a populated row and
from a failure (a throw), and the write must be one transaction the adapter owns — a label list
handed to `SetLabels` leaves the translation in the core, which is exactly what this change removes.

### D2 — Hold rides the position read; the hold write stays a release

`GetPosition` returns hold as a boolean beside the position, because "held over backlog" is today's
contract and a held PBI keeps the status its work reached. The hold *write* keeps its own semantics
from `pbi-status-and-holds`: `SetPbiHold` only clears, never applies, `status:holding` is never
touched by a move. Mechanically GitHub implements the release through its label mechanism; that is
the adapter's business, not the contract's.

### D3 — Precedence is the adapter's, documented, and so is unknown

`GitHubPbiSource` keeps a table of `status:kebab` → `LoopPosition` covering all ten declared
positions. Precedence, where a PBI carries several loop labels, stays "furthest along wins" — the
existing agreed behaviour, now documented in the adapter where it can be reasoned about per vendor.
What changes from today: **no loop label at all is `Unknown` ("no position at the vendor"), not
`Backlog`** — ADO/Jira/Linear stop pretending. An *unrecognised* `status:` label stays an ordinary
chip and leaves the position as it was — today's requirement, kept. ADO translates `System.State`
per checklist column mapping documented in the adapter; a state it does not know answers `Unknown`
with the raw state as the reason.

*Alternative considered:* ask the vendor to never present two positions. GitHub cannot enforce that;
the precedence rule is the agreed mitigation and stays.

### D4 — The move is one vendor call, translated inside the adapter

`SetPbiStatus` resolves the PBI (source + repository + key, as today) and calls `MoveTo`. Inside
`GitHubPbiSource` that is today's sequence preserved: write the new label **before** removing the
old, remove every other loop label, leave `status:holding` alone, write nothing when the position is
already the one asked. ADO writes `System.State` as one field update. Jira/Linear throw
`NotSupportedException`-shaped errors translated to a typed refusal the use case reports as a 4xx
with the remedy, never a 500. Moving to `Unknown` is refused — it is a read's answer, not a place.

After a write that changed something, the snapshot is emptied (existing rule in `read-the-vendors`):
the next read's position comes from what the vendor answers, never from what we last wrote.

### D5 — The wire opens, and `unknown` is not a lane

`PbiStatus` in `contracts.ts` becomes the declared vocabulary in kebab + `"unknown"`. The board
renders unknown PBIs in a defined place (an "unknown" zone beside the board, not a lane) with the
reason the vendor gave, clearly distinct from `backlog`; drag-onto-unknown is not offered. The
`status:{status}` literal in `PbisScreen.tsx` goes: the lane heading comes from `en.ts`, as
DESIGN.md requires for all copy. `GetPbi`'s and `ListPbis`' answers keep a `status` field (now the
wire position string) so the watcher's shape is unchanged — what the string *means* is the connector's
translation, and `LabelWatcher`'s prefix-strip is replaced by consuming the same wire vocabulary.

*Alternative considered:* break the watcher's shape now and carry a structured position. Rejected:
out of the Seam — the issue explicitly does not move the watcher's mechanisms; keeping the string
shape while changing its origin is the smallest truthful contract.

### D6 — Module keeps a projection only

What remains in the module is the enum→wire-string projection (`LoopPosition.Done` → `"done"`) and
the permission/admission/hold rules of the use cases. The dictionary, `LabelFor`, `StatusOf`,
`Name`, `Parse`, the `Hold` const — deleted from the core. The three shared frontend files rule
applies: `contracts.ts`, `queries.ts`, `en.ts` get appended-to in this change's blocks.

### D7 — Permissions stay `Access.AnyCaller`, the gap recorded

The position writes inherit today's documented gap (the `SetPbiStatus` XML doc names it: the fix is
attributing a task to a folder, which this change does not build). The gap stays named in this
design and is carried as a recorded risk rather than silently inherited.

## Risks / Trade-offs

- [GitHub translation regresses today's agreed precedence] → the existing `pbi-status-and-holds`
  scenarios (furthest-along, unrecognised label, hold survival, new-before-old) are kept as delta
  requirements with the same scenarios; the adapter's tests carry them over.
- [`Unknown` where readers used to see `backlog` changes the board's population] → the board's
  unknown zone is a first-class UI state with its own copy; the acceptance criterion demands the
  distinction rather than forbidding it.
- [Watcher depends on the kebab string but the origin changed] → the wire vocabulary is now the same
  string the declared loop spells; the watcher consumes it as today. A string-shape change is out of
  scope and would be this change's finding to report, per AGENTS.md.
- [Snapshot could be treated as the authority] → every requirement in `read-the-vendors` about
  writes-emptying-the-snapshot applies unchanged; the position never has a write-back path.
- [Permission gap inherited] → recorded in D7, carried as known risk, fix needs folder attribution.
- [Two repositories, same key] → the position calls take the URL like every per-PBI call already
  does; the repository disambiguation scenarios stay.

## Migration Plan

No data migration: the position is read from and written to the vendor; the snapshot is in-memory.
Deploy is one build; ADO scopes gain real positions on first read. GitHub readers see the three
formerly-dropped labels as positions instead of `backlog` — a display correction, not a data change.
Rollback is reverting the commit; the vendor state is untouched by the rollback itself.

## Open Questions

None blocking — the product decisions are taken (#447, #449 body). Implementation-order details
(which use case loses `StatusOf` first) are tasks' business, not design's.
