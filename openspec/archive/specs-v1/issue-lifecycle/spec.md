# issue-lifecycle Specification

## Purpose
How work moves through this repository: one issue on one branch in one pull request, driven by the project's
own `/aio:*` commands, with GitHub owning the state and the label `status:holding` meaning a person must act
before anything else does. Reverse-engineered from the shipped repository harness, not negotiated ahead of it.
## Requirements
### Requirement: GitHub owns the state of the work and the repository owns facts about the code
The state of an item SHALL live in GitHub: the issue is the item and its milestone is the wave.
Nothing in `docs/` SHALL track progress or carry a checkbox, because a file lives on a branch
and a plan written into one is invisible to every other worktree until it merges.

For the legacy manual loop, its work branch SHALL remain the visible claim between worktrees;
comments and planning documents SHALL NOT replace it. For opt-in shared controlled execution,
a work branch or `git branch --list` SHALL NOT prove exclusive ownership. Ownership and
repository capacity SHALL require the verified remote registry protocol in
`controlled-workflow-authority`, without moving shared phase or Hold into SQLite.

#### Scenario: A plan is not written into the repository
- **WHEN** work needs sequencing across agents
- **THEN** it is expressed as issues, milestones and branches rather than as a tracked document

#### Scenario: Two writers push the same work commit
- **WHEN** ordinary branch pushes both report success for the same SHA
- **THEN** neither response alone authorizes shared controlled execution

### Requirement: Six commands drive one issue from refined to retro, and none of them removes a hold
`.ai/commands/aio/` SHALL provide the loop's **steps** as prompts, single-sourced and symlinked so every runtime
reads the same file: **refine** interrogates an issue to the Definition of Ready, writes no code, and on success
moves the issue to `status:spec-ready` (gaps move it to `status:needs-refinement`); **propose**
branches from fresh `origin/master`, writes the proposal into the issue, opens a **draft** pull request and
applies the hold in the same edit — and its gate SHALL require `status:spec-ready`, accepting
`status:ready-for-proposal` as the migration for issues refined before that label existed; **implement**
checks the hold gate and then the WIP gate before writing code,
then marks the pull request ready and holds again; **sync** is the only merge path — squash-merge, close the
issue and its milestone if it was the last one open, delete the branch and its worktree, append the retro entry;
**review** writes what the change taught, or nothing; **status** is read-only. Nothing SHALL remove a hold and
nothing SHALL retry: a failure SHALL halt, apply the hold, and comment the reason in the words the tool used.

**The order between those steps, and the guard on each, SHALL come from the machine a checkout declares**
(`declared-loop-machine`) rather than from agreement between the prompts. A prompt SHALL NOT be the only place a
transition is written: where the prompts and the declared machine disagree about what follows a step, the
declaration is what the harness obeys.

The manual convention of human Hold release SHALL NOT be imported as a shared controlled
grant. Controlled execution SHALL additionally require explicit bound human authorization,
confirmed ownership and durable effect identity. Legacy prompt execution and Proposed
documents SHALL NOT satisfy those requirements. Unknown Hold publication SHALL be reported
as unknown and SHALL NOT clear the failure blocker.

#### Scenario: A person removes the hold, and that act is the approval
- **WHEN** a proposal in the legacy manual loop is written and the hold applied
- **THEN** nothing proceeds until a person removes the label, and no second label records the approval

#### Scenario: A failure halts and says why in the tool's words
- **WHEN** a command fails mid-loop
- **THEN** it stops, applies the hold, and comments the failure verbatim rather than retrying

#### Scenario: The order lives in one place, not in six
- **WHEN** what follows a step has to change
- **THEN** it changes in the declared machine, and no prompt has to be edited to agree with the others

#### Scenario: Refine's success is a label the loop can act on
- **WHEN** `/aio:refine` completes on an issue that meets the Definition of Ready
- **THEN** the issue carries `status:spec-ready`, and the same command on an issue with gaps leaves
  `status:needs-refinement`

#### Scenario: Propose refuses an unrefined issue
- **WHEN** `/aio:propose` runs on an issue carrying neither `status:spec-ready` nor the migration
  label `status:ready-for-proposal`
- **THEN** it refuses and names `/aio:refine` as the fix

#### Scenario: Controlled approval is not a missing label
- **WHEN** a controlled Task has no Hold but no current bound grant
- **THEN** shared dispatch remains blocked

### Requirement: The WIP limit and the label names are configuration the loop reads
The loop's tunables SHALL be declared by the loop itself: the WIP limit in
`.harness/issue-loop.yaml`'s workflow-level `x-harness` (`x-harness: { wip: 4 }`), the label names
remaining in `.harness/config.json`. The implement gate SHALL enforce the limit by naming the
issues currently holding it. The limit SHALL be a person's decision recorded in the declaration
rather than a constant in a prompt.

Both the automatic path and the written gate SHALL read the same declaration, so they cannot
enforce two different numbers, and a limit the harness cannot read SHALL stop a launch rather than
permit one.

#### Scenario: The WIP gate names what is in the way
- **WHEN** the limit is reached and another implement starts
- **THEN** it refuses and names the issues occupying the limit

