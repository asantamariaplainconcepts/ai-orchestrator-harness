## 1. Remove obsolete mechanisms

- [x] 1.1 Delete the Postgres importer, its solution entry and its module friendships.
- [x] 1.2 Remove the unused CQS cache markers, decorators and composition.
- [x] 1.3 Remove the unused running-Workflow probe and nonexistent UnitTests friendships.
- [x] 1.4 Remove five unused frontend exports and their unused supporting types and imports.
- [x] 1.5 Remove the obsolete Workflows translation family without touching dynamic keys.

## 2. Concentrate Task resolution

- [x] 2.1 Share Task identity and Connector resolution within TaskSnapshot.
- [x] 2.2 Wire all eight use cases while preserving no-op behavior, errors and attachment clearance.
- [x] 2.3 Add endpoint regressions for shared resolution and error precedence.

## 3. Align documentation and verify

- [x] 3.1 Update live documentation and stale comments without rewriting accepted ADRs or archives.
- [x] 3.2 Validate the OpenSpec change and inspect the diff for unintended behavior changes.
- [x] 3.3 Run the focused Tasks tests and the complete local Aspire gate.
