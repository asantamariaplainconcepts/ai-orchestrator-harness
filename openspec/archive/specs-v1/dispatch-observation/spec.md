# dispatch-observation Specification

## Purpose
TBD - created by archiving change label-triggered-launch. Update Purpose after archive.
## Requirements
### Requirement: Observation is opt-in and never dispatches
The watcher SHALL be disabled by default and SHALL observe when its effective mode is `observe`,
observe and acquire when it is `observe-claims`, or observe, acquire and dispatch when it is
`dispatch-claims`. The effective mode SHALL be the persisted screen value when one exists and the
`LabelWatcher:Mode` configuration value otherwise, re-read per tick, so a change from the settings
screen governs the next tick without a restart. In `observe` and `observe-claims`, every tick SHALL
be dispatch-free as before. In `dispatch-claims`, the tick SHALL dispatch only a task at
`ready-for-implementation` whose hold is absent, after acquiring its registry claim and reserving a
journal intent, through the Runs admission that binds effectId to Run — and a task at
`ready-for-proposal` SHALL receive the same observed log line as any other mode and SHALL NOT be
dispatched. A CAS conflict SHALL produce no dispatch and no in-process retry; the next tick is a new
decision. A mode value — configured or persisted — other than `observe`, `observe-claims`,
`dispatch-claims` or `disabled` SHALL be refused with a log that names controlled admission and
trusted authority as what is missing.

#### Scenario: Default configuration observes nothing
- **WHEN** no mode is set anywhere
- **THEN** the watcher starts no tick loop and issues no request at all, and the settings screen
  names disabled as the mode

#### Scenario: Observe mode advises a prompt
- **WHEN** a task is `ready-for-proposal` and the effective mode is `observe`
- **THEN** the watcher logs an `Observed` candidate with the `/aio:propose #<key>` prompt and every
  request it makes is a `GET`

#### Scenario: Observe-claims acquires and still never dispatches
- **WHEN** the effective mode is `observe-claims` and a candidate is eligible
- **THEN** the watcher logs the observed candidate and the acquisition outcome, and no Run, label,
  hold or module write API is called

#### Scenario: A conflict is a stop, not a retry
- **WHEN** the acquisition returns a conflict
- **THEN** the watcher logs the observed revision, proceeds to no other effect for that task, and
  waits for the next tick to decide again

#### Scenario: Dispatch-claims dispatches only implementation work with its hold released
- **WHEN** a task is `ready-for-implementation` with no hold and the effective mode is
  `dispatch-claims`
- **THEN** the watcher reserves a journal intent, acquires the claim, and starts the Run through the
  durable dispatch admission, and the started Run is named with the task's own effect identity

#### Scenario: A proposal is never machine-written
- **WHEN** a task is `ready-for-proposal` and the effective mode is `dispatch-claims`
- **THEN** the watcher logs the observed candidate and issues no write to any module

#### Scenario: Dispatch sends the declared workflow, not a prompt
- **WHEN** a `ready-for-implementation` task with no hold is dispatched
- **THEN** the start names the effect's workflow key as its WorkflowId, and the run carries the
  controlled plan's steps

#### Scenario: A missing workflow is a typed stop, never a degradation
- **WHEN** the transition's workflow key does not resolve in the folder
- **THEN** the tick logs `workflow-missing` with the key, writes nothing for that task, and never
  starts a prompt-only run instead

#### Scenario: An unknown mode is refused
- **WHEN** the effective mode is a value other than `observe`, `observe-claims`, `dispatch-claims`
  or `disabled`
- **THEN** the loop does not run and an error names controlled admission and trusted authority

#### Scenario: A mode changed from the screen governs the next tick
- **WHEN** the persisted mode is lowered or raised between two ticks
- **THEN** the following tick decides and acts under the new mode, and no restart occurred

### Requirement: The tick reconciles uncertain intents before deciding anything new
Before evaluating candidates, a dispatch-mode tick SHALL resolve journal intents left `pending` or
`unknown` past the declared escalation window, by querying Runs by effectId and reading the
registry — never by resubmitting the effect. An intent whose Run exists SHALL settle to `started`;
an intent past the window that cannot be established SHALL remain blocked with its typed reason
and SHALL escalate once, through the attention-comment seam, keyed on its effectId. A tick SHALL
NOT retry a refused or failed dispatch.

#### Scenario: Response lost, then found by observation
- **WHEN** a start's response was lost and the next tick queries the effectId
- **THEN** the intent settles to the existing Run, and no second Run is created

#### Scenario: The unknown stays blocked and is named once
- **WHEN** an intent is `unknown` past the window and observation establishes nothing
- **THEN** one attention comment carrying its effectId is published, the intent keeps its typed
  state, and no later tick republishes for the same effectId

#### Scenario: Failure is not retried
- **WHEN** a dispatch journey recorded `failed` or `refused`
- **THEN** no later tick resubmits that effect, and the task's next step is a person's

