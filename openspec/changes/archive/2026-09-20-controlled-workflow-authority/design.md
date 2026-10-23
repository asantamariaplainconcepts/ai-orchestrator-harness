## Context

This ACP-00 slice starts at `master` commit `1e15ecc`, where #320 is shipped but its OpenSpec
change is still unarchived. ADR-0005 is accepted. #317 is held; #318 at
`56b8d1ded10ec3cc84520c1a26157bb918d533ed` proposes working-copy policy and excludes claims.
Neither is a shared execution permission. We preserve both historical artifacts.

## Goals / Non-Goals

**Goals:** one reviewable authority contract usable by ACP-01/02/04/05; an explicit no-effects
boundary for unknown inputs; exact claim/capacity primitives, effect identity and handover
semantics; narrow compatibility deltas.

**Non-Goals:** scheduler, module implementation, migrations, lifecycle activation, acceptance
of this ADR, #317 Hold release, shared-state writes, remote tests or Agent spend.

## Decisions

### One normative contract, separate from historical rationale

The field contracts, transition table and deployment gates live in
[`docs/control-authority-contract.md`](../../../docs/control-authority-contract.md).
[ADR-0006](../../../docs/adr/0006-shared-control-authority-is-explicit-and-fenced.md) records the
choice and alternatives as Proposed. Specs make its safety properties testable. No diagram,
progress document or copied executable lifecycle becomes a second authority.

### Remote CAS reserves Task and capacity together

A single git ref stores an append-only commit chain; each mutation contains a unique nonce.
Explicit expected-OID receive-pack CAS, followed by content/owner read-back, wins over ordinary
push, labels and local locks. A claim and a slot in separate refs would require a distributed
transaction we do not have. Same-desired-OID no-op success cannot establish another owner.

### External effects require more than registry CAS

Cooperating executors atomically validate ownership and admit durable shared permits by CAS.
A lifetime local journal lock prevents competing consumers. No generation changes until old
permits and processes are settled. This trades availability for safety instead of claiming a
generation field can fence GitHub REST. A hosted App/service or credential-isolated executor
is a possible stronger deployment, not a mandatory platform for P0. All shared dispatch stays
disabled until admission, explicit owner authorization, held lifecycle integration and
target-host conformance are demonstrated.

### Identity, policy and grants bind the content

Full provider/host/repository/key identity distinguishes mirrors. A published policy pin and
explicit owner-authorized grants bind Task input, spec, final candidate/base and verification
evidence. Working-copy files simulate only. A local-only Workflow, PR-ready flag, released
Hold, process restart or successful shell cannot create shared permission.

### Effects belong to their modules

ACP-04 adds the World registry seam and Infrastructure/Git primitive only. ACP-05 owns the
proposed Coordination module and local journal. Runs owns an atomic dispatch-ID mapping.
Intent precedes effects; unknown outcomes retain capacity; recovery observes instead of
launching again. Cross-module table access is not a shortcut.

## Risks / Trade-offs

- A process/HTTP operation can outlive a connectivity failure -> retain its reservation
  indefinitely until observation and trusted quiescence; no timeout takeover.
- GitHub labels and admission cannot be one atomic transaction -> fresh reads before permits,
  block successors on change, state the in-flight revocation limit explicitly.
- Existing local host identity does not isolate an Agent from owner credentials -> constrain
  the guarantee to cooperative participants; do not claim malicious same-user protection.
- Single-ref contention and history growth -> bounded document, typed exhaustion refusal;
  no silent compaction or broad retry loop.
- New controlled rules differ from display/manual conventions -> scope the deltas explicitly;
  do not retrofit historical grants or change labels on import.

## Migration Plan

Deliver documents and OpenSpec deltas only. Do not archive the existing controlled execution
change or this proposal, or rewrite accepted ADR-0005. Subsequent opt-in primitive work can
use the Proposed contract without implying its acceptance. A future acceptance records the
decision explicitly and reconciles #318 without removing its Hold automatically.

Activation then requires published policy enrollment, explicit authenticated owner operations,
durable recovery and target-host conformance evidence. Rollback disables new admission,
drains outstanding effects and preserves history; it never falls back to legacy dispatch.

## Open Questions

There is no runtime activation approval. The owner must review this Proposed ADR and authorize
a disposable-provider conformance test separately. Version 1 deliberately excludes
organization-wide capacity, automatic executor failover, delegated approvers and registry
compaction; those require later decisions, not permissive defaults.
