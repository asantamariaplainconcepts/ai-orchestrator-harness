## Why

The merge's release step tagged a commit other than the merge it followed (#462). `merge` ran
`release master`, and `gh pr merge` never moves the local master: v0.2.14 landed on `93d7f02b`, the
master before #472's own squash. Bringing master up to date by hand before the next merge did not
help — the squash does not exist locally until it is fetched — and v0.2.15 landed on `8ac6f113`,
one commit short of `1febf577`, with the version read from the bump's worktree while the commit
came from master. The tag and the number it carries came from two places.

## What Changes

- `merge` asks the vendor for the merge commit it just made, fetches it, and passes that SHA to
  `release`. No merge commit named, nothing tagged, and the answer says so.
- `release` reads `<Version>` from `src/Directory.Build.props` **at the commit it tags**, with
  msbuild, not from the checkout it runs in.
- `release` packs only when the checkout holds the commit's files; otherwise it says so and tags
  nothing, because `pack.sh` builds the checkout.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `land-a-change`: the merge's release step tags the merge's own commit, with the version that
  commit carries, and packs only that commit's files.

## Impact

`.harness/scripts/merge`, `.harness/scripts/release`. No product code, wire or migration. The copy of
`merge` the app carries learns it with the next app build.
