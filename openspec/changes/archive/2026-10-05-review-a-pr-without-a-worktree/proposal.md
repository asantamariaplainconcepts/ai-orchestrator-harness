## Why

The epic #447 centres the product on the PBI and its journey through the loop. The remote review
that #419 built and shipped is the right mechanism — read a published PR from its provider with no
checkout — but it was specified before that redesign: it spoke of `modules/Tasks` (now `modules/Pbis`
after #448, with position read via the Connector after #449), of the PR screen as a destination in
its own right, and of `publish-evidence-on-the-issue` as a forward dependency that is now the
baseline capability `evidence-from-the-issue`.

The decisions refined on 5 October 2026 resolve how this work fits #447 rather than whether to do
it. This change reconciles the existing proposal, design, deltas and tasks against those decisions on
its own branch. It closes the one real gap the inventory found — checks of the gate, which the seam
does not read — and it narrows the local-review specs so the published PR is the only source of
reviewed code once one exists, rather than one of two.

Issue #454. Epic #447. Blocked by #450, which contributes the PBI shell and its tabs; the Review
tab's content and the direct entry by an external PR are this change's, over the same
`PullRequestReview` component.

## What Changes

- Reconcile this change, on its branch, with #447: Review is a tab of the PBI that #450's shell
  exposes, the code-review step stands out without displacing the journey as the default entry, and
  the logged-in PBI's identity remains the entry by default for its PR. No second review source.
- Add reads of the PR's checks to the remote reader — combining the provider's combined status and
  check-runs over the head commit — so the reviewer sees green, red and in-flight checks; the absence
  of declared checks is an honest empty, and an unreadable check read is an error, never silence or
  a zero presented as success.
- Open any PR the credential can reach directly by its remote reference, even with no folder, worktree,
  PBI or run, and without inventing a PBI to host the review.
- Read linked issue evidence through the existing `evidence-from-the-issue` convention, with the PR
  as the link; when there is no such convention or no linked issue, show evidence not found and keep
  the code reviewable. A permission, auth or transport failure is distinct from absence.
- Before a PR exists, the Review tab explains where the PBI stands in its journey and offers no local
  diff as a substitute for review. The local-review specs become this case alone.
- Refresh reads when the PR's head changes, so stale patches are not shown as the current version;
  the read is identified by base and head and refuses a moved comparison.
- Reuse the shipped `IRemotePullRequestReader` and `IRemotePullRequestWriter` as they stand; the only
  seam change is the check read added to the reader, in its own shape, not a contract rewrite.

## Capabilities

### New Capabilities

- None. `review-a-remote-pull-request` is the capability #419 introduced; this change amends it in
  place rather than forking it.

### Modified Capabilities

- `review-a-remote-pull-request` — the PBI tab framing (not a standalone destination), the checks read,
  the direct external-PR entry without a synthesised PBI, evidence-not-found as an honest state, and
  the refresh-on-head-change contract.
- `review-a-change-in-one-tab` — local review is only the case before a PR exists, not a parallel
  source once one does (acceptance criterion 6).
- `read-the-working-diff` — the local working diff is the before-PR reading, not a second review
  source alongside the pinned remote comparison.

## Impact

- One new method on `IRemotePullRequestReader` and a small immutable checks record, in
  `BuildingBlocks/World/`, with the GitHub adapter under `Infrastructure/PbiSources/` appending its
  registration in `World.cs` in its own block at the end, never reordering lines it did not add.
- New Pbis-owned use cases for check reads and for the external-PR entry, one file each, declaring
  their permission explicitly. Routes under `/api/pull-requests/{owner}/{repo}/{number}`.
- Frontend in `src/frontend/features/pbis/` — `PullRequestReview` consumed as a tab of the PBI shell
  #450 builds, `PullRequestEntry` for the direct external-PR path. The three shared frontend files
  (`contracts.ts`, `queries.ts`, `en.ts`) gain this screen's block, appended next to their own, not
  tidying a neighbour's. No file #450 owns (`PbiWalk.tsx`, `PbisScreen.tsx`, journey) is touched.
- No migrations. No new schema. Accepted ADRs remain immutable.
- Initial provider is GitHub. Azure DevOps, Jira, Linear, cloud deployment, multi-user identity,
  editing code, running agents, publishing comments or verdicts, and merging are excluded — merging
  is #456, publishing is #455.

## Migration Plan

1. Land #450 (the PBI shell and its tabs) — the Review tab is content of that shell.
2. Add the checks read to the seam and adapter, behind the Pbis-owned use case.
3. Wire `PullRequestReview` as the Review tab of the PBI, and `PullRequestEntry` for the
   direct external-PR address, consuming the same review component.
4. Narrow the local-review specs to the before-PR case and run the gate.

## Open Questions

- This change writes the spec, not the code; the `aio:state` of #454 carries the change name and the
  PR. A person reviews before implementation begins under the proposal hold.
