## MODIFIED Requirements

### Requirement: A marked review step is stored against the working directory and the commit it was marked on

A marked review step SHALL be a `reviewed:<step>` label on the vendor issue, written through the
existing task-label write path. The five step keys SHALL be `reviewed:proposal`, `reviewed:code`,
`reviewed:tests`, `reviewed:evidence`, `reviewed:app`.

The label SHALL be written vendor-first and the mirror SHALL follow through the ordinary poll, so a
step marked in this product and a step marked in a browser are indistinguishable downstream.

The label SHALL NOT carry a commit, a timestamp, or a working directory: a label is present or absent,
and that is the whole answer. The label SHALL NOT expire when the branch moves — a label survives a
rebase, and the screen draws it the same. This is the trade-off accepted in #300: a tick that survives
a `git commit` is a false claim wearing the shape of a true one, and the visibility of the state on
the issue — in any browser, for anyone, on any machine — is what that cost buys.

A named working directory SHALL be honoured only when it is one of that folder's own, and an unknown
one SHALL be refused as not found rather than as forbidden, so that probing paths teaches nothing.

#### Scenario: A mark outlives the session
- **WHEN** three of the five steps are marked and the application is restarted
- **THEN** those three are still marked, read from the issue's labels

#### Scenario: A mark is visible on the issue
- **WHEN** a step is marked from the Change tab
- **THEN** the `reviewed:<step>` label is present on the issue in the vendor

#### Scenario: A mark survives a rebase
- **WHEN** a step is marked and the branch is then rebased
- **THEN** the step is still marked, because the label has no commit dimension

#### Scenario: A mark removed in a browser
- **WHEN** the `reviewed:<step>` label is removed from the issue in a browser
- **THEN** the step reads as unmarked after the next sync

#### Scenario: Two worktrees of one project
- **WHEN** a step is marked on the issue one worktree's branch claims
- **THEN** the label is on the issue, which any other worktree of that project sharing the same issue
  also reads

#### Scenario: A probe does not unmark anything
- **WHEN** the worktree rows of a folder are reconciled against git
- **THEN** every `reviewed:*` label on the issue is still there, because the labels are on the vendor
  issue and not in the workspace

#### Scenario: A working directory that is not this folder's
- **WHEN** a mark is requested against a path the folder does not own
- **THEN** it is refused as not found, in the same words as an unknown folder

### Requirement: The marks say a person looked, and nothing else

The marks SHALL NOT be turned into a verdict. The product SHALL derive from them only how many steps
are outstanding. Marking or unmarking a step SHALL NOT place or remove a hold, and SHALL NOT change
the task's `status:*` labels.

The `reviewed:*` label namespace SHALL be separate from `status:*` and from the vendor's own labels.
A `reviewed:*` label SHALL render as an ordinary chip on the inbox and be stripped from the Change
tab's count, the way `status:*` is stripped into `status` and `held`.

Marking or unmarking a step SHALL NOT place or remove a hold: a hold is the vendor's and a person
removes it. The marks SHALL NOT be sent anywhere — they are labels on the vendor issue, like every
other label.

#### Scenario: Five marks are not an approval
- **WHEN** all five steps are marked
- **THEN** the product says none is outstanding and states nothing about whether the change may land

#### Scenario: A mark leaves the hold alone
- **WHEN** a step is marked on an issue that is holding
- **THEN** the issue is still holding

#### Scenario: Reading is not marking
- **WHEN** a caller may read the task but has not been granted the right to write its labels
- **THEN** it can read the `reviewed:*` labels and cannot write one

## ADDED Requirements

### Requirement: The marks are read from the task's labels

The Change tab SHALL read the step state from the task's `labels` array — the labels already mirrored
on `TaskDetail` and `TaskItem` — filtering for the `reviewed:` prefix. A step is done when its label
is present, and not done when it is absent. There SHALL be no freshness comparison, no commit row,
and no four-state model.

#### Scenario: All five marked
- **WHEN** all five `reviewed:*` labels are on the issue
- **THEN** the Change tab says none is outstanding

#### Scenario: None marked
- **WHEN** no `reviewed:*` label is on the issue
- **THEN** the Change tab says five are outstanding

### Requirement: Marking and unmarking go through the task-label write path

Marking a step SHALL add the `reviewed:<step>` label through `POST /api/tasks/{key}/labels` with
`add: ["reviewed:<step>"]`. Unmarking SHALL remove it through the same path with
`remove: ["reviewed:<step>"]`. The label SHALL be written vendor-first, and the mirror SHALL follow
through the poll — the same path every other label write takes.

A step key outside the five SHALL be refused by the Change tab, not by the server: the server's label
write path is generic, and the five are the screen's own bound.

#### Scenario: Marking a step
- **WHEN** the reader marks the Proposal step from the Change tab
- **THEN** the `reviewed:proposal` label is added to the issue through the label write path

#### Scenario: Unmarking a step
- **WHEN** the reader unmarks a marked step
- **THEN** the `reviewed:<step>` label is removed from the issue through the label write path

## REMOVED Requirements

### Requirement: A mark is read back as one of four answers, and only one of them counts as done

**Reason**: The four-state freshness model — done on this commit, done on an earlier commit, no
longer comparable, never looked at — is removed. A label has no commit dimension, so there is no
freshness to compare. A step is marked or unmarked, and that is the whole answer.

**Migration**: The `since` field on `ReviewStepMark`, the `GetReviewSteps` endpoint that computed it,
and the `ReviewStep` aggregate that stored it are deleted. The Change tab reads from the task's labels
instead.

### Requirement: Marking is refused where the mark could never mean anything

**Reason**: The refusal was about a commit git could not name — a mark stored without a commit could
only ever read as "not comparable." A label has no commit, so there is nothing to refuse on those
grounds. An unknown step key is still refused, but by the Change tab rather than by the server — the
server's label write path is generic.

**Migration**: `FolderErrors.NoSuchStep` and `FolderErrors.StepNotComparable` are removed.
`MarkReviewStep` and its route are deleted. The step key bound is the Change tab's, not the server's.

### Requirement: Unmarking removes the mark, and marking again replaces it

**Reason**: This is subsumed by the label write path. Adding a label that is already present is a
no-op at the vendor; removing a label that is not there is a no-op. The server's label write path
already handles both.

**Migration**: `MarkReviewStep` is deleted. The `done: false` delete path is replaced by
`remove: ["reviewed:<step>"]` through `SetTaskLabels`.
