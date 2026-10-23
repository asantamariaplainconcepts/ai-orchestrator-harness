## Context

`merge` runs from the change's worktree and called `release master`; `release` read the version
from its own checkout and the commit from `master`.

## Decisions

- **The commit is named by the vendor.** `gh pr view --json mergeCommit` is the squash `gh pr merge`
  created; it is fetched so the local repository can name it. Falling back to `master` when the
  vendor names nothing is the fault being removed, so that case tags nothing and says why.
- **The version is the commit's.** The props are written out of the commit to a temporary
  directory and read with `dotnet msbuild -getProperty:Version`, the same tool as before.
- **The package is the checkout's, so it is checked.** `git diff --quiet <commit>` and no untracked
  files: the checkout's content equals the commit's. A worktree whose branch was brought up to date
  before the squash passes; one that was not is refused rather than packed.

## Risks / Trade-offs

- [A merge from a stale branch now releases nothing] → the answer names the reason, and
  `release <sha>` from an up-to-date checkout settles it.
