## 1. Preparation

- [x] 1.1 `Ensure` resolves the base from the remote's default branch when blank or `HEAD`, and fetches it
- [x] 1.2 A pushed branch is resumed from `origin/<branch>`; a local branch is attached; otherwise branch from the fetched base
- [x] 1.3 Refuse on uncommitted files, on a branch that diverged from what was pushed, and on a failed fetch
- [x] 1.4 Merge up to the base; on conflict abort and refuse naming the files
- [x] 1.5 Keep the no-`origin` path as it was, and leave `Create` untouched

## 2. Words

- [x] 2.1 `IWorktreeManager.Ensure` says what preparation guarantees
- [x] 2.2 `/aio:propose` uses the prepared `change/<issue>`

## 3. Verification

- [x] 3.1 `WorktreePreparationTests`: fetched trunk over a stale checkout, merge-up keeping work, conflict refused and aborted, dirty refused, pushed branch resumed, unreachable origin refused, no origin unchanged
- [x] 3.2 `aspire do ci` from this worktree: 7 of 7 test assemblies, 1086 tests, 0 failed
