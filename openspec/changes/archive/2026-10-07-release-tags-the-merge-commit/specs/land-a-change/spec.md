## ADDED Requirements

### Requirement: The release tags the merge's own commit, with the version that commit carries

After a squash-merge, `.harness/scripts/merge` SHALL ask the vendor for the merge commit it made,
fetch it, and run the release step on that commit — never on `master` as the local checkout holds
it, which `gh pr merge` does not move. When the vendor names no merge commit, nothing SHALL be
tagged and the answer SHALL say so.

`.harness/scripts/release` SHALL read `<Version>` from `src/Directory.Build.props` as it is at the
commit being tagged, not from the checkout it runs in, so the tag and the number it names come from
one commit. It SHALL pack only when the checkout's files equal that commit's, because the pack step
builds the checkout; otherwise it SHALL tag and publish nothing and say why.

#### Scenario: The merge's squash is what is tagged

- **WHEN** a pull request whose close-out bumped the version to 0.2.16 is squash-merged by the script
- **THEN** `v0.2.16` is created on that squash commit, not on the commit master held before it

#### Scenario: The version comes from the tagged commit

- **WHEN** the release step runs from a checkout whose props say 0.2.15 on a commit whose props say 0.2.14
- **THEN** the step works with `v0.2.14`, and never names `v0.2.15` for that commit

#### Scenario: A checkout that is not the commit packs nothing

- **WHEN** the release step would pack a commit whose files the checkout does not hold
- **THEN** nothing is packed, tagged or published, and the answer names the checkout and the commit
