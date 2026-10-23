## Why

ACP-02 of #321 needs deterministic Task input before controlled execution can consume it. Forms alone cannot validate API-created bodies or later edits, and historical prose must not silently become execution authority.

## What Changes

- Add an opt-in versioned Task input block, a GitHub issue form, and a reference-only PR template.
- Add a pure Tasks/Inbox validator and an authorized assessment endpoint that share one parser for all body origins.
- Report field-level remedies, unsupported providers, unresolved dependencies, and non-executable epics without changing legacy execution.

## Capabilities

### New Capabilities

- `controlled-task-input`: Structured, deterministic input assessment without lifecycle authority.

### Modified Capabilities

None.

## Impact

Owns GitHub templates, an additive DoR pointer, Tasks/Inbox validation and its functional tests. No database migration, new package, provider write, or change to held #317. ACP-03/07 will consume the assessment; it is not an approval or a dispatch gate.
