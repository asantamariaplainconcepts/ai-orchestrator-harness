# read-a-run Specification

## Purpose
Reading a run: the History table, the sidebar's answer to what is running, and the Run screen that loads
the history before it joins the live stream. A run's screen is replayed as a screen rather than as an
animation with no background, because a TUI's bytes are deltas. Reverse-engineered from the shipped code,
not negotiated ahead of it.
## Requirements
### Requirement: One query answers history, a folder's runs, and what is running now

The system SHALL expose `GET /api/runs` (`Features/Runs/UseCases/ListRuns.cs`,
`Access.FiltersToCaller`) as one query serving a worktree's journey header and sidebar answers
filtered by folder and by working directory, and — filtered to what has not finished — what is running
right now. The unscoped History screen is deleted; the query's unscoped shape remains available to
spend reporting. The rail SHALL match those against its branches by working directory: a run knows
the directory it works in, a branch is a directory, and that join is the liveness. The transcript SHALL
be excluded by a **projection** rather than by omission, because the log is an owned collection that
EF would otherwise fetch with the run, and a history of a hundred runs would carry every line any
agent ever printed to draw a table that shows none of them.

A run row the PBI detail matches to its PBI SHALL join by the PBI's **full identity** — the source,
repository and key — and not by key alone: with two repositories in one scope, `#12` is two PBIs, and
first-match by key would offer the other repository's run as this PBI's execution. A run row carries
its folder's path rather than a repository, so the join resolves the run's folder to the repository it
names and compares that against the repository the PBI's own address names, the way every folder
question on the screen is already answered. Where the PBI cannot be attributed to a repository at all,
the entry SHALL say so rather than offering a run whose repository is unknown as if it were confirmed.
The run DTOs' identifier for the PBI a run belongs to SHALL be spelled `key`, the context carried by
the DTO it sits in rather than repeated in the field's name.

#### Scenario: The history table does not fetch the transcript

- **WHEN** a worktree's screen loads
- **THEN** no transcript lines are read

#### Scenario: The journey header does not fetch the transcript

- **WHEN** a worktree's screen loads
- **THEN** no transcript lines are read

#### Scenario: The rail lights the branch a run is working in

- **WHEN** a run is working in a worktree
- **THEN** the sidebar row for that branch says a run is working in it

#### Scenario: A run joins its task by identity

- **WHEN** two repositories in one scope each carry a PBI with the same key and only one has a run
- **THEN** the PBI detail of the repository with the run offers it, and the other PBI's detail does not

#### Scenario: A run whose task names no repository is not a confirmed match

- **WHEN** a PBI cannot be attributed to a repository
- **THEN** the execution entry names the absence of an attribution, rather than offering any run as confirmed by identity

### Requirement: A live run carries its screen as a frame
A live run SHALL carry the current screen of its terminal as a frame, tracked in memory from the same bytes
the transcript persists — one tracker per run, created on the first bytes seen and sized to what the agent
was last told, resized when a viewer's terminal reports its size. A run with no viewer SHALL still track,
at the last size anybody set. A finished run SHALL carry **no** frame, and finishing SHALL forget it. The
frame SHALL NOT be persisted: `RunLogLine` is the store of record, a frame is derivable state, and a
server restarted mid-run SHALL degrade to the ring tail said out loud as truncated rather than to invented
history. The screen SHALL be modeled by a complete VT emulator rather than a hand-written subset of
escape sequences, so that a TUI which repaints with scroll regions, insert/delete-line, erase-chars,
tab stops or autowrap produces the screen the agent drew. Only stdout SHALL feed the tracker; stderr stays
raw and out of the screen.

#### Scenario: A late attacher gets one clean frame
- **WHEN** a browser attaches to a run already ten minutes in
- **THEN** it receives the screen as it stands, not a replay of deltas with no background

#### Scenario: A TUI that repaints with scroll regions is shown whole
- **WHEN** the agent's TUI maintains its list through a DECSTBM scroll region
- **THEN** the serialized frame shows the list as the agent drew it, with no shifted or duplicated lines

#### Scenario: Insert and delete line move what was there
- **WHEN** the agent's TUI opens or closes a list entry with IL or DL
- **THEN** the serialized frame shows the entries above and below moved, not overwritten in place

#### Scenario: Finishing forgets the frame
- **WHEN** a run finishes
- **THEN** it carries no frame

### Requirement: The task detail answers where the loop stands, for its own task

The task detail SHALL draw, for the PBI it is about, the loop's walk —
the stages and transitions observed for that PBI — grouped by the PBI's
full identity across the working directories its runs used, and SHALL NOT
require a working directory to answer. The walk it draws SHALL be the same
reading the worktree's journey header draws — the declared machine with
its observed position, from `journeyFor`'s resolution — when the PBI's
newest run's directory carries the declared loop, and the observed command
words in run order when it does not, which is the reading closed journeys
already honour. It SHALL NOT derive a second interpretation of the loop:
no parallel state machine, no lifecycle-label-to-stage mapping, and no
list of `PbiStatus` values standing in for the loop's declared nodes.

