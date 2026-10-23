## Why

The review publication that #419 shipped — inline comments, thread replies and an approve / request
changes verdict bound to the viewed head — is real code on master, but its spec has no home after
#454. The change `review-a-pr-without-a-worktree` rewrote that change on its own branch to reconcile
the read side with epic #447 (the PBI's Review tab, the direct external-PR entry, the checks read)
and explicitly excluded publication: its Non-Goals read "Publishing comments or a review verdict (that
is #455)", and its spec delta dropped the three publication requirements the master change carried.
Those requirements are neither in `openspec/specs/` (the capability is still a live change, not yet
archived to baseline) nor in #454's rewritten delta. This change restores them as the spec for the code
that is already running, refined to where #447 placed publication: the Review tab of the PBI and the
shared `PullRequestReview` component, not a standalone destination.

Issue #455. Epic #447. Blocked by #454, whose merge folds `review-a-pr-without-a-worktree` (the read
side, plus the now-rewritten capability delta) into baseline; this change's delta attaches its
publication requirements to the same `review-a-remote-pull-request` capability. The publication code,
the writer seam and the GitHub adapter do not change.

## What Changes

- Amend the `review-a-remote-pull-request` capability with the publication requirements #454's
  rewrite dropped: native provider review bound to the viewed revision; provider review threads kept
  native and revision-aware; review write races and uncertain outcomes explicit. The wording is
  refined to the #447 framing — publication happens from the Review tab of the PBI and from the direct
  external-PR entry, both composed from the shared `PullRequestReview` component — rather than the
  standalone screen the original #419 spec described.
- State explicitly, in the publication requirement, what publication SHALL NOT do: merge a PR, bypass
  policies, remove a Hold or change issue lifecycle status. That guardrail already lives in the
  `IRemotePullRequestWriter` contract and is the boundary with #456 (the merge change).
- Carry no new capability: the writer, the `SubmitReview` / `Reply` use cases, the frontend publisher
  and the `RemoteReviewEvent` verdict (`Comment`, `Approve`, `RequestChanges`) already exist on
  master. The implementation work this change names is reconciliation and live verification, not a
  new write path.

## Capabilities

### New Capabilities

- None. `review-a-remote-pull-request` is the capability #419 introduced; #454 reconciles its read
  side with #447, and this change amends its publication side in place rather than forking it.

### Modified Capabilities

- `review-a-remote-pull-request` — the publication requirements #454 excluded (native review
  publication bound to the viewed revision; provider threads kept native and revision-aware; write
  races and uncertain outcomes explicit), refined to the Review tab / shared `PullRequestReview`
  component framing of #447. The capability is not yet at baseline; its delta lives in the live
  change `review-a-pr-without-a-worktree`, and this change adds these requirements to that delta so
  they survive the merge of #454 alongside the read requirements it carries.

## Impact

- Spec-only delta against `review-a-remote-pull-request`: one `specs/review-a-remote-pull-request/
  spec.md` with the three publication requirements and their scenarios, refined to the PBI Review tab.
- No change to the writer seam (`IRemotePullRequestWriter`, `RemoteReviewSubmission`,
  `RemoteReviewEvent`, `RemoteWrite`, `RemoteWriteOutcome`), the GitHub adapter
  (`GitHubPullRequestWriter`), the Pbis-owned use cases (`SubmitPullRequestReview`,
  `ReplyToPullRequestThread`) or their `[Requires(Access.FiltersToCaller)]` permission. The
  `github-review-writes` HTTP client stays without retry.
- No change to `World.cs`, no migrations, no new schema, no new public CQS request. Accepted ADRs
  remain immutable.
- The three shared frontend files (`contracts.ts`, `queries.ts`, `en.ts`) are not touched by the
  spec; the `prReview.*` i18n block, the `PullRequestReviewStep` publisher and the `ThreadCard`
  reply are on master already. If the implement step finds a wording gap against the tab framing, it
  appends next to its own screen's block, never a neighbour's.
- Excluded: reading the review (#454), checks (#454), resolve-thread (outside the writer contract),
  publishing evidence on the issue (the `publish-issue-evidence` capability), merge (#456), and the
  direct external-PR entry's reads (those are #454's; this change only publishes through the same
  component). Initial and only provider is GitHub.

## Migration Plan

1. Land #454 — it folds `review-a-pr-without-a-worktree` (the reconciled read side) into baseline.
2. This change's spec delta attaches the publication requirements to the same capability, so they
   land with it whether #454 merges first or the two merge in sequence.
3. The implement step reconciles any wording drift in the `prReview.*` copy and the publisher UI to
   the Review tab framing, then runs live verification against an authorized test PR.

## Open Questions

- This change writes the spec, not the code; the `aio:state` of #455 carries the change name and the
  PR. A person reviews before implementation begins under the proposal hold. Whether publication stays
  a delta of `review-a-remote-pull-request` or is split into its own baseline capability when #454
  archives is a decision for the archive step, not this proposal.