#### Scenario: One number, two enforcements
- **WHEN** the automatic path and a person following `implement` both check the limit
- **THEN** both read the loop's `x-harness.wip` and reach the same answer

#### Scenario: An unreadable limit stops a launch
- **WHEN** the loop declares no `wip` under `x-harness`, or names one out of bounds
- **THEN** the automatic path starts nothing for that Folder rather than starting without a limit

### Requirement: The retro log is append-only and records what a change taught
`docs/process/retro-log.md` SHALL be appended to when a change is synced, one entry per change, recording what
the change **taught** rather than what it did. Nothing in it SHALL be edited once written, including entries
that turned out to be wrong, because being able to read what somebody believed at the time is the value of the
log. An accepted ADR SHALL likewise be superseded rather than rewritten.

#### Scenario: A wrong entry stays and is superseded
- **WHEN** a later change contradicts an earlier entry
- **THEN** a new entry is appended and the old one is left standing

### Requirement: The lifecycle is the eight states the loop writes
The documented lifecycle — `CONTRIBUTING.md` and the `set-issue-status` skill — SHALL name exactly the
`status:*` states the declared loop and the commands write and the app reads: `backlog`,
`ready-for-proposal` (the entry label a person applies), `needs-refinement`, `spec-ready`,
`ready-for-implementation`, `in-progress`, `code-review` and `done`, with `needs-refinement` beside
`spec-ready` rather than before it. It SHALL NOT document a state nothing writes. `/aio:sync` SHALL
move the issue to `status:done` when it closes it, so the label agrees with the closed issue.

The WIP cap and the hold label SHALL be named in the prose where the harness reads them —
`x-harness.wip` in `.harness/issue-loop.yaml` and `labels.hold` in `.harness/config.json` — and no
prompt or skill SHALL name a key neither file declares.

#### Scenario: A merged change reads as done
- **WHEN** `/aio:sync` merges a change and closes its issue
- **THEN** the issue carries `status:done`

#### Scenario: A prompt points at the file that holds the number
- **WHEN** `/aio:implement`, `/aio:ship` or `/aio:waves` checks the WIP cap
- **THEN** it reads `x-harness.wip` in `.harness/issue-loop.yaml`, the same line the watcher reads

### Requirement: The claim is the pushed `change/<issue>` branch, taken before any work
Every path that starts work on an issue — `/aio:propose`, `/aio:waves` and the loop's `claim` node —
SHALL claim it through `.harness/aio/claim` **before** writing anything on the branch. The script
SHALL create `change/<issue>` at origin with an empty commit of its own on top of the base, made
without a checkout, so that no other clone can push the same commit; and SHALL count the claim as won
only when its push **created** the ref (`*` in `git push --porcelain`). It SHALL remember its claim
commit in `refs/aio/claims/<issue>`, and a later run from the same clone SHALL count the claim as its
own when origin's branch contains that commit. Any other existing `change/<issue>` SHALL be a lost
claim, and the caller SHALL stop. No command SHALL claim with a differently named branch or with a
branch that exists only locally.

#### Scenario: Two clones claim from the same trunk
- **WHEN** two clones that just fetched the same trunk claim `change/<issue>`
- **THEN** one creates it and holds the issue, and the other's push is rejected and it stops

#### Scenario: A rerun recognises its own claim
- **WHEN** `/aio:propose` failed after claiming, and runs again from the same clone
- **THEN** the claim script answers that the branch already carries this clone's claim, and work goes on

#### Scenario: A wave hands out an issue
- **WHEN** `/aio:waves` claims an issue for a worktree
- **THEN** the worktree is on `change/<issue>` from origin, which `/aio:propose` then uses as it is

### Requirement: The loop's deterministic steps are scripts, one file per step
Every step of the loop that needs no judgement SHALL be one executable file under `.harness/aio/`
that answers one JSON line: `claim`, `prepare`, `file-overlap`, `gate`, `status`, `evidence`, `merge`
and `cleanup`. The `/aio:*` commands SHALL name the script rather than describe its git or gh calls,
the declared loop SHALL run it as a `bash` node where it is a node, and where the app takes the same
step — preparing a Run's worktree, merging, removing a worktree or its branch — it SHALL run the same
file: the repository's copy, or the copy it carries when the repository has none. Each script SHALL
be runnable by hand from anywhere in the repository, worktrees included.

`status` SHALL move an issue in one edit, removing every other `status:*` label, never removing the
hold, and refusing a state outside the eight. `gate` SHALL stop on the hold, then on the WIP limit read
from `x-harness.wip`, naming the issues holding it, and SHALL stop when the limit cannot be read.

#### Scenario: A move keeps the hold
- **WHEN** a held issue is moved with `status` to another state, without `--hold`
- **THEN** it carries the new state and still carries the hold

#### Scenario: The WIP gate names who holds the room
- **WHEN** the limit is 2 and two other issues are in progress
- **THEN** `gate` answers `go: false` naming both

#### Scenario: The same step by hand and from the app
- **WHEN** a person runs `.harness/aio/prepare 42` and the app starts a Run for #42
- **THEN** both run the same file and land in the same worktree, prepared the same way

