## MODIFIED Requirements

### Requirement: Every id is produced by the domain, and the model is told so
`DomainGeneratedIds` SHALL be called at the end of every module's `OnModelCreating`, declaring that every id
is produced by the domain rather than by the store. Without it EF's default for a Guid key is
`ValueGeneratedOnAdd` and it decides Added-versus-Modified by whether the key still holds its default value —
but `BaseEntity` assigns a v7 GUID in its constructor, so a new entity arrives with a non-default key and EF
concludes it must already exist. That is harmless for a new aggregate, which states `Add` explicitly, and it
bites on a child appended to an owned collection of an already-tracked parent: EF emits an UPDATE, it affects
zero rows, and the failure surfaces as `DbUpdateConcurrencyException`.

An entity whose identity is not a minted Guid SHALL not inherit one: `RunDispatch` declares its
`EffectId` and `RunLogLine` its `(RunId, Ordinal)` — a composite key needs no surrogate, and a
surrogate nobody queries is a per-insert cost paid for nothing (#271: 22 MB of index on the
checkout's own database). `DomainGeneratedIds` keeps its contract unchanged — it names `Id`
properties, and the entities without one need nothing.

#### Scenario: A worktree added to a loaded folder is inserted, not updated
- **WHEN** a worktree is appended to a folder the context already tracks
- **THEN** the row is inserted rather than producing a zero-row UPDATE

#### Scenario: A transcript line carries no minted id
- **WHEN** a run writes a log line
- **THEN** the row's identity is `(RunId, Ordinal)` alone, with one index over it — the key — and
  no second index storing a Guid nobody reads
