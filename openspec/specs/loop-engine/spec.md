# loop-engine Specification

## Purpose
The engine takes every open PBI of every folder through its repository's loop: it observes the PBI at
the vendor, plans what the loop asks for next, and applies it. It is the only part of the product that
moves a label, opens or merges a pull request, or starts a loop run, so the loop's decisions have one
owner and one journal. Reverse-engineered from the shipped code
(`src/modules/Loop/AiOrchestrator.Modules.Loop/Engine/`), not negotiated ahead of it.
## Requirements
### Requirement: The engine runs per folder, on settings read before every wait
The engine SHALL keep its settings in its own table, `loop_settings`: the seconds between two passes and
a switch per repository. A setting nobody stored SHALL read as configured: the interval as
`Loop:IntervalSeconds` (60 by default) and every switch as `Loop:Enabled`, so with `Loop:Enabled` unset
every folder starts off. The schedule SHALL read the settings before every wait and run one pass as
soon as a folder is on, then one every interval counted from the last pass, walking only the folders
that are on; with no folder on, or the interval at zero or below, nothing SHALL be scheduled. A change
saved through the API SHALL wake the schedule, so it takes effect on the next pass without a restart.
`POST /api/loop/run-once` SHALL run one pass by hand over every folder, on or off, and answer
`{folders, pbis, skipped, written}`. Passes SHALL run one at a time, and a pass that throws SHALL be
logged and kept as the last pass's failure without stopping the schedule.

#### Scenario: A checkout started to look at a screen does not work its issues
- **WHEN** the host starts without `Loop:Enabled` and nobody turned a folder on
- **THEN** no pass runs until somebody calls `POST /api/loop/run-once` or turns a folder on

#### Scenario: A new interval counts from the last pass
- **WHEN** the last pass ran at 09:00 with a 60-second interval and a person saves 600 seconds
- **THEN** no pass runs at 09:01, and the next runs at 09:10

### Requirement: A folder's engine is switched from the API, and the switch is journalled
`GET /api/loop/settings` SHALL answer `{intervalSeconds, folders: [{path, repository, on}]}` for the
folders `IFolderDirectory` lists. `PUT /api/loop/settings {intervalSeconds}` SHALL store an interval
between 10 and 86400 seconds and answer the same shape. `PUT /api/loop/engine {repository, on}` SHALL
store the switch of the folder holding that repository, compared without case, and answer
`{repository, on, written}`; a repository no folder holds SHALL be 404 `Loop.NoFolder`. Turning on a
folder whose `.aio/loop.yaml` is missing or breaks its rules SHALL be 409 `Loop.NoLoop` with that
problem, storing nothing and writing no line, because the passes would skip it; turning off SHALL
always be taken. A switch that
changes SHALL write a `Switched` line to the journal; setting what is already set SHALL write none.
Turning a folder off SHALL stop nothing: the scheduled passes leave its PBIs unread, so no script or run
starts there, its live runs go on, and what they end with is applied by the first pass after it is on
again.

#### Scenario: One folder off, the other on
- **WHEN** `owner/repo` is off with a live run and `owner/other` is on, each with a PBI in
  `ready-for-proposal`
- **THEN** the scheduled pass starts a run for `owner/other`'s PBI, starts nothing in `owner/repo`, and
  its live run is still live after the pass

#### Scenario: A folder without a loop cannot be turned on
- **WHEN** a person turns on `owner/bare`, whose checkout has no `.aio/loop.yaml`
- **THEN** the answer is 409 with "No loop declared. Add .aio/loop.yaml to the repository.", and the
  folder stays off

### Requirement: A pass walks every folder that has a loop, and only its own PBIs
A pass SHALL walk the folders `IFolderDirectory` lists, skip one whose `.aio/loop.yaml` is missing
or breaks its rules, whose vendor has no adapter, or whose vendor does not answer, and say why in the
answer's `skipped`. For each remaining folder it SHALL list the open PBIs at the vendor. It SHALL ignore
a PBI whose state the loop does not declare, and a PBI assigned to anybody other than the identity the
control plane acts as, compared without case.

