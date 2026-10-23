# Why

The durable pieces exist — the Coordination journal (`ReserveDispatchIntent`, `RecordDispatchOutcome`),
Runs' effectId→Run mapping with typed replay, `observe-claims` acquiring through the registry — and
none of them are connected. The watcher's last word on a `ready-for-implementation` task is still a
log line; nobody reserves an intent, nobody reconciles a start whose response was lost, and a task
that failed to launch stays failed until a person notices. ADR-0006's accepted v1 authorization (a
fresh hold release by the repository owner) has no automatic path consuming it. This change connects
the chain for one transition: the one that follows the hold the person already releases by hand.

# What Changes

- **A dispatch mode for the watcher:** `LabelWatcher:Mode=dispatch-claims` gates on
  `ready-for-implementation` tasks (not `ready-for-proposal` — the proposal a person has to read is
  created by hand, per ADR-0006's v1 authorization terms), requires the hold to be absent, acquires
  the registry claim, reserves the journal intent, and calls the existing `POST /api/runs` dispatch
  shape with effectId/FolderId/Task. The Agent runs `/aio:implement`.
- **Reconcile-on-tick:** before deciding anything new, a tick resolves journal intents left in
  `pending` or `unknown` older than a declared window — by querying Runs by effectId and reading the
  registry, never by resubmitting. What cannot be established stays blocked with its typed reason
  and is not retried within the tick.
- **Escalation reuses the #328 seam:** an intent `unknown` past the window escalates as one typed
  attention comment on its own task's issue — same `IAttentionCommentWriter`, one comment per
  effectId, never a takeover and never a resubmit.
- **Failure publication through the journal:** refused or failed journeys record their typed state
  first, and the existing `LoopUnreadable`/`Suppressed` vocabulary grows the sibling a dispatch
  failure needs. Failed Hellos do not relaunch: a failure is a failure (zero Agent retry, repo rule).

# Capabilities

### Modified Capabilities

- `dispatch-observation`: the mode vocabulary and tick behaviour — dispatch behind claims, reconcile
  before deciding, escalate the unknown.
- `durable-controlled-dispatch`: the watcher becomes the journal's first writer binding intent to
  the existing Runs admission, and the reconcile path is specified against it.
- `shared-control-registry`: nothing new — claims arrive through the same `Acquire` #328 wired, and
  this change only names what a dispatch-claims tick does with each outcome.

# Impact

- **Code:** `LabelWatcher` (mode, reconcile pass, escalation trigger), `HarnessApi` (POST to the
  journal and runs endpoints), Coordination (ListDispatchIntents gains the stale-pending/unknown
  filter), `IDeclaredConfigFile` (escalation window already standard from #328's shape; no new
  tunables beyond it).
- **Specs:** deltas for the three named capabilities.
- **One migration-free path:** every table involved already exists; this change writes through the
  use cases that own them.
- **Not in scope:** `ready-for-proposal` automation (ACP-06 territory, after grants), gates over the
  candidate (ACP-08), evidence publication (ACP-09), merge (ACP-10).
