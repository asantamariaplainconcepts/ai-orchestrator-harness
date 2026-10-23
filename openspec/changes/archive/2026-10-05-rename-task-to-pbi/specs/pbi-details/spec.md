# pbi-details Delta

## ADDED Requirements

### Requirement: The comment thread is read from the vendor, never held
The system SHALL expose `GET /api/pbis/{key}/comments` (`Features/Inbox/UseCases/GetPbiComments.cs`,
`Access.FiltersToCaller`) reading the thread from the vendor in the vendor's own order. The PBI
snapshot SHALL NOT carry comments: across the 92 issues of this repository the bodies are 351,931
characters against 514,934 of comments, so holding them would more than double every read to keep a
copy that is stale between reads, for text one screen reads one PBI at a time. An empty thread SHALL
be an ordinary answer — 33 of those 92 issues have none — and a key neither the snapshot nor the
vendor has SHALL say so rather than answering nothing.

#### Scenario: An issue with no comments answers an empty thread
- **WHEN** the PBI has no comments
- **THEN** an empty thread is returned rather than an error

#### Scenario: A key no vendor has says so
- **WHEN** the key is not in the snapshot and the vendor has no such PBI
- **THEN** the response says so rather than returning an empty thread

### Requirement: The label list is what the vendor has, and a vendor with no list gets an empty one
The system SHALL expose `GET /api/pbis/{key}/labels` (`GetPbiLabels.cs`, `Access.FiltersToCaller`)
returning the labels the repository offers for the picker. GitHub has a real list with colours; Azure
DevOps and Jira do not — their tags are free text — so a vendor with no list SHALL return an empty
one.

#### Scenario: A vendor without a label list returns an empty picker
- **WHEN** the PBI belongs to a vendor whose tags are free text
- **THEN** the picker receives an empty list rather than an error

### Requirement: Assignees are read from the vendor because the PBI read was built for the inbox
The system SHALL expose `GET /api/pbis/{key}/assignees` (`GetPbiAssignees.cs`,
`Access.FiltersToCaller`) reading assignees from the vendor: an inbox row needs to know what a PBI is
and what state it is in, not who is doing it, and the detail panel is where a person looks for that.

#### Scenario: The detail panel reads assignees live
- **WHEN** the detail panel opens
- **THEN** the assignees come from the vendor rather than from the PBI snapshot

### Requirement: Labels and assignees are written at the vendor first, and the UI reverts on refusal
The system SHALL expose `POST /api/pbis/{key}/labels` (`SetPbiLabels.cs`, `Access.AnyCaller`) and
`POST /api/pbis/{key}/assignees` (`SetPbiAssignees.cs`, `Access.AnyCaller`), each adding and removing
at the vendor. The vendor SHALL be written first and the PBI snapshot SHALL then be emptied, so the
next read polls the vendor and a change made here and one made in a browser are indistinguishable
downstream. The optimistic update in the UI SHALL revert on refusal, and a write SHALL be addressed
with the row's own URL.

Addressing the write with the row's URL SHALL NOT be the whole of it: the row itself SHALL be found
by source, repository and key together — `#12` under two repositories is two PBIs, and a write SHALL
land on the one the caller named.

#### Scenario: A refused write is reported and the UI reverts
- **WHEN** the vendor refuses a label or assignee write
- **THEN** the optimistic state reverts and the refusal carries the vendor's own sentence

#### Scenario: A write lands only on the PBI named
- **WHEN** two repositories in one scope carry the same key and the write names one
- **THEN** only that repository's PBI changes

### Requirement: The body is rendered as a whole, and a body that was cut stays honest
The detail body drawn from the vendor SHALL render the snapshot's text and, where the reader marked
it cut, SHALL show the truncation note the `mirrored-body-completeness` capability governs, so a
person reading a PBI never mistakes the front of the body for all of it.

#### Scenario: A truncated body says so on the panel
- **WHEN** a PBI's body was cut by the reader's trim
- **THEN** the detail panel shows the truncation marker beside the body

#### Scenario: A whole body carries no note
- **WHEN** a PBI's body was read whole
- **THEN** the detail panel draws it without a truncation marker

### Requirement: Evidence is what the PBI carries, read from storage and the vendor alike
The system SHALL expose `GET /api/pbis/{key}/evidence` (`GetPbiEvidence.cs`,
`Access.FiltersToCaller`) drawing the PBI's evidence without querying any folder or worktree — a
worktree can be gone and the evidence SHALL still answer. Screenshots attached to the vendor's
body/comments SHALL be read through `GET /api/pbis/{key}/attachments/bytes`
(`GetPbiAttachmentBytes.cs`, #294) returning AttachmentRead's four answers (Read, Gone, Unreachable,
TooLarge).

#### Scenario: Evidence survives the worktree
- **WHEN** a PBI's worktree has been removed and its evidence is read
- **THEN** the evidence answers from where it is stored rather than erroring

