## ADDED Requirements

### Requirement: Provider review threads remain native and revision-aware from the Review tab

The Review tab of the PBI, and the direct external-PR entry that shares the `PullRequestReview`
component, SHALL display provider comments and threads with their original identities, file/line
context and resolved/outdated facts. Replies SHALL target the actual provider thread. The product
SHALL NOT persist a parallel review verdict.

#### Scenario: An inline comment is answered from the Review tab
- **WHEN** the reviewer replies from the Review tab to an existing inline thread
- **THEN** the reply appears under that provider thread, not as a disconnected top-level comment

#### Scenario: A thread targets old code
- **WHEN** a provider marks the thread outdated
- **THEN** the reader preserves that state rather than placing the comment as if it targeted the current line

#### Scenario: Thread state cannot be fetched
- **WHEN** the provider refuses the operation needed to read resolved/outdated state
- **THEN** the reader states that limitation rather than representing all threads as current or claiming there are none

### Requirement: Native review publication is bound to the viewed revision

Submitting a comment, approval or request for changes from the Review tab or the direct external-PR
entry SHALL publish a native provider review only after explicit user confirmation. Inline comments
SHALL identify the viewed comparison, file, side and valid changed-hunk line/range.

Before submission, the server SHALL compare current provider head/base with the draft. A changed
comparison SHALL refuse publication and retain unsent text. The provider write SHALL be bound to the
viewed head commit. Invalid line locations and provider permission refusals SHALL be explicit.

A review published from an external PR opened by direct reference SHALL bind to that PR's head and
threads under the same rules as the logged-in PBI's PR; the publication path SHALL be the same
component.

Publication SHALL NOT merge a PR, bypass policies, remove a Hold or change issue lifecycle status.

#### Scenario: The PR moves before submission
- **WHEN** the current comparison differs from the draft's comparison
- **THEN** submission is refused as stale and the draft remains available

#### Scenario: A comment names an unchanged location outside the hunks
- **WHEN** an inline draft targets a line/range not valid for the viewed diff
- **THEN** it is refused before any review publication

#### Scenario: The provider refuses approval
- **WHEN** the account cannot approve that PR
- **THEN** the provider's remedy is shown and no successful approval is recorded

#### Scenario: A review is published
- **WHEN** GitHub confirms a review against the viewed head commit
- **THEN** its provider identity is shown without changing the PR merge or issue Hold state

#### Scenario: The reviewer edits a draft while publication is pending
- **WHEN** the reviewer changes the summary or outcome, or adds inline comments, after a review request has been sent
- **THEN** confirmation clears only the submitted summary and inline comments, retains the later edits as an unsent draft, and does not resend them automatically

#### Scenario: The reviewer edits a reply while publication is pending
- **WHEN** the reviewer edits a thread reply after its request has been sent
- **THEN** confirmation retains the newer text and other threads' drafts instead of clearing text that was never submitted

#### Scenario: The account already has a pending review
- **WHEN** submission discovers a provider-side pending review belonging to the current account
- **THEN** it refuses to create or silently submit a different review and provides the existing review context for explicit reconciliation

#### Scenario: A reply addresses an old thread
- **WHEN** the reviewer confirms a reply to an outdated inline thread belonging to the selected PR
- **THEN** publication targets that native thread and preserves its original revision context without claiming to review the new head

#### Scenario: A verdict is published from an external PR opened directly
- **WHEN** the reviewer submits an approve or request-changes verdict from the direct external-PR entry
- **THEN** the review publishes against that external PR's head under the same rules as the logged-in PBI's PR, through the same `PullRequestReview` component

### Requirement: Review write races and uncertain outcomes are explicit

A provider change after preflight SHALL NOT cause a review tied to the old commit to be represented
as approval of a newer revision. The Review tab SHALL re-read after confirmed publication and mark
an intervening change as outdated.

A lost response after a potentially accepted write SHALL report uncertainty, retain enough
provider/revision context for manual inspection and SHALL NOT automatically resend.

#### Scenario: The head moves after preflight
- **WHEN** the review is confirmed against its original commit but the provider's head has changed
- **THEN** the result identifies the original commit and is shown as outdated for the new head

#### Scenario: The submission response is lost
- **WHEN** a review might have been accepted but the response did not arrive
- **THEN** the result is uncertain and does not claim failure with a safe-to-retry assumption or resend automatically

#### Scenario: Publication is confirmed but the freshness check fails
- **WHEN** the provider returned the new review identity but the subsequent metadata read fails
- **THEN** the review remains confirmed with unknown freshness and the application does not resend it
