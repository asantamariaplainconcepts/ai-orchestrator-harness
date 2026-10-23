## Context

The review publication that #419 shipped is real code on master (`ac3b391d`): two write use cases in
the Pbis module, a writer seam and a GitHub adapter in `BuildingBlocks/World/` and
`Infrastructure/PbiSources/`, and a frontend publisher under `src/frontend/features/pbis/`. The
contract is frozen in its intent: `IRemotePullRequestWriter` exposes `SubmitReview` (a review bound
to a commit) and `Reply` (a reply under an existing thread) and nothing else — no merge, no label,
no hold, no issue lifecycle change. `RemoteReviewEvent` carries `Comment`, `Approve` and
`RequestChanges` as the verdict; `RemoteWriteOutcome` carries `Confirmed`, `Refused` and `Uncertain`.

What changed is the world around that code. Epic #447 centres the product on the PBI and its journey,
and #454 reconciled the read side of `review-a-remote-pull-request` with that epic: Review is a tab of
the PBI shell #450 builds, the same `PullRequestReview` component serves a PR opened by direct remote
reference, and the journey stays the PBI's default entry. In doing that reconciliation #454 rewrote the
`review-a-pr-without-a-worktree` change in place and, because publication was out of its scope,
dropped the three publication requirements the master change carried — native publication bound to the
viewed revision, provider threads kept native and revision-aware, and write races / uncertain
outcomes. Those requirements are not in `openspec/specs/` (the capability is not yet at baseline) and
not in #454's delta. The code that implements them is on master; their spec is not.

This change is the spec for that code. The host stays loopback-only, implicitly authenticated; the
credential is the server's; initial and only provider is GitHub.

## Goals / Non-Goals

**Goals:**

- Give the shipped publication code its spec home as a delta of the `review-a-remote-pull-request`
  capability, refined to the #447 framing — publication happens from the Review tab of the PBI and
  from the direct external-PR entry, both composed from the shared `PullRequestReview` component — not
  the standalone screen the original #419 spec described.
- Restore the three requirements #454 excluded: native review publication bound to the viewed
  revision; provider review threads kept native and revision-aware; review write races and uncertain
  outcomes explicit.
- State the boundary with #456 in the requirement: publication SHALL NOT merge, bypass policies,
  remove a Hold or change issue lifecycle status.
- Name the live verification the issue's check requires — publish a comment, a reply and a verdict on
  an authorized test PR and observe author and content in the vendor — as the implement-side work.

**Non-Goals:**

- No new write path, no new seam method, no new verdict value, no new writer client. The
  `IRemotePullRequestWriter` contract, `RemoteReviewSubmission`, `RemoteReviewEvent`, `RemoteWrite`
  and `RemoteWriteOutcome` are unchanged.
- No change to the GitHub adapter's HTTP shape, its `commit_id` / `event` / `comments` body, the
  reply endpoint, or the `github-review-writes` client's no-retry policy.
- No change to `SubmitPullRequestReview` or `ReplyToPullRequestThread`, their preflight
  (comparison check, diff-hunk validation, pending-review detection, head/base re-read), their
  `[Requires(Access.FiltersToCaller)]` permission or their `Confirmed` / `NotConfirmed` result shape.
- No resolve-thread, no evidence publication, no merge. Reading the review, the checks and the
  evidence are #454's; merge is #456.
- No cloud, no public listener, no multi-user identity.

## Decisions

### Amend the live capability rather than fork a new one

`review-a-remote-pull-request` is the capability #419 introduced; it is still a live change
(`review-a-pr-without-a-worktree`) and not yet at baseline. #454's rewrite is a MODIFIED delta of that
same capability for its read side. This change adds an ADDED delta of that same capability for its
publication side, so the two land against one capability rather than splitting review publication
into a second capability that the code does not justify. The alternative — a new
`publish-review-comments-and-verdicts` capability — would imply a seam and a contract this change
does not add, and would orphan the requirement that publication and reads share one pinned
comparison. The capability name stays one.

### Restore the three dropped requirements, refined to the tab framing

The master change's three publication requirements (lines 121–193 of its spec delta) covered the
contract exactly: explicit confirmation before publish, inline comments bound to the viewed
comparison's file/side/line, a stale head refusing publication and retaining the draft, provider
permission refusals explicit, a confirmed review's identity shown without touching merge or holds,
draft edits during a pending send retained and not resent, a pending review detected and not
silently duplicated, replies bound to the native thread with their original revision context, an
intervening head change after confirmation shown as outdated, and a lost response reported as
uncertain without automatic resend. Those map one-to-one onto the seven acceptance criteria of #455.
The refinement is wording only: "the review screen" becomes "the Review tab of the PBI and the
direct external-PR entry, both composed from the shared `PullRequestReview` component", because that
is where #447 put publication and where #454 put the reads it sits beside.

### Keep the boundary with #456 in the requirement, not just the contract

The contract already enforces "two writes and no others" — `IRemotePullRequestWriter` cannot merge,
label or so on. Keeping the `Publication SHALL NOT merge a PR, bypass policies, remove a Hold or
change issue lifecycle status` clause in the spec means a future change that wants to combine verdict
and merge reads the boundary at the spec, not only at the C# interface, and sees it is deliberate.

### The implement step is reconciliation and live verification, not a build

Because the code is on master, the tasks are: re-read the proposal delta against any drift the #447
framing introduced in the `prReview.*` copy or the publisher UI (append-only if found), then the live
check the issue requires — publish a comment, reply and an allowed verdict on an authorized test PR,
verify author and content in the vendor, observe a permission refusal and a head change that retains
the draft — and the full `aspire do ci` gate. If reconciliation finds no drift, the implement diff is
the empty set, and the change closes the spec gap rather than shipping code; that is an honest outcome
this design names explicitly.

## Risks / Trade-offs

- [Two open changes against one capability] → #454 and #455 both delta `review-a-remote-pull-request`.
  #454 is MODIFIED, this is ADDED; they touch disjoint requirement sets (reads vs. writes), so they do
  not conflict at the spec level. The sequencing is enforced by #455's dependency on #454; #454 folds
  the capability to baseline first, and this change's requirements ride in the same archive.
- [Code ahead of spec] → the publication code has run only against stubbed HTTP responses
  (`GitHubPullRequestWriterTests`, `PullRequestReadTests`). The spec is written for the shipped
  contract, not for an ideal one; the live verification the implement step names is what catches a gap
  between the spec's intent and the code's behaviour on a real PR.
- [Resolve-thread drift] → the issue's "Qué no pide" section rules resolve-thread out, and the writer
  has no such method. The spec says replies target the native thread and does not promise resolve; a
  future change that adds resolve edits the requirement then.
- [Wording drift between standalone screen and tab] → master's frontend has `PullRequestReviewScreen`
  as a route and the publisher under `PullRequestReviewStep`. If the #447 framing needs copy changes
  (e.g. "screen" → "tab"), they are append-only to the `prReview.*` block, and the spec carries the tab
  wording so a future reader sees where publication belongs.
