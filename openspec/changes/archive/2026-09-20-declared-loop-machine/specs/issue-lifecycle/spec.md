# Spec Delta

## MODIFIED Requirements

### Requirement: Six commands drive one issue from refined to retro, and none of them removes a hold
`.ai/commands/aio/` SHALL provide the loop's **steps** as prompts, single-sourced and symlinked so every runtime
reads the same file: **refine** interrogates an issue to the Definition of Ready and writes no code; **propose**
branches from fresh `origin/master`, writes the proposal into the issue, opens a **draft** pull request and
applies the hold in the same edit; **implement** checks the hold gate and then the WIP gate before writing code,
then marks the pull request ready and holds again; **sync** is the only merge path — squash-merge, close the
issue and its milestone if it was the last one open, delete the branch and its worktree, append the retro entry;
**review** writes what the change taught, or nothing; **status** is read-only. Nothing SHALL remove a hold and
nothing SHALL retry: a failure SHALL halt, apply the hold, and comment the reason in the words the tool used.

**The order between those steps, and the guard on each, SHALL come from the machine a checkout declares**
(`declared-loop-machine`) rather than from agreement between the prompts. A prompt SHALL NOT be the only place a
transition is written: where the prompts and the declared machine disagree about what follows a step, the
declaration is what the harness obeys. This is what the prompts had become by accident — six files that had to
agree about a table none of them owned, which is how the repository came to hold three different answers to
which states exist.

#### Scenario: A person removes the hold, and that act is the approval
- **WHEN** a proposal is written and the hold applied
- **THEN** nothing proceeds until a person removes the label, and no second label records the approval

#### Scenario: A failure halts and says why in the tool's words
- **WHEN** a command fails mid-loop
- **THEN** it stops, applies the hold, and comments the failure verbatim rather than retrying

#### Scenario: The order lives in one place, not in six
- **WHEN** what follows a step has to change
- **THEN** it changes in the declared machine, and no prompt has to be edited to agree with the others

### Requirement: The WIP limit and the label names are configuration the loop reads
`.harness/config.json` SHALL carry the loop's tunables — `wipLimit` and the label names for ready, in-progress
and holding — and the implement gate SHALL enforce the limit by naming the issues currently holding it. The
limit SHALL be a person's decision recorded in that file rather than a constant in a prompt.

**That file SHALL have a sibling**: the declared machine, carrying the loop's shape where
`.harness/config.json` carries its numbers. Both SHALL be read on the terms every declared file is read on, and
a limit the harness cannot read SHALL stop a launch rather than permit one. The limit SHALL be enforced by the
declared machine's guards as well as by the implement gate, and both SHALL read the same file, so the automatic
path and the written one cannot enforce two different numbers.

#### Scenario: The WIP gate names what is in the way
- **WHEN** the limit is reached and another implement starts
- **THEN** it refuses and names the issues occupying the limit

#### Scenario: One number, two enforcements
- **WHEN** the automatic path and a person following `implement` both check the limit
- **THEN** both read `.harness/config.json` and reach the same answer

#### Scenario: An unreadable limit stops a launch
- **WHEN** the tunables file is absent or malformed
- **THEN** the automatic path starts nothing for that Folder rather than starting without a limit
