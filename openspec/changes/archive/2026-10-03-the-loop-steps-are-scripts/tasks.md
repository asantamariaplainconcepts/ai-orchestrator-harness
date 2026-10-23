## 1. Scripts

- [x] 1.1 `prepare`: the logic of `Ensure`, from the main checkout even when run inside a worktree, accepting an issue number
- [x] 1.2 `cleanup`: worktree without force unless asked, branch only when nothing is lost, claim record dropped, already-gone counts as gone
- [x] 1.3 `status`: one edit, hold kept, eight states only, label created if missing, `--close`
- [x] 1.4 `gate`: hold, then `x-harness.wip`, unknown limit stops, self not counted
- [x] 1.5 `evidence`: the pasted bash of `implement.md`, plus an UNPUBLISHED answer when gh refuses the comment

## 2. The app

- [x] 2.1 `HarnessScript`: repository copy or carried copy in a temp file, login-shell PATH, one JSON line read
- [x] 2.2 `Ensure`, `Remove`, `RemoveBranch` and `Squash` run their scripts
- [x] 2.3 Embed and ship all five; `IWorktreeManager` docs name the scripts

## 3. Words

- [x] 3.1 `implement`, `propose`, `refine`, `sync`, `waves` and `set-issue-status` name the scripts
- [x] 3.2 `AGENTS.md` lists `.harness/scripts/` and says what is a script and what is an agent's

## 4. Verification

- [x] 4.1 `WorktreePreparationTests` through the carried `prepare`, plus three `cleanup` cases: pushed branch dropped, local-only branch kept, dirty worktree refused unless forced
- [x] 4.2 `status`, `gate` and `evidence` against a stub gh: hold kept across moves, unknown state refused, held gate, WIP full naming issues, done and closed, no captures, published, signed out
- [x] 4.3 `prepare` and `cleanup` by hand from the checkout and from inside the worktree
- [x] 4.4 `aspire do ci` from this worktree: 7 of 7 test assemblies, 1094 tests, 0 failed
