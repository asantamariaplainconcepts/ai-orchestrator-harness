# Why

The watcher observes and names candidates (#322's `dispatch-observation`, hardened by #328's identity fix),
and `GitControlRegistry` implements the whole ADR-0006 registry contract against
`refs/heads/harness-control-v1` with race tests over bare remotes. Nothing connects them: the watcher never
claims, no repository we run against has the control ref, and a claim that blocks is invisible to the
person whose work is stuck behind it. ADR-0006 was accepted with an escalation term — a stalled
coordinator surfaces, a person recovers — and both halves of that sentence are unimplemented.

# What Changes

- **Bootstrap as an owner-operation CLI.** A host command initializes the control ref with an explicit
  remote, policy pin and capacity limit, using the empty-lease initial push the contract specifies.
  Re-running it against an existing ref with the same pin returns a typed conflict; absence of the
  ref is never an invitation for a watcher tick to create it.
- **An escalation seam for attention.** A World-seam comment writer publishes a typed attention
  message on the blocked task's own issue: reason, owner, generation, last known effect and a remedy.
  This is the comment/diagnostic write path ACP-01 named and never got; it writes through the
  provider Connector, never `gh` from a module.
- **The watcher exercises the registry in a claim-only mode.** A new `LabelWatcher:Mode` value
  `observe-claims` behaves exactly like `observe` — the same reads, the same logged candidates, zero
  dispatch, every request a GET — plus one `Acquire()` per eligible candidate against the Folder's
  registry, logged with its outcome. The protocol is rehearsed end to end without spending an Agent:
  dispatch with a claim is #329, not this change.

# Capabilities

### Modified Capabilities

- `shared-control-registry`: bootstrap becomes a reachable operation, and escalation gives a blocked
  claim a visible surface.
- `dispatch-observation`: the observation mode grows a sibling that exercises acquisition without
  dispatching.

# Impact

- **Code:** a new host CLI command, a comment-writer seam in BuildingBlocks/World registered in
  `AddWorld()`, and `LabelWatcher` acquiring in the new mode.
- **Specs:** deltas for the two named capabilities.
- **No migration** beyond any the Coordination or Tasks modules already own for their own state; the
  registry itself lives in git, not SQLite.
- **Ref protection is a deployment note, not code:** ADR-0006's accepted precondition (deny force-push
  and delete on `harness-control-v1` by GitHub rule) is documented in the command's output where the
  person bootstrapping will read it.
