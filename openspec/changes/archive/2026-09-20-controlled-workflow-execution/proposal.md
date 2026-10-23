## Why

The current Run plan advances when an Agent becomes idle. That is an execution boundary, not proof that a candidate exists or a gate accepted it. A control plane needs explicit results and durable decisions before dispatching its next effect.

## What Changes

- Add opt-in controlled execution to declared Workflow plans, preserving existing plans.
- Require a correlated Agent result before advancing; validate candidate commits independently.
- Run declared gates against a clean, identified candidate and refuse stale or changed inputs.
- Persist the resolved plan and immutable decision evidence before advancing.
- Expose the plan and actual execution path in the Run view, including blocked and failed outcomes.
- Keep Holds terminal for automatic execution. No retries, merge, issue relabelling or cross-machine scheduler are introduced.

## Capabilities

### New Capabilities

- `controlled-workflow-execution`: Typed outcomes, candidate verification, durable decisions and an inspectable execution graph for opt-in controlled plans.

### Modified Capabilities

- `declared-workflow-plans`: Preserve the controlled-execution setting across import, comparison and export.
- `run-a-task`: Controlled plans use validated outcomes rather than idle as authority to advance; legacy plans remain unchanged.

## Impact

Runs and Workflows own their respective migrations. New World contracts isolate result-file and git observations from modules. Existing Agent and command runners remain the executors. Frontend changes are limited to Run inspection and the control-plane response contract.

This local change does not implement or modify held issue #317 or the label watcher in #313. Publishing a tracking issue and PR remains subject to the repository's shared-state confirmation rule.