#### Scenario: A vendor-hosted attachment is read with the vendor's credential
- **WHEN** a screenshot pinned to the PBI's body is asked for
- **THEN** its bytes come from this origin, fetched with the vendor's own credential — never an
  `<img>` pointed at the vendor (#275)

### Requirement: A per-task call names the repository, and a mismatch is a 404 rather than the other row
Every per-PBI read and write — `GET /api/pbis/{key}/comments`, `GET /api/pbis/{key}/labels`,
`GET /api/pbis/{key}/assignees`, `POST /api/pbis/{key}/labels` and
`POST /api/pbis/{key}/assignees` — SHALL let the caller **name the repository** the key belongs to,
matched case-insensitively, and SHALL resolve the PBI by source, repository and key together. A key
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
/api/pbis/{key}` included, so the eight cannot drift into eight rules.

The browser SHALL carry the repository **in the query cache key as well as in the request**, so a cached
answer for one repository's `#217` is never served to a screen showing another's.

#### Scenario: A named repository decides which row answers
- **WHEN** two repositories hold `#217` and a comment thread is read naming one of them
- **THEN** the thread of that repository's PBI is returned

#### Scenario: A key absent from the named repository is a 404
- **WHEN** the read names a repository that has no such key
- **THEN** the response is a 404 rather than the other repository's row

#### Scenario: A write names the row it means
- **WHEN** a label is written on `#217` naming one of two repositories holding it
- **THEN** the label lands on that repository's PBI and the other is untouched

#### Scenario: The cache does not cross repositories
- **WHEN** one repository's `#217` has been read and another's is opened
- **THEN** the second read is not served the first's cached answer

### Requirement: The detail offers review and execution as two independent entries

The PBI detail SHALL offer **Review PR** and **Watch execution** as two
independent entries, resolved separately from the PBI's own identity —
source, host, repository and key together. Either entry SHALL be available
whenever its own destination resolves, regardless of the other's state: a
PBI with an open PR and an active run offers both at once, a PBI with
only a run offers only execution, and a PBI with only a PR offers only
review. What the entries open SHALL be the surfaces the product already
has — the remote PR review screen and the run's own tab — rather than new
destinations invented for this panel.

#### Scenario: Both entries on one PBI

- **WHEN** a PBI has an open pull request and a run that has not finished
- **THEN** the detail shows Review PR and Watch execution side by side, and each opens its own destination

#### Scenario: Execution without a pull request

- **WHEN** a PBI has a run and no pull request
- **THEN** Watch execution is offered and Review PR is not, and no pull-request destination is invented

#### Scenario: Review without a run

- **WHEN** a PBI has a pull request and neither this folder nor any other holds a run for it
- **THEN** Review PR is offered and Watch execution is not, and the absence is named rather than shown as an error

### Requirement: Destinations are resolved by full identity, never by the first match

Each entry SHALL resolve its destination from the PBI's full identity —
source, host, repository and key together — and SHALL NOT offer a
destination belonging to another repository's PBI that happens to carry
the same number, nor one inferred from a reused branch or the first run
with a matching key. Where several runs exist for one PBI, the entry
SHALL distinguish runs that have not finished from runs that have, SHALL
name each candidate — its agent, where it works, and how long since its
last activity — and SHALL let the person choose; the first match SHALL
NOT be selected in silence.

#### Scenario: Two repositories, one number

- **WHEN** two repositories in one scope each hold a PBI numbered 12, and one of them has runs
- **THEN** only that PBI's runs are offered as destinations, and the other repository's runs are not

#### Scenario: Several runs for one PBI

- **WHEN** a PBI has one run that has not finished and two that have
- **THEN** the unfinished run is listed first and each run is named, and opening one is the person's choice

### Requirement: Loading, absence, error and ambiguity are four named states

Each entry SHALL name its state, and the four SHALL NOT collapse into one
another: **loading** while the answer is on its way, **confirmed absence**
when the query answered and there is nothing, **error** when the query
failed — with the failure's own words — and **ambiguous** when the answer
cannot be attributed to this PBI's identity. An error SHALL NOT be shown
as absence, and absence SHALL NOT be shown as error. A failed runs lookup
SHALL NOT start a run: dispatch is a controlled decision governed by
`dispatch-observation`, and navigation is not a fallback path into it.

#### Scenario: A failed lookup is an error, not an absence

- **WHEN** the runs query refuses or fails while the detail is open
- **THEN** the entry shows the error and Watch execution does not become a confirmed absence, and no run is started

#### Scenario: A stale answer does not replace the destination

- **WHEN** an answer arrives for a PBI the person has already navigated away from
- **THEN** it is dropped rather than replacing the destination of the PBI now on screen

### Requirement: The lifecycle does not redirect the detail

Closing the PBI SHALL NOT remove nor redirect its entries, and merging
the pull request SHALL NOT collapse the detail into the PR view. The
entries the detail offers SHALL follow what the PBI's identity resolves
to — what its runs and its pull request are — and not what its lifecycle
labels say. A closed PBI with a run that has not finished SHALL keep
Watch execution available and the run SHALL remain reachable and active.

#### Scenario: A closed PBI with a live run

- **WHEN** a PBI is closed at the vendor while a run for it has not finished
- **THEN** Watch execution stays available, and the run it opens keeps its own state

#### Scenario: Merge does not absorb the detail

- **WHEN** the pull request of a PBI merges
- **THEN** the detail keeps both entries, each opening its own destination

