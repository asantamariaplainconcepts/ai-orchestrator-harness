# commit-marks Specification

## Purpose
Every commit the loop makes says so in its committer, so a person's commit on a PBI's branch can be told
from the loop's by who made it, not by when. On one machine the engine, its runs and the person commit
with the same git identity, so the author tells nobody apart. Written with the code of #741
(`src/shared/AiOrchestrator.BuildingBlocks/Ports/CommitMark.cs`, `AgentLaunch.cs`, `LocalScriptRunner.cs`,
`scripts/keep-committer`).
## Requirements
### Requirement: A run's commits are committed by the run
Every agent started for a run SHALL have `GIT_COMMITTER_NAME` set to `aio run <run id>` and
`GIT_COMMITTER_EMAIL` to `noreply@aio.invalid` in its environment, in a worktree and in an sbx sandbox,
where they are passed with `sbx exec -e` like every other variable of the launch. The author SHALL be
left as it is. A folder session, which has no run, and a terminal a person opens SHALL carry no
committer of the loop.

#### Scenario: An agent of a run
- **WHEN** the agent of run `0199e2a4-1111-7000-8000-000000000741` is launched
- **THEN** its environment has `GIT_COMMITTER_NAME=aio run 0199e2a4-1111-7000-8000-000000000741` and `GIT_COMMITTER_EMAIL=noreply@aio.invalid`, and no author

### Requirement: A script's commits are committed by the script
Every script the engine runs SHALL have `GIT_COMMITTER_NAME` set to `aio script <name>` and
`GIT_COMMITTER_EMAIL` to `noreply@aio.invalid` in its environment. The claim's commit, made with
`git commit-tree`, SHALL take its committer from there.

#### Scenario: The claim
- **WHEN** `claim 42` runs as the engine runs it, against a repository on disk
- **THEN** the commit it pushes to `change/42` is committed by `aio script claim` and authored as before

### Requirement: A rebase in the loop keeps each committer
The `rebasing` state SHALL replay the branch with `git rebase -x scripts/keep-committer <upstream>`,
which gives each replayed commit the committer it had before, so a person's commit stays without a mark
and a run's keeps its run. A pick that came out empty SHALL be left alone without stopping the rebase.

#### Scenario: A run's commit and a person's, rebased by another run
- **WHEN** a branch with a commit of run `aa` and then a person's is rebased by run `bb` with `keep-committer`
- **THEN** the first is still committed by `aio run aa` and the second by the person

### Requirement: The mark is read from the committer or a trailer
A commit SHALL be the loop's when its committer begins `aio run ` or `aio script `, or when its message
ends with a trailer `Aio-Run: <id>` or `Aio-Script: <name>`, for a path that cannot keep the committer.
Every other commit SHALL be a person's.

#### Scenario: A trailer
- **WHEN** a commit committed by the person ends with `Aio-Run: 00000000-0000-0000-0000-0000000000aa`
- **THEN** it is the loop's