### Requirement: One door for a transition, whatever started it
A run started through a lifecycle transition — by the watcher's dispatch or by a person starting
the task's next step in the UI — SHALL travel the same contract: the same declared workflow, the
same effect identity discipline, the same admission checks. A run a person starts with free-form
prompt text and no workflow SHALL carry `not-a-transition` visibly: it exists, it runs, and the
loop does not count it — not in-progress, not WIP, not claimable, and no transition of the
machine fired to start it.

#### Scenario: The person and the watcher use the same workflow
- **WHEN** a person starts the implement step of a loop task from the UI
- **THEN** the run starts the same declared workflow the machine's dispatch would start, and both
  carry the transition marker

#### Scenario: A prompt-only run is not a transition
- **WHEN** a person starts a run with typed prompt text and no workflow
- **THEN** the run is marked `not-a-transition`, and no in-progress count, WIP room or claim
  changes because of it

### Requirement: Tunables are read per folder and null is never no-limit
A Folder's `.harness/config.json` SHALL be read through a bound, comment-tolerant declared-file
seam, per folder, and a read that returns null (missing, malformed or unreadable) SHALL suppress
that folder's candidates rather than treat the folder as unlimited, because null never means
"no limit". The WIP room SHALL be counted per folder from the same task rows the manual loop
counts, prefixing the label the way the tunables file spells it, and a limit out of bounds
SHALL be refused rather than clamped.

#### Scenario: An unreadable config suppresses the folder
- **WHEN** a Folder's `.harness/config.json` is malformed while the watcher is in `observe` mode
- **THEN** the Folder's tasks appear in no `Observed` candidate

#### Scenario: A held-back task is retried by the next tick, not queued
- **WHEN** a task was not advised on one tick because its folder's WIP room was exhausted
- **THEN** the same task can be advised on a later tick without anyone re-triggering it

#### Scenario: Several tasks never exceed one folder's limit in one tick
- **WHEN** three tasks in one folder are eligible and only one slot is free under `wipLimit`
- **THEN** at most one is advised on that tick

### Requirement: A reserved intent is durable and idempotent per effect
A dispatch intent reservation at `POST /api/coordination/dispatch-intents` SHALL persist the
effect id, folder scope, canonical Task identity and operation before returning, and a replay
of the same reservation SHALL return the same row rather than creating a second dispatch,
arbitrated by the database. The journal SHALL be scoped to the Folder: one folder's intents
SHALL NOT be readable or writable through another folder's scope.

#### Scenario: Replay returns the same row, not a second dispatch
- **WHEN** the same effect id is reserved twice
- **THEN** the second call returns the existing record and no new intent exists

#### Scenario: Concurrent reservation is arbitrated by the database
- **WHEN** two callers reserve the same effect id at the same time
- **THEN** exactly one row exists afterwards and both read the same record

#### Scenario: One folder cannot read another's journal
- **WHEN** a journal read is scoped to Folder A
- **THEN** no intent reserved under Folder B's scope appears

### Requirement: An outcome is written once and history is protected
`PUT /api/coordination/dispatch-intents/{effectId}/outcome` SHALL move a `pending` intent
exactly once to `started`, `failed`, `refused` or `unknown`, and SHALL refuse to rewrite an
already-recorded outcome. A `started` outcome SHALL bind a non-empty Run id; every other
outcome SHALL carry a failure code and reason instead. An unknown effect id or an outcome
that clears a failure SHALL be refused.

#### Scenario: An outcome is recorded once
- **WHEN** an intent's outcome is recorded and a second outcome is attempted for the same effect id
- **THEN** the second call is refused and the first outcome stands

#### Scenario: Started requires a run and refuses a failure code
- **WHEN** an outcome of `started` arrives
- **THEN** it is accepted only with a non-empty run id and no failure code or reason; otherwise it is refused

#### Scenario: A failure cannot be cleared to a second failure
- **WHEN** an intent already carries a `failed` outcome
- **THEN** a later outcome write for that effect id is refused rather than rewriting the history

### Requirement: Unresolved journal entries suppress observation, fail closed
On every tick in `observe` mode the watcher SHALL read the journal through the folder-scoped
read API and SHALL suppress from its candidates every task whose canonical identity matches
a journal entry, logging the suppression. A journal that cannot be read, or returns something
that is not a valid journal, SHALL fail the whole tick rather than be treated as empty. The
suppression set SHALL be read from the journal rather than remembered in memory, so it
survives the process.

#### Scenario: A journaled task is suppressed from advice
- **WHEN** a task's identity matches a journal entry and the mode is `observe`
- **THEN** the tick logs `Suppressed` for it and it appears in no `Observed` candidate

#### Scenario: An unavailable journal is not an empty journal
- **WHEN** the journal read fails or returns an invalid body
- **THEN** the tick fails, nothing is advised, and no candidate is logged

#### Scenario: Suppression survives a restart
- **WHEN** the host restarts and a journal entry is still unresolved
- **THEN** the task remains suppressed on the first tick after restart

