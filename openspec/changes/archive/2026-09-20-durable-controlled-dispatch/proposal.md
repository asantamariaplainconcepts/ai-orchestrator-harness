## Why

A lost controlled StartRun response can cause a caller to start a second Agent. ACP-05 needs a durable local binding that can be observed after a crash without replaying effects.

## What Changes

- Accept an optional stable effect UUID with a canonical Task identity and authorized Folder scope on controlled starts.
- Persist admission before creating a Worktree; bind one Run and immutable request fingerprint per effect.
- Return existing observations for exact duplicates, conflict on changed input, and expose a permission-scoped recovery query.
- Distinguish pending admission, launch intent, process observation and refusal. Never replay an uncertain dispatch.
- Leave legacy starts unchanged and shared automated dispatch disabled.

## Capabilities

### New Capabilities

- `durable-controlled-dispatch`: Local, permission-scoped idempotency and observation-only recovery for controlled Run admission.

### Modified Capabilities

None.

## Impact

Runs schema, migration, StartRun admission, process evidence sink, recovery endpoint and functional tests. Reuses shared canonical Task identity and authoritative Folder scope lookup. No Coordination implementation, external authority grant, Hold mutation, frontend or root composition changes.
