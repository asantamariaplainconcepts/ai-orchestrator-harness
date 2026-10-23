# Design

Three decisions, all forced by reading what #322/#328 actually shipped.

## 1. The tick's order is: reconcile, then read, then decide, then claim, then dispatch

The contract's chain — persist local intent, CAS-claim, permit, StartRun — described the coordinator
of ACP-05's future. What exists today is narrower and simpler, and pretending otherwise would
produce a protocol inside one BackgroundService. The real order this change implements:

1. **Reconcile.** Intents in `pending`/`unknown` older than the declared window, for every folder
   known this tick, are resolved by observation: query Runs by effectId (the mapping's replay
   endpoint exists and is typed), read the registry, and settle the intent — `started` where the Run
   exists, `unknown` stays `unknown` and escalates (one comment per effectId), `pending` past the
   window without a Run escalates too. No resubmission anywhere: the whole point of an uncertain
   outcome is that only observation can settle it.
2. **Read.** The same GETs as every mode: tasks, folders, tunables, loops, intents.
3. **Decide.** `LabelLaunchPlan.For` unchanged, on `ready-for-implementation` tasks with
   `held: false`.
4. **Claim.** The same `Acquire` path from `observe-claims`, with the same typed outcomes — a
   foreign claim is a stop for this task this tick (the escalation of *that* case remains #328's
   `ForeignClaim` log, not a comment: a foreign claim blocking work is normal operation, an
   *unknown intent* is the anomaly worth surfacing).
5. **Dispatch.** `ReserveDispatchIntent` (operation `implement`), then `POST /api/runs` with the
   effectId/FolderId/Task that the Runs admission already knows how to make idempotent. The two
   calls and the launch are not one transaction; the gap between reserve and start is exactly what
   the reconcile pass exists to close.

## 2. `dispatch-claims` never dispatches for `ready-for-proposal`

ADR-0006's accepted v1 authorization is a hold release by the person, which reads as "this work may
now proceed" — not "this proposal may now be written". Automating the writer of the thing a human
approves would invert the review the whole loop exists to keep: the hold a person releases after
reading a proposal is precisely the act that should remain human. So the machine dispatches the
*next* run of approved work (`implement`), and a `ready-for-proposal` task in `dispatch-claims`
mode gets the same `Observed` log line every other mode gives it — visible, deliberate, not
started. When ACP-06 lands content-bound grants, the proposal write is its conversation.

## 3. Escalation comments key on effectId, not owner+generation

#328's claim-stalled escalation (when it later fires) keys on (owner, generation) because a claim is
a long-lived fact. An intent is a per-effect fact: recycling the key would merge two different
unknowns into one comment, and a fresh key would re-comment an unchanged unknown on every tick.
The watcher keeps a process-local set of already-escalated effectIds, and the comment itself
carries the effectId — the durable dedupe is the comment thread, read before writing, exactly as
ugly and exactly as honest as the rest of the "CAS and SQLite are not one transaction" gap.

## What this deliberately does not decide

- **No permits.** The registry CAS from #328 is the claim; the git-side permit machinery of the full
  contract stays unwired, and one effect in flight per task is enforced by the journal's existing
  unresolved-intent conflict, not by a registry check on StartRun.
- **No retry, anywhere.** A refused or failed dispatch records its state and the tick stops for that
  task. The person's remedy is the attention comment or the issue itself.
