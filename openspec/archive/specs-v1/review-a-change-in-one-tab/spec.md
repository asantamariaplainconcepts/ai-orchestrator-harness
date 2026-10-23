# review-a-change-in-one-tab Specification

## Purpose
Approving a worktree's change, in one tab and in one order. It used to be seven tabs and a five-tile
verdict card: Issue said what was asked for, Review showed the diff, Checks said how the commands ended,
and whoever approved assembled the judgement from three of them and five tiles, every time — while the
card above wrote a verdict none of them had asked it for. What replaces it is a header that states the
facts and judges nothing, five ordered steps, and a footer that counts how many of them a person has
marked. The product stops having an opinion about whether a change may land; what blocks is the
reviewer's own checklist.
## Requirements
### Requirement: A worktree's strip is five fixed tabs and the first of them is the change

A worktree SHALL draw its fixed tabs — **Change** and **Files** — under a journey header that
answers where the loop stands (see `worktree-is-a-loop`). There SHALL be no tab called Issue, no
tab called Review and no tab called Checks in a worktree's strip: what those three answered is a
step of the change, and reading three tabs to assemble one judgement is the thing this replaces.

**A checkout's strip SHALL keep Review and Checks.** They are withdrawn from the worktree only. A
checkout somebody edits by hand has a real diff, and a check's result belongs to the directory it
ran in — so the tabs are rescoped and not removed, and no strip other than the worktree's changes.

The tab a worktree rests on SHALL be Change, and it SHALL be a tab that strip actually offers: a
resting place the strip does not contain sends the screen straight back to it.

#### Scenario: The worktree strip under the journey header
- **WHEN** the folder screen is scoped to a worktree of a repository
- **THEN** the fixed tabs are Change and Files under the journey header, and none of them is Issue,
  Review or Checks

#### Scenario: The worktree strip
- **WHEN** the folder screen is scoped to a worktree of a repository
- **THEN** the fixed tabs are Change and Files under the journey header, and none of them is Issue,
  Review or Checks

#### Scenario: The checkout keeps what the worktree gave up
- **WHEN** the same folder is read on its checkout
- **THEN** Review and Checks are still on the strip, with the diff and the checks they always drew

#### Scenario: A link to a tab this scope no longer has
- **WHEN** a link names a tab the scope does not offer, including one this change removed
- **THEN** the screen resolves to Change rather than to a blank panel

### Requirement: The change header names the issue and states its facts without judging them

The Change tab's header SHALL name the issue the worktree is about — its number and its title — and
beneath it SHALL state the change's facts as one mono line. The facts and the wording of an unknown are
the `merge-gate` capability's; this requirement is that the header is **where** they are said and that
the header says nothing else about readiness.

The header SHALL carry the screen's actions in **one place**, and no fact SHALL disable one.

The issue SHALL be resolved the way every other surface on this screen resolves it — from the most
recent run in that directory, and otherwise from the number the branch proposes and the task read
confirms — so the header and the facts beneath it can never name different issues for one worktree.

**The header SHALL paint with cached data before `useTask` and `useWorktreePullRequest` resolve.**
The rail's marks SHALL read as empty while the task is pending, and the merge button SHALL read as
not-merged while the pull request is pending. When either resolves, the marks and the button SHALL
update without remounting the header.

**The step panel SHALL NOT be replaced by a full-panel spinner while its query is pending.** Each
step panel SHALL draw its own layout skeleton — the heading bar, muted placeholders for rows — so the
header, the rail and the footer stay visible while the panel's data arrives.

**Once a pull request exists — open or otherwise — the steps the strip offers SHALL draw from the
remote reader's pinned comparison** (`review-a-remote-pull-request`) rather than from the working
directory: the Code, Proposal and Evidence steps read the comparison's files, the change's OpenSpec
artefacts as the review carries them, and the remote evidence with its provenance — and none of them
completes a missing piece with a local read. The local working diff is only the reading used before a
PR exists, never a parallel source of reviewed code once one does. **Open, merged, and closed without
merge are named as the different states they are**, and an open pull request keeps the local
unpushed-work reading it has today.

#### Scenario: The header over a resolved issue

- **WHEN** the worktree's task resolves
- **THEN** the header names that issue's number and title, with the facts as one line beneath it

#### Scenario: The header over a worktree whose issue does not resolve

- **WHEN** no run and no branch resolves a task for this worktree
- **THEN** the header says the issue is not known and still states every fact it can, rather than drawing nothing

#### Scenario: Nothing in the header is a verdict

- **WHEN** a check has failed and the tree is dirty
- **THEN** the header states both and offers every action it always offered

#### Scenario: The header paints before the task resolves

- **WHEN** the Change tab mounts and `useTask` is still pending
- **THEN** the header paints with the worktree's branch, the diff counts from the cache, and an empty set of step marks — and fills in the marks when `useTask` resolves, without remounting

#### Scenario: The step panel draws a skeleton while pending

- **WHEN** the selected step's query is still pending
- **THEN** the panel draws its layout skeleton rather than a full-panel spinner, and the header, rail and footer above it stay visible

#### Scenario: Switching tabs does not blank the screen

- **WHEN** the reader switches from another tab to Change, or between steps inside Change
- **THEN** the previous content stays visible while the new content mounts, and there SHALL be no blank gap between the two

#### Scenario: An open PR keeps its local unpushed-work reading

- **WHEN** the change's pull request is open and the worktree has unpushed work
- **THEN** the steps read the local working diff including that unpushed work, and the pinned remote comparison is not offered as a replacement for it

#### Scenario: The closed result's steps read remotely

