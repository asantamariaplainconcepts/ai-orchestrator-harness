## Why

A run's worktree starts from whatever the main checkout has checked out. `StartRun` passes `HEAD`
when the start names no base, and `GitWorktreeManager.Ensure` hands that to `git worktree add`
without fetching anything. So a run started while the checkout sits on a stale `master` — or on
somebody's feature branch — builds on that, and the drift surfaces at merge time as a conflict or as
work that silently includes commits it should not.

The reuse path has the mirror problem: the second run of a task lands in the worktree the first one
made, exactly as it was, however far the trunk has moved since.

## What Changes

- `Ensure`, the one door every run goes through, prepares the worktree deterministically when the
  repository has an `origin`:
  1. A blank or `HEAD` base means the remote's default branch, asked with `ls-remote --symref` —
     not `origin/HEAD`, which is a clone-time cache. A named base is fetched too.
  2. The base is fetched; a fetch that fails refuses the run.
  3. A branch already pushed is resumed from `origin/<branch>`, so a second machine continues the
     work rather than forking it.
  4. The worktree is merged up to the base — a merge, not a rebase, because the branch is the claim
     and already pushed. A conflict aborts the merge and refuses the run naming the files.
  5. Uncommitted files refuse the run, and so does a local branch that diverged from what was pushed.
- A repository with no `origin` branches from the base it was given, as before.
- `Create`, the dialog's door, is unchanged: there a person picks the base on purpose.
- `/aio:propose` uses the prepared `change/<issue>` it finds itself on.

## Capabilities

### Modified Capabilities

- `run-a-task`: a run's worktree is prepared from the fetched trunk.

## Impact

- `GitWorktreeManager.Ensure` and `IWorktreeManager`'s summary; `.ai/commands/aio/propose.md`.
- A new `WorktreePreparationTests` against a real bare `origin`.
- A run now needs the network to start in a repository with an `origin`. That is deliberate: a run
  that cannot see the trunk cannot know it is not starting stale.