### Requirement: Folder and task identity are resolved exactly, never guessed
A task's Folder SHALL be resolved by comparing the task's URL against every known Folder's
remote URL under a host-aware repository identity that reads both `https://` and
`git@host:owner/name.git` forms, and a first-match folder wins. A task that matches no Folder
SHALL produce no candidate and SHALL be retried on a later tick, because a Folder can be added
after the task was first seen. A task whose identity cannot be established exactly — an
unsupported provider, an Enterprise API host, or a URL that is not a github.com issue — SHALL
be excluded and logged, never guessed by key alone.

#### Scenario: A task with no checkout on this machine starts nothing
- **WHEN** an eligible task's repository matches no known Folder's remote URL
- **THEN** no candidate is produced for it, and it is reconsidered on the next tick

#### Scenario: Adding the Folder later lets a previously-unresolved task launch
- **WHEN** a Folder for a task's repository is added after that task first became eligible
- **THEN** the task is advised on the next detection tick after the Folder exists

#### Scenario: Equal keys in another repository or host do not collide
- **WHEN** a journal entry for `key 313` in `another/repo`, or in `owner/repo` on an Enterprise host, suppresses tasks
- **THEN** github.com's `owner/repo` issue `313` is not suppressed by either

#### Scenario: An ambiguous provider identity is not guessed
- **WHEN** a task's source is not GitHub, or its URL is an Enterprise host, a non-default port or a non-github.com host
- **THEN** its control identity is reported unavailable and no candidate is produced for it

### Requirement: Verification evidence projects to the issue it reviews
A passing gate's test inventory diff SHALL be published to the task's own issue as one typed
attention comment — reason `verification-summary`, the added and lost test names, both digests —
through the World comment-writer seam, once per run-and-step. The comment is a projection: the
manifest in the Run's trace is the fact a later reader or queue groups on. A failed or unknown
comment write SHALL be logged and not retried within the tick.

#### Scenario: One comment per gate
- **WHEN** the same run's gate step is observed again
- **THEN** no second diff comment is published for the same run and step

#### Scenario: The comment names the novelty
- **WHEN** a candidate adds two tests and loses one
- **THEN** the comment names both additions and the loss, with the digest change

### Requirement: A recorded decision can be replayed without effects
A Run whose dispatch carries a decision snapshot SHALL be re-evaluated against any candidate
machine — the stored one or one supplied in the request — by a simulation that launches zero
processes, writes nothing to any provider, and reads nothing but the stored snapshot and the
supplied machine. The answer SHALL be a decision diff: what the stored machine decided, what the
candidate would decide, and the guards whose values differ. A fact the replay cannot re-derive
from the snapshot (a live-run observation) SHALL be named as not re-evaluable with its reason, not
silently fixed. A Run without a snapshot SHALL answer `not reproducible` with its reason — never
an empty diff, never a simulation over invented facts.

#### Scenario: Same inputs reproduce the same decision
- **WHEN** the stored machine is replayed against the stored facts
- **THEN** the answer is the decision that was recorded, and the diff is empty

#### Scenario: A candidate machine shows its differences without cost
- **WHEN** a candidate declaration is replayed against a recorded decision
- **THEN** the answer names every transition that would change, no process is launched, and no
  provider is written

#### Scenario: A Run with no snapshot is said, not simulated
- **WHEN** the Run predates decision snapshots or was started by hand
- **THEN** the answer is `not reproducible` naming the reason, never an empty graph

#### Scenario: A live fact the snapshot cannot re-derive
- **WHEN** the replay reaches a fact that was observed of the running world
- **THEN** the answer names it as not re-evaluable with its reason, rather than assuming either
  value

### Requirement: The facts a claim owes are read where the claim lives
A dispatch-mode tick SHALL read the OpenSpec change a `ready-for-implementation` claim owes from
the claim's working path — the worktree `change/<n>` when the checkout carries one, the primary
checkout otherwise — and SHALL fall back to the checkout's `origin/change/<n>` ref (read through
git, never a checkout) when the path it reads holds no change. A change found in either place
satisfies the fact; the refusal SHALL stand only when neither place holds one. The branch fact is
already read against local and remote refs and SHALL NOT change. The loop file itself SHALL still
be read from the checkout, never a worktree's copy (#365's rule is untouched).

#### Scenario: A proposal made in the worktree passes the gate
- **WHEN** a `ready-for-implementation` task's change exists under the worktree `change/<n>` and not
  under the primary checkout
- **THEN** the tick finds the change and dispatches, rather than refusing with
  `no OpenSpec change in openspec/changes/`

#### Scenario: A proposal made on another machine passes the gate
- **WHEN** the worktree `change/<n>` does not exist locally but `origin/change/<n>` holds the
  change's `openspec/changes/<name>/tasks.md`
- **THEN** the tick finds the change through the ref and dispatches

#### Scenario: No change anywhere still refuses
- **WHEN** neither the working path nor the local and remote `change/<n>` refs hold a change
- **THEN** the tick refuses, and the refusal names `/aio:propose` as the remedy

