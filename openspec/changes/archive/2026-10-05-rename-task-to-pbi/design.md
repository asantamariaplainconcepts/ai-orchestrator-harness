## Context

Issue #448, parent epic #447. The product's centre is becoming the PBI and its journey through the
loop; the code, the wire and the UI still say Task. The rename touches every layer at once — the
module assembly, the World seams, 15 HTTP routes, the DTOs, the SPA, 122 i18n keys and the
capability specs. Visual reference: `design-import/pbi-redesign.html` (PR #457) — reference only for
route and wording choices; it is a prototype, not a contract.

Issue #448 carries a verified inventory (4 Oct 2026, against `master` at `0866c0a9`) that this design
builds on rather than re-deriving. Its load-bearing findings:

- Module discovery globs `AiOrchestrator.Modules.*.dll`, so renaming the assembly keeps discovery
  working; the file moves are `src/AiOrchestrator.slnx`, `AiOrchestrator.Server.csproj`,
  `AiOrchestrator.MigrationService.csproj`, the module project and
  `src/tests/AiOrchestrator.Modules.Tasks.FunctionalTests`.
- There is no `MirroredTask` table and no `TaskRow`: the cache is the in-memory `TaskSnapshot`. The
  persisted `TaskKey` columns live in Runs (`RunsDbContext.cs` 51/65/98, indexed) and dispatch.
- `MergeEffectId.Derive` (`MergeGrantAdmission.cs:177`) hashes the canonical identity
  `{Provider, Host, Repository, Key, folderId, operation}` — none of these names moves.
- `LoopFacts(Status, Held, Folder, Room, LiveRun)` — the stored `DecisionSnapshot` JSON carries no
  "task" key anywhere.
- The 15 routes under `/api/tasks` have exactly two consumers outside the module tree:
  `AiOrchestrator.Server/Attention/AttentionQueue.cs` and `Watching/HarnessApi.cs`. Neither
  `.harness/` nor `.ai/commands/` calls the API.
- The Workflow module is empty and not in `slnx`; the real consumers are Tasks, Runs, Workspace,
  Coordination and the host.

Decisions already made by the person (from the issue and the design review): project/namespace
rename with the assembly; wire key is `key` a secas (route `/api/pbis/{key}` carries the PBI
context); Task remains reserved for vendor-provided children of a PBI; `MirroredTask` stays out of
public vocabulary.

## Goals / Non-Goals

**Goals:**
- One delivery in which domain code, contracts, routes, UI, i18n, tests and specs all say PBI —
  no window where the wire and the screen disagree.
- A PBI reference keeps its full scope: vendor + repository/project + key. Two repositories with the
  same key never share runs.
- Existing runs, references and snapshots stay readable after upgrading a real database; repeating an
  already-registered operation does not create a second effect.
- The storage inventory that `#448` requires: what is actually persisted, what migrates, what
  demonstrably does not need to.

**Non-Goals:**
- No new loop engine, no new screens, no change to behaviour the current specs already pin down —
  the rename must not smuggle in a redesign. The PBI-centred navigation of #447 is later waves.
- No vendor-provided child tasks: Task stays reserved in the vocabulary, with no code naming it.
- No rewriting of accepted ADRs, the retro log or the archived changes. `MirroredTask.FolderPath`
  stays visible in ADR-0005 as history.
- No rename of the persisted `TaskKey` columns or the `DecisionSnapshot` JSON member names — a table
  rebuild that buys nothing.
- No touching `System.Threading.Tasks.Task`, `TaskScheduler`, `Task.FromResult` or any other platform
  type the inventory classifies as platform noise rather than vocabulary.

## Decisions

### D1 — Capability supersedure, not in-place edits, for the task-* specs

`task-inbox`, `task-details`, `task-status-and-holds`, `run-a-task` and `controlled-task-input`
become `pbi-inbox`, `pbi-details`, `pbi-status-and-holds`, `run-a-pbi` and `controlled-pbi-input`:
new spec files with every requirement rewritten to the new vocabulary and the new routes, and the old
capability bodies replaced by a one-line superseded marker naming the replacement.

Alternative rejected: editing five capabilities in place with MODIFIED deltas. A capability's id is
part of the product's vocabulary — `openspec list --specs` would keep saying task forever, and the
issue's own criterion says the specs change with the delivery. The superseded marker (rather than
deletion) keeps the history discoverable: an archived change that names `task-inbox` still resolves
to something that says where it went.

`read-the-vendors`, `mirrored-body-completeness` and the seam/navigation/run/join capabilities stay
and are **modified in place**: their ids describe what they do, not the noun. Splitting `ApplyLabel`'s
seam into a `pbi-*` id just to change a word would orphan `read-the-vendors`' history — it has been
the vendor-read capability through five changes.

### D2 — `IPbiSource` and friends: the World seam renames its types, not its shape

`ITaskSource`→`IPbiSource`, `ITaskSourceCatalog`→`IPbiSourceCatalog`, `SourcedTask`→`SourcedPbi`,
`TaskAssignee`→`PbiAssignee`, `TaskLabel`→`PbiLabel`, `TaskComment`→`PbiComment`,
`ControlTaskIdentity`→`ControlPbiIdentity`, `TaskSnapshot`→`PbiSnapshot`,
`TaskSourceKind`→`PbiSourceKind` (enum members keep their stored spelling — the connector kind is
configuration). Method parameters `taskKey`/`taskUrl` become `key`/`url`: inside the seam the PBI
context is the only context, and `ITaskSource`'s own doc comment made that argument's ID the whole
point of its design (a URL is an address, a key is not).

