## 1. The record

- [x] 1.1 Add `issue-state.mjs`: find, parse, render and write the `aio:state` comment through `gh api`, total it, and print it when run directly
- [x] 1.2 Rewrite `push-session-report.mjs` to require `--issue`, take `--change` and `--pr`, replace the session's figure and append the phase, and print `UNPUBLISHED` on failure
- [x] 1.3 Merge transcript rows by message and request id keeping the maximum, and price by longest prefix from the `ModelPrices.cs` table
- [x] 1.4 Point `scripts/collect-corpus.mjs` at the issue's comment for cost
- [x] 1.5 Ship both scripts in `HarnessManifest` and the Infrastructure `.csproj`

## 2. The skills

- [x] 2.1 `push-session-report` and `collect-usage` describe the comment and the one read command
- [x] 2.2 `set-issue-status` names the eight states and stops asking for a confirmation the command already gave
- [x] 2.3 `read-issue` and `set-issue-status` read the hold from `labels.hold`

## 3. The commands and documents

- [x] 3.1 `implement`, `ship` and `waves` read the WIP cap from `x-harness.wip`
- [x] 3.2 `propose` pushes `change/<issue>` before writing; `waves` claims the same branch, pushed
- [x] 3.3 Every push step passes `--issue`; `propose` passes the change and the pull request
- [x] 3.4 `sync` closes at `status:done`; `review` reads the comment instead of a deleted file
- [x] 3.5 `CONTRIBUTING.md` lifecycle, the loop's header comment, `docs/design/README.md` and `.gitignore`

## 4. Verification

- [x] 4.1 Run the script against a stub `gh`: one comment created then edited, a session pushed twice counted once, a block-split message counted once, `claude-opus-5-5` priced
- [x] 4.2 `openspec validate issue-state-lives-in-one-comment --strict`
- [x] 4.3 `aspire do ci` from this worktree: 7 of 7 test assemblies, 1086 tests, 0 failed
