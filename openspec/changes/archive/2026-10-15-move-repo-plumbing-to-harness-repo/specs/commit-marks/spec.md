## MODIFIED Requirements

### Requirement: A rebase in the loop keeps each committer
The `rebasing` state SHALL replay the branch with `git rebase -x .harness/repo/keep-committer <upstream>`,
which gives each replayed commit the committer it had before, so a person's commit stays without a mark
and a run's keeps its run. A pick that came out empty SHALL be left alone without stopping the rebase.

#### Scenario: A run's commit and a person's, rebased by another run
- **WHEN** a branch with a commit of run `aa` and then a person's is rebased by run `bb` with `keep-committer`
- **THEN** the first is still committed by `aio run aa` and the second by the person
