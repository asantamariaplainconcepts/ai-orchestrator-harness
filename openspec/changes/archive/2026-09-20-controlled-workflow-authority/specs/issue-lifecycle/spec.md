## MODIFIED Requirements

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
`.ai/commands/aio/` SHALL provide the manual loop as prompts, single-sourced and symlinked so
every runtime reads the same file: **refine** interrogates an issue to the Definition of Ready
and writes no code; **propose** branches from fresh `origin/master`, writes the proposal into
the issue, opens a **draft** pull request and applies the hold in the same edit; **implement**
checks the hold gate and then the WIP gate before writing code, then marks the pull request
ready and holds again; **sync** is the only merge path — squash-merge, close the issue and its
milestone if it was the last one open, delete the branch and its worktree, append the retro
entry; **review** writes what the change taught, or nothing; **status** is read-only.
Nothing SHALL automatically remove a hold or retry: a failure SHALL halt, apply the hold,
and comment the reason in the words the tool used.

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

#### Scenario: Controlled approval is not a missing label
- **WHEN** a controlled Task has no Hold but no current bound grant
- **THEN** shared dispatch remains blocked
