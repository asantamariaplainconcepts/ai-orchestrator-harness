# pbi-details Specification

## Purpose
TBD - created by archiving change rename-task-to-pbi. Update Purpose after archive.
## Requirements
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

### Requirement: The detail shows the position the Connector translated, and unknown is its own state
The detail of a PBI SHALL show the position and hold exactly as the Connector's read answered,
without mapping, deriving or re-parsing anything in the UI. `unknown` SHALL render as its own
state — with the reason the vendor's answer carried — distinct from every declared position and
from `backlog`; a PBI whose position is unknown SHALL NOT be shown as done or ready to run, and no
write, menu or drag target SHALL be hidden behind it silently distinguishing the state. An error
reading the position SHALL render with the vendor's own words, and a hold SHALL render orthogonal
to the position, exactly as the read answered.

#### Scenario: Unknown renders as itself
- **WHEN** the Connector answered `unknown` with a reason
- **THEN** the detail names it as unknown with that reason, in its own visual state, and the PBI sits in no lane

#### Scenario: A vendor error is not an unknown position
- **WHEN** the position read failed with the vendor's words
- **THEN** the detail shows the error as an error, not an unknown badge and not a position

#### Scenario: Held is shown beside the position
- **WHEN** a held PBI in code review is opened
- **THEN** the detail shows the code review position with the hold beside it, one not overriding the other

### Requirement: The detail opens the journey as its centre, with or without a PR or a worktree

The PBI detail SHALL open the journey — the loop's spine of stages and the runs grouped under them —
as its resting tab, with the issue body and thread on the Issue tab beside it rather than above it,
and the launch form SHALL NOT dominate the heading of the screen. Launching an agent SHALL be an
action offered by the step the loop is currently on or the next permitted one, not a form that owns
the top of the screen; the full launch form MAY remain reachable as a closed disclosure at the foot
of the Journey tab, and SHALL NOT be rendered for a held PBI.

The journey SHALL be drawn the same way before and after a PR exists: the spine and its runs are
the PBI's own facts, and the PR is a reference in the heading that opens the published object, not
a gate that restructures the detail.

#### Scenario: The detail opens on the journey

- **WHEN** a PBI is opened in the detail
- **THEN** the Journey tab is selected, its spine is the centre of the screen, and the launch form does not dominate the heading

#### Scenario: The journey is the same before and after a PR

- **WHEN** a PBI with no PR and one with an open PR are opened
- **THEN** both draw the journey's spine as the centre, and the PR is a reference rather than a restructure of the detail

#### Scenario: Launch is an action of the step

- **WHEN** the loop is on a stage that permits launching an agent
- **THEN** the launch action is offered in the heading as the step's action, and the full form is folded at the foot of the Journey tab

#### Scenario: A held PBI offers no launch form

- **WHEN** a held PBI is opened
- **THEN** the heading offers releasing the hold, and no launch form is rendered anywhere on the screen

### Requirement: The journey survives the worktree, reading what was persisted

The journey SHALL remain consultable when the PBI's worktree no longer exists: the spine and its
runs SHALL be drawn from the runs persisted by identity and the loop declaration read from the
checkout, and the parts that died with the worktree — the working path and the loop command button —
SHALL NOT be drawn. What was not persisted — the live frame of a run whose process is gone — SHALL
be named unrecoverable rather than invented.

The closed journey SHALL NOT be a different screen from the open one: the same spine reading serves
both, because the runs are read by identity and the loop declaration is the trunk's fact. A PBI whose
worktree is gone and one whose worktree is live SHALL answer the journey through the same path, and
the distinction between "the vendor has no data" and "there is no history" SHALL be named.

#### Scenario: The journey is read after the worktree is removed

- **WHEN** a PBI whose worktree has been removed is opened
- **THEN** the spine and its runs are drawn from the persisted facts, the working path and the loop command button are not drawn, and the live frame of a gone process is named unrecoverable

#### Scenario: The closed and the open journey are the same path

- **WHEN** a PBI with a live worktree and one whose worktree is gone are opened
- **THEN** both answer the journey through the same spine reading, and neither is a different screen

#### Scenario: No vendor data is named as no history

- **WHEN** a PBI's vendor has no position and no runs
- **THEN** the spine names the absence of data from the vendor, distinct from the absence of persisted history

### Requirement: The PR and evidence references open the published object and the issue

Where the journey shows a PR reference or an evidence reference, opening it SHALL navigate to the
published object at the vendor (the PR) or to the issue's evidence respectively, not to a local
working path. A reference that resolves no destination SHALL be named rather than offered as a link
to nothing.

#### Scenario: A PR reference opens the published PR

- **WHEN** the journey shows a PR for a PBI and the reference is opened
- **THEN** it opens the PR at the vendor, not a local path

#### Scenario: An evidence reference opens the issue's evidence

- **WHEN** the journey shows an evidence reference and it is opened
- **THEN** it opens the issue's evidence rather than a local directory

#### Scenario: A reference that resolves no destination is named

