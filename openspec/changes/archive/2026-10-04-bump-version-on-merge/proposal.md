# Bump the version on every merge to master

## Why

Master has landed five pull requests since the last release — the whole of epic #435 — and the
version says nothing about it: the last tag is `v0.1.3` from 27 September, while
`src/Directory.Build.props` already reads `0.2.2` with no tag and no release ever made for it. A
version that only moves when somebody remembers is a version nobody can cite: a person reporting
a bug has "current master", and a release is a name for a state of the software that today does
not exist.

## What Changes

- One script — `.harness/scripts/release` — creates the tag and the release for what just merged:
  tag `v<Version>` on the squash commit, release notes gathered from the pull requests between
  the previous tag and now.
- The merge itself bumps: `/aio:sync`'s close-out (the same edit that carries the archived change
  and the retro) also bumps `<Version>`'s patch by one, so the tag names a master whose source
  already carries the version the tag says.
- `.harness/scripts/merge` calls `release` once its own squash-merge succeeded, and answers its
  JSON line with a new `released` field: the tag it created, or the reason there is none. The
  three callers — `/aio:sync`, the loop's `merge` node, the app's merge button — inherit the
  behaviour without changing shape.
- Idempotent by naming: a tag that already exists is said and kept — never rewritten, never
  duplicated. A release that already exists is said too.
- The launch that settles the backlog: `v0.2.2` tagged and released over the master that carries
  epic #435, produced by the same script, recorded in the issue's check.
- `minor` when the PR title says feature-shaped work: the merge decides patch vs minor by the
  conventional-commit prefix of the PR's title, checked by the script, overridable by the caller.
  The default is patch.

## Capabilities

### Modified Capabilities

- `read-product-version`: the version step now takes a new minor or patch revision when it moves,
  which also to the published tag belongs — the release is the public word of what the build says.

## Impact

- `.harness/scripts/merge` (caller, now also the release's), `.harness/scripts/release` (new),
  `src/Directory.Build.props` (bumped by the sync close-out, not by this change's code).
- `/aio:sync` (`land-a-change`) gains the bump instruction in prose: the close-out edit carries
  the version alongside the retro.
- No backend, no migrations, no frontend contracts — the version's single source stays MSBuild.
- The tags land on this repository at the vendor; shared state, created by the script the person
  already runs.
