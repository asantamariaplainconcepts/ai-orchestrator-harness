## Context

ACP04 supplies the registry primitive for epic #321, behind the World seam and without an active
caller. ACP00 defines the proposed authority model; ACP05 will own local orchestration and effect
reconciliation. Neither a process-local lock nor a successful ordinary Git push is shared CAS.

## Goals / Non-Goals

**Goals:** exact registry CAS, atomic Task claim and repository capacity, immutable decision
helpers, bounded strict documents, generation tombstones, and explicit confirmed/absent/unknown
observations.

**Non-goals:** dispatch, automatic takeover, TTL expiry, provider effects, process recovery,
Coordination persistence, UI and a completed multiwriter execution authority.

## Decisions

- The only ref is `refs/heads/harness-control-v1`. Its commit contains only `control.json`.
  New commits name the observed commit as parent; bootstrap has no parent. Each proposed mutation
  gets a new UUID distinct from the caller's durable operation UUID. The ref is never deleted.
- Receive-pack applies `git push --force-with-lease=<ref>:<observed-oid>`. Bootstrap uses an
  explicitly empty expected value. A full validated read-back verifies the desired commit and
  document; exit code alone is not evidence. There are no automatic retries.
- Read and write use the same single configured push endpoint, not a possibly different fetch
  endpoint. Git plumbing stores objects without checking out the ref or touching HEAD, index or
  worktree. Fetch does not write FETCH_HEAD or remote-tracking refs. GitCli gains bounded stdio
  support; all arguments remain distinct process arguments.
- A schema and policy pin, canonical repository identity, capacity, mutation counter and UUID,
  claim tombstones and operation receipts form an immutable snapshot. Acquisition inserts a claim
  and consumes one capacity slot in that same document. Released rows preserve generation.
  A bounded receipt ledger prevents operation UUID reuse with a different request.
- The public seam offers Read, Acquire, Query and Release, not arbitrary document writes.
  Release requires the matching owner, generation and reservation plus explicit confirmed process
  quiescence and absent unsettled effects. Stored unsettled effects block release regardless.
  No clock participates in a decision.
- Unknown transport outcomes or invalid schema never become an empty registry. Failures carry
  fixed diagnostic codes rather than raw Git stderr, remote URLs or credentials.

## Risks / Trade-offs

- [Shared credentials bypass fencing] -> registry CAS fences only registry mutations. Shared
  dispatch stays disabled until a separate single-writer or effect-enforcing authority exists.
- [Lost acknowledgements] -> return unknown unless full read-back proves the exact mutation.
  Querying the same operation UUID can reconcile registry state, not external effects.
- [Bounded history fills] -> refuse further mutations rather than prune idempotency evidence or
  reset generations. A future reviewed protocol must handle compaction.
- [Remote deletion/reset by an administrator] -> outside cooperative CAS guarantees; v1 never
  offers delete/reset and does not promise tamper resistance.
- [Caller attestation is not OS fencing] -> quiescence/effect evidence is explicit input, not a
  claim that the primitive independently inspected a process or provider.

## Migration Plan

Register the implementation without adding a caller. No database migration or existing Workflow
behavior changes. Removing the registration rolls back local availability without deleting shared
history.

## Open Questions

Shared dispatch requires the later authority decision; this primitive does not resolve it.
