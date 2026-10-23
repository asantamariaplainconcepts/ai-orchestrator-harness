## ADDED Requirements

### Requirement: A GitHub PR is reviewable without a local Worktree

The system SHALL open a GitHub PR review from its PBI's published PR or from a validated provider
address, using an authorized Connector. Reading SHALL NOT require a registered Folder, Worktree, local
git checkout or Run, and SHALL NOT create those records or clone a repository.

A PR SHALL be openable by its remote reference even when no Folder, Worktree, PBI or Run exists for it,
and the system SHALL NOT synthesise a PBI to host a review opened this way. The logged-in PBI's PR
remains the default entry for that PBI; the direct entry is for any PR the credential can reach.

The identity SHALL distinguish provider host, repository and PR from an issue with the same number.
Unsupported hosts/providers and inaccessible repositories SHALL produce explicit refusals rather than
accepting an arbitrary credentialed target.

#### Scenario: The reviewer has no local Folder
- **WHEN** an authorized reviewer opens a GitHub PR with no local Folder or Worktree registered
- **THEN** provider metadata, available code and available checks are readable without creating local execution records

#### Scenario: A PR is opened with no Folder, Worktree, PBI or Run
- **WHEN** a reviewer opens a PR by its remote reference and there is no registered Folder, Worktree, PBI or Run for it
- **THEN** the review opens against the pinned remote comparison and no PBI is created to host it, no Folder is registered, no worktree is claimed and no Run is authored

#### Scenario: The logged-in PBI's PR is the default entry
- **WHEN** a PBI with a published PR is opened
- **THEN** its Review tab opens the pinned comparison of that PR, and the journey remains the PBI's default resting entry

#### Scenario: An issue number is presented as a PR
- **WHEN** the selected number identifies a GitHub issue rather than a PR
- **THEN** the PR entry refuses that resource instead of treating issue content as a code comparison

#### Scenario: Two repositories share a PR number
- **WHEN** two repositories each contain a PR with the same number
- **THEN** their code, Connector scope checks and cache identities remain separate

#### Scenario: No matching Connector exists
- **WHEN** the supplied provider/repository has no authorized Connector
- **THEN** the read refuses with a remedy and performs no arbitrary credentialed fetch

#### Scenario: A pasted address is checked before any credentialed read
- **WHEN** a person pastes a PR address into the review entry
- **THEN** the system parses it without calling the provider, refuses another host, a non-PR resource such as `/issues/<n>`, or a repository outside the configured Connector's scope, and only then opens the review of that PR

### Requirement: Remote code is read against one pinned PR comparison

The review SHALL identify the base tip, head commit, merge base and base/head repositories with
immutable commit IDs. Files and patches SHALL represent the PR's three-dot comparison. The left
full-content side SHALL be the merge base, not the current target-branch tip; the right side SHALL be
the actual head commit and repository.

Reads SHALL be identified by their base and head and SHALL refresh when the PR's head changes, so a
patch from an older head is not presented as the current version. Pagination and current-PR operations
SHALL not combine different comparisons. A revision change during a current-PR read SHALL refuse the
incoherent result without automatic retries. Cache keys SHALL include the comparison, path and side.

#### Scenario: The head changes while the list is read
- **WHEN** the provider moves the head before all current-PR file pages have been read
- **THEN** the reader reports a changed comparison instead of combining old and new pages

#### Scenario: A fork uses the same branch name
- **WHEN** the PR head belongs to a fork with a branch name also present in the base repository
- **THEN** head contents are read from the fork's actual head commit, not from the base branch

#### Scenario: Two revisions are read
- **WHEN** a PR is refreshed from one comparison to another
- **THEN** code cached for the old comparison is not presented as the new code

#### Scenario: The target branch has advanced independently
- **WHEN** the target tip contains changes absent from the merge base
- **THEN** left Raw contents and left-side hunk locations use the merge base rather than including those target-only changes

### Requirement: Remote review exposes only facts available remotely and sits as a PBI tab

The PR review SHALL be the Review tab of the PBI shell, composed from the same component the direct
external-PR entry uses, and SHALL NOT be a standalone destination that displaces the journey as the
default entry. The code-review step SHALL stand out without removing the journey from its resting
place.

The Review tab SHALL offer Task context, remotely discoverable Proposal content, Code, Checks,
Evidence and Review threads without inventing local execution facts. Missing Proposal content or
linked issues SHALL be stated. Existing local Change, uncommitted review, terminal and merge behavior
SHALL remain unchanged.

Before a PR exists, the Review tab SHALL explain where the PBI stands in its journey and SHALL NOT
offer a local working diff as a substitute for review.

The screen SHALL use the shared kit, tokens, typed i18n and locked vocabulary, and SHALL remain
operable in both themes at a page-proven 390px width.

#### Scenario: Review is a tab of the PBI
- **WHEN** a PBI with a published PR is opened
- **THEN** the Review tab hosts the PR review component, and the journey is the PBI's default resting entry rather than review