The PBI's own position SHALL be the Connector's read (`IPbiSource`'s
position, the fact #449 serves), and the runs' transitions SHALL be the
observed history: the label is the claim of where the PBI stands, the
dispatch is the observation of what moved the loop, and where the two
disagree the walk shows the Connector's position as the current pointer
and the runs' transitions as the walked history rather than collapsing
the two.

The detail SHALL draw the walk as a **vertical spine of stages**, one node
per declared stage, ordered as the loop declares them. Each stage SHALL be
marked with one of: **reached** (a transitioned run observed it, or the
loop's declaration reached it through a script the harness runs), **current**
(the Connector's position answers this node and the run on it has not
finished), **held** (the Connector reports the hold beside the position),
or **ahead** (declared, not yet observed). A stage's node state SHALL NOT
be inferred from the PBI's lifecycle label.

A stage the loop reached through a `bash` script the harness runs itself
— a node that produces no run row — SHALL be marked as reached, SHALL draw
no run card under it, and SHALL NOT be invented as a run. A stage an
agent run reached SHALL carry that run's card beneath it. The spine SHALL
make the distinction visible: a stage with evidence (a run) vs a stage
reached by declaration (no run).

Runs SHALL be grouped under their stage by the run's command word to the
node's (the correlation `journeyFor` already computes), chronological
within the stage. A run whose command word no declared node carries SHALL
be drawn off-loop, beside the spine, as `handRuns` already honours — the
walk happened even if the declaration moved on.

The current stage SHALL name the next permitted action or the reason for
the block, read from the Connector's position and hold answer and the
loop's own declared next action — not re-parsed from labels in the UI.
A held stage SHALL name the hold with the hold's own words.

#### Scenario: The walk for a task whose directory carries the loop

- **WHEN** the PBI's newest run worked in a directory that carries the declared loop
- **THEN** the detail draws the machine with the observed position marked, the same reading the worktree's header draws

#### Scenario: The walk for a task whose directories are gone

- **WHEN** none of the PBI's runs' working directories exists any more
- **THEN** the detail draws the observed command words in run order, drawn off-loop, and no position is invented

#### Scenario: The same key in two repositories

- **WHEN** two repositories each carry a PBI with the same key and runs
- **THEN** each PBI's walk shows only its own runs, and neither directory's history mixes into the other PBI's walk

#### Scenario: The Connector's position is the current pointer

- **WHEN** the Connector answers a position and the runs show transitions up to it
- **THEN** the spine marks that node as current with the hold beside it when the Connector reports one, and the runs' transitions are drawn as reached stages beneath their nodes

#### Scenario: A bash stage reached without a run

- **WHEN** the loop reached a `bash` node the harness runs itself and no run row exists for it
- **THEN** the spine marks the stage as reached, draws no run card under it, and does not invent a run

#### Scenario: A run whose command word no node declares

- **WHEN** the PBI's runs include one whose command word no declared node carries
- **THEN** it is drawn off-loop beside the spine, not as a journey mark, and the walk is not lost for the declaration moving on

#### Scenario: The label and the dispatch disagree

- **WHEN** the PBI's lifecycle label names a position and the runs' transitions show another
- **THEN** the spine shows the Connector's position as current and the runs' transitions as walked history, and names the label as the workflow's claim, not a position

#### Scenario: The current stage names the next action or the block

- **WHEN** the PBI is on a stage that is not held
- **THEN** the stage names the next permitted action from the loop's own declared next action for that position

- **WHEN** the PBI is held on a stage
- **THEN** the stage names the hold with the hold's own words, and no action offered behind it silently

### Requirement: A stage appears only when observed

A stage or transition SHALL appear on the walk only when a fact observes
it — a run's transition, a command's transcript, a recorded step — and
SHALL NOT be inferred from the task's lifecycle label or a known
declarative progress. The task's own lifecycle SHALL be named as what it
is — the workflow's claim — beside the walk, and where no node is
observed the walk SHALL say no transitioned run rather than drawing a
position, a percentage or a stage.

#### Scenario: A lifecycle label is not a stage

- **WHEN** a task carries `status:implementing` and its runs show no transitioned node
- **THEN** the walk names the lifecycle as the workflow's claim, draws no position and says no transitioned run

#### Scenario: A run's transition marks the stage

- **WHEN** the task's latest transitioned run names the node it dispatched
- **THEN** the walk marks that node as where the journey stands, with that run's facts — command, activity, duration

### Requirement: Several runs are distinguished and chosen, never first-matched

Where a PBI has several runs, the walk SHALL distinguish runs that have
not finished from runs that have, and each run SHALL be named with what
is known of it — its agent, its last activity, its state, where it worked,
its duration and its cost — and opened in its existing terminal or
transcript by the person's choice. A run that has not finished SHALL lead
the stage it belongs to; finished runs SHALL follow, chronological within
the stage. Observing SHALL NOT start, resume or provision
anything: reading the walk and opening what exists are the whole of it.

A run whose environment is gone SHALL open its persisted transcript
through `read-a-finished-transcript`, and the live frame of a run whose
process is gone SHALL be named unrecoverable rather than invented.

#### Scenario: An active run and historical runs

- **WHEN** a stage carries a run that has not finished and two that have
- **THEN** the unfinished run leads the stage, the finished runs follow chronological within it, and each opens its own existing transcript

#### Scenario: Observation never computes

- **WHEN** the walk is opened for a run whose environment is stopped or gone
- **THEN** nothing is resumed, provisioned or spawned, and the persisted facts are what the walk shows

#### Scenario: A finished run opens its persisted transcript

- **WHEN** a run whose process is gone is opened from the walk
- **THEN** its transcript is read through `read-a-finished-transcript`, and no live frame is invented for it

### Requirement: Disconnection, staleness, absence and unknown are four named states

The walk SHALL name its connection state, and the four SHALL NOT collapse
into one another: **disconnected** keeps the last observation and its
time without asserting the process's current state, **stale** data is
named stale with the stamp it was read at, **absence** is a confirmed
answer that there is nothing, and **unknown** says the fact is not
known. A disconnected run SHALL NOT be marked finished, failed or
cancelled by the disconnection alone.

#### Scenario: A disconnected run is not a finished one

- **WHEN** the connection to a run's environment drops mid-run
- **THEN** the walk shows disconnected with the last observation and its time, and the run's state is unchanged by the drop

#### Scenario: An environment withdrawn keeps its persisted history

- **WHEN** a task's working directories are gone and its runs' transcripts were persisted
- **THEN** the walk reads the persisted history without recreating any environment, and the parts not persisted are declared unrecoverable

### Requirement: A run names its environment, and a remote run keeps its destination

A run's facts SHALL name the environment it works in — a local working
directory or a remote environment — and a run in a remote environment
SHALL open its existing session from the surfaces that open runs, rather
than being refused a destination for having no local path. The local
terminal, transcript and session behaviour SHALL change nothing for local
runs: the remote environment is another place a run works, not a
replacement for the places it works today.

#### Scenario: A remote run opens from an observation surface

- **WHEN** a run that works in a remote environment is opened from the task detail or the walk
- **THEN** it opens its own session in that environment, through the observe-a-remote-agent-session capability's two doors

#### Scenario: A local run is unchanged

- **WHEN** a run works in a local working directory
- **THEN** its terminal, transcript and session behave exactly as they do today

### Requirement: The PBI's runs are read by identity, once at the seam

The system SHALL serve a read of a PBI's runs by the PBI's full identity — source, repository and
key together — joining the run's folder to the repository the PBI's own address names, the way every
folder question on the screen is already answered. The read SHALL NOT filter by a working directory:
a run belongs to a PBI when its key matches and its folder is the folder the repository resolved to,
and a run in a directory that no longer exists still belongs to the PBI. The read SHALL be served by
the Runs module beside `ListRuns`, reading the same schema, so no module crosses another's schema.

The read SHALL name four states, and the four SHALL NOT collapse into one another: **loading**
while the answer is on its way, **error** when the query failed with the failure's own words,
**absence** when the query answered and the PBI has no runs, and **unattributed** when the PBI
cannot be attributed to a repository and a run's repository cannot be confirmed. A PBI that cannot
be attributed to a repository SHALL say so rather than offering a run whose repository is unknown as
confirmed. A failed read SHALL NOT start a run: dispatch is a controlled decision governed by
`dispatch-observation`, and navigation is not a fallback path into it.

A read for a PBI whose working directory is gone SHALL answer from the runs persisted by identity
and `WorktreeName`, because the closed journey is still consultable, and the parts not persisted
shall be declared unrecoverable rather than invented.

#### Scenario: A read by identity answers a PBI's runs across folders

- **WHEN** a PBI's runs worked in several directories, one of them gone
- **THEN** the read answers all of them by identity, and the gone directory's runs are not lost

#### Scenario: Absence is a confirmed answer

- **WHEN** the read answers and the PBI has no runs
- **THEN** the spine says so rather than drawing a position, and no run is started

#### Scenario: An unattributed PBI is not a confirmed match

- **WHEN** a PBI cannot be attributed to a repository
- **THEN** the read names the absence of an attribution, rather than offering any run as confirmed by identity

#### Scenario: A failed read is an error, not an absence

- **WHEN** the runs query refuses or fails
- **THEN** the spine shows the error, and no run is started as a fallback

#### Scenario: A closed journey's runs are read by identity

- **WHEN** a PBI whose worktree is removed is opened
- **THEN** its runs are read by identity, the live frame of a process that is gone is named unrecoverable, and no environment is recreated

