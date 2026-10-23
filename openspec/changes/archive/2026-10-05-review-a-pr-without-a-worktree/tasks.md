## 1. Reconcile the change against #447

- [x] 1.1 Confirm the scope: Review is a tab of the PBI shell #450 builds, the code-review step stands out without displacing the journey, a PR opens by direct remote reference with no folder/PBI/run synthesised, evidence follows the existing convention with honest absence, and local review is only the before-PR case.
      req: review-a-remote-pull-request/1, review-a-remote-pull-request/4
- [x] 1.2 Reconcile this change on its own branch — rewrite proposal, design, spec deltas and tasks against the #447 decisions; do not spawn a second change.
- [x] 1.3 Update the deltas for `review-a-change-in-one-tab` and `read-the-working-diff` so local review is only the «before a PR exists» case, not a parallel source once one does (criterion 6, criterion 8).
      req: review-a-change-in-one-tab/2, read-the-working-diff/1
- [x] 1.4 `openspec validate "review-a-pr-without-a-worktree" --strict` passes on the reconciled artifacts.
- [x] 1.5 Commit, push, open a draft PR with `Closes #454`, push the propose report, apply the hold, stop for a person.

## 2. Add the PR checks read to the remote seam

- [x] 2.1 Extend `IRemotePullRequestReader` with `ReadChecks` and a `RemoteChecksRead` record that combines the provider's combined status and check-runs over the head commit, scoped to the pinned comparison; append without rewriting existing members.
      req: review-a-remote-pull-request/9
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IRemotePullRequestReader.cs
- [x] 2.2 Implement the GitHub adapter: combined status + check-runs over the head, each check named with provider identity and a link; carry the head commit the checks were read for; detect a moved head and refuse a stale read.
      req: review-a-remote-pull-request/9
      files: src/shared/AiOrchestrator.Infrastructure/PbiSources/GitHubPullRequestReader.cs
- [x] 2.3 Distinguish passing, failing and in-flight checks; no-checks-declared as an honest empty (not success); an unreadable read as an access error with the provider's remedy, distinct from absence. Do not reuse `ITestInventoryStore`.
      req: review-a-remote-pull-request/9
      files: src/shared/AiOrchestrator.Infrastructure/PbiSources/GitHubPullRequestReader.cs
- [x] 2.4 Add the Pbis-owned one-file use case `GetPullRequestChecks` (`GET /api/pull-requests/{owner}/{repo}/{number}/checks?comparisonId=...`) with its permission declared; comparison-scoped so a stale comparison cannot yield current checks.
      req: review-a-remote-pull-request/9
      files: src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/PullRequests/UseCases/GetPullRequestChecks.cs
- [x] 2.5 Cover: checks aggregated over the head; no checks declared; access error distinct from absence; head change invalidates the read; pagination caps exposed as incomplete, not empty.
      req: review-a-remote-pull-request/9
      files: src/tests/AiOrchestrator.Modules.Pbis.FunctionalTests/PullRequestReadTests.cs, src/tests/AiOrchestrator.InfrastructureTests/GitHubPullRequestReaderTests.cs

## 3. Wire Review as a PBI tab and the direct external-PR entry

- [ ] 3.1 Consume `PullRequestReview` as the Review tab of the PBI shell #450 builds; do not touch `PbiWalk.tsx`, `PbisScreen.tsx` or the journey. Meet #450 at the tab the shell reserves for review.
      req: review-a-remote-pull-request/4
      files: src/frontend/features/pbis/PullRequestReview.tsx
- [ ] 3.2 The direct external-PR entry (`PullRequestEntry`) opens any PR the credential can reach by remote reference with no folder, worktree, PBI or run synthesised; reuses the same review component as the tab.
      req: review-a-remote-pull-request/1
      files: src/frontend/features/pbis/PullRequestEntry.tsx, src/frontend/app/routes.tsx
- [ ] 3.3 Before a PR exists, the Review tab states where the PBI stands in its journey and offers no local working diff as a substitute for review.
      req: review-a-remote-pull-request/4, review-a-change-in-one-tab/2
      files: src/frontend/features/pbis/PullRequestReview.tsx
- [x] 3.4 Refresh reads when the PR's head changes; identify reads by base and head; do not present a stale patch as the current version (criterion 7).
      req: review-a-remote-pull-request/2
      files: src/frontend/features/pbis/PullRequestReview.tsx, src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/PullRequests/PullRequestComparisons.cs
- [ ] 3.5 Browser coverage: open a PR with no registered Folder/Worktree; open one with no linked issue and confirm evidence-not-found; refresh a head change; observe an access failure; both themes and actual `innerWidth` 390.
      req: review-a-remote-pull-request/1, review-a-remote-pull-request/4, review-a-remote-pull-request/5, review-a-remote-pull-request/9
      files: src/tests/AiOrchestrator.ArchitectureTests/e2e/PullRequestReview.spec.ts

## 4. Evidence from the linked issue, with honest absence

- [x] 4.1 Read linked issue evidence through `evidence-from-the-issue` (matching by `evidence-<n>-*` name, provenance) with the PR as the link; multiple linked issues stay distinguishable.
      req: review-a-remote-pull-request/5
      Already shipped on master (`ac3b391d`); verified on the real product in 5.2.
- [x] 4.2 Show evidence not found when there is no linked issue, the linked issue has no matching attachments, or no convention applies; state the matching rule.
      req: review-a-remote-pull-request/5
      Already shipped on master (`ac3b391d`); verified on the real product in 5.2.
- [x] 4.3 Distinguish a permission/auth/transport failure from absence; show the provider's remedy where it gives one; do not present an access error as evidence not found.
      req: review-a-remote-pull-request/5
      Already shipped on master (`ac3b391d`); verified on the real product in 5.2.

## 5. Validate and document

- [x] 5.1 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`; the run prints every test assembly in `src/AiOrchestrator.slnx`, zero failures.
- [x] 5.2 Open a real accessible PR of a repository that is not registered and has no local evidence; observe the diff and the evidence-not-found message. Open one linked to an issue with evidence; refresh a head change; observe a real access error.
      req: review-a-remote-pull-request/1, review-a-remote-pull-request/2, review-a-remote-pull-request/5, review-a-remote-pull-request/9
- [ ] 5.3 Update architecture documentation to distinguish remote provider review from cloud deployment and from local execution; record the seam change (the checks read) and the single-source principle for reviewed code.

## Open tasks revisited against #447 (5 oct 2026)

The previous change had five open tasks; the reconcile disposes of them as follows:

- **2.6 (verify cross-fork immutable comparison and GraphQL thread/link capabilities)** — the shipped reader already does this against real PRs; folded into the implement-side verification of 5.2.
- **3.5 (browser tests: Patch/Raw reset, deletions, renames, binaries, incomplete lists)** — retained, widened in 3.5 here to cover the checks read and the evidence-not-found + access-failure cases at 390px and both themes.
- **5.6 (pending reviews, reply ownership, draft protection)** — out of this change's scope; publication is #455.
- **6.1 (browser coverage opening a PR with no registered Folder/Worktree at 390px, both themes)** — retained as part of 3.5.
- **6.5 (architecture docs distinguishing remote review from local execution and cloud deployment)** — retained as 5.3, extended to record the single-source principle.
