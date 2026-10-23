# loop-format Specification

## Purpose
The loop of a repository is one YAML file, `.aio/loop.yaml`, that says which states a PBI goes
through and what runs in each. `LoopReader` reads it and checks its rules, so a loop that breaks one is
refused with a sentence a person can act on rather than run half-way. Reverse-engineered from the
shipped code (`src/modules/Loop/AiOrchestrator.Modules.Loop/Definition/`), not negotiated ahead of it.

## Requirements
### Requirement: The loop declares a name, a WIP limit, its agents and its states
`LoopReader.Read` SHALL accept exactly the keys `name`, `wip`, `agents` and `states` at the root; for
each agent, `provider` and `model`; and for each state, `script`, `agent`, `command`, `score`,
`provider`, `model`, `max_loops`, `on`, `next`, `pr`, `back` and `tab`. Any other key SHALL be refused as
`<path>: not a key of the loop format; it takes <list>`. A provider SHALL be one of `claude-code`,
`copilot`, `opencode` or `codex`, for an agent and for a state's override. `wip` and `max_loops` SHALL
be whole numbers above zero. `script` SHALL be one script or a list of them. States SHALL keep the order
the file declares them in, because that order decides what goes back.

#### Scenario: This repository's loop reads into its states
- **WHEN** `.aio/loop.yaml` of this repository is read
- **THEN** it is `spec-driven-issue-loop` with `wip: 4`, the agents `refiner`, `coder` and `reviewer`,
  ten states from `ready-for-proposal` to `rebasing`, and the undeclared targets `needs-refinement` and
  `done`; every agent's provider is `claude-code`, the reviewer's too until v2 has a Copilot adapter

#### Scenario: An unknown key is named with its path
- **WHEN** a state carries a key the format does not take
- **THEN** the reading has no loop, and its problem names the state and the keys that are accepted

### Requirement: A state runs an agent or a score, and produces one outcome
A state SHALL NOT carry `score` together with `agent` or `command`; the reader SHALL refuse it as
"runs an agent and a score; a state runs one or the other". A state's `agent` SHALL be one declared
under `agents`. A state SHALL NOT carry both `on` and `next`, refused as "has both on and next; next is for a state
with one way out". An outcome under `on` SHALL lead to a
state, to a mapping with its `next` and an optional `pr`, or to a mapping holding `hold`, with its
reason, and an optional `next`; anything else
nested under an outcome SHALL be refused as "on does not nest — a state produces one outcome".

#### Scenario: An agent and a score together are refused
- **WHEN** `proposal-review` declares `score: risk-score` and `agent: reviewer`
- **THEN** the problem is `states.proposal-review: runs an agent and a score; a state runs one or the other`

#### Scenario: A hold carries its reason
- **WHEN** an outcome is a mapping with an empty `hold`, or with neither `hold` nor `next`
- **THEN** the reading is refused, because a hold without its reason is not something a person can read

### Requirement: A state that can go back says how many times
A transition, or a hold's `next`, whose target is declared at or above the state it leaves SHALL count
as going back, and the state SHALL carry `max_loops`. Without it the reader SHALL refuse the loop with
"goes back to <target> without max_loops; a state that can go back says how many times".

#### Scenario: A back edge without max_loops is refused
- **WHEN** `code-review` sends `rejected` to `in-progress` and its `max_loops: 2` is removed
- **THEN** the problem is `states.code-review: goes back to in-progress without max_loops; a state that can go back says how many times`

#### Scenario: A hold's next counts as going back
- **WHEN** the red hold of `verifying` is given `next: in-progress`
- **THEN** the reading is refused for `verifying` without `max_loops`

### Requirement: A script decides by its line, not by its name
A state's `script` and `score` SHALL name scripts, found by name in `.aio/` then `.aio/steps/` (and, for a repository that has not reinstalled since harness 36, in `.harness/`), whose last JSON line carries
`outcome`, `wait` and `why` (openspec/specs/harness-scripts). An `outcome` SHALL be read as one of the
state's `on` keys, as an agent's outcome is; one the state's `on` does not name SHALL hold the PBI. An
outcome the `on` does not name from a script that exited non-zero SHALL be that script failing, so a
`loop.yaml` whose `on` predates a script's outcomes still holds on the ones that exit 1 (#733). The
loop format SHALL keep `score` only to say which script is the state's score: the engine runs it last
and reads its line as any other script's.

#### Scenario: A score that answers no outcome holds
- **WHEN** `proposal-review`'s score exits 0 with `{"band": "medium", "why": "..."}` and no `outcome`
- **THEN** the PBI holds, saying `proposal-review` ran `risk-score` and none of them named an outcome its on knows

### Requirement: A target nobody declares is a state where the loop does nothing
A target that no entry of `states` declares SHALL be read as an undeclared state
(`LoopDefinition.Undeclared`) rather than as an error. The loop SHALL do nothing for a PBI in such a
state; it stays there until a person moves it.

