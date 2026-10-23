# local-persistence Specification

## Purpose
The database is a file inside the checkout. This app is local and single-person by decision, and a Postgres
container was machinery for a database server it never needed to share — silently empty volumes, orphaned
containers, and a `PANIC: could not locate a valid checkpoint` after an interrupted run (ADR-0003).
Reverse-engineered from the shipped code, not negotiated ahead of it.

## Requirements
### Requirement: Persistence is a SQLite file derived from the checkout
Local persistence SHALL run on SQLite (ADR-0003, accepted 2026-08-20) rather than on a database server, and
the AppHost SHALL derive the file's path from the checkout root, so a worktree's database is its own without
the per-worktree volume machinery a container needed. The AppHost SHALL start no containers, so a `postgres-*`
container on the machine is leftover history rather than this AppHost's doing.

#### Scenario: A worktree gets its own database without extra machinery
- **WHEN** the app is started from a worktree
- **THEN** it uses a SQLite file derived from that checkout root

#### Scenario: Docker is not in the loop
- **WHEN** the AppHost starts
- **THEN** no container is created

### Requirement: Each module keeps its own migration set and its own history table
Each module that persists anything — Runs, Workspace and Coordination; Tasks holds nothing on disk — SHALL
own its EF migration set, and migration history SHALL stay per module as `__EFMigrationsHistory_<schema>` now
that schemas have vanished. Table names SHALL NOT collide: the tables across those modules are already
distinct. Nothing SHALL cross between modules by foreign key.

#### Scenario: Two modules migrate independently
- **WHEN** one module gains a migration
- **THEN** only its history table and its own tables are affected

### Requirement: The migration step is its own process, run before the server
Migration SHALL be a dedicated resource (`src/root/AiOrchestrator.MigrationService`) that the AppHost runs to
completion **before** the server, rather than a side effect of the server starting: an in-process migration
gated on `!IsProduction()` guesses, and under `aspire run` the server's environment silently defaults to
Production — fresh database, no schema, 500s. The migrator SHALL compose secrets and resolve the default
database exactly as the server does, so the process that creates the schema and the process that queries it
cannot disagree about which file it is, and SHALL build without running so hosted services never start —
migrating must not also mean polling.

#### Scenario: A fresh clone boots with an up-to-date schema
- **WHEN** the app is started against a database file that does not exist
- **THEN** the migration resource runs to completion first and the server starts against the current schema

#### Scenario: Migrating does not start the pollers
- **WHEN** the migration process runs
- **THEN** no hosted service polls a vendor or probes a folder

### Requirement: The two SQLite costs are handled in the model, not worked around at call sites
`DateTimeOffset` SHALL be stored through a model-wide `DateTimeOffsetToBinaryConverter` convention, because
SQLite cannot order or compare the default `TEXT` form — `OrderByDescending` returned 500 and failed 32 of 73
of the then Tasks module's tests without it. A primitive collection SHALL map to a JSON-encoded `TEXT`
column natively, with no converter. The native SQLite package SHALL be pinned centrally to a version without
the known advisory, because warnings are errors and the transitive default fails the build as NU1903.

#### Scenario: Ordering by a timestamp works
- **WHEN** the runs list orders runs newest first by when they started
- **THEN** the query succeeds against SQLite

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

### Requirement: The move off Postgres carries the existing data across, once
`tools/AiOrchestrator.PostgresImport` SHALL copy what a Postgres container holds into the checkout's SQLite
file **through each module's own DbContext** — the model read on Npgsql and written on SQLite — so every mapping
difference (the labels collection, the binary `DateTimeOffset` encoding, decimals as text) is EF's to translate
rather than the tool's. It SHALL be a one-off run by hand with both connection strings, which is the data
continuity ADR-0003 asked for.

#### Scenario: The import translates through the model
- **WHEN** the tool copies a module's tables
- **THEN** it reads and writes through that module's DbContext rather than moving raw values
