## 1. Shared contract

- [x] 1.1 Align canonical Task identity with ACP01, ACP02 and ACP05 and verify its helper.
- [x] 1.2 Add immutable registry snapshots, operation identities and pure claim/capacity decisions.

## 2. Git transport

- [x] 2.1 Add bounded standard input/output plumbing without modifying checkout state.
- [x] 2.2 Implement strict document reads and exact receive-pack CAS with full read-back.
- [x] 2.3 Register the World seam without an active caller or dispatcher.

## 3. Verification

- [x] 3.1 Verify bootstrap and last-slot races across independent clones of a local bare remote.
- [x] 3.2 Verify stale owners, unknown outcomes, strict schema, operation identity and quiescence.
- [x] 3.3 Verify checkout preservation, coordinate the full gate with the parent and commit locally.

Verified: ControlRegistryTests 26/26 and full AiOrchestrator.InfrastructureTests 401/401
(0 failed); openspec validate --strict clean. GitCli now states its own
safe.bareRepository=all for every invocation: a host global of explicit refused the
local bare remotes these tests bootstrap, and the registry cannot inherit the
operator's global policy. The seam is registered but has no caller yet; the dispatcher
(ACP05) consumes it.
