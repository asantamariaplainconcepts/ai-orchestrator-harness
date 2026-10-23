# issue-lifecycle Delta

## MODIFIED Requirements

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
