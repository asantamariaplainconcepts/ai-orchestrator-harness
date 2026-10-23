## Context

The remote PR review that #419 shipped is real and on master (`ac3b391d`): `IRemotePullRequestReader`
and `IRemotePullRequestWriter` in `BuildingBlocks/World/`, registered in `World.cs`; the GitHub
adapter in `Infrastructure/PbiSources/`; eleven Pbis-owned use cases; and a frontend under
`src/frontend/features/pbis/` (`PullRequestReview`, `PullRequestCode`, `PullRequestEvidence`,
`PullRequestEntry`, `PullRequestReviewStep`, `PullRequestParts`, `pullRequest`). The
`evidence-from-the-issue` capability it once depended on as future is baseline. What this change does
is reconcile the spec for that shipped code with the epic #447, close the one gap the inventory found,
and narrow the local-review specs so the published PR is the single source of reviewed code once one
exists.

The redesign #447 drives is concrete. After #448 `modules/Tasks` is `modules/Pbis`; after #449 the
loop's position is read via the Connector. The PBI is the centre, its journey is the default entry,
and #450 contributes the PBI shell and its tabs. Review is a tab of that shell, not a destination of
its own; the code-review step stands out without displacing the journey. A PR is also openable
directly by its remote reference with no folder, worktree, PBI or run, because a person reviewing
somebody else's PR owes the loop nothing.

The host remains loopback-only, implicitly authenticated. Reading a provider PR without a checkout is
not a licence to expose that identity on a public listener — that decision is out of scope and stays
out.

## Goals / Non-Goals

**Goals:**

- Review a published GitHub PR from its provider with no folder, worktree, local clone or run, whether
  it belongs to a logged-in PBI or is opened by direct remote reference.
- Read the PR's checks — combined status and check-runs over the head — distinguishing passing, failing
  and in-flight, the honest absence of declared checks, and an unreadable read.
- Read linked issue evidence through the existing convention, with evidence-not-found when no
  convention or linked issue applies; distinguish access errors from absence.
- Sit Review as a tab of the PBI shell #450 builds, sharing the `PullRequestReview` component with the
  direct external-PR entry.
- Narrow local review to the only case it now has: before a PR exists.

**Non-Goals:**