#### Scenario: A PBI assigned to another person stays out of the loop
- **WHEN** an open PBI in a declared state is assigned to a login that is not the control plane's
- **THEN** the pass leaves it alone and writes nothing for it

#### Scenario: A folder without a loop is skipped with its reason
- **WHEN** a folder's checkout has no `.aio/loop.yaml`
- **THEN** the pass lists the folder under `skipped`, saying no loop is declared, and goes on with the others

### Requirement: Each state runs its scripts, then its agent or its score
For a PBI in a declared state, the planner SHALL run the state's scripts in order, with the score last,
all in one place from `IRunRequests.OpenScriptPlace` on the branch `change/<id>` and never in the
folder's own checkout. The place SHALL be prepared once, by the first script that runs, and retired once
after the last that ran, also when a script fails or waits and the rest do not run (#751). The first script that fails SHALL hold the PBI with `<script> failed: <why>`; the first that
answers wait SHALL leave it waiting; an outcome SHALL lead through `on`. When several scripts answer an
outcome, the first whose transition holds SHALL be the state's, and otherwise the first (#733), so the
evidence's `blocked` holds a PBI whose gate was green. A state with an agent SHALL
then start a loop run with the state's provider and model, or its role's, and the command
`<command> <pbi id>`, whose outcomes are the keys of `on`, or `next`. A state with only scripts and a
`next` SHALL move on. A state with a command and no agent SHALL start with its folder's default agent
and model, a model the state names winning; when the folder has no default agent it SHALL hold, saying
so.

#### Scenario: A state that names no agent runs the folder's
- **WHEN** a state declares `command: /aio:implement` and no `agent`, and its folder defaults to
  `claude-code` with `claude-fable-5-1`
- **THEN** the engine starts `claude-code` with `claude-fable-5-1` and `/aio:implement <id>`

#### Scenario: A passing claim assigns the issue
- **WHEN** `claim` answers that this clone holds the issue
- **THEN** the engine assigns the PBI to its own identity and reads the assignment back, and a mismatch
  fails the claim

#### Scenario: The evidence runs after a green gate
- **WHEN** `gate` answers green in `verifying`
- **THEN** `evidence` still runs before the state's outcome is taken, because an outcome does not stop
  the script list

#### Scenario: The gate's reports reach quality
- **WHEN** `gate` writes its coverage report in `verifying` without committing it, in a folder git
  ignores
- **THEN** `evidence` and `quality` run in the same worktree and `quality` finds the report

#### Scenario: A script that holds still retires the place
- **WHEN** the first script of a state fails
- **THEN** no other script runs and the place it was prepared for is retired once

#### Scenario: Blocked evidence after a green gate
- **WHEN** `gate` answers green in `verifying` and `evidence` answers `blocked` with exit 1
- **THEN** the PBI holds with `verifying`'s `blocked` hold and the evidence's why, and is not marked ready

#### Scenario: A loop.yaml that names no evidence outcome
- **WHEN** `verifying`'s `on` names only `green` and `red`, `gate` answers green and `evidence` exits 1 answering `blocked`
- **THEN** the PBI holds with `evidence failed: <its why>`; had evidence answered `skipped` with exit 0, the gate's green would move it

### Requirement: The WIP limit bounds the live agent runs
Before starting an agent, the planner SHALL count the live loop runs and SHALL wait with
`WIP n of m` when they reach the loop's `wip`. Manual runs SHALL NOT count, and a state that only runs
scripts SHALL NOT wait on the limit.

#### Scenario: A fifth run waits
- **WHEN** four loop runs are live and the loop's `wip` is 4
- **THEN** a PBI that would start an agent waits, and the journal says `WIP 4 of 4`

### Requirement: An agent that does not trust the folder is not started there
Before starting an agent run, the engine SHALL ask that CLI's adapter whether it trusts the folder.
When it does not, the engine SHALL NOT start the run and the PBI SHALL wait with the adapter's
sentence, which names the folder and says to run the CLI there once and accept its question, written
once however many passes the wait lasts. The engine SHALL NOT answer the question for the person. A CLI
whose adapter asks no such question starts as before. A run whose state is placed in a sandbox SHALL
NOT be held to this machine's trust, because the CLI there keeps its own configuration.

#### Scenario: A new repository Claude Code has never seen
- **WHEN** the planner would start `/aio:refine 2` with `claude-code` in a folder `~/.claude.json` does not trust
- **THEN** no run starts, the journal says `waited` once with the folder and the command, and the run
  starts on the first pass after the person has accepted the question in `claude`

### Requirement: A run waits for an environment that cannot take it
Before starting an agent run, the engine SHALL ask Runs where it would be placed. When that environment
cannot take it now, such as sbx signed out or with no Claude credential, the engine SHALL NOT start
the run and the PBI SHALL wait with the environment's sentence, written once however many passes the
wait lasts.

#### Scenario: sbx with no Claude credential
- **WHEN** the planner would start `/aio:implement 7` and the folder's `in-progress` runs in `sbx`, which holds no `anthropic` secret
- **THEN** no run starts, the journal says `waited` once with the sentence and `sbx run claude`, and the run starts on the first pass after the person has signed in

### Requirement: A sub-issue waits for what its epic's graph puts before it
Before starting an agent run on a PBI that has a parent, the engine SHALL read the first mermaid block
of the parent's body, a flowchart whose nodes are `e<n>` for the epic and `i<n>` for each sub-issue, and
its `-->` arrows. For every arrow `i<a> --> i<b>` where `b` is this PBI, while `a` is open, the engine
SHALL NOT start the run and the PBI SHALL wait with `#a goes first (the epic's graph)`, written once
however many passes the wait lasts. An arrow out of the epic (`e<n> --> i<b>`) SHALL make nobody wait,
and a parent with no mermaid block SHALL make nobody wait. A block the engine cannot read, because it is
not closed, is not a flowchart or has an arrow between anything but `e<n>` and `i<n>` nodes, SHALL make
nobody wait either: the run starts and its `started` line says the graph was not read and why, once in
the PBI's journal. The engine SHALL NOT write the graph.

#### Scenario: The four sub-issues of an epic labelled at once
- **WHEN** an epic's graph reads `e29 --> i30`, `e29 --> i31`, `i31 --> i32`, `i31 --> i33` and its four sub-issues are labelled `ready-for-proposal` at once
- **THEN** #30 and #31 start, #32 and #33 each journal `waited` once with `#31 goes first (the epic's graph)`, and both start on the first pass after #31 closes

#### Scenario: A graph that cannot be read
- **WHEN** an epic's mermaid block has the line `i61 --> #62`
- **THEN** both sub-issues start, and the first `started` line of each says the graph was not read, with that line

### Requirement: A run's end decides the transition, and a failure holds
When a run the engine asked for has ended, its outcome SHALL lead through the state's `on`. A run Runs
does not know, a run that failed, a run that ended `blocked`, and an outcome the state does not name
SHALL each hold the PBI with the reason, and the run SHALL be consumed once. The exception is a run a
person stopped and then released (#707): its end SHALL be written as a `ran` line and SHALL NOT hold the
PBI again; a run that failed with nobody stopping it holds as before. Nothing SHALL be retried:
a vendor that does not answer leaves the PBI waiting, and the only retry is the next pass reading the
vendor again.

#### Scenario: A run without an end record holds the PBI
- **WHEN** the agent exits without calling `aio done`
- **THEN** the PBI is held with `the run failed (NoEndRecord): …`

#### Scenario: A run that waited for a person holds the PBI with what to do
- **WHEN** a run ends with `NotLoggedIn` or `WaitingForPerson` (#706), or `WaitingForInput` (#764)
- **THEN** the run's end asks the schedule for its next pass at once rather than at the end of the
  interval, and that pass holds the PBI with `the run failed (NotLoggedIn): Claude Code is not logged
  in. Run `claude /login` on this machine, then release.`, the sentence with the command, or the
  last lines of the run's screen, so it is under Needs you within the minute

### Requirement: max_loops bounds the way back
The engine SHALL count, per state, the moves and releases its journal records from that state to a
state at or above it. When a back edge has been taken `max_loops` times, the outcome SHALL hold the PBI
instead, saying how many times it was sent back. Entering a state whose every transition goes back and
whose `max_loops` is spent SHALL hold too.

#### Scenario: A second conflict holds
- **WHEN** `merging` answers `conflict` after `rebasing` already ran once
- **THEN** the PBI is held instead of entering `rebasing` again

### Requirement: The engine does what touches the vendor at the transition
Moving a PBI SHALL first do the vendor step its transition declares with `pr` (loop-format) — `draft`
opens a draft pull request that closes the PBI, `ready` marks it ready, `close` closes the issue; in
this repository's loop, on leaving `spec-ready`, on leaving `verifying` with `green` and on leaving
`merging` with `merged` — and then move the label. A hold SHALL set the hold label.
Every one of those writes SHALL be read back, and a write whose read-back does not match SHALL hold the
PBI with what was observed; a hold that did not stick SHALL be journalled so the next pass redoes the
state.

#### Scenario: Leaving spec-ready opens the pull request
- **WHEN** the coder's run in `spec-ready` ends and the loop leads to `proposal-review`
- **THEN** the engine opens a draft pull request whose body closes the issue, then moves the label

### Requirement: The last sub-issue to close closes its epic
After a move whose transition closed the PBI (`pr: close`), the engine SHALL read the PBI's parent and,
when the parent is open and every one of its children is closed, SHALL comment on the parent, close it
and write a `moved` line to the state that transition led to in the parent's journal, from the parent's
state or `no state`. The comment SHALL say how many sub-issues and how many pull requests, the dates the
parent was opened and closed, each sub-issue with its pull request, and the PBIs its body names as
`#<n>` that are still open, as follow-ups. A child still open, held or not, SHALL leave the parent as it
is. The engine SHALL NOT reopen a parent, move a child or change the parent's label. A vendor that
cannot read the parent or its children SHALL be logged, and nothing written.

#### Scenario: The third sub-issue merges
- **WHEN** an epic has three sub-issues, two closed, and the third answers `merged` in `merging`
- **THEN** the engine closes the third, moves it to `done`, comments "Closed by the loop: all 3 sub-issues are closed, with … pull requests" on the epic, closes it and journals `moved no state → done` on it

#### Scenario: A sub-issue is held
- **WHEN** an epic's third sub-issue merges while another one is held in `proposal-review`
- **THEN** the epic stays open and its journal gets no line

### Requirement: A held PBI waits for a person, or for an approval at the vendor
A held PBI SHALL wait. When the engine's own latest hold of it leads to a state and the PBI's pull
request is approved at the vendor, the engine SHALL release the hold to that state, journalled as "the
pull request was approved at the vendor". When a person removed the hold label by hand, the engine SHALL
enter the hold's `next`, or run the state again when the hold had none.

#### Scenario: An approval releases merge-review
- **WHEN** a PBI is held in `merge-review` with `next: retro` and its pull request is approved on GitHub
- **THEN** the next pass releases the hold and moves the PBI to `retro`

### Requirement: The model server is ensured before the first score of a pass
Before running a state's score, the engine SHALL run `.aio/scorers/laya/laya ensure` in the folder's own
checkout, not on the PBI's branch, when that file exists there, and at most once a pass. A checkout
that has not installed harness 37 has it in `.harness/aio/scorers/laya/`, or beside the steps in `.harness/aio/`
before harness 36, and SHALL have that one run (#759, #779). A pass that runs
no score SHALL NOT run it. An `ensure` that fails SHALL NOT fail or hold anything: it is logged, and the
score answers `unknown` with its reason.

#### Scenario: Two scores in one pass
- **WHEN** two PBIs are in `proposal-review` in the same pass
- **THEN** `laya ensure` runs once, in the folder's checkout, before the first `risk-score`

#### Scenario: laya cannot start
- **WHEN** `laya ensure` exits non-zero and `proposal-review` does not name `unknown`
- **THEN** the PBI is held with `proposal-review produced unknown, which its on does not name: risk-score: no se pudo puntuar: …`

#### Scenario: laya only beside the steps
- **WHEN** the folder's checkout has `.harness/aio/laya` and no `.aio/scorers/laya/laya`, and a pass runs a score
- **THEN** `.harness/aio/laya ensure` runs once, before the first score

