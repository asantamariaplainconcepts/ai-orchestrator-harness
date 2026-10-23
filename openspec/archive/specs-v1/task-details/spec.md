# task-details Specification

## Purpose
The parts of a task the inbox read deliberately does not carry — its comment thread, the repository's label
list, and who is assigned — read from the vendor when a person opens the detail panel, and written back to
the vendor first. Reverse-engineered from the shipped code, not negotiated ahead of it.
## Requirements
### Requirement: The comment thread is read from the vendor, never held
This capability is superseded by `pbi-details` (#448). Every requirement of `task-details` SHALL
live on in `pbi-details` with the behaviour unchanged and the vocabulary renamed from task to PBI,
answering at `/api/pbis/{key}/…`. Nothing new SHALL be added to this capability: it exists only to
name its replacement.

#### Scenario: An issue with no comments answers an empty thread
- **WHEN** the task has no comments
- **THEN** an empty thread is returned rather than an error

#### Scenario: A key no vendor has says so
- **WHEN** the key is not in the snapshot and the vendor has no such task
- **THEN** the response says so rather than answering nothing

### Requirement: The label list is what the vendor has, and a vendor with no list gets an empty one
The system SHALL expose `GET /api/tasks/{key}/labels` (`GetTaskLabels.cs`, `Access.FiltersToCaller`)
returning the labels the repository offers for the picker. GitHub has a real list with colours; Azure DevOps
and Jira do not — their tags are free text — so a vendor with no list SHALL return an empty one.

#### Scenario: A vendor without a label list returns an empty picker
- **WHEN** the task belongs to a vendor whose tags are free text
- **THEN** the picker receives an empty list rather than an error

### Requirement: Assignees are read from the vendor because the task read was built for the inbox
The system SHALL expose `GET /api/tasks/{key}/assignees` (`GetTaskAssignees.cs`,
`Access.FiltersToCaller`) reading assignees from the vendor: an inbox row needs to know what a task is and
what state it is in, not who is doing it, and the detail panel is where a person looks for that.

#### Scenario: The detail panel reads assignees live
- **WHEN** the detail panel opens
- **THEN** the assignees come from the vendor rather than from the task snapshot

### Requirement: Labels and assignees are written at the vendor first, and the UI reverts on refusal
The system SHALL expose `POST /api/tasks/{key}/labels` (`SetTaskLabels.cs`, `Access.AnyCaller`) and
`POST /api/tasks/{key}/assignees` (`SetTaskAssignees.cs`, `Access.AnyCaller`), each adding and removing at
the vendor. The vendor SHALL be written first and the task snapshot SHALL then be emptied, so the next read
polls the vendor and a change made here and one made in a browser are indistinguishable downstream. The optimistic update in the UI
SHALL revert on refusal, and a write SHALL be addressed with the row's own URL.

Addressing the write with the row's URL SHALL NOT be the whole of it: the row itself SHALL be found by
repository as well as by key, because the right URL read off the wrong row is still the wrong issue.

#### Scenario: A refused write reverts the screen
- **WHEN** the vendor refuses a label write
- **THEN** the optimistic change is reverted and the vendor's words are shown

#### Scenario: The write goes to the row's own repository
- **WHEN** two repositories hold the same key
- **THEN** the row is resolved by repository, and the write is addressed with the URL that row carries

### Requirement: A per-task call names the repository, and a mismatch is a 404 rather than the other row
Every per-task read and write — `GET /api/tasks/{key}/comments`, `GET /api/tasks/{key}/labels`,
`GET /api/tasks/{key}/assignees`, `POST /api/tasks/{key}/labels` and
`POST /api/tasks/{key}/assignees` — SHALL let the caller **name the repository** the key belongs to,
matched case-insensitively, and SHALL resolve the task by source, repository and key together. A key
that is absent from the named repository SHALL be a 404 rather than the row another repository holds under
the same number.

Resolving by key alone SHALL NOT be how these calls find their row. Reading the wrong row answers with
another repository's thread, and writing to it puts a label on somebody else's issue; the first is
recoverable and the second is not.

A caller that names no repository SHALL still be answered — that is how every caller asked before one
scope could name two repositories — and the answer SHALL be the same one every time rather than whichever
vendor answered first: open before closed, then by vendor, then by URL. Where the caller also names a **source**, that SHALL narrow the row too: two
vendors may use the same key, and an issue and the pull request that closes it share a number.

The rule SHALL be written once and read by every call that resolves a row from a key, `GET
/api/tasks/{key}` included, so the eight cannot drift into eight rules.

The browser SHALL carry the repository **in the query cache key as well as in the request**, so a cached
answer for one repository's `#217` is never served to a screen showing another's.

#### Scenario: A named repository decides which row answers
- **WHEN** two repositories hold `#217` and a comment thread is read naming one of them
- **THEN** the thread of that repository's issue is returned

#### Scenario: A key absent from the named repository is a 404
- **WHEN** the read names a repository that has no such key
- **THEN** the response is a 404 rather than the other repository's row

#### Scenario: A write names the row it means
- **WHEN** a label is written on `#217` naming one of two repositories holding it
- **THEN** the label lands on that repository's issue and the other is untouched

#### Scenario: The cache does not cross repositories
- **WHEN** one repository's `#217` has been read and another's is opened
- **THEN** the second read is not served the first's cached answer

### Requirement: The detail offers review and execution as two independent entries

The task detail SHALL offer **Review PR** and **Watch execution** as two
independent entries, resolved separately from the task's own identity —
source, host, repository and key together. Either entry SHALL be available
whenever its own destination resolves, regardless of the other's state: a
task with an open PR and an active run offers both at once, a task with
only a run offers only execution, and a task with only a PR offers only
review. What the entries open SHALL be the surfaces the product already
has — the remote PR review screen and the run's own tab — rather than new
destinations invented for this panel.

#### Scenario: Both entries on one task

- **WHEN** a task has an open pull request and a run that has not finished
- **THEN** the detail shows Review PR and Watch execution side by side, and each opens its own destination

#### Scenario: Execution without a pull request

- **WHEN** a task has a run and no pull request
- **THEN** Watch execution is offered and Review PR is not, and no pull-request destination is invented

#### Scenario: Review without a run

- **WHEN** a task has a pull request and neither this folder nor any other holds a run for it
- **THEN** Review PR is offered and Watch execution is not, and the absence is named rather than shown as an error

### Requirement: Destinations are resolved by full identity, never by the first match

Each entry SHALL resolve its destination from the task's full identity —
source, host, repository and key together — and SHALL NOT offer a
destination belonging to another repository's task that happens to carry
the same number, nor one inferred from a reused branch or the first run
with a matching key. Where several runs exist for one task, the entry
SHALL distinguish runs that have not finished from runs that have, SHALL
name each candidate — its agent, where it works, and how long since its
last activity — and SHALL let the person choose; the first match SHALL
NOT be selected in silence.

#### Scenario: Two repositories, one number

- **WHEN** two repositories in one scope each hold a task numbered 12, and one of them has runs
- **THEN** only that task's runs are offered as destinations, and the other repository's runs are not

#### Scenario: Several runs for one task

- **WHEN** a task has one run that has not finished and two that have
- **THEN** the unfinished run is listed first and each run is named, and opening one is the person's choice

### Requirement: Loading, absence, error and ambiguity are four named states

Each entry SHALL name its state, and the four SHALL NOT collapse into one
another: **loading** while the answer is on its way, **confirmed absence**
when the query answered and there is nothing, **error** when the query
failed — with the failure's own words — and **ambiguous** when the answer
cannot be attributed to this task's identity. An error SHALL NOT be shown
as absence, and absence SHALL NOT be shown as error. A failed runs lookup
SHALL NOT start a run: dispatch is a controlled decision governed by
`dispatch-observation`, and navigation is not a fallback path into it.

#### Scenario: A failed lookup is an error, not an absence

- **WHEN** the runs query refuses or fails while the detail is open
- **THEN** the entry shows the error and Watch execution does not become a confirmed absence, and no run is started

#### Scenario: A stale answer does not replace the destination

- **WHEN** an answer arrives for a task the person has already navigated away from
- **THEN** it is dropped rather than replacing the destination of the task now on screen

### Requirement: The lifecycle does not redirect the detail

Closing the task SHALL NOT remove nor redirect its entries, and merging
the pull request SHALL NOT collapse the detail into the PR view. The
entries the detail offers SHALL follow what the task's identity resolves
to — what its runs and its pull request are — and not what its lifecycle
labels say. A closed task with a run that has not finished SHALL keep
Watch execution available and the run SHALL remain reachable and active.

#### Scenario: A closed task with a live run

- **WHEN** a task is closed at the vendor while a run for it has not finished
- **THEN** Watch execution stays available, and the run it opens keeps its own state

#### Scenario: Merge does not absorb the detail

- **WHEN** the pull request of a task merges
- **THEN** the detail keeps both entries, each opening its own destination

