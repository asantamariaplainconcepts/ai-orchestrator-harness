# pbi-review Specification

## Purpose
The Review tab answers what the change touched, and what was seen of it: the pull request's diff with
its threads, the checks, the quality of the change, and the evidence's captures, in that order. A person approves or requests
changes here and the verdict is written at the vendor. Reverse-engineered from the shipped code
(`src/modules/Pbis/AiOrchestrator.Modules.Pbis/Features/Vendor/Review/`,
`src/frontend/features/pbi/tabs/ReviewTab.tsx`), not negotiated ahead of it.

## Requirements
### Requirement: The review is read from the vendor in four parts
`GET /api/pbi/review?repository&id` SHALL read in parallel the pull request's change (base and head
with their SHAs, commits, additions, deletions, and each file with its patch), its review threads with
whether each is resolved or outdated, and the checks on the head, as one read of the vendor; and the
evidence beside it. On GitHub that read SHALL be four `gh` calls at once when the pull request comes from
`change/<n>`, the branch the loop claims the PBI with: the pull request with its threads in one GraphQL
query, and by the branch's name its files from the compare endpoint, its check runs and its statuses
(#625). An answer by the branch SHALL be kept only when it is of the pull request's head and merge base,
and lists as many files as the pull request; otherwise, and for a branch that is gone or named otherwise,
the files and the checks SHALL be asked again by the pull request and its head, a second wave, with the
same answer (#621). A failure to read the
change or the threads SHALL fail the request with 409; a failure to read the checks or the evidence
SHALL be that part's problem while the rest is still answered. A PBI with no pull request SHALL answer
every part empty. The pull request SHALL say whether it is merged, and whether it was closed without a
merge (#719).

When the vendor reports no checks on the head, the Gate section SHALL show the why of the newest `ran`
line of the PBI's `gate` in this machine's journal, with when it ran and a link to its log (#719). With
no such line, as when another instance ran the gate, it SHALL say that the gate runs on this machine in
`verifying` and that the Journey shows what it said.

#### Scenario: The checks cannot be read
- **WHEN** GitHub answers the diff and threads but not the check runs
- **THEN** the tab shows the diff and its threads, and the checks say why they are missing

#### Scenario: The vendor reports no checks and this machine ran the gate
- **WHEN** GitHub reports no checks on the head and the journal has `gate: aspire do ci passed: 10/10 steps succeeded` for the PBI
- **THEN** the Gate section says the gate ran on this machine, when, and `aspire do ci passed: 10/10 steps succeeded`, with a link to its log

#### Scenario: The branch was deleted after the merge
- **WHEN** the Review tab of a PBI whose pull request merged and whose `change/<n>` is gone is opened
- **THEN** the compare and the checks by the branch find nothing, and the files and checks are read by the pull request and its head, as before

### Requirement: Threads are written on the pull request
`POST /api/pbi/review/comment` SHALL open a thread on a line of the diff, `POST /api/pbi/review/reply`
SHALL reply in a thread, and `POST /api/pbi/review/resolve` SHALL resolve one after checking it belongs
to this pull request; each SHALL be read back. A line outside the diff SHALL be 409
`Review.VendorRefused` in GitHub's words.

#### Scenario: Resolving a resolved thread
- **WHEN** a person resolves a thread that is already resolved
- **THEN** nothing is written and the thread reads as resolved

### Requirement: The evidence is the newest marked comment, or none
The evidence SHALL be the images of the newest comment of the issue carrying `<!-- aio:evidence`, never
an older one as a fallback (#598). The head SHA SHALL be the full SHA the marker names, or none. A
capture SHALL be an image at one of the vendor's asset addresses whose alt text reads
`<screen> <width>x<height> <theme>`, as `.aio/steps/evidence` writes it, where the screen is any
text without a space: a screen's name such as `settings`, or a route such as `/?day=2026-10-15` (#719).
The tab SHALL show the screen as written. Any other image of the comment SHALL not be a capture. A failure to read the comments SHALL be shown as the evidence's problem.

#### Scenario: Another repository's routes
- **WHEN** the newest marked comment carries the eight captures of aio-dogfood#43, alt texts such as `/ 1440x900 light` and `/?day=2026-10-15 390x844 dark`
- **THEN** the evidence has eight captures, of the screens `/` and `/?day=2026-10-15`

#### Scenario: An older comment has captures and the newest has none
- **WHEN** the newest comment with the marker carries no image whose alt text says what it shows
- **THEN** the tab shows no pictures, and does not fall back to the older comment's

### Requirement: An agent reads and reviews the pull request through aio pr
`IPbiProvider` SHALL read a PBI's pull request's conversation: its title, state, author and body, every
comment on it as a whole and every review submitted with its verdict and body, or null when the PBI has
no pull request. The GitHub adapter SHALL read it in one GraphQL call, and the cached provider SHALL
keep it like the other reads. `Modules.Pbis` SHALL serve it as `GET /api/pbi/review/conversation`, and
a review written through `SubmitReviewAsync` as `POST /api/pbi/review/submit` with a verdict of
`comment`, `approve` or `request-changes`, which moves nothing in the loop. `.aio/aio pr`
SHALL call these and `GET /api/pbi/review` at `AIO_API` for `AIO_REPOSITORY`, with `aio issue`'s exit
codes and body files: `view <issue>` answers one JSON line `{pr, why}` with the checks, the comments,
the reviews and every thread with its file and line; `view <issue> --markdown` prints it as text, each
comment anchored to a line under its `file:line`, which `gh pr view --comments` did not show (0 of 2 on
PR #457); `diff <issue>` prints the unified diff; `review <issue> --verdict <v> --body-file <f>` answers
`{review, why}`. An approval or a request for changes the vendor keeps as a comment because the reviewer
wrote the pull request SHALL be said in `why` (#544). A PBI with no pull request yet SHALL answer `null`
with exit 0.

#### Scenario: Line comments are read
- **WHEN** an agent runs `aio pr view 7 --markdown` and the pull request has a thread on `src/Port.cs` line 2
- **THEN** the text carries `### src/Port.cs:2` and each comment of the thread followed by `on src/Port.cs:2`

#### Scenario: A review from the author
- **WHEN** `aio pr review 7 --verdict approve --body-file .aio/review.md` runs as the pull request's author
- **THEN** GitHub keeps a comment, the script answers exit 0 with `asComment` set and a `why` that says it was kept as a comment instead of an approval, and `.aio/review.md` is gone

### Requirement: Approve and Request changes decide merge-review
The tab SHALL be marked while the PBI's state declares `tab: review` in the loop (`code-review` and
`merge-review` in this repository). *Request changes* SHALL appear only when the PBI's state declares
`back`, and the line under the verdicts SHALL say the state declares none when it does not. *Approve*
and *Request changes* SHALL submit the review at the vendor and then move the loop as the
person-decisions capability says; a review the vendor refused SHALL be shown in the vendor's words with the loop unmoved.
A merged pull request, or one closed without a merge, SHALL offer neither verdict, and the line SHALL say
it is merged or closed and that nothing is left to decide (#719).

#### Scenario: Approving a PBI held in merge-review
- **WHEN** a person who did not open the pull request approves a PBI held in `merge-review`
- **THEN** the pull request is approved on GitHub and the PBI moves to `retro`

#### Scenario: A state without back
- **WHEN** a PBI in `verifying`, which declares no `back`, is opened on the Review tab
- **THEN** Approve is there, Request changes is not, and the line under them says `verifying` declares no back in the loop

#### Scenario: A merged pull request
- **WHEN** the Review tab of a PBI whose pull request is merged is opened
- **THEN** there is no Approve and no Request changes, and the line says the pull request is merged and nothing is left to decide
