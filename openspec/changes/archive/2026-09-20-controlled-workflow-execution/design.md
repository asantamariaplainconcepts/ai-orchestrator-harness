## Context

RunPlan already separates sequencing from process execution. Workflow declarations and imported rows describe the plan, while Run stores a copy. Agent idle currently ends a step, and CheckResult retains only the latest terminal check. Neither is a durable control-plane decision.

## Goals / Non-Goals

**Goals:** opt-in execution with correlated results, independently observed candidate commits, recorded gates and a readable graph/trace. Preserve legacy workflows.

**Non-Goals:** replacing GitHub lifecycle labels, modifying #313/#317, automatic retries, automatic merge, distributed scheduling, or allowing a model score to grant approval.

## Decisions

### Controlled is a property of the imported Workflow

An optional `controlled: true` is preserved across declaration, import, source comparison, export and execution. Existing workflows retain their current semantics. Controlled plans refuse unresolved or skipped required steps rather than silently weakening the plan.

### Agent results are claims, not success

Each controlled Agent step receives a unique result-file location and correlation data. A bounded JSON result names the Run, step and challenge and returns candidate, needs_input, blocked or failed. Idle triggers validation, never unconditional advancement. A missing or invalid result parks the plan with a reason.

The result reader and git observation live behind a World interface. Candidate results must name the actual HEAD of a clean working tree. Shell steps require that candidate and observe the same clean HEAD after completion. Missing exit codes, dirty trees and changed commits cannot pass.

### The control decision is pure and terminal outcomes are explicit

Version 1 accepts one Agent step followed by resolved one-shot gates and a final Hold. Other shapes are refused, not interpreted as legacy plans. The initial graph is a sequence with named failure and Hold exits. Each result is accepted at most once for its expected step. Agent and shell completion are different inputs. A Hold ends automatic dispatch and cannot be bypassed by another idle signal.

The Agent publishes its result atomically as its last action. The harness stops and joins the Agent before checking the candidate or running a gate. Both publication and idle can trigger validation; idle with no result cannot pass. Each step has a stored 1800-second deadline.

`held` is a new terminal Run state, distinct from legacy `waiting`, which still has a connected Agent. Reconciliation records execution loss in the durable trace and ends at `held`, without replay.

### Evidence is persisted before another effect

The Run stores the resolved controlled plan and its hash. A separate Runs-owned event table stores immutable step observations and decisions. Control-plane persistence failures stop dispatch; transcript batching remains unchanged.

No completed control step is replayed automatically after restart. An interrupted Run retains its evidence and requires a new Run; generic conversation resume must not silently drop controlled semantics.

### Inspection is a projection

A read endpoint returns the stored definition and ordered trace. The Run screen derives its execution graph from that definition, not from a second hand-written graph. Result details identify the observed commit, exit code, outcome and reason.

## Risks / Trade-offs

- Result-file correlation prevents accidental reuse, not a malicious Agent with the owner's filesystem access. Independent git and gate checks remain mandatory; this is not a sandbox.
- Before/after clean-tree observations detect changed candidates, not adversarial transient edits. A stronger isolated-verifier environment is a separate boundary.
- This is local execution control. Multi-machine claim and shared capacity remain blocked on a separately approved lifecycle design.
- Freeform conversational resume has no safe interpretation for an interrupted controlled graph. Refuse it with a remedy instead of downgrading to a legacy Run.

## Migration Plan

Add a false-default flag to Workflows and nullable plan metadata plus an event table to Runs. Ship a separately named controlled plan; do not change the existing staged and ship plans. Rollback disables the opt-in plan and leaves history readable.

## Open Questions

The shared lifecycle and claim protocol remain in #317 and follow-on work. This increment neither answers those questions by accident nor starts the label watcher.
