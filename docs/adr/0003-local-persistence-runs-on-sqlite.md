# ADR-0003: Local persistence runs on SQLite, not Postgres

- **Status:** Accepted <!-- Proposed | Accepted | Rejected | Deprecated | Superseded by ADR-NNNN -->
- **Date:** 2026-08-20
- **Deciders:** repository owner; agent implementing #210
- **Tags:** backend, infra, persistence

## Context

This app is local and single-person by decision (#210): the host refuses to listen anywhere but
loopback, and identity composes the machine's owner. Yet its database is PostgreSQL in a Docker
container (`src/root/AiOrchestrator.AppHost/AppHost.cs`), so the app does not start unless Docker
is running. Four modules each own a Postgres schema and an EF migration set; the functional tests
start a Testcontainers Postgres per collection.

The coupling to Postgres itself is one line: `Labels` as a `text[]` in `TasksDbContext.cs`. No
jsonb, no raw SQL, no LISTEN/NOTIFY, and never more than one writer.

What the container costs is recorded in `docs/process/retro-log.md`: silently empty data volumes,
orphaned containers (`postgres-2c197ffc`, `postgres-ad1ee381`), `PANIC: could not locate a valid
checkpoint` after an interrupted run, and the #50/#52 machinery that derives a volume and database
per worktree — all of it machinery for a database server this product never needed to share.

A throwaway spike (branch `spike/210-sqlite`, deleted after measuring; 74 files, +900/−6803)
answered the three doubtful points on this codebase, not in the abstract:

1. **Migrations regenerate.** All four modules' migration sets regenerated against the SQLite
   provider with one `dotnet ef migrations add` each. Migration history stays per-module as
   `__EFMigrationsHistory_<schema>`. Table names do not collide once schemas vanish — the
   seventeen tables across four modules are already distinct.
2. **The `text[]` needs no converter.** EF Core 10 maps a `PrimitiveCollection` to a JSON-encoded
   `TEXT` column on SQLite natively. Zero code.
3. **The functional tests run without Testcontainers.** Tasks module: 73/73 green against a
   temp-file SQLite database, 7.9s wall clock warm versus 11.7s on Testcontainers Postgres —
   and no Docker daemon in the loop at all.

The spike also surfaced the real costs, which this ADR records rather than hides:

- `Microsoft.EntityFrameworkCore.Sqlite` 10.0.9 pulls `SQLitePCLRaw.lib.e_sqlite3` 2.1.11, which
  carries a known high-severity advisory (GHSA-2m69-gcr7-jv3q) and fails the warnings-as-errors
  build as NU1903; pinning 2.1.12 centrally resolves it.
- SQLite cannot order or compare `DateTimeOffset` stored as its default `TEXT`.
  `OrderByDescending(task => task.MirroredAt)` returned 500 and failed 32 of 73 Tasks tests. EF's
  documented remedy — a model-wide `DateTimeOffsetToBinaryConverter` convention — turned all 73
  green. Its caveat: ordering across mixed offsets is approximate; every write in this codebase
  is UTC, so the caveat is theoretical here, but it belongs in the convention's comment.
- Respawn has no SQLite adapter; the test fixture's reset becomes "delete all rows outside the
  migration-history tables", which the spike showed is behaviourally equivalent for these suites.
- `EnableRetryOnFailure` does not exist on the SQLite provider (nothing transient to retry on a
  local file) — dropped, not replaced.

## Decision

We will move local persistence to SQLite: one database file derived from the checkout, opened by
the process itself. Each module keeps its own EF migration set and its own migration-history table
(`__EFMigrationsHistory_<module schema>`); the schema names live on only in those suffixes.
`MigrationService` remains a separate AppHost resource — the reasons the Server never migrates are
about ordering, not about Postgres. The AppHost drops the `postgres` container resource, the
per-worktree volume derivation, and the persistent-lifetime special case; a worktree's database
becomes a file beside its checkout, which is what #52 was approximating with volumes.

The migration itself is a follow-up issue citing this ADR; nothing from the spike merges.

Reversal conditions — what would have to become true to bring Postgres back (or stop the
migration if it becomes true first):

- The product grows past one person or one machine: a hosted deployment, shared inboxes, or any
  second concurrent writer.
- A feature genuinely needs a database server: LISTEN/NOTIFY-style push, cross-connection
  full-text search, or data too large for a local file to serve interactively.
- The `DateTimeOffset` binary encoding's mixed-offset ordering caveat stops being theoretical —
  i.e. a vendor timestamp must be stored with its original non-UTC offset *and* sorted.

## Consequences

- **Positive:** the app starts without Docker; the entire retro-log class of volume/orphan/PANIC
  failures becomes unreachable; functional tests lose the container start (11.7s → 7.9s warm for
  Tasks, times four assemblies, and no image pull on a fresh machine); a worktree's isolated
  database stops being derived machinery and becomes a file path.
- **Negative:** every module's migration set regenerates at once (one issue owns all four modules'
  migrations for that change, per the AGENTS.md collision rule); a model-wide `DateTimeOffset`
  convention must land in every DbContext with its ordering caveat documented; the vulnerable
  `SQLitePCLRaw` transitive needs a central pin until upstream moves; Respawn leaves the test
  fixture in favour of a hand-rolled reset.
- **Neutral:** the seams above the database do not move — modules, `IModule.Migrate`, the CQS
  pipeline and the World interfaces are provider-blind, which the spike's 74-file diff (almost
  entirely regenerated migrations) demonstrates; backups change character from `pg_dump` to
  copying a file; the Aspire dashboard loses its database resource row.

## Alternatives considered

- **Postgres in Docker (status quo)** — rejected: pays a running container, a volume lifecycle
  and a per-worktree derivation scheme for multi-user capability this product has decided never
  to need locally.
- **SQLite, one file per module** — rejected: four files buy nothing once table names are proven
  disjoint and history tables are suffixed; they quadruple the "which file is this worktree's"
  surface that #52 existed to shrink.
- **Postgres without Docker (Homebrew/embedded)** — rejected: removes the container but keeps a
  server process, a port, credentials and a data directory to manage; the pain the retro-log
  records is the server lifecycle, not the container specifically.
- **Dual-provider (SQLite locally, Postgres someday)** — rejected: two migration sets and a
  conditional provider is permanent tax on every schema change, purchased against a future the
  product has explicitly declined.

## References

- Issue: #210 (Change-ID: adr-postgres-vs-sqlite); spike branch `spike/210-sqlite` (deleted).
- Retro: `docs/process/retro-log.md` — the #50/#52 entries and the orphaned-container mornings.
- EF Core SQLite limitations: DateTimeOffset ordering and the converter remedy.
- Related: ADR-0002 (the record the Server keeps lives in this database).
