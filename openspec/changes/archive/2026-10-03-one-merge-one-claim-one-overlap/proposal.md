## Why

Four loose ends of the loop, found reviewing it on 2026-10-03:

- **The automatic loop never got past `refine`.** The `overlap` node called
  `.harness/scripts/file-overlap`, which did not exist, and answered `clear: false` in its place, so
  `claim` and everything after it never ran.
- **Three ways to merge.** The loop's `land` node ran `gh pr merge --squash` and deleted the remote
  branch; `/aio:sync` called the app's merge endpoint; the endpoint ran `gh pr merge --squash` and
  left the remote branch — the claim — standing, so a merged issue still read as in flight.
- **A claim could be lost to itself.** Pushing the trunk commit as `change/<n>` cannot tell two
  clones apart, so a `/aio:propose` rerun after a failure stopped as if someone else held its issue.
- **ADR-0007** sends every gate to the main checkout because the CLI measured it from a worktree in
  #388; the same command measured the worktree on 2026-10-03.

## What Changes

- **`.harness/scripts/merge`** is the one merge: expected head checked and passed to
  `--match-head-commit`, `--squash`, one observation instead of a retry when gh fails, a clean-tree
  guard, and the branch deleted at origin. The loop's node (renamed `land` → `merge`), `/aio:sync`
  and the app's merge button (`GhPullRequestLookup.Squash`) all run it; the app runs the
  repository's copy, or the one it carries when the repository has none.
- **`.harness/scripts/claim`** is the one claim: an empty commit of its own pushed as
  `change/<n>`, remembered in `refs/aio/claims/<n>` so a rerun recognises it.
- **`.harness/scripts/file-overlap`** answers `overlap`: in flight is every `change/<n>` at origin,
  compared by declared Seam paths (and Task input `seam.paths`), by suffix and by directory, without
  case. An issue with no Seam is never clear.
- **ADR-0007 is rejected** with the measurement; `AGENTS.md#the-gate` stands.
- The three scripts ship with the adoption and are made executable.

## Capabilities

### Modified Capabilities

- `land-a-change`: the vendor merge is the shared script, and it deletes the remote branch.
- `run-the-declared-loop`: the `overlap` node answers from the claimed branches and declared seams.
- `issue-lifecycle` (through `issue-state-lives-in-one-comment`'s delta): the claim is the script's.

## Impact

- `.harness/scripts/{merge,claim,file-overlap}`, `.harness/issue-loop.yaml`.
- `GhPullRequestLookup.Squash`, `HarnessManifest`, `HarnessAdopter`, the Infrastructure `.csproj`.
- `.ai/commands/aio/{sync,propose,waves}.md`, `docs/adr/0007-*.md`.
- The merge button now deletes the branch at origin, which it did not before.
