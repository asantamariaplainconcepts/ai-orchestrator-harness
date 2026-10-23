# ADR-0006: Shared control authority is explicit and fenced

- **Status:** Superseded by the v2 product (#496) (2026-10-09). The two documents it links were deleted with the first product (#553) and remain in the history.
- **Date:** 2026-09-20
- **Deciders:** repository owner (Andoni Santamaria), accepted in session; deltas grilled and
  recorded below
- **Tags:** workflow, coordination, authority

## Context

The local controlled Workflow shipped in #320 verifies Agent results and command execution.
It does not own shared Task phase, reserve shared capacity or record human permission.
ADR-0005 separates GitHub state from local decisions, but ordinary branch push is not
create-if-absent: two pushes to the same commit can both succeed. Deterministic decisions
alone cannot exclude two coordinators. A cleared Hold also does not identify reviewed content.

The held #317/#318 proposal reads working-copy policy and excludes claims. Composing it
with the watcher without a further boundary would allow different policies and local
failure histories to authorize competing effects.

## Decision

We propose one remote git control ref, `refs/heads/harness-control-v1`, for repository-scoped
coordination metadata. An explicit expected-object-ID compare-and-swap changes the entire
registry: Task claim and capacity reservation are one update. A unique mutation ID makes
each proposed commit distinct. Read-back, not push success alone, establishes ownership.
Generation tombstones persist; heartbeat expiry never transfers ownership.

GitHub remains authoritative for shared phase and Hold. Git stores the published, pinned
specification and policy; SQLite stores local Run history and durable effect intent.
The registry records ownership, grants and shared effect receipts, not a competing phase
machine. Full Task identity is provider, host, repository and key.

We propose cooperative, authorized harness participants, all using the same CAS admission
protocol. Every authoritative harness effect requires a durable remote permit whose atomic
admission validates owner, generation, grant and policy. Only the original winning execution
instance can send that effect; re-observing its permit after restart cannot send it again.
The local journal has a single execution owner enforced by an exclusive OS lock.

Registry CAS cannot fence an HTTP request already sent to GitHub or an OS process. Therefore
owner/generation checks and durable effect permits are serialized by remote CAS; no
generation handover occurs while a permit or process could still execute. An uncertain
outcome retains its reservation. Revocation stops new permits; an already authorized
in-flight operation must be observed and drained rather than claimed to have been undone.
Shared dispatch remains disabled until admission, the held lifecycle integration and the
real-provider acceptance checks in the [contract](../control-authority-contract.md) exist.
This is not a sandbox or protection against malicious code sharing the owner's
credentials. Stronger isolation would require a separate threat-model decision.

**Accepted 2026-09-20 — v1 defers grants.** Human authorization in v1 is the repository
owner releasing a Hold on GitHub, observed fresh and recorded as the authorization fact.
It matches the manual loop's standard for the same person; content-bound approval arrives
with ACP-06, and no stage that needs content-bound authorization (final merge) ships
automated before then. The grant mechanism below is ACP-06's future contract.

Human grants come through an explicitly authorized repository-owner operation, bind action and reviewed
content to a policy revision, expire and can be revoked. Neither an Agent, a ready PR, a
label release nor this proposal creates a grant. Working-copy policy is simulation only.
Legacy manual and local-only controlled execution retain their behavior without gaining
shared authority.

Accepted on 2026-09-20, this decision supersedes only ADR-0005's claims that ordinary
branch push establishes exclusive ownership, deterministic evaluation removes coordination
needs, and lease expiry can clear execution ownership. It resolves published policy and
controlled authorization boundaries. ADR-0005's accepted text remains intact; GitHub phase,
level-triggered observation and pure decisions remain. This decision does not accept or
implement #317.

## Acceptance deltas, 2026-09-20

Grilled with the repository owner at acceptance; recorded here because they are the terms the
acceptance actually carries.

1. **The core invariant is automatic-vs-automatic exclusion:** two automatic Tasks never
   launch the same issue (CAS claim), and the last capacity slot is exactly one reservation in
   the same push. A human starting a fifth run by hand is outside what the protocol polices.
2. **A stalled coordinator escalates, never takes over.** A coordinator observing another's
   blocking claim with no observable progress raises an attention-queue item; only the owner
   recovers. No TTL, no takeover. (See Consequences, mitigation.)
3. **v1 defers grants.** The repository owner releasing a Hold on GitHub is v1 human
   authorization — the same standard the manual loop holds for the same person. Content-bound
   grants arrive with ACP-06, and no content-bound stage (final merge) ships automated before
   then.
4. **The cooperative threat model is accepted as scoped**, with a deployment precondition:
   the control ref is protected by GitHub rule (no force-push, no delete) before any pilot.
   A ref deletable by the same credentials is a registry-as-suggestion.

## Consequences

- **Positive:** competing reservations cannot consume the same final slot; failures and
  uncertain effects survive restarts; approval identifies the exact content.
- **Negative:** a partition or abandoned permit can block indefinitely. Every participating
  effect path must enforce admission; an uncooperative credential holder can bypass it.
  One registry ref serializes unrelated Tasks in a repository and grows with receipts.
- **Neutral:** ACP-04 implements the registry seam, not a scheduler. ACP-05 owns the proposed
  Coordination module and its journal, with Runs owning the unique dispatch-to-Run mapping.
  Runtime implementation and human acceptance are separate from this documentation slice.
- **Mitigation for the indefinite block (accepted 2026-09-20):** a coordinator that observes
  another's claim with no observable progress — no Run steps, no heartbeat — escalates it to
  the human attention queue (epic #246) with owner, generation and last known effect. The bot
  detects and surfaces; only the owner-operation recovers (quiesce the old executor, resolve
  outstanding effects, then a new generation). Escalation adds no takeover rule and no TTL:
  the executor that answers is still the owner until a person acts.

## Alternatives considered

- **Ordinary branch push, labels or local semaphores** — rejected as shared arbitration:
  same-SHA pushes and label writes can both succeed; local locks do not span machines.
- **Remote CAS as sufficient fencing** — rejected: it does not atomically condition GitHub
  label/merge APIs or process launch on owner generation.
- **An advisory single-coordinator setting** — rejected: two installations can both enable
  it. A technically enforced single coordinator could instead hold a non-expiring remote CAS
  registration plus a local lifetime lock, with no automatic handover. That is implementable
  but does not remove the need for durable effect permits or solve uncertain external effects;
  the selected protocol permits cooperating writers without that extra global bottleneck.
- **A GitHub App with a hosted transactional coordination service** — viable later, but
  introduces a new hosted authority and operations burden. It still needs explicit
  in-flight revocation and external-effect recovery semantics; an App token alone is no lock.
- **Mandatory credential-isolated executor and owner signing keys** — not required by the
  cooperative P0 scope. They would address a stronger adversarial threat model, at the cost
  of a new deployment/identity boundary. This proposal does not claim those guarantees.
- **Automatic TTL takeover** — rejected: silence does not prove an old owner or process
  cannot still act. Escalation to the attention queue keeps a human as the only recovery
  path, which is why it does not weaken this rejection.
- **SQLite as shared authority or working-copy policy as active policy** — rejected because
  disconnected checkouts can disagree without either knowing.

## References

- [ADR-0005](0005-loop-state-lives-in-github-next-step-computed-locally.md)
- [Authority and transition contract](../control-authority-contract.md)
- [OpenSpec change](../../openspec/changes/controlled-workflow-authority/proposal.md)
- [Shipped local boundary](../controlled-workflows.md)
- [Epic #321](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/321),
  [held #317](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/317),
  [proposal #318](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/pull/318)
