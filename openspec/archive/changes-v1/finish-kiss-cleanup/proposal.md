## Why

Recent simplifications left contracts, frontend exports and documentation for mechanisms that no
longer exist. Remove that residue and concentrate the repeated Task lookup without adding another
public interface.

## What Changes

- **BREAKING**: remove the one-off Postgres import tool, its solution entry and module friendships.
  Local persistence remains SQLite; importing an old Postgres database is no longer supported.
- Remove the unused CQS caching markers, decorators and memory-cache registration, preserving
  logging, authorization and validation.
- Remove the unused running-Workflow probe and friendships to nonexistent UnitTests assemblies.
- Remove five unused frontend exports and the obsolete Workflows translation family.
- Concentrate Task and Connector resolution inside Tasks while preserving repository/source
  identity, read/write errors, attachment clearance ordering and vendor operations.
- Update live documentation to describe the actual seeds, declared loop and local watcher.
  Keep accepted ADRs and historical archives unchanged.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `local-persistence`: remove the requirement to ship a Postgres import tool.
- `the-request-pipeline`: describe authorization and validation without an unused cache stage.
- `the-world-seam`: remove retired interfaces from the inventory of active seams.

## Impact

Touches BuildingBlocks CQS, the Tasks use cases, frontend query/copy residue, module project files,
the solution, the import tool and live documentation. Existing HTTP endpoints, World adapters,
Coordination, controlled execution evidence and per-module migrations retain their behavior.
