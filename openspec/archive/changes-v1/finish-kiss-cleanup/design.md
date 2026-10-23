## Context

The audit found seven cleanup candidates. The owner approved all seven and explicitly requested
removing the entire Postgres importer rather than repairing its obsolete Workflows reference.
The current application reads Tasks from vendors, runs a local/plain watcher and retains separate
controlled execution and shared control contracts.

## Goals / Non-Goals

**Goals:** remove unused mechanisms and exports, align live documentation, and keep Task identity
resolution in one implementation.

**Non-Goals:** remove HTTP endpoints, change permissions, change database schemas, rewrite accepted
ADRs, combine terminal transports, or remove the control registry and journal.

## Decisions

- Delete the importer, its solution entry and its module friendships. Remove its capability
  requirement through a delta; existing SQLite data and migrations do not change.
- Reduce CQS to logging, authorization, validation and the handler. No request implements the
  cache markers, so retaining the decorators buys no behavior. TaskSnapshot's actual vendor cache
  stays independent.
- Keep shared Task lookup inside the existing TaskSnapshot module rather than introduce a resolver
  interface. Resolve source and repository once; distinguish reader and writer selection with
  explicit methods and existing error codes. Each use case retains its permission, operation,
  no-op rules and invalidation.
- Attachment clearance stays after resolving the Task and before requiring a reader. A missing
  Connector must not change a disallowed attachment from NotFound into NoReader.
- Remove only proven-unused frontend exports and translation keys. Keep terminal invalidation
  keys, dynamic translation families and all HTTP endpoints.
- Update current documentation; accepted ADRs and archived changes remain historical evidence.

## Risks / Trade-offs

- Removing the importer drops old Postgres data migration support. The owner explicitly selected
  that trade-off; existing SQLite installations do not need migration.
- Refactoring eight Task callers could change error precedence or identity. Add endpoint tests for
  invalid source, duplicate repository keys, issue/PR identity and attachment clearance, then run
  the Tasks suite and full gate.
- A literal search can miss generated translation keys. Preserve all four current dynamic
  translation families and validate the remaining catalogue with TypeScript.
- Documentation can confuse local/plain dispatch with shared control. Describe both separately,
  without treating the watcher's compatibility mode names as proof of registry acquisition.