- Cloud hosting, public-listener authentication, multi-user roles.
- Running agents or checks remotely, editing code, cloning for review, or merging (that is #456).
- Publishing comments or a review verdict (that is #455).
- Other PR providers, or a second review database.
- Replacing the shipped reader/writer contracts; only the checks read is new.

## Decisions

### Review is a PBI tab, not a destination

The PBI is the entry the logged-in loop uses. #450's shell draws the PBI's tabs; the Review tab is
this change's content, and it hosts `PullRequestReview` — the component already on master — rather
than a parallel screen. The component is owned here, in `src/frontend/features/pbis/`; #450 owns
`PbiWalk.tsx`, `PbisScreen.tsx` and the journey, and this change does not touch them. The two
changes meet at the tab the shell reserves for review; neither edits the other's files.

`PullRequestEntry` is the direct path: a person pastes a PR reference and the review opens with no
folder, worktree, PBI or run. It does not synthesise a PBI to host the review, because the review of
somebody else's PR is not a loop item — there is nothing for the loop to advance. The same
`PullRequestReview` component serves both: the tab knows its PBI's PR; the entry resolves a pasted
address. Either way the read is by provider identity, never by a local directory.

The default entry for a logged-in PBI is its journey, not review. The code-review step stands out — it
is where a reviewer heads — but it does not displace the journey as the resting place. That is #447's
decision and it is reflected here, not redecided.

### Address the PR by provider identity, not a local directory

Unchanged from the shipped design and affirmed by the inventory: the read is by GitHub host,
repository and PR number, against an authorized Connector, validated before any credentialed request.
`IRemotePullRequestReader` is not `IPullRequestLookup` — the latter asks the GitHub CLI inside a
checkout and is the local source; the former has no directory at all. Fork heads resolve to their
actual head repository. A pasted address is parsed and scope-checked before the credential is spent.
No synthetic folder or worktree row is created.

### Pin a coherent code comparison and refresh on head change

The comparison is the PR's three-dot change: base tip, head commit, merge base, and the base/head
repository identities, all at full commit IDs. The left full-content side is the merge base, not the
current target-branch tip — a left-side line comment must not appear beside content that was never in
the patch. File-list caps, missing patches and content limits stay visible, never an empty diff.

Reads are identified by base and head and SHALL refresh when the PR's head changes, so a stale patch
is not presented as the current version (criterion 7). Pagination and current-PR reads SHALL not
combine different comparisons; a revision change during a read refuses the incoherent result without
silent retries. A comparison cache key includes the comparison, path, side and caller/Connector
identity, invalidated on configuration changes; an expired entry is an explicit reread, not a
reinterpretation as current HEAD.

### Read the PR's checks from the provider (the one gap)

The inventory found one real gap against criterion 1 («checks and estado del PR»): the seam reads the
PR's `State`, comparison, threads and linked issues, but not the status of the gate's checks. The
refine chose option (a): extend `IRemotePullRequestReader` with a check read that combines the
provider's combined status and check-runs over the head commit.

`ReadChecks` returns a value that distinguishes:

- **passing, failing and in-flight checks** — aggregated from the combined status and the individual
  check-runs on the head commit, each named with its provider identity and a link;
- **no checks declared** — the head has no check-runs and no combined status. This is an honest empty,
  not a failure, and not a green presented as success. The screen says no checks were declared;
- **unreadable** — the provider refused the read or the transport failed. This is an access error,
  distinct from absence, with the provider's remedy where it gives one.

It does **not** reuse `ITestInventoryStore`. That seam reads the inventory of tests this product
itself publishes (the `evidence-<n>-*` records); the vendor's CI checks are a different thing, read
from the provider over the head commit, and conflating them would make a missing local inventory look
like missing CI.

The result is pinned to the head commit it read, like the comparison. A head change invalidates it;
checks from an older head are not shown as the current state.

### Evidence follows the existing convention, and absence is honest

Linked issue evidence is read through the `evidence-from-the-issue` capability — matching by the
`evidence-<n>-*` name against the linked issue's attachments, with the row's provenance — and the PR
is the link to that issue. When the provider links more than one issue, they stay distinguishable and
the reader does not guess from a bare number.

When there is no linked issue, or the linked issue has no evidence under the convention, the Review
tab shows **evidence not found** and keeps the code reviewable (criterion 4). Absence is not failure:
a missing convention, a missing issue, or an issue with no matching attachments is an honest empty,
stated with the rule it matched by. A permission, auth or transport failure is an error and is shown
as one, distinct from absence (criterion 5) — the seam's existing `NotFound` / `Unreachable` /
`OutOfScope` distinction already enforces this, and the checks read follows the same shape.

### Before a PR exists, the tab says where the PBI stands

Criterion 6: before a PR exists, the Review tab explains where the PBI is in its journey and does not
offer a local working diff as a substitute for review. This is the only case the local-review specs
now cover — the working diff is the before-PR reading, not a parallel source alongside the pinned
remote comparison. Once a PR exists, the published PR is the single source of reviewed code.

### Keep review publication in GitHub

Unchanged: read provider threads with their resolved/outdated state; a draft is unsent and marked so;
submission publishes a native PR review bound to the viewed head; a changed comparison refuses
submission and retains the draft; replies target the native thread. Publication does not merge, bypass
policies, remove a hold or change issue status. (Publication itself is #455's scope; this change keeps
the contract and the spec, it does not implement the write.)

### Concrete module boundary and API shape

Existing routes are on master and unchanged:

| Operation | Shape |
| --- | --- |
| Read comparison | `GET /api/pull-requests/{owner}/{repo}/{number}` |
| Read file | `GET .../files?comparisonId=...&path=...&side=left|right` |
| Read context | `GET .../context?comparisonId=...` |
| Read threads | `GET .../threads?comparisonId=...` |
| Submit review | `POST .../reviews` |
| Reply | `POST .../threads/{threadId}/replies` |

This change adds:

| Operation | Shape | Outcome |
| --- | --- | --- |
| Read checks | `GET .../checks?comparisonId=...` | Checks aggregated over the head, or no-checks-declared, or an access error |

The check read is comparison-scoped (the `comparisonId` carries the head), so a stale comparison
cannot yield current checks. The use case declares its permission explicitly; an undeclared request is
refused. No database table or durable review state is introduced.

### File ownership and the collision rules

| Surface | Owner | Notes |
| --- | --- | --- |
| `BuildingBlocks/World/IRemotePullRequestReader.cs` | this change | adds `ReadChecks` and a `RemoteChecksRead` record, appended |
| `Infrastructure/PbiSources/GitHubPullRequestReader.cs` | this change | adapter implements the check read; registration appended in `World.cs` in its own block |
| `modules/Pbis/.../Features/PullRequests/UseCases/GetPullRequestChecks.cs` | this change | one-file use case, permission declared |
| `modules/Pbis/.../Features/PullRequests/UseCases/OpenExternalPullRequest.cs` | this change | resolves a pasted address; no PBI synthesised |
| `src/frontend/features/pbis/PullRequestReview.tsx` + siblings | this change | consumed as the Review tab and via `PullRequestEntry` |
| `src/frontend/shared/http/contracts.ts`, `queries.ts`, `shared/i18n/en.ts` | this change | this screen's block appended; no neighbour tidied |
| `src/frontend/features/pbis/PbiWalk.tsx`, `PbisScreen.tsx`, journey | #450 | not touched by this change |

The seam contract change (one method on the reader, one record) is additive; the existing methods and
their shipped behaviour are not rewritten.

## Risks / Trade-offs

- Provider check-run pagination and status aggregation differ from comparison reads; expose
  incompleteness and keep the provider link, do not label a partial read as a clean result.
- The checks read and the comparison read share the head but not the endpoint; a head change between
  them must be detected and reported, not silently reconciled.
- The direct external-PR entry bypasses the loop's PBI identity; it must not leave a half-resolved PBI
  behind, and it must not gain write paths the logged-in entry has — the writer is the same contract,
  used the same way.
- Local review and the tab shell are owned by other changes (#450); the meet-point is the tab, and a
  rebase there is expected and bounded.
- The three shared frontend files are the classic collision surface; this change appends its block and
  touches no other.

## Migration Plan

1. Land #450 — the PBI shell and its tabs — so the Review tab has a host.
2. Add `ReadChecks` to the seam and the GitHub adapter, behind the Pbis-owned use case and route.
3. Wire `PullRequestReview` as the Review tab and `PullRequestEntry` for the direct external-PR path.
4. Narrow local review to the before-PR case in the specs and confirm the working-diff route is not
   offered as review once a PR exists.
5. Run the gate and verify reads against a real accessible PR with and without a linked issue, refresh
   on a head change, and observe an access failure.

## Open Questions

- A person reviews these artifacts before implementation begins under the proposal hold. The change
  writes the spec; it does not implement the code or mutate shared provider state.
- GitHub is the initial and current provider. Other providers are a separate scope decision.