#### Scenario: done and needs-refinement are undeclared
- **WHEN** this repository's loop is read
- **THEN** `done` and `needs-refinement` are its undeclared states and no problem is reported

### Requirement: Every problem is reported at once, and a loop with one is not a loop
The reader SHALL collect every problem of a file before answering, each prefixed with the path it is
about, and SHALL answer no loop when there is any. `POST /api/loop/validate` with `{yaml}` SHALL answer
`{valid, states, undeclared, problems, providers}`, `providers` being each CLI the loop names in its
agents or on a state and each state carrying `agent`, true when it names an agent or a command, and
Settings SHALL show those problems on the folder's row.

#### Scenario: Bad YAML names its line
- **WHEN** the file is not valid YAML
- **THEN** the problem is `line N: <message>` and nothing else is checked

### Requirement: A transition declares what the engine does to the pull request
A transition MAY carry `pr`, one of `draft` (open the pull request as a draft that closes the PBI),
`ready` (mark it ready for review) or `close` (close the PBI). In the short form `pr` SHALL sit beside
`next` on the state; in the long form, on the outcome's mapping beside its `next`
(`green: {next: code-review, pr: ready}`). Any other value SHALL be refused as
`<path>.pr: '<value>' is not a value of the loop format; it takes draft, ready, close`. A `pr` on a state
with `on` SHALL be refused as "goes beside next; under on, it goes on the outcome that takes it", and a
`pr` on a hold as "a hold moves nothing at the vendor". A transition without `pr` touches nothing at the
vendor, whatever its states are called: the planner reads `pr` and knows no state by name.

#### Scenario: This repository's loop declares the three steps
- **WHEN** `.aio/loop.yaml` of this repository is read
- **THEN** `spec-ready` carries `pr: draft` beside its `next`, `verifying`'s `green` carries `pr: ready`
  and `merging`'s `merged` carries `pr: close`, and no other transition carries `pr`

#### Scenario: A value outside the three is refused
- **WHEN** `verifying`'s `green` declares `pr: merge`
- **THEN** the problem is `states.verifying.on.green.pr: 'merge' is not a value of the loop format; it takes draft, ready, close`

#### Scenario: A loop with other names takes the same steps
- **WHEN** a loop names its states `drafting`, `testing` and `shipping` and declares `pr` on their transitions
- **THEN** leaving `drafting` opens the draft, `testing`'s `passed` marks it ready and `shipping`'s
  `shipped` closes the PBI, as `spec-ready`, `verifying` and `merging` do here

### Requirement: A state says where a person sends it back and which tab to look at
A state MAY declare `back: <state>`, the state a person's *Request changes* sends the PBI to from it,
and `tab: <tab>`, the PBI screen's tab marked while the PBI is in it. `back` SHALL name a state the loop
declares, refused otherwise as "<state>.back: '<name>' is not a state of the loop"; it is a person's
act, not an outcome, so it SHALL NOT count as going back for `max_loops`. `tab` SHALL be one of
`journey`, `issue`, `proposal`, `review` or `files`, refused otherwise as "<state>.tab: '<name>' is not
one of journey, issue, proposal, review, files". `GET /api/loop/definition` SHALL carry both on every
state, null when absent. This repository's `proposal-review` declares `back: spec-ready` and
`tab: proposal`, and its `code-review` and `merge-review` declare `back: in-progress` and `tab: review`;
the harness package's `code-review` declares the same.

#### Scenario: This repository's review states declare back and tab
- **WHEN** this repository's loop is read
- **THEN** exactly `proposal-review`, `code-review` and `merge-review` carry a `back` and a `tab`

#### Scenario: A back to a state the loop does not run is refused
- **WHEN** `proposal-review` declares `back: drafting`
- **THEN** the problem is `states.proposal-review.back: 'drafting' is not a state of the loop`

#### Scenario: A tab the PBI screen does not have is refused
- **WHEN** `proposal-review` declares `tab: design`
- **THEN** the reading is refused with the five tabs the PBI screen has

### Requirement: A score's unknown is an outcome like low and high
A state with a `score` SHALL produce `low`, `high` or `unknown`, and `on` MAY name `unknown` as it
names the other two. `unknown` means there was no score to give, not a risky change. When `on` does not
name it, the PBI SHALL be held with the score's own why, which says it could not score; it SHALL NOT be
read as `high`. This repository's loop names `low` and `high` only.

#### Scenario: unknown declared
- **WHEN** `proposal-review` declares `unknown: in-progress` and `risk-score` answers `unknown`
- **THEN** the PBI moves to `in-progress`, and the journal line is `unknown: <the score's why>`

#### Scenario: unknown not declared
- **WHEN** this repository's `proposal-review` gets `unknown` from `risk-score`
- **THEN** the PBI is held, and the hold's why carries `no se pudo puntuar`, not `El score de riesgo salió alto`
