## 1. One merge

- [x] 1.1 `.harness/scripts/merge`: expected head, `--squash --match-head-commit`, one observation on failure, clean-tree guard, remote branch deleted, one JSON line
- [x] 1.2 `GhPullRequestLookup.Squash` runs the repository's script, or the carried copy; a timeout keeps #337's single observation
- [x] 1.3 The loop's `land` node becomes `merge` and runs the script with `HEAD` as the expected head
- [x] 1.4 `/aio:sync` merges with the script and removes the worktree and local branch from the main checkout

## 2. One claim

- [x] 2.1 `.harness/scripts/claim`: an empty commit of its own via `commit-tree`, `*` required, remembered in `refs/aio/claims/<n>`, fast-forward when standing on the base
- [x] 2.2 The `claim` node, `/aio:propose` and `/aio:waves` run it

## 3. Overlap

- [x] 3.1 `.harness/scripts/file-overlap`: claimed branches at origin, Seam bullets and Task input paths, suffix and directory matching without case, unknown never clear
- [x] 3.2 The loop's comments no longer name anything as not built

## 4. Adoption and ADR

- [x] 4.1 Ship the three scripts and make everything under `.harness/scripts/` executable
- [x] 4.2 Reject ADR-0007 with the 2026-10-03 measurement

## 5. Verification

- [x] 5.1 `merge` against a stub gh and a real origin: merged, head moved, lost response observed, real failure, already merged, dirty tree
- [x] 5.2 `claim` with two clones: first wins, second loses on the same base, rerun recognised, fast-forward then push
- [x] 5.3 `file-overlap` with a stub gh: nothing in flight, disjoint, same file at another root, file under a declared directory, sibling name, Task input, missing Seam on either side, unreadable issue
- [x] 5.4 `PullRequestLookupTests`: the button runs the repository's script with the expected head, and a refusal or unprinted output is never a merge
- [x] 5.5 `aspire do ci` from this worktree: 7 of 7 test assemblies, 1091 tests, 0 failed