- **WHEN** a PR or evidence reference resolves no destination
- **THEN** it is named as absent rather than offered as a link to nothing

### Requirement: Tasks hijas appear only when a vendor integration provides them

The journey SHALL NOT synthesise tasks hijas from runs, and SHALL NOT create speculative storage for
them. Tasks hijas SHALL be shown only when a vendor integration actually provides them through the
seam, and a vendor that provides none SHALL draw nothing for them rather than a fabricated list.

#### Scenario: A vendor with no tasks hijas draws nothing

- **WHEN** a PBI's vendor provides no tasks hijas
- **THEN** the journey draws no tasks hijas, and none are synthesised from runs

#### Scenario: A vendor with tasks hijas shows them as provided

- **WHEN** a PBI's vendor provides tasks hijas through the seam
- **THEN** the journey shows them as the vendor provided them, with no speculative storage created

### Requirement: The voyage is the destination the rail, the inbox and Loop open
The PBI's voyage — the detail/journey the `pbi-journey-by-stages` change centres — SHALL be the single
destination a rail row, an inbox row and the `Loop` entry open when they reach a PBI, and SHALL be
resolved by the PBI's full identity (source, host, repository, key). A row SHALL NOT open a second
reading beside the voyage: no rail-owned detail, no forked journey, no destination inferred from a
reused branch or the first run with a matching key. Where the voyage's read is not yet served (before
the `pbi-journey-by-stages` change lands), a row SHALL open the `PbiDetailPane`/`PbiWalk` reading of
today, and SHALL NOT invent a destination the voyage does not offer — the row's destination is a
contract the voyage satisfies, not a parallel screen.

#### Scenario: A rail row opens the same voyage as the inbox
- **WHEN** a PBI is opened from a rail row and from the inbox
- **THEN** both open the same voyage, reached by full identity, with no second reading

