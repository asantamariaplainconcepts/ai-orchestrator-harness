# Design

Two decisions that reading the ledger forced, one boundary the contract already drew.

## 1. A grant is a receipt-shaped fact, not a claim-shaped one

The document today counts two collections: claims (capacity-occupying, task-bound) and receipts
(the bounded, replayable ledger). A grant is the second kind wearing the first's clothes:
task-bound for lookup, but **occupying no capacity and claiming no exclusivity** — two grants for
two different SHAs of the same task may coexist (one stale, one live) and the ledger's job is to
answer "is _this_ candidate approved", not "who owns the task". So:

- `ControlRegistryDocument` grows `Grants: ImmutableArray<RegistryGrant>`; `ValidDocument`
  replays them with the same discipline as receipts: unique grantId, unique consumed-transition
  binding, epoch monotonic per document, expiry sane.
- A `RegistryGrant` record: `GrantId`, `Task`, `ActorId`, `Action` (v1: `approve-candidate`),
  `CandidateOid`, `TreeOid`, `BaseOid`, `SpecPin`, `Epoch`, `ExpiresAt`, `RevokedAt?`,
  `ConsumedBy?` (transitionId). Expiry is carried in the document, interpreted against a `now`
  the caller supplies at read time — the ledger stores when it was written, the admission decides
  whether it still stands, because a document cannot know the future.
- The mutation is `RegistryOperationKind.Grant`/`RevokeGrant`: validated on the document
  (no overwrite of an identical grantId — re-submitting the same grant with the same binding is
  the operation replayed, exactly like claim replay; anything else conflicts), CAS-pushed the
  same way every other mutation is. Bootstrap ordering rule: a grant requires a bootstrapped
  registry, and refuses on empty.

Why a grants **read** (`Grants(taskIdentity)`) rather than exposing the documents: the admission
calls a domain question ("what approves this task, live right now"), not a data dump; the
read-back, `now`, and validity rules live in one place instead of every consumer.

## 2. The CLI owner-operation is the only writer, and it prints the same fence bootstrap prints

`approve-candidate` runs from `AiOrchestrator.Control` — the same host composition, the same
`AddWorld()`, the same registry seam. Arguments: task identity (provider/host/repository/key),
candidate SHA, base SHA, tree, spec pin; expiry defaults to a declared window
(`grantExpiryDays` in the repo's tunables, bounded), overridable with days. The output names the
operación, the binding, and the precondition the contract gives it: a grant does not remove the
hold, and hold-release does not create one — printed where the person approving will read it, the
same trick bootstrap's ref-protection line plays. `revoke-grant` is analogous and append-only.

The v1 owner is the implicit local identity (ADR-0006's accepted limitation: cooperative
same-user scope). The command does not promise it verified a human; it promises who ran it, what
they bound, and that the ledger recorded exactly that.

## 3. Hold reasons: vocabulary at the seam, projection through the attention writer

`SetTaskHold` gains `Reason?` from a new closed enum in `BuildingBlocks/World`
(`AttentionReason` — held-for-review, blocked-needs-input, failed-needs-person, and the states
the queue in #246 will name). The reason writes as a typed attention comment through
`IAttentionCommentWriter` (a projection, the registry-adjacent comment thread carrying it), and
the mirror reads it back where it already reads labels — because two observers must see the same
reason, and reasons-in-SQLite would drift (ADR-0005's whole argument). An unrecognized reason
renders raw with the hold intact: an unknown "why" is information, never permission to proceed.

## What this deliberately does not decide

- **No gate wiring yet.** `Grants()` exists for ACP-07/ACP-10 to consume; this change adds no
  merge or admission call sites beyond the read itself — the ship of the consumer is the
  follow-up issue, by design.
- **No delegation, no approve_proposal.** Owner-only, candidate-only, per the accepted deltas.
