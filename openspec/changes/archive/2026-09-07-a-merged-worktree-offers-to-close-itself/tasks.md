## 1. i18n keys

- [x] 1.1 `shared/i18n/en.ts` — append `mergeAction.close`, `mergeAction.closeTitle`,
      `mergeAction.closeBody`, `mergeAction.closeConfirm`, and `mergeAction.closeMerged` keys
      beside the existing `mergeAction.*` block
      req: land-a-change/1

## 2. ChangeTab reads the PR state and swaps the button

- [x] 2.1 `features/folders/ChangeTab.tsx` — call `useWorktreePullRequest(detail.id, worktree.path)`
      (cache hit beside `VerdictRow`), derive `const merged = answer.data?.pullRequest?.state === "MERGED"`,
      pass `merged` to `MergeChangeDialog`
      req: land-a-change/1
      files: src/frontend/features/folders/ChangeTab.tsx
- [x] 2.2 `features/folders/ChangeTab.tsx` — swap the button label to `t("mergeAction.close")`
      with a `FolderMinus` icon when `merged`, keep `t("mergeAction.merge")` with `GitMerge` otherwise
      req: land-a-change/1
      files: src/frontend/features/folders/ChangeTab.tsx

## 3. MergeChangeDialog adapts its copy

- [x] 3.1 `features/folders/MergeChangeDialog.tsx` — accept an `alreadyMerged` prop, and when true
      use `t("mergeAction.closeTitle")`, `t("mergeAction.closeBody")`,
      `t("mergeAction.closeConfirm")` instead of the merge copy
      req: land-a-change/1
      files: src/frontend/features/folders/MergeChangeDialog.tsx
- [x] 3.2 `features/folders/MergeChangeDialog.tsx` — the "merged landed" result line uses
      `t("mergeAction.closeMerged")` when `alreadyMerged` was true on entry, keeping the existing
      `landed`/`resumed` lines for the merge path
      req: land-a-change/1
      files: src/frontend/features/folders/MergeChangeDialog.tsx

## 4. The gate

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` —
      full gate, both sides, no halves. 15/15 steps, 718 tests, 0 failures
