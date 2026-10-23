# merge-gate Specification

## Purpose
What is true of a worktree's change, and where those facts come from. The question the folder screen
exists for was the one thing not on it: seven tabs each answered one part well, and a person assembled
the verdict from five of them every time. So a panel was built that answered it — and then answered too
much, wording a verdict over five conditions on a screen that still made the reader do the reading. The
facts stayed and the verdict went: they are now one line above the change's own steps, and what blocks is
the reviewer's checklist rather than this capability's opinion. It states; it never judges and never
locks. Reverse-engineered from the shipped code, not negotiated ahead of it.
## Requirements
### Requirement: Position against the trunk is counted both ways, and absent rather than zero
The position read SHALL count both directions from the trunk, SHALL report a clean merge by saying so and
naming nothing, SHALL name which file conflicts when it is not clean, and SHALL report **absent** counts
rather than zero where there is no trunk. It SHALL answer null for a path that is not a repository, SHALL
recognise an old git by what it says, and SHALL resolve the trunk from where the commits actually are: a
stale `origin/HEAD` SHALL lose to the branch the commits are on, a repository whose trunk is `master`
SHALL stay on `master`, an unconventional trunk SHALL be answered rather than overridden, and no trunk at
all SHALL be head with no position.

#### Scenario: No trunk means no counts
- **WHEN** the repository has no trunk
- **THEN** ahead and behind are absent rather than 0, and the Base tile reads unknown

#### Scenario: A conflict names the file
- **WHEN** the dry-run merge conflicts
- **THEN** the answer is not clean and says which file

### Requirement: A check's result is opened when it starts and outlives its session
`OpenTerminal` SHALL open — or reopen — a `CheckResult` row for a declared check the moment it starts,
because the commit is only knowable then: a check that takes four minutes is a check whose repository may
have moved by the time it exits, and the honest answer is what it ran against. There SHALL be one row per
command per working directory, cleared and reused, so a check has a last result and not a history and
nothing on screen can show a green tick beside a command that is running. `ICheckSink` SHALL write only
how it ended and SHALL NOT create a row: an ending with no row SHALL be dropped rather than invented at a
commit nobody can name.

#### Scenario: Starting a check wipes the previous verdict
- **WHEN** a declared check is started again
- **THEN** its previous exit code is cleared at the start, not at the end

#### Scenario: An ending with no row is dropped
- **WHEN** a command that was started before the row existed, or in a folder since removed, reports its
  ending
- **THEN** nothing is written

### Requirement: Only a declared check counts, and staleness is measured against the commit
A command SHALL count toward the verdict only when it declares that its exit code counts (`"gate"` in
`.harness/commands.json`), and only a one-shot may declare it — something that never ends has no exit code
to count. The Checks tile SHALL distinguish no check declared, none run here, failing, stale, and green,
and SHALL say whether the result was recorded on the commit the directory is on now, naming that commit's
short hash. A check recorded on an earlier commit SHALL read stale rather than green.

#### Scenario: A tab is not a claim
- **WHEN** a declared command runs and does not declare `gate`
- **THEN** the verdict counts nothing from it

#### Scenario: A check from an earlier commit is stale
- **WHEN** the checks passed three commits ago and the directory has moved on
- **THEN** the tile reads stale and names the commit the result belongs to

### Requirement: A worktree's task is resolved from its runs first and its branch second

The system SHALL resolve which task a worktree is about from **the most recent run in that
directory** when there is one, and otherwise from a number proposed by **the branch name** and
confirmed by the task read. Both surfaces that need the answer — the Seam tile and the Issue tab —
SHALL take it from the same place, so the two can never name different tasks for one worktree.

A worktree's task is resolved from its runs first and its branch second — and the merge's
admission SHALL use that resolution: the task names the grant the merge requires, and the
grant's candidate names the head the merge expects. A merge without a live grant for the resolved
task SHALL be refused typed, and a candidate that moved after approval SHALL never merge on the
old expectation.

A run SHALL always win. Measured across this repository's own history, 3 of 26 worktrees with runs
carry a task key their branch does not name — a worktree gets reused — and in those cases the branch
would name the wrong issue over data that is already right.

The branch SHALL only **propose**: the candidate is used solely because the task read finds that task
for this folder's repository, resolved by repository and key together. A branch that names no number,
or names one the task read does not find, SHALL resolve nothing. **No branch-naming convention SHALL be
encoded**, because a product used by more than one loop may not impose one loop's naming.

The answer SHALL carry **where it came from**, and a key derived from a branch SHALL be shown as
derived rather than as something observed in that directory.