**Why rename the seam at all** — the issue's inventory ends at World for a reason: `ITaskSource` is
the seam every module reads the vendor through, and a rename that stops one layer below it leaves the
architecture saying Task in its most-load-bearing interface. **Why the shape stays** — the doc
comments, the guard clauses and the scoping rules in `ITaskSource.cs` encode shipped bug costs (#217,
#275, #294); the rename moves names and comments' wording, never the rules.

`System.Threading.Tasks.Task` appears as a return type in the same files (`Task<…>`). The rename is
name-based, applied to the domain terms listed above only; `Task`/`Task<T>`/`Task WhenAll` from the
platform namespace keep compiling untouched. The inventory task classifies every `Task` occurrence
(domain vocabulary / platform type / vendor text / immutable history) before any file is edited.

### D3 — Wire and routes: `/api/pbis/{key}` and `key` a secas

15 routes move (`/api/tasks` family, including `/status`, `/hold`, `/sync`, `/input/validate` and
`{key}/comments|labels|assignees|evidence|attachments/bytes`). `{vendor}` webhooks keep their
`/api/webhooks/{vendor}` shape — the vendor names them, not the PBI. `id` stays `id`; URI templates
in `ApiResults` keep their semantics. The identifier in a PBI DTO is `key`, and the run DTOs'
`taskKey` becomes `key` with the PBI context implicit in the DTO (a run belongs to its PBI; there is
no second key a run carries).

Alternative rejected: `pbiKey`. Rejected in the design review: the route already says `/pbis/`, so
the field name repeating the noun is `taskKey`'s mistake wearing a new coat. A key with its
repository — the full identity the join in `read-a-run` requires — is still carried as the
`{ source, repository, key }` triple the DTOs already spell; only the field's name moves.

### D4 — Persisted names stay; reads map, writes inherit

`TaskKey` columns (`Runs` three columns, indexed; dispatch columns) and any JSON with `taskKey`
member names keep their stored spelling. The EF Core entity property becomes `PbiKey` with
`.HasColumnName("TaskKey")` on each mapping that already declares `HasMaxLength(100)` — the property
renames in code, the column stays on disk. Same for `DecisionSnapshot`: `LoopFacts`'s members
(`Status, Held, Folder, Room, LiveRun`) carry no task noun, so the JSON does not change at all; the
design's verification step confirms this against a real row from an existing database before the
change lands.

This is the meaning of the identity criterion: `MergeEffectId.Derive` hashes
`{Provider, Host, Repository, Key, folderId, operation}` and that tuple is built from
`ControlPbiIdentity`'s renamed properties with identical values — `SHA-256` over the same canonical
string, so an operation registered before the rename deduplicates against the same operation after
it. The property renames are compile-time; the serialised tuple is byte-identical.

Migration, then, is exactly what the inventory finds: **no table rebuilds**, no data motion, and one
class of exception — anything the migration inventory task finds alive (it is expected to find
nothing, because neither `TaskSnapshot` nor the connector enums persist). A migration is added only
where a persisted name would lie about what it holds, and none has been found: column names are
history, not vocabulary.

### D5 — One delivery, tests inside it

The rename lands as one change: backend routes, wire, frontend calls and i18n in the same merge.
Within the change, tasks.md sequences the work so each merge-to-master candidate compiles (seam →
module → host consumers → frontend), but there is no compatibility window with both route families
alive — the two route families are a refusal criterion, not a step. Returns 404 with no
`/api/tasks` fallback.

### D6 — The module rename rides the discovery glob

`Tasks` → `Pbis`: folder, `AiOrchestrator.Modules.Tasks.csproj` → `AiOrchestrator.Modules.Pbis.csproj`,
namespace, `slnx`, ProjectReference entries in `Server` and `MigrationService`, functional tests
folder. The pattern `AiOrchestrator.Modules.*.dll` in `ModuleRegistration.Discover()` keeps matching
— no code edits, only the copy it globs against changes name.

## Risks / Trade-offs

- [A domain `Task` slips through `System.Threading.Tasks.Task` renames, or vice versa] → the file
  inventory task classifies every occurrence first; the rename is applied per-occurrence with the
  inventory as the map, and a file is closed only when its inventory line is exhausted.
- [Persisted columns and a renamed property drift apart] → `HasColumnName("TaskKey")` on each renamed
  mapping; the check line compares `PRAGMA table_info` on a migrated database to the copy it upgraded.
- [The effectIds break anyway] → a test repeats a registered operation across the rename and asserts
  the intent resolves to the same UUID; if that fails, the change stops rather than re-deriving.
- [A '"task"' string in vendor wire text gets renamed] → the inventory separates vendor-owned shape
  (e.g. `repodata.label.name`, webhook payloads) from our vocabulary; label `status:` values are the
  vendor's own and stay.
- [Lost work on merge with other concurrent tracks] → the changes touch nearly every file in the
  module tree; the issue holds WIP until this change merges (`/aio:implement`'s WIP gate names it),
  and the worktree claim is the branch, per AGENTS.md's rule 5.
- [Inbox UI and API disagree mid-delivery] → cosmetic risk at zero: the rename ships in one squash
  merge, so there is no window where the screen and the routes disagree.

## Migration Plan

Copy a database from a real existing run; upgrade it with the migrations this change adds (expected:
zero, verified by the inventory); compare `count(*)` and identity checksums for `Runs`, dispatch and
journal rows before/after; open a pre-rename PBI and its run history in the UI; confirm the join
still resolves by `{source, repository, key}`; repeat an already-registered operation and confirm no
new effect. Rollback: revert the merge; the persisted schema is unchanged by design, so a pre-rename
binary runs against the same database — nothing to roll back that moved.

## Open Questions

None blocking. The design review already fixed the three that were open: namespace rides the
assembly, wire key is `key` a secas, Task is reserved for vendor children. Suite/test data: the
functional tests' fixtures carry Task in dataset names — they rename with the module, no decision
needed there.
