# Design

Two decisions were only visible after reading the code, and both shape the seam.

## 1. Commenting is a new write on a read-mostly seam

`ITaskSource` documents itself as read-mostly on purpose: `ApplyLabel` is *the single write*, and it
exists because a trigger label a person has not applied does not exist at the vendor. Escalation
needs the second write — a typed attention comment on the blocked task's own issue — and grafting it
onto `ITaskSource` would rewrite that doctrine mid-change. So the seam is its own World interface:

- `IAttentionCommentWriter` in `BuildingBlocks/World`, one operation: `Write(taskUrl, AttentionComment)`,
  where `AttentionComment` is a typed record (reason code, claim owner, generation, last known
  effect, remedy line) — not a free-form string, so what escalates is a vocabulary, not prose.
- Infrastructure implements it against GitHub's issue-comment API using the same
  `ISecretResolver`-by-name rule every other vendor call in the file already follows. It resolves the
  task from the URL exactly as `LabelWatcher.Identity` does — the URL is the address; a key is not.
- The watcher never formats prose. It maps a typed escalation to the record; the writer renders.
  Comments remain a projection (the registry is the fact), matching the contract's "a comment is a
  projection, not the authority".

Failed or unknown writes are retried never, logged always — the escalation is also a claim in the
local journal once #329 lands, which is what makes a lost comment observable later.

## 2. Bootstrap is a host CLI, not an endpoint, not a watcher action

The contract forbids the registry's absence being an invitation ("absence is not permission to
initialize during a watcher tick"), so bootstrap lives where a person runs it:

- A new console entry in `src/root/` (`AiOrchestrator.Control`, one project, no web host) invoking
  the same composition `AddWorld()` already builds — no duplicate registrations, the registry
  implementation is the one `GitControlRegistry` the tests race.
- Arguments: `--remote <url> --repository <owner/name> --policy-pin <oid> --capacity <n>` plus
  `--pin-file` reading the repo's own `.harness/` pin block for the common case. It performs the
  empty-lease initial push the contract specifies, then prints the ADR-0006 deployment precondition
  (protect `harness-control-v1` against force-push and delete by GitHub rule) — the reminder lives
  where the person bootstrapping will read it, or the precondition never happens.
- A second run against an existing ref is a typed conflict naming the observed revision, not an
  error dump and not a silent no-op. Unknown and absent are different answers, as everywhere else
  in this protocol.

## 3. `observe-claims` is `observe` plus one acquisition

`LabelWatcher.MayObserve()` grows one accepted value. The tick's reads are unchanged — every request
before acquisition is still a GET, and nothing dispatches. After the plan names a candidate:

- `Acquire()` against the Folder's own registry location (remote resolved from the Folder's
  remote URL, pin from `.harness/`), `expectedRevisionOid` from the Read that opened the tick.
- `Acquired` logs the ownership (owner, generation, transition); `Conflict` logs the observed
  revision and stops for that task — never an in-process CAS retry loop, the contract forbids it.
- An acquisition is not a lease on the tick: the next tick reads fresh, sees its own claim, skips a
  second acquisition for the same identity (idempotent by TaskIdentity, same as the journal).
- Every registry write goes through the same `GitControlRegistry` API the race tests cover; the
  watcher adds no git of its own. A folder whose repository has no control ref is
  `LoopUnreadable`'s sibling: logged with the path, zero candidates suppressed — absent is not
  permission, and it is also not an error.

## 4. Escalation trigger

A coordinator escalates when it observes a claim that (a) is not its own, (b) blocks a candidate it
would otherwise acquire, and (c) shows no observable progress — no heartbeat, and no recorded effect
newer than a policy-declared window. The window is declared in the repo's `.harness/config.json`
(`escalationWindowMinutes`, default 30), because a number a person tunes belongs in the file the
person already tunes, not in a prompt and not in code. Escalation is one comment per
(owner, generation) — repeating every tick would be the notification multiplication the attention
design forbids.

## What this deliberately does not decide

- **No dispatch.** `observe-claims` never calls StartRun; that ordering belongs to #329.
- **No takeover, no TTL.** The escalation comment's remedy names the owner-operation, and nothing in
  this change executes it.
- **No grant machinery.** Hold-release is v1 authorization; ACP-06 owns grants.