#### Scenario: The same component serves the direct entry
- **WHEN** a reviewer opens an external PR by its remote reference
- **THEN** the review component is the same one the PBI's Review tab hosts, and no standalone PR destination exists

#### Scenario: The PR has no Proposal
- **WHEN** no Proposal can be discovered in the pinned comparison
- **THEN** the review states its absence and Code remains readable

#### Scenario: The execution machine is unavailable
- **WHEN** the machine that produced the PR cannot be reached
- **THEN** remote code, available checks and available provider evidence remain readable and no local app or check result is fabricated

#### Scenario: No PR exists yet
- **WHEN** the PBI has no published PR and the Review tab is opened
- **THEN** the tab states where the PBI stands in its journey and offers no local working diff as a substitute for review

#### Scenario: Local uncommitted review is opened
- **WHEN** the reviewer returns to a local Worktree with uncommitted files
- **THEN** the existing local review behavior is preserved

### Requirement: Linked issue evidence follows the convention and absence is honest

Remote review SHALL read provider-reported linked issue evidence through the `evidence-from-the-issue`
convention, with the PR as the link to the issue. Multiple linked issues SHALL remain distinguishable;
the reader SHALL not guess one from a bare number.

Evidence SHALL retain its original observation metadata. Current freshness SHALL require all three
recorded head, base-tip and merge-base SHAs and repository identities to match the viewed comparison,
with clean source state and a verified execution revision. Known revision mismatches SHALL be stale;
dirty source, unverified execution or missing comparison tuple members SHALL be unknown. A current
revision match SHALL NOT establish passing tests, approval or independent attestation.

When there is no linked issue, the linked issue has no evidence under the convention, or no convention
applies, the Review tab SHALL show evidence not found and SHALL keep the code reviewable. A permission,
authentication or transport failure SHALL be distinguished from absence and SHALL be shown as an error
with the provider's remedy where it gives one.

#### Scenario: The PR has two linked issues
- **WHEN** two authorized issues are linked by the provider
- **THEN** their attachments and identities remain separately selectable rather than merged by number

#### Scenario: Evidence names an older head
- **WHEN** an attachment's observed head differs from the viewed PR head
- **THEN** it is labelled stale and remains readable

#### Scenario: Evidence lacks comparison metadata
- **WHEN** the available metadata cannot establish whether evidence matches the viewed comparison
- **THEN** freshness is unknown, not current

#### Scenario: Matching HEAD was captured from dirty source
- **WHEN** metadata names the viewed head but records a dirty source tree or unverified execution revision
- **THEN** exact-revision freshness is unknown and the attachment remains readable

#### Scenario: No linked issue or no convention
- **WHEN** the PR links no issue, or the linked issue carries no evidence under the matching convention, or no convention applies
- **THEN** the Review tab states that no evidence was found, states the rule it matched by, and the code remains reviewable

#### Scenario: An access failure is not absence
- **WHEN** the vendor refuses the evidence read or the transport fails
- **THEN** the Review tab reports the access error and does not present it as evidence not found

#### Scenario: Evidence is asked for an issue the PR does not link
- **WHEN** the review asks for the evidence of an issue that is not among the PR's provider-reported linked issues
- **THEN** the read is refused as not linked, and no evidence of that issue is shown as this PR's

### Requirement: The PR's checks are read from the provider over the head

The remote reader SHALL read the PR's checks by combining the provider's combined status and
check-runs over the head commit, scoped to the pinned comparison. The read SHALL distinguish checks
that are passing, checks that are failing and checks that are in-flight, each named with its provider
identity and a link.

The head commit the checks were read for SHALL be carried with the result, so checks from an older
head are not shown as the current state. A head change SHALL invalidate the read; a refresh SHALL read
against the new head.

When the head has no check-runs and no combined status, the reader SHALL report that no checks were
declared, and SHALL NOT present that absence as success. When the provider refuses the read or the
transport fails, the reader SHALL report an access error with the provider's remedy where it gives one,
distinct from no checks declared. The checks read SHALL NOT reuse the local test inventory seam; the
vendor's CI and this product's own evidence are different things.

#### Scenario: Checks are aggregated over the head
- **WHEN** the reviewer opens a PR whose head has checks
- **THEN** passing, failing and in-flight checks are listed, each with its provider identity and link, and the head commit they were read for

#### Scenario: No checks were declared
- **WHEN** the PR's head has no check-runs and no combined status
- **THEN** the Review tab states that no checks were declared and does not show an empty result as success

#### Scenario: The check read cannot be performed
- **WHEN** the provider refuses the check read or the transport fails
- **THEN** the reader reports an access error, distinct from no checks declared, and the rest of the review remains usable

#### Scenario: Checks are read for the viewed head
- **WHEN** the PR's head changes after the checks were read
- **THEN** the prior checks are not shown as the current state, and a refresh reads against the new head
