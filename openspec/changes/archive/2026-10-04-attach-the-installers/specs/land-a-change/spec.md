## MODIFIED Requirements

### Requirement: The vendor merge is one script, run by the button, the loop and `/aio:sync`

The squash-merge of a pull request SHALL be performed by `.harness/scripts/merge`, and the app's merge
seam, the loop's `merge` node and `/aio:sync` SHALL each run it rather than call `gh pr merge`
themselves. The app SHALL run the repository's copy from the worktree, or the copy it carries when
the repository has none. The script SHALL refuse uncommitted files; SHALL refuse a pull request whose
head is not the expected head, and pass that head to the vendor so the check holds at the merge;
SHALL squash; SHALL read the pull request's state once when the merge call fails and count `MERGED`
as merged; and SHALL delete the branch at origin, because the pushed branch is the claim and a claim
left behind reads as work in flight. A branch already gone at origin SHALL count as deleted.

The merge's release step SHALL deliver the installable, not only the name: the release it creates
SHALL carry the installer package for the version the tag names — built by the repository's own
pack step for that version — together with the full package and the release feed it leaves beside
it. A release whose version is not yet packed SHALL be packed before anything is tagged; a pack
that fails SHALL fail the release, tagging nothing. A release that already exists without its
artifacts — a state an earlier machinery landed — SHALL be completed in place, its tag never
moved.

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

#### Scenario: A release carries its installer

- **WHEN** a merge creates a release for the version its close-out bumped
- **THEN** the release's assets include the Setup package, the full package and the release feed for that version

#### Scenario: A pack that fails lands nothing

- **WHEN** the repository's pack step fails for the version being released
- **THEN** no tag is created and no release is published, and the answer names the pack's failure

#### Scenario: An assetless release is completed, not reproduced

- **WHEN** the release step finds its tag's release already existing with no artifacts
- **THEN** the artifacts are uploaded to that release and the tag is not moved
