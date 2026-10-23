## Context

StartRun currently creates the Worktree before persisting a Run. Launch returns before the process starts; its return is not proof of execution. Controlled Runs already persist step evidence and prohibit freeform resume.

## Goals / Non-Goals

**Goals:** One local durable binding per effect UUID, scoped authorized admission and recovery, no second launch on duplicate or crash, and explicit uncertainty.

**Non-Goals:** Shared ownership, signed execution permits, authenticated coordinator provenance, provider Task/Hold writes, cross-machine guarantees, retries, or enabling a watcher to dispatch.

## Decisions

- The optional effect envelope on POST `/api/runs` selects a separate `runs:start` Folder-scoped command. `IFolderScopeReader` verifies the registered Folder id/path. Existing starts without the envelope retain their behavior.
- Canonical `ControlTaskIdentity` is descriptive input, not authority. Its numeric key must equal TaskKey after removing one presentation `#`; the Run retains that presentation key.
- A Runs-owned table uses EffectId as its primary key and a unique preallocated RunId. Admission stores the canonical request JSON, SHA256 digest, Task identity and resolved WorkflowPlan snapshot before Worktree creation. Equality checks both digest and canonical JSON. A SQLite primary-key conflict is replay only if that exact effect exists with matching scope and payload.
- Exact duplicates read the existing observation before probing health or resolving current Workflow configuration. The initial WorkflowPlan snapshot and later resolved control definition belong to that admission; changing configuration does not reinterpret an existing effect.
- Only the successful insert may proceed. Pending admission can have no Run row yet. The Run insert and `launch-intent` transition commit together before Launch. `process-observed` is recorded with a real PID through RunSink, never from Launch returning. Refused setup preserves its typed error.
- GET `/api/runs/dispatches/{effectId}?folderId=...` requires `runs:read` and compares stored scope before returning anything. A different scope gets a nonrevealing 404, while changed input in the same scope gets 409.
- Recovery observes pending or launch-intent without advancing them. Such observations explicitly mean uncertain admission/process state, not success. No expiry, takeover or reset exists.

## Risks / Trade-offs

- SQLite and process creation cannot form one transaction -> persist intent first, report the gap, never retry.
- A crashed pending admission blocks forever -> intentional until separately reviewed human reconciliation exists.
- A caller can choose a new UUID -> this primitive does not grant shared Task ownership or prevent deliberate new starts.
- Folder lookup or database failure -> surface the failure and stop before the next effect.
- Later command configuration can change before command resolution -> freeze the resolved control definition before launch; duplicates never resolve it again.

## Migration Plan

Add one Runs-owned table with EF tooling. Existing Run rows and legacy requests are unchanged. Do not drop dispatch history to recover a failed effect.

## Open Questions

External authority, canonical policy pins, shared reservation verification and coordinator wiring remain separate gated work under epic #321.
