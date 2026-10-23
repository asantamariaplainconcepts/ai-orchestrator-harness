## ADDED Requirements

### Requirement: The vendor merge is one script, run by the button, the loop and `/aio:sync`
The squash-merge of a pull request SHALL be performed by `.harness/scripts/merge`, and the app's merge
seam, the loop's `merge` node and `/aio:sync` SHALL each run it rather than call `gh pr merge`
themselves. The app SHALL run the repository's copy from the worktree, or the copy it carries when
the repository has none. The script SHALL refuse uncommitted files; SHALL refuse a pull request whose
head is not the expected head, and pass that head to the vendor so the check holds at the merge;
SHALL squash; SHALL read the pull request's state once when the merge call fails and count `MERGED`
as merged; and SHALL delete the branch at origin, because the pushed branch is the claim and a claim
left behind reads as work in flight. A branch already gone at origin SHALL count as deleted.

#### Scenario: The button deletes the claim
- **WHEN** a change lands from the merge button
- **THEN** its `change/<n>` branch no longer exists at origin

#### Scenario: A head that moved is not merged
- **WHEN** the expected head differs from the pull request's head
- **THEN** nothing is merged and the answer names both heads

#### Scenario: A lost response is settled by looking
- **WHEN** the merge call fails but the pull request reads `MERGED` afterwards
- **THEN** the change counts as merged, by observation, and nothing is retried

#### Scenario: One merge, three callers
- **WHEN** the loop's `merge` node, `/aio:sync` or the merge button lands a change
- **THEN** each runs `.harness/scripts/merge`