#### Scenario: A closed PBI's voyage is reachable
- **WHEN** a PBI is closed at the vendor and a rail row or History journey is opened
- **THEN** the voyage opens, because closing does not remove the voyage, the way it does not collapse
the detail (`pbi-details`'s lifecycle rule)

#### Scenario: The destination is the identity's, not the first match
- **WHEN** two repositories hold a PBI numbered 12 and a rail row for one is clicked
- **THEN** only that repository's voyage opens, the rule `pbi-details` enforces for its entries

### Requirement: Old worktree links resolve to the PBI's voyage or explain
A `?worktree=` link (a published folder-route query the bookmarks carry) SHALL resolve to the PBI whose
voyage owns that worktree when the relation is unambiguous, and SHALL open the voyage by full identity.
When the relation is not unambiguous — a worktree whose branch name a removed PBI left behind, or a
path two folders' PBIs could own — the link SHALL show an explanation naming the reason it cannot
resolve, and SHALL NOT select another PBI that shares a key or path. This is the same rule
`pbi-details` enforces for review and execution entries: a destination belongs to one identity, never
to the first match.

#### Scenario: A worktree link opens the PBI's voyage
- **WHEN** a folder route carries `?worktree={path}` identifying one PBI
- **THEN** that PBI's voyage opens, reached by full identity

#### Scenario: A removed worktree's link explains
- **WHEN** a folder route carries `?worktree={path}` whose worktree was removed and no PBI owns it
unambiguously
- **THEN** the screen shows an explanation naming the removal, and no other PBI is selected

#### Scenario: A shared key does not reselect
- **WHEN** a folder route carries `?worktree={path}` and two PBIs could own it by shared key or path
- **THEN** the screen shows an explanation naming the ambiguity, and neither is selected in silence

### Requirement: The journey's done step offers the merge with state and block motive

The `done` step of the PBI journey (`PbiSpine`) SHALL present the merge as its action — the same
operation the loop's `merge` node and `/aio:sync` run — offered by the step itself rather than by a
form above the pane. The step SHALL state its state and, where the merge is blocked, the motive for
the block drawn from the admission's typed refusals: a held PBI names the hold, a missing or moved
grant names the grant, an unarchived change names the archive, a dirty tree names the tree. The
launch SHALL be addressed by the PBI's identity (source, repository, key) and the folder, with the
worktree as the launcher's resolved detail; the step SHALL NOT require a `?worktree=` the screen
carries.

The merge's confirmed result — landed, resumed, branch left behind — SHALL be drawn on the step from
the same response the worktree's `ChangeTab` reads, and the server's refusals SHALL pass through in
the vendor's own words. A step whose PBI has no pull request or no worktree in flight SHALL name the
absence rather than offering a launch to nothing, the way the existing merge dialog names a missing
PR. The worktree's `ChangeTab` SHALL keep its merge control; this change composes the same confirmed-
merged renderer into the step rather than moving it.

#### Scenario: The done step offers the merge for a PBI ready to land

- **WHEN** the journey reaches the `done` step for a PBI whose PR is open, whose worktree is in
  flight, whose change is archived, whose tree is clean and which carries no hold
- **THEN** the step offers the merge as its action, the launch is addressed by the PBI's identity,
  and the worktree's path and branch are the launcher's detail

#### Scenario: The done step states the motive for a block

- **WHEN** the PBI the step is for carries an active hold, or its grant is missing, or its change is
  not archived, or its tree is dirty
- **THEN** the step states which block it is, in the admission's typed words, and the launch is
  still reachable rather than disabled — the refusal comes when it is taken, the way the merge-gate
  spec requires

#### Scenario: A PBI with no worktree or PR in flight names the absence

- **WHEN** the `done` step is drawn for a PBI whose folder holds no worktree for its identity, or
  whose branch has no pull request
- **THEN** the step names the absence rather than offering a launch to nothing

#### Scenario: The worktree's ChangeTab keeps its merge control

- **WHEN** the worktree's `ChangeTab` is open for the same PBI
- **THEN** its merge control behaves as it did, and the journey's step is a second surface for the
  same operation rather than a replacement

#### Scenario: The merge's result is drawn on the step

- **WHEN** the merge lands, resumes or leaves a branch behind
- **THEN** the step draws the same result words the worktree's dialog draws, from the same response,
  and the step's state advances with the journey

### Requirement: A PBI has a screen of its own, with five tabs

The system SHALL give a PBI a screen of its own on the route `/pbis/:key`, with the folder the PBI
belongs to in the `folder` query, and SHALL read the PBI by key and repository (`GET
/api/pbis/{key}`) so a PBI the vendor has closed opens too. Its heading SHALL carry the key and
title, the position the Connector translated, the hold when there is one, and the PR when one is
joined. Its tabs SHALL be **Journey**, **Issue**, **Proposal**, **Code review** and **Evidence**, in
that order, with Journey selected on arrival. While the PBI's position is `code-review` the Code
review tab SHALL carry a mark saying so, and SHALL NOT be selected in Journey's place.

The Issue tab SHALL draw the issue's body, its assignees, its labels and its thread as the vendor
answers them. Before a PR is joined, the Proposal, Code review and Evidence tabs SHALL say where the
PBI stands in its journey and SHALL NOT offer a local working diff in its place.

A key that names no PBI of that repository SHALL be drawn as absence, a failed read as an error in
the server's words, and a folder that resolves no repository as unattributed — never as another
PBI that shares the key.

#### Scenario: A PBI opens on its own route

- **WHEN** `/pbis/%23412?folder=/repo` is requested
- **THEN** the PBI `#412` of that folder's repository is drawn with its heading and five tabs, Journey selected

#### Scenario: A closed PBI opens

- **WHEN** the PBI was closed at the vendor and is no longer in the inbox
- **THEN** the screen reads it by key and repository and draws it, with its journey

#### Scenario: Code review is marked at code review

- **WHEN** a PBI at `code-review` is opened
- **THEN** the Code review tab carries the code-review mark, and Journey is the selected tab

#### Scenario: No PR yet

- **WHEN** a PBI with no joined PR is opened on the Code review tab
- **THEN** the tab says where the PBI stands and offers no local diff

### Requirement: The heading offers one action, the current step's

The PBI screen's heading SHALL offer at most one action, chosen in this order: **release the hold**
when the PBI is held; **stop the run** when a run of this PBI, joined by key and repository, has not
finished; otherwise **the loop's next command** for the PBI's position, when the position is one a
command starts. A position no command starts SHALL offer no action rather than a disabled one.

#### Scenario: Held

- **WHEN** a held PBI with no live run is opened
- **THEN** the heading offers releasing the hold, and nothing else

#### Scenario: A run is live

- **WHEN** a PBI that is not held has a run that has not finished
- **THEN** the heading offers stopping that run

#### Scenario: Ready for the next command

- **WHEN** a PBI at `ready-for-implementation`, not held and with no live run, is opened
- **THEN** the heading offers the loop's implement command for it

### Requirement: The PR of an issue-shaped PBI is joined from the inbox

For a PBI that is an issue, the screen SHALL find its PR among the pull-request PBIs of the same
repository already in the inbox, as the one whose body closes the issue by GitHub's closing keywords
(`close`, `closes`, `closed`, `fix`, `fixes`, `fixed`, `resolve`, `resolves`, `resolved`, followed by
`#<number>`), case-insensitively and as a whole word. Where several match, the lowest PR number SHALL
be used and the heading SHALL say how many matched. Where none matches, the screen SHALL say no PR was
found rather than report an error. A PBI that is itself a pull request SHALL be its own PR. No
endpoint SHALL be added for the join.

#### Scenario: An issue with a closing PR

- **WHEN** issue `#412` is opened and the inbox holds PR `#422` of the same repository whose body says `Closes #412`
- **THEN** the heading carries PR `#422` and the review tabs read it

#### Scenario: A PR of another repository is not joined

- **WHEN** a PR of another repository says `Closes #412`
- **THEN** it is not joined to this repository's `#412`

#### Scenario: No closing PR

- **WHEN** no PR in the inbox closes the issue
- **THEN** the heading says no PR was found, and nothing is drawn as an error