#### Scenario: A worktree the loop created resolves without a run
- **WHEN** a worktree on branch `change/275` has never had a run and the task read finds `#275` for this repository
- **THEN** its task is `#275`, and the answer says it was derived from the branch

#### Scenario: A run outranks the branch
- **WHEN** a worktree on branch `change/174` has a run whose task key is `#104`
- **THEN** its task is `#104`, and the answer says it came from a run

#### Scenario: A branch that names no task resolves nothing
- **WHEN** the branch carries no number, or a number the task read does not find for this repository
- **THEN** no task is resolved, and the surfaces draw their existing unknown states

### Requirement: The same facts are stated in one line, and an unknown is stated as unknown

The change header SHALL state, as **one mono line of facts joined by `·`**, everything the five
condition tiles stated — **Base** (how far the branch is ahead of and behind the trunk), **Merge**
(whether it merges clean), **Checks** (how the folder's declared checks ended, and where), **Tree**
(whether the working copy is clean) and **Seam** (whether the changed paths stay inside what the task
declared) — and one fact they did not: **the pull request** the branch has, its number and its state.
No fact SHALL be dropped for width.

A fact with no source SHALL be **stated as absent, in its own words**, never invented, never rounded
to zero and never drawn as a tick. The line SHALL keep the distinctions the sources keep: null counts
are "not compared" and not `0 ahead, 0 behind`; a pull request nobody could ask about is a different
sentence from a branch that has none; and the Seam SHALL keep all three of its readings — a
declaration compared, no task or no declaration to compare, and a body only part of which was read.

The Seam fact's task input SHALL be the resolved answer below, so a worktree that has never had a run
is compared against the issue its branch names rather than against nothing. Comparing against the
**wrong** issue SHALL remain worse than comparing against none: the fact keeps its unknown state for
every case the resolution does not settle.

**Nothing in the line SHALL be drawn as a success.** Colour SHALL mark only what is failing or worth
reading first, so there is no green in the row for a reader to mistake for a verdict, and the words
SHALL carry the fact on their own.

#### Scenario: A condition with no answer is unknown, not green
- **WHEN** the repository has no trunk to compare against, or this git cannot answer a merge dry run
- **THEN** the line names each as not compared and not asked, rather than showing a tick

#### Scenario: The seam tile waits for both its inputs
- **WHEN** the scope has just opened and either the task or the diff has not resolved
- **THEN** the seam fact reads unknown until both are in

#### Scenario: A worktree with no run still compares its seam
- **WHEN** a worktree has no runs and its branch names a task the task read finds
- **THEN** the seam fact compares the diff against that task's declaration instead of reading unknown

#### Scenario: A cut body is not a missing declaration
- **WHEN** only the front of the issue was read and the seam section falls past the cut
- **THEN** the line says the body could not be read whole, which is a different sentence from
  "declares no seam"

#### Scenario: A worktree with no pull request, no trunk and a dirty tree
- **WHEN** the branch has no pull request, the repository has no trunk, and the working copy is dirty
- **THEN** the line states three unknowns in their own words and one dirty tree, and no fact in it is
  a tick

### Requirement: The screen states the facts and the person's own checklist is what blocks

The screen SHALL NOT word a verdict over the facts. There SHALL be no "Ready to merge", no "Cannot
merge yet" and no sentence in which the product judges, from what it can see, whether a change may
land — the facts SHALL be stated and the reading SHALL be the reader's.

What is outstanding SHALL be **the person's own review checklist**: the count of ordered steps they
have not yet marked, said as a number beside the tab and again in the header. A machine's opinion of
five conditions SHALL NOT stand in for it.

**Every action SHALL stay available whatever the facts say.** A person overriding a stale check with
a reason is normal work, and a disabled button with no path is how a tool gets worked around. No
action on this screen SHALL be disabled by a fact.

#### Scenario: A failing check does not disable anything
- **WHEN** a declared check has failed, or the tree is dirty, or nothing has run here at all
- **THEN** the facts say so, no action is disabled, and no sentence tells the reader whether to merge

#### Scenario: The outstanding count is the person's, not the machine's
- **WHEN** every fact in the line is ordinary and no step has been marked
- **THEN** the screen says how many review steps are left rather than that the change is ready

### Requirement: What may hold an action back is a person's own checklist or a mechanical precondition, never a fact in the line

The rule that no action on this screen is disabled by a fact SHALL stand, and this requirement says
what is therefore left. Two things, and neither of them is the product's reading of the facts.

**A person's own ticks.** An action whose whole meaning is "the review is finished" MAY require the
review steps to be marked, because a tick is a statement somebody made and not a condition a machine
measured. Where an action does require them, the count outstanding SHALL be **said in words, beside
the control, in the place where it is held back** — a control greyed with no sentence is the thing
this screen removed, and the number alone is not the sentence. **Nothing in the header's actions box
SHALL be disabled at all**: an action with a precondition SHALL open and state it.

**A mechanical precondition of the operation itself.** An action MAY be refused by the server for a
fact about the branch that would make the operation *wrong* rather than merely inadvisable — work
that must be committed first, a change that must be archived first. Such a refusal SHALL name the
remedy, and where the remedy is a command it SHALL name that command. It SHALL be a refusal that
happens when the action is taken, and SHALL NOT become a disabled control.

**Neither of these is one of the facts in the line.** None of Base, Merge, Checks, Tree, Seam or the
pull request, as the line states them, SHALL disable or gate any action. A failing check, an unclean
merge, a branch behind the trunk and a pull request nobody could ask about SHALL all leave every
action exactly as available as they found it — a person overriding a stale check with a reason is
ordinary work, and the facts are stated so the reading can be the reader's.

#### Scenario: An unfinished checklist is said rather than merely shown
- **WHEN** an action requires the review steps and some are unmarked
- **THEN** the action is still reachable, and where it is held back the outstanding count is stated in
  words as the reason

#### Scenario: A mechanical precondition refuses when taken, and names its remedy
- **WHEN** an action is taken against a branch whose state would make the operation wrong
- **THEN** it is refused with a sentence naming the remedy, rather than having been disabled

#### Scenario: The facts in the line still disable nothing
- **WHEN** a check has failed, the merge is not clean, and the pull request could not be asked about
- **THEN** every action is as available as before, and none of the three is given as a reason to
  disable one

### Requirement: The merge's effect is idempotent and confirmed by observation
A squash-merge SHALL carry the expected head: the pull request's head read before the request,
refused typed on divergence, merged at the vendor, and confirmed after by reading the pull
request back. A lost merge response SHALL be settled by observation — a re-read answering
`MERGED` with the recorded head means it landed — and never by requesting the merge again. The
mechanical half of `/aio:sync` SHALL travel this same effect as the screen's button does: one
door, the person's own steps around it unchanged.

#### Scenario: Response lost, merge already landed
- **WHEN** the squash response was lost and the pull request re-reads `MERGED` with the expected
  head
- **THEN** the merge is confirmed by observation, and no second merge is requested

#### Scenario: The head moved before the request
- **WHEN** the pull request's head read differs from the expected head before the merge request
- **THEN** the merge refuses typed `candidate-changed`, nothing is merged, and the remedy names
  re-approval

#### Scenario: The vendor refuses
- **WHEN** GitHub refuses the squash in its own words — protection, review, conflict
- **THEN** the refusal passes through as the vendor said it, typed as ours only where the reason
  is ours

### Requirement: The close is a named, resumable effect

The merge's close — stop environment, remove worktree, drop branch — SHALL retain its resumable
semantics, and the whole merge effect SHALL be journaled: recorded before the merge request,
settled after the response or observation, with a derived effectId (task identity, folder and
operation hashed) so re-entry resumes rather than repeats. The effectId SHALL derive from the same
canonical PBI identity the admission resolved, whether the merge was launched by the PBI's identity
from the journey's `done` step or by `WorktreeId` from the folder screen, so the two entries journal
one effect for one merge. "Merge confirmed, cleanup pending" SHALL be a query the journal answers,
not a memory the person or the prompt holds.

#### Scenario: A retry resumes where it stopped

- **WHEN** the merge landed and the cleanup failed, and the person retries
- **THEN** the re-entry reads the pull request as merged, resumes the cleanup, and merges nothing
  a second time

#### Scenario: The journal names what finished

- **WHEN** the cleanup is partially done
- **THEN** the journal's record settles to the state that names what happened, and a pending
  cleanup is visible as pending

#### Scenario: A merge launched by identity and one by worktree share one effect

- **WHEN** a merge is launched by the PBI's identity and the worktree it resolves is the one a
  worktree-scoped launch would have named
- **THEN** both derive the same effectId, the journal recognises the second as the first's resume,
  and the vendor merge is not requested a second time

### Requirement: The admission refuses a merge when the PBI it resolves to is held

The grant admission SHALL read the PBI's position through `IPbiSource.GetPosition` for the identity
it resolved (provider, host, repository, key) before the grant lookup, and SHALL refuse typed when
the read answers `Held: true`. A hold is orthogonal to the grant and to the head: a valid grant on
the approved head with an active hold is a merge that SHALL NOT happen, and reading the hold first
short-circuits the grant and vendor round-trips a hold forbids. The refusal SHALL name the remedy —
release the hold on the PBI before merging — in the handler's sentence, the way every other refusal
does, and SHALL carry a typed reason the closed attention vocabulary projects. A position read that
fails SHALL be refused as the vendor's own words rather than read as not-held: merging past an
unknown position is what the admission exists to stop, on the same terms an unknown grant is.

The hold SHALL be read from the vendor through the seam — never from the snapshot, which a write
empties — so a hold applied after the PR's approval and before the merge is caught at the admission,
not inferred from a stale cache. The grant admission's existing `Missing` and `CandidateChanged`
refusals SHALL stand unchanged beside the new `HeldByPbi` refusal; the admission answers one
question with five typed answers rather than four, and the handler's switch stays the single place
sentences are written.

#### Scenario: A held PBI is refused before the grant is read

- **WHEN** the PBI the merge resolves to carries `status:holding` at the vendor
- **THEN** the admission refuses typed `HeldByPbi`, the grant is not read, nothing is merged, and
  the refusal names releasing the hold as the remedy

#### Scenario: A hold that appeared between approval and merge is caught

- **WHEN** the pull request was approved and the grant is live, and the PBI was held between the
  approval and the merge's admission
- **THEN** the admission refuses `HeldByPbi`, the live grant does not override the hold, and
  nothing is merged

#### Scenario: A position read that fails is a refusal, not a not-held

- **WHEN** the position read cannot be put or the vendor refuses
- **THEN** the admission refuses with the vendor's own sentence, and is not read as `Held: false`

#### Scenario: An unheld PBI proceeds to the grant

- **WHEN** the position read answers `Held: false`
- **THEN** the admission proceeds to the grant lookup unchanged, and the hold is not read again

#### Scenario: A folder that never adopted the registry still reads the hold

- **WHEN** the folder's tunables declare no policy pin, so the grant admission returns the pre-
  adoption no-op answer
- **THEN** the hold is still read for the PBI the merge resolves to, and a held PBI is refused
  before the no-op grant, because the hold is orthogonal to the registry's adoption

### Requirement: The merge is launched by the PBI's identity from the journey's done step

The merge use case SHALL accept the PBI's identity — source, repository and key together — as the
entry the journey's `done` step launches, resolving the worktree from that identity and the folder as
the launcher's detail, and cleaning it up after the merge is confirmed. A PBI-keyed entry SHALL sit
beside the folder-scoped, `WorktreeId`-keyed route the worktree screen keeps; the journey's step
SHALL NOT launch the folder-scoped route with a worktree it had to invent. The permission the merge
requires SHALL be unchanged — `WorkspacePermissions.ManageWorktrees` — and an undeclared entry is
refused, the way an undeclared request is anywhere else.

The worktree SHALL be resolved by the PBI's identity and the folder, not by a branch-naming
convention: the admission already resolves the identity from the folder's remote and the worktree's
branch, and the launcher resolves the worktree from the same identity the admission will use, so a
PBI with one worktree in flight is launched against that one and a PBI with none is refused with the
remedy rather than invented. The effect identity (`MergeEffectId.Derive`) SHALL derive from the same
canonical identity the admission resolved, so a merge launched by identity and a merge launched by
worktree journal as one effect when they are one merge.

#### Scenario: The journey's done step launches the merge by identity

- **WHEN** the journey's `done` step offers the merge and it is taken
- **THEN** the request carries the PBI's identity (source, repository, key) and the folder, the
  worktree is resolved as the launcher's detail, and the operation is the same squash the loop's
  merge node and `/aio:sync` run

#### Scenario: A PBI with no worktree in flight is refused with a remedy

- **WHEN** the journey's `done` step offers the merge for a PBI whose folder holds no worktree for
  its identity
- **THEN** the launch is refused naming that no worktree is in flight for this PBI, rather than the
  launcher inventing one

#### Scenario: The folder-scoped route is unchanged

- **WHEN** the worktree's `ChangeTab` launches the merge with a `WorktreeId`
- **THEN** the folder-scoped route behaves as it did, and the worktree screen's contract is
  untouched

#### Scenario: A merge launched by identity and one by worktree journal as one effect

- **WHEN** a merge is launched by the PBI's identity and the worktree it resolves is the same a
  worktree-scoped launch would have named
- **THEN** the derived effect id is the same, and a retry from either entry resumes rather than
  repeats

