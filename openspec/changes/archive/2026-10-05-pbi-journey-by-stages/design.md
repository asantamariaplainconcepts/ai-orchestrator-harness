## Context

The PBI detail was built around the launch form: `PbiDetailPane` paints the body and a `LoopFlow`
row when a live task exists, and only falls back to `JourneyWalk` when the mirror row is gone. The
richest reading that exists — `PbiWalk`'s `journeyFor`, which resolves the declared loop against the
runs' command words and marks live, held, walked and ahead — lives in the `pbis` screen and is not
the path the detail pane takes. The Connector (#449) now serves the position and the hold as facts
read from the vendor, and the loop declares 18 nodes — of which the bash ones (overlap, claim,
risk, gate, tripwires, evidence, merge) produce no run row, so a position reached by a script is
indistinguishable from a stage nobody reached. The worktree that owns `workingPath` and the
`PbiLoopCommandButton` dies with its branch; the runs survive by `WorktreeName` and key. There is no
endpoint that answers a PBI's runs by identity — `PbiWalk` filters them client-side, joining a run's
`folderPath` to the repository the PBI's address names.

The reference for the screen is `design-import/pbi-redesign.html` on `origin/design/pbi-redesign`:
a vertical spine of stages, with `reached` / `current` / `held` / `ahead` node states, runs grouped
under their stage by correlation, and the current stage naming the next action or the reason for
the block. The prototype's `STAGES` are the seven `PbiStatus` of the wire; the real spine reads the
declared loop's nodes, not a derived lifecycle list.

## Goals / Non-Goals

**Goals:**
- The PBI detail opens the journey as its centre, the same reading before and after a PR exists and
  before and after a worktree is gone.
- One resolution for the journey: `journeyFor`'s, extended to mark a stage a loop script reached
  without inventing a run for it.
- The position and hold are the Connector's answer (#449), drawn on the spine without re-deriving
  them in the UI.
- A PBI's runs are read by full identity once at the seam, the join the browser does today.
- The four connection states (loading, error, absence, unattributed) and the bash-stage-without-a-run
  distinction are named as themselves.

**Non-Goals:**
- The Review, Proposal and Evidence tabs of the detail (#454/#455) and the sessions tray (#453) are
  not in this change.
- Synthesising tasks hijas from runs, or any speculative storage for them; they appear only when a
  vendor integration truly provides them, which none does today.
- A new schema. The run row already carries `WorktreeName`, `TaskKey` (#448), `folderPath`, cost
  and timing; the journey stays a projection over facts the runs already own.
- Re-deriving the loop's declared machine, the position translation, or the hold. Those are #449's
  and the declared-loop-machine's; this change composes their reads.

## Decisions

### 1. The spine is `journeyFor`'s resolution, extended — not a second interpretation

The screen reads the declared loop (`IDeclaredLoopFile`) and the PBI's runs, and resolves the
journey with the same `journeyFor` the worktree's header and `PbiWalk` already use. The
`live` / `held` / `walked` / `ahead` kinds become the spine's node states (`current`, `held`,
`reached`, `ahead`), and `no-loop` / `none` fall back to the observed command words in run order —
the reading `ClosedJourneys` and `JourneyWalk` already honour. No parallel state machine is
introduced, and the PBI's lifecycle label is named beside the walk as the workflow's claim, never a
position on the machine. This is the rule `read-a-run` already states ("The task detail SHALL draw…
the same reading the worktree's journey header draws"); the decision is to make the detail actually
follow it.

**Why not derive the spine from the `PbiStatus` list the prototype's `STAGES` uses:** that list is
the seven statuses of the wire, not the loop's declaration, and it would rebuild the
position-from-label derivation the Connector was built to remove. The declared loop's nodes are the
stages; the Connector's position answers which one is current.

### 2. A stage reached by a loop script is reached without a run

Several nodes of the declared loop are `bash` steps the harness runs itself (overlap, claim, risk,
gate, tripwires, evidence, merge). They produce no row in `runs`, so a position they reached has
no run to hang under it. The spine marks such a stage as **reached** (the loop's own `status` for
the node, read from the declared graph's per-node `x-harness.status`) but draws **no run card**
under it, and a person sees the stage was reached without the surface lying that an agent ran there.
A stage an agent run reached carries that run's card under it. The distinction is visible: a stage
with evidence (a run) vs a stage reached by declaration (no run), and neither is invented.

**Why derive the reached-without-a-run from the declared graph's `x-harness.status` rather than
from a new persisted fact:** the loop already declares what each node does (`command` vs `bash`),
and the Connector already reads the position the vendor reports. Persisting a "stage reached"
timestamp would duplicate the position the vendor owns and go stale the moment a label moves; the
declared graph and the Connector's position are the two facts, and the join is a read.

**Why not invent a synthetic run for the bash steps:** that would put fabricated rows in `runs`,
the record a person reads as "what an agent said", and the first one a stopped run sat beside would
be indistinguishable from a real failure. The spine says a stage was reached without claiming an
agent ran there.

### 3. The position and hold are the Connector's, read once

The current stage and the hold are `IPbiSource`'s position read (#449), not a derivation from the
runs' command words. `journeyFor` continues to resolve the *observed* position from the runs (for
the `walked` stages and for the off-loop runs), but the *current* pointer is the Connector's answer:
the vendor's labels are the claim of where the PBI stands, and the run's dispatch is the
observation of what moved the loop. Where the two disagree, the spine shows the Connector's
position as the current pointer and the runs' transitions as the walked history, the way
`pbi-position-connector` and `read-a-run` already state ("the position and hold exactly as the
Connector's read answered, without mapping, deriving or re-parsing anything in the UI").

This keeps the bash-stage case honest: a `gate` node the loop script reached reports `done` at the
vendor (the Connector reads it), so the spine marks it current or reached by the Connector's answer
even though no run row exists for it.

### 4. A PBI's runs are read by identity, once at the seam

Today `PbiWalk` calls `useRuns(repo?.path, …)` and filters client-side by `run.key === task.key &&
run.folderPath === repo.path`. The join is correct (it is #431's identity join) but it is done in the
browser, and a closed journey whose worktree is gone still has its runs because they were keyed by
`TaskKey` (#448). A read of runs by PBI identity is served: source, repository and key together,
joining the run's `folderPath` to the repository the PBI's address names, the way every folder
question on the screen is already answered. The four states — loading, error, absence,
unattributed — are named as themselves, and a PBI that cannot be attributed to a repository says so
rather than offering a run whose repository is unknown as confirmed.

**Why not keep the client-side filter:** the filter works, but it pulls every run in the folder to
find the few that are this PBI's, and a closed journey's runs live in a folder the worktree no
longer names — so the closed case has to read by key anyway. Serving the join once, by the identity
the rest of the screen already uses, makes the two cases one path and stops the browser from
holding a folder's worth of runs to draw one PBI's spine.

**Why by identity and not by worktree:** a worktree is the instance of a loop for one issue, and a
run is one transition of it. The runs survive the worktree by `WorktreeName` and key; reading by
identity is what makes the closed journey consultable.

### 5. The journey survives the worktree by reading what was persisted

When the worktree is gone, `workingPath` and the `PbiLoopCommandButton` are not drawn — they died
with the branch. The spine, the runs and their facts are drawn from the runs persisted by identity
and the loop declaration read from the checkout (the trunk's declaration, the same fact the executor
reads). The chat/terminal views of a run are the run's own transcript, read through
`read-a-finished-transcript` for a finished run and the live frame for one that has not finished;
neither requires the worktree's directory. What was not persisted — the live frame of a run whose
process is gone — is named unrecoverable rather than invented.

### 6. The detail's layout: journey is the centre, launch is an action of the step

The detail's body is the spine, with the issue body and thread beneath or beside it rather than
above it. Launching an agent is an action offered by the step the loop is currently on (or the next
permitted one), not a form that dominates the heading. This is the layout the prototype proves and
the issue's acceptance criteria state ("the form ceases to dominate the screen").

## Risks / Trade-offs

- **A bash stage the Connector reports as `unknown`** → the spine draws it as ahead with the
  reason, the way `pbi-position-connector`'s "unknown is its own state" already requires. The
  decision does not invent a position; it shows the gap.
- **A run whose command word no declared node carries** (a hand-typed `/aio:foo`, or a node the
  loop was edited to remove) → drawn off-loop, beside the spine, the way `journeyFor` already
  draws `handRuns`. The walk happened even if the declaration moved on.
- **Two repositories carrying the same key** → the identity join resolves the run's folder to the
  repository the PBI's address names, not first-match by key; a run whose folder names no known
  repository is named unattributed rather than confirmed.
- **The Connector's position and the runs' observed position disagree** → the spine shows the
  Connector's as the current pointer and the runs' as walked history. This is the honest reading:
  the label is the claim, the dispatch is the observation, and the difference is what the question
  "why is this not moving" answers.
- **Reading runs by identity at the seam** → the read touches the Runs module's schema, done through
  the Runs module's own read (the way `ListRuns` already serves the worktree's journey header), so
  no module crosses another's schema. The join is composed above the seam from the reads each
  module serves.

## Migration Plan

No migration. The run row carries `WorktreeName`, `TaskKey` (#448), `folderPath`, cost and timing;
the journey is a projection. The read of runs by identity is a new query served by the Runs module
beside `ListRuns`, reading the same schema. The frontend converges `PbiDetailPane`, `PbiWalk`,
`LoopFlow` and `JourneyWalk` on one spine component; `journey.ts` gains the
reached-without-a-run distinction. The three shared files (`contracts.ts`, `queries.ts`, `en.ts`)
gain the journey's contract, query and copy, appended to the PBI screen's block.

Rollback is the branch not merging; nothing in `master` changes until it does.

## Open Questions

(None at the spec level. The check describes the verification against a PBI in several states — in
progress with several runs and a failure, without a PR, and closed with its worktree gone — in both
themes and narrow width, before implementation.)
