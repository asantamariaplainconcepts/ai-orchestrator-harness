# Why

The accepted grant contract (`controlled-workflow-authority`, ADR-0006) exists on paper and nothing implements it. What exists: the registry CAS with claims and bootstrap (#328), the dispatch chain that consumes hold-release as v1 authorization (#329), and a `status:holding` that is a mute boolean — applied automatically, read back, never saying why. The next cut in the epic (ACP-07) needs a content-bound admission gate at the merge boundary, and ACP-10 behind it needs exactly that: approval that names the SHA it approved. Without this change, the follow change has nothing to consume.

# What Changes

- **`approve_candidate` as a ledger operation.** `RegistryOperationKind.Grant` — granted through an owner-operation subcommand of `AiOrchestrator.Control` (`approve-candidate --task <identity> --sha <oid> --base <oid> --spec <pin>`), CAS-recorded in the same `harness-control-v1` document. The grant binds actor, Task, candidate HEAD, base, spec pin, authorization epoch and expiry; it is single-transition, append-only revoked, and invalidated by any change to what it approved.
- **Revoke, same path.** An owner-operation `revoke-grant --grant <id>` appends the revocation; the next admission that reads the grant refuses it. No in-flight undo, no takeover — publication of intent, observation of effect, the contract's own boundary.
- **A grant-read the admission can call.** `IControlRegistry` gains `Grants(taskIdentity)` returning the currently-valid (unexpired, unrevoked, epoch-matching) grants with their bindings — the query ACP-07's admission gate and ACP-10's merge boundary will consume. Exactness rule: a binding that disagrees with the presented candidate is `grant_invalid`, an absent grant is `grant_missing`, and neither ever falls back to "close enough".
- **Typed hold reasons.** A closed vocabulary in `BuildingBlocks/World` (the same set #246's attention queue will read): `SetTaskHold` gains an optional typed reason; whoever applies a hold automatically writes it; it is projected as a typed attention comment through the #328 seam and displayed raw when unrecognized — an unknown reason shows itself, never disables the hold.

# Capabilities

### Modified Capabilities

- `shared-control-registry`: the ledger gains the Grant operation, its claim-independent accounting (grants occupy no capacity, claim no task), its replay in `ValidDocument`, and the grants read.
- `controlled-workflow-authority`: the Human-grants requirement moves from contract-on-paper to implemented seam for `approve_candidate` — with `approve_proposal` explicitly deferred to hold-release v1 (ADR-0006 acceptance delta 3).
- `task-status-and-holds`: holds carry typed reasons.

# Impact

- **Code:** `IControlRegistry` (+Grants read, +Grant kind), `ControlRegistryDecisions` (grant transition, expiry window, replay), `ControlRegistryCodec` (document carries grants), `AiOrchestrator.Control` (+`approve-candidate`, `+revoke-grant`), `SetTaskHold` (+reason), the attention comment seam usage for projection, `en.ts` blocks if any screen text changes.
- **Document shape change is additive:** existing registries (claims side) validate unchanged; grants start empty. `ValidDocument` grows a grants table with the same replay discipline as receipts.
- **Not in scope:** merge itself (ACP-10 consumes), the proposal grant (v1: hold-release), delegation (owner-only), the attention queue UI (#246 reads the same vocabulary).