- **WHEN** the change's pull request has merged
- **THEN** the Code, Proposal and Evidence steps draw from the remote comparison, and the state of the worktree changes nothing they show

#### Scenario: Closed without merge is named as its own state

- **WHEN** the change's pull request is closed without merging
- **THEN** the steps say so, and neither a merged comparison nor a local diff is offered in its place

### Requirement: The Code step offers a raw view of the selected file beside the patch view

The Code step's patch panel SHALL offer a toggle between **Patch** (the default, unchanged) and
**Raw** — the full file contents with each line tinted by its change status. The toggle SHALL be
per-file and SHALL reset to Patch when the selected file changes, so a reader who switched to Raw
on one file does not read the next file as "all context" without noticing.

**The raw view composes two existing endpoints** — `GET /api/folders/{id}/file` for the file text
and `GET /api/folders/{id}/patch` for the line statuses — and introduces no new endpoint, no new
query, and no new seam. The change status of each line SHALL be derived from the patch's line
numbers: a line with a `newLine` and no `oldLine` is added, a line with an `oldLine` and no
`newLine` is removed, and a line with both is context.

A binary file SHALL NOT offer the raw toggle, the same way the patch view says "binary file" rather
than showing hunks. A file whose text is truncated by the file endpoint SHALL say so — a preview
that quietly ends lies about what is in the file.

**The raw view does not edit.** It is read-only. It is not a tree browser: it shows the file that
is in the diff, not the filesystem.

#### Scenario: The toggle is Patch by default

- **WHEN** a text file is selected in the Code step
- **THEN** the panel shows its patch (hunks with gutters and tints), and the toggle reads "Raw"

#### Scenario: Switching to Raw shows the whole file with change tints

- **WHEN** the reader presses the Raw toggle on a selected text file
- **THEN** the panel shows the full file contents, with added lines tinted green, removed lines
  tinted red, and context lines untinted

#### Scenario: The toggle resets when the file changes

- **WHEN** the reader switches to Raw on file A, then selects file B in the sidebar
- **THEN** file B's panel opens in Patch view, not in Raw

#### Scenario: A binary file offers no raw toggle

- **WHEN** the selected file is binary
- **THEN** the panel says "binary file," the toggle is not offered, and no file contents are fetched

#### Scenario: A truncated file says so

- **WHEN** the file endpoint truncates the text
- **THEN** the raw view shows the text it has and says it stops short of the end, rather than ending
  quietly

### Requirement: The change is four ordered steps, drawn as a rail, and nothing is marked

The Change tab SHALL draw an ordered rail of four steps — **Proposal**, **Code**, **Evidence**,
**The app** — one panel for the selected step, and a footer that moves between them.

**The steps are an order to read in, not a checklist.** Selecting a step, moving forward and moving
back SHALL leave the worktree exactly as it was, and there SHALL be no control that marks a step
read, no count of steps left and no progress: the reviewer is trusted to have read what they merge.
Merging SHALL NOT wait on any step.

#### Scenario: Four steps, in order
- **WHEN** the Change tab opens on a worktree
- **THEN** four numbered steps are drawn in order, and nothing on the tab counts them

#### Scenario: The footer moves and stores nothing
- **WHEN** the reader presses forward and then back through the rail
- **THEN** the panel follows the selection and nothing about the worktree has been recorded

#### Scenario: Merge opens on an unread change
- **WHEN** the reader presses Merge change without opening a step
- **THEN** the dialog offers the merge; only the server's own refusals hold it back

### Requirement: The Evidence step reads what the gate gathered beside what the checks said
The Evidence step SHALL draw, in this order: the review evidence `aspire do review-evidence` left in
the worktree's `evidence/` folder; what the declared checks last said and which tests the change
added; and what was attached to the issue. It SHALL judge nothing — a figure's tone says which side
of a conventional line it is on, never whether the change is good enough — and a section that was
not measured SHALL be a sentence with its reason, never a zero.

A worktree with no `evidence/` folder SHALL be told how to gather it, and evidence that describes an
earlier commit than the worktree's head SHALL say so.

#### Scenario: Nothing gathered yet
- **WHEN** the worktree has no `evidence/review-evidence.json`
- **THEN** the step names `aspire do review-evidence` and still shows the checks and attachments

#### Scenario: Evidence from an earlier commit
- **WHEN** the evidence names a commit other than the worktree's head
- **THEN** the step says the figures describe an earlier commit

### Requirement: A task's review entry is not the whole detail

The task detail SHALL open the remote PR review screen as one entry beside execution, and neither
closing the task at the vendor nor merging its pull request SHALL redirect, replace or absorb the task
detail into the review screen. Reviewing is an intent the person expresses by opening the entry, not a
state the task's lifecycle infers: a merged pull request SHALL keep the entry — it opens the merged
comparison — and a closed task with an unmerged pull request SHALL keep it too.

**The published PR is the single source of reviewed code once it exists.** The local working diff is
the reading used before a PR exists — to see what a person is typing before it is pushed — and SHALL
NOT be offered as a second, parallel source of review alongside the pinned remote comparison once a PR
is published.

#### Scenario: A merged pull request keeps its entry

- **WHEN** a task's pull request has merged
- **THEN** the task detail still offers Review PR, and it opens the merged comparison rather than redirecting the detail

#### Scenario: A closed task with an open pull request

- **WHEN** a task is closed at the vendor and its pull request is still open
- **THEN** the task detail offers both Review PR and Watch execution, each resolving independently

#### Scenario: The working diff is not a second review source

- **WHEN** a PR exists for the task and the reviewer opens review
- **THEN** the pinned remote comparison is the source of the reviewed code, and the local working diff is not offered as a parallel review alongside it

