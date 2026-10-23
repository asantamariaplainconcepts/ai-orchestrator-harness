# run-a-task Specification

## Purpose
Starting a real agent CLI in a real pseudo-terminal in a real directory, and driving it through a
workflow's steps until something holds or the plan ends. The run outlives the request that started it and
the browser watching it: the server owns the process (ADR-0001), so closing the tab does not stop the
agent. Reverse-engineered from the shipped code, not negotiated ahead of it.
## Requirements
### Requirement: A run needs something to say, and the agent is checked before the row exists
This capability is superseded by `run-a-pbi` (#448). Every requirement of `run-a-task` SHALL live on
in `run-a-pbi` with the behaviour unchanged and the vocabulary renamed from task to PBI. Nothing new
SHALL be added to this capability: it exists only to name its replacement.

#### Scenario: Neither a workflow nor a prompt is refused
- **WHEN** a run is started with both a workflow and a prompt
- **THEN** the run starts and the prompt is what the agent is asked

#### Scenario: A prompt alone is a run
- **WHEN** a run is started with a prompt and no workflow
- **THEN** the run starts

#### Scenario: An unusable agent leaves no run behind
- **WHEN** the agent named for a run is not usable
- **THEN** the run row is not created

### Requirement: A requested Workflow must exist
If a Run request names a Workflow that cannot be resolved in its Folder, the system SHALL refuse it
before creating a Run or Worktree. It SHALL NOT silently substitute a freeform prompt.

#### Scenario: A removed Workflow is requested
- **WHEN** a caller names an unavailable Workflow
- **THEN** the request is refused with a remedy and no Agent starts

### Requirement: A run works in the directory the run row records
A run SHALL always work in a worktree beside the checkout: when the start names a `WorktreeName`, that branch; when it names none, the branch the system derives — `change/<task-key>` with the `#` stripped (`#482` becomes `change/482`) and sanitized with the same rules as the Create worktree dialog (`..` refused, no leading `-`, any character a branch name cannot carry becomes `-`). A derived branch SHALL be ensured, not created: the second run of the same task lands in the worktree the first one made. The checkout itself SHALL NOT be a working directory of a run. The directory a run worked in SHALL be recomposable from what the run wrote down — the folder and the worktree's name — with the slash rule applied, because a branch called `change/80` lives in a directory called `change-80`. `StartRun` SHALL take the path `git worktree add` reported rather than composing one, since git's answer is the authoritative one.

#### Scenario: A start with no worktree named gets one derived
- **WHEN** a start carries no worktree name and the task key is `#482`
- **THEN** the run works in the worktree for `change/482`, ensured beside the checkout

#### Scenario: The second run of a task lands where the first worked
- **WHEN** two runs of the same task start with no worktree named
- **THEN** both work in the same worktree, because a derived branch is ensured rather than created

#### Scenario: The checkout is never the working directory
- **WHEN** any run starts, with or without a worktree named
- **THEN** the agent works in a worktree, never in the folder's checkout

#### Scenario: A branch that cannot be ensured refuses the start
- **WHEN** git refuses to ensure the derived worktree
- **THEN** the start is refused with git's own sentence and no run row is written

#### Scenario: The recorded path is git's, not a composition
- **WHEN** a run starts in a fresh worktree
- **THEN** the path stored is the one git reported

#### Scenario: A branch with a slash resolves to its directory
- **WHEN** anything asks afterwards where a run on `change/80` worked
- **THEN** the answer is the `change-80` directory

### Requirement: A plan advances one step at a time, and a hold stops it
A run with a workflow SHALL start on its plan's first step, and a run with no plan SHALL do nothing at
all. For legacy plans, going idle SHALL end the current step and send the next. For opt-in controlled
plans, going idle SHALL only request validation of the current step's correlated result; the controlled
execution contract SHALL determine whether another step is permitted. A step that holds SHALL stop the
run there and nothing SHALL move after a hold. The last step ending SHALL finish the plan exactly once.
An unresolved step this product would run itself SHALL stop the plan rather than being skipped. Stopping
a run SHALL end its plan wherever it was, and an ended run SHALL have no step still queued or running.

#### Scenario: A hold parks the run
- **WHEN** the plan reaches a step that holds
- **THEN** the run waits there and no further step is sent

#### Scenario: The plan finishes once
- **WHEN** the last step ends
- **THEN** the plan is finished a single time

#### Scenario: Stopping settles the plan
- **WHEN** a run is stopped mid-plan
- **THEN** its plan ends where it was, with nothing left queued

#### Scenario: Controlled idle is not success
- **WHEN** an Agent in a controlled plan becomes idle
- **THEN** the next step is withheld until the result has been validated

### Requirement: Output goes to the watcher first and the record second, in batches
`RunLauncher` SHALL push every chunk to whoever is watching **before** writing it down, because a person
is waiting for it and the record can lag by a batch without anybody noticing, and SHALL hand the sink a
**batch** rather than a chunk at a time. `RunSink` SHALL be implemented by the module rather than by the
launcher writing rows itself, because nothing below the modules may touch a module's schema. Appending
SHALL never load the run: the next ordinal SHALL be read once per run from `MAX(Ordinal)` and then carried
in memory, so appending costs the same at thirty thousand lines as at one thousand. A batch SHALL be
written in one transaction, ordinals SHALL continue from what the run already wrote, and output for a run
that does not exist SHALL be dropped.

#### Scenario: Appending is flat
- **WHEN** thirty thousand lines have been written
- **THEN** the next append costs what the first did, and the run is never loaded to do it

#### Scenario: Output for a dead run is dropped
- **WHEN** a chunk arrives for a run id that does not exist
- **THEN** it is dropped rather than written

### Requirement: A run is held back to what its slowest watcher has drawn
`RunFlow` SHALL hold the launcher back when a watching browser falls a window behind, where the
acknowledgement is what the client has actually **drawn** rather than what it received. Holding the
launcher back stops the pseudo-terminal being read, which fills the kernel's buffer, which blocks the
agent's own write — so the backpressure lands on the process producing the output. A run nobody is
watching SHALL never be held back, a watcher keeping up SHALL never be held back, the run SHALL wait for
the watcher furthest behind, a watcher that goes away or has said nothing for long enough SHALL stop being
counted, acknowledging more than was sent SHALL NOT buy credit, a window of zero SHALL send without
waiting, and a finished run SHALL stop being accounted for.

#### Scenario: A stalled tab does not stall the others forever
- **WHEN** a watcher stops acknowledging and then disappears
- **THEN** it stops being counted and the run resumes

#### Scenario: Nobody watching means no ceiling
- **WHEN** no browser is attached
- **THEN** the run is never held back

### Requirement: Stopping is an ending, and steering is guidance
`POST /api/runs/{id:guid}/stop` (`ControlRun.cs`, `StopRun`, `Access.AnyCaller`) SHALL record the run as
**ended rather than failed**: a person who stops a run knows why, and filing it under failed would put it
beside the runs that broke. `POST /api/runs/{id:guid}/steer` (`SteerRun`) SHALL send a line to a working
agent that is read before its next step — guidance, not an interrupt — and SHALL refuse while the agent is
blocked at a safety check, because that question is a menu drawn in terminal cells and a paste would press
whatever is highlighted. A steer SHALL be wrapped as a paste carrying no submit byte, an empty line SHALL
paste nothing, and newlines inside the text SHALL survive as content.

#### Scenario: A stopped run is not a failed run
- **WHEN** a person presses Stop
- **THEN** the run is recorded as ended, and the history does not count it as a failure

#### Scenario: Steering is refused at a safety check
- **WHEN** the agent is waiting on a permission question and a steer arrives
- **THEN** it is refused, telling the person to answer it in the terminal

### Requirement: A run's worktree is prepared from the fetched trunk, never from the checkout's HEAD
Where the Folder's repository has an `origin`, ensuring a run's worktree SHALL prepare it the same way
every time, before the run starts:

- A blank or `HEAD` base SHALL mean the remote's default branch, asked of the remote rather than read
  from the clone-time `origin/HEAD`; a named base SHALL be used as named. Either SHALL be fetched
  first, and a fetch that fails SHALL refuse the start.
- A new worktree SHALL be made from `origin/<branch>` when the branch was already pushed, from the
  local branch when one exists, and otherwise from the fetched base.
- The worktree SHALL then be brought forward to what was pushed of its branch and merged up to the
  base. It SHALL NOT be rebased, because the branch is the claim and is already pushed.
- Uncommitted files, a local branch that diverged from what was pushed, and a merge conflict SHALL
  each refuse the start with a sentence naming what to settle, and a conflicted merge SHALL be
  aborted so the worktree is left as it was.

What the main checkout has checked out SHALL NOT decide where a run begins. A repository with no
`origin` SHALL branch from the base it was given, as before. The Create worktree dialog is not a run
and is not changed by this.

#### Scenario: A stale checkout does not leak into a run
- **WHEN** the checkout stands on an unpushed commit on another branch and the remote's trunk has moved
- **THEN** a new run's worktree starts at the remote trunk's new tip, without the unpushed commit

#### Scenario: The second run of a task catches up with the trunk
- **WHEN** a worktree holds committed work and the trunk moved since it was made
- **THEN** the next run's start merges the trunk in and keeps the work

#### Scenario: A conflict with the trunk stops the run
- **WHEN** the worktree's branch and the moved trunk changed the same lines
- **THEN** the start is refused naming the conflicted files, and the worktree's HEAD and tree are
  unchanged

#### Scenario: Uncommitted files stop the run
- **WHEN** the worktree has uncommitted files
- **THEN** the start is refused saying how many

#### Scenario: Work pushed from another machine is resumed
- **WHEN** `change/<n>` exists at the remote and not locally
- **THEN** the worktree starts at the pushed tip rather than at the trunk

#### Scenario: No remote to fetch from
- **WHEN** `origin` cannot be fetched
- **THEN** the start is refused and no worktree is made

