## 1. Tab switch transition

- [x] 1.1 Add `useTransition` in `FolderDetailScreen` around `setTab`, so the previous tab stays visible while the new one mounts
      req: review-a-change-in-one-tab/3
      files: src/frontend/features/folders/FolderDetailScreen.tsx
- [x] 1.2 Verify the tab strip highlights the pressed tab immediately (before the transition completes)
      req: review-a-change-in-one-tab/3

## 2. Step panel skeletons

- [x] 2.1 In `ProposalStep`, replace `if (read.isPending) return <Loading />` with a skeleton of the two-pane layout (artifact list placeholder + reader placeholder)
      req: review-a-change-in-one-tab/3
      files: src/frontend/features/folders/ProposalStep.tsx
- [x] 2.2 In `CodeStep`, replace `if (diff.isPending) return <Loading />` with a skeleton of the file list + patch pane
      req: review-a-change-in-one-tab/3
      files: src/frontend/features/folders/CodeStep.tsx
- [x] 2.3 In `TestsStep`, replace `if (annotations.isPending) return <Loading />` with a skeleton of the tests heading + rows
      req: review-a-change-in-one-tab/3
      files: src/frontend/features/folders/TestsStep.tsx
- [x] 2.4 In `EvidencePanel`, replace any pending state with a skeleton of the evidence rows, not a full-panel spinner
      req: review-a-change-in-one-tab/3
      files: src/frontend/features/folders/EvidencePanel.tsx

## 3. Header does not block on pending queries

- [x] 3.1 Verify `ChangeTab`'s header paints when `useTask` is pending — `stepMarksFromLabels(undefined)` already returns empty set, check nothing else blocks
      req: review-a-change-in-one-tab/3
- [x] 3.2 Verify `VerdictRow` paints when `useWorktreePullRequest` is pending — `pullRequestPending: true` is already handled in `changeFacts`, check the row does not return null/Loading
      req: review-a-change-in-one-tab/3
- [x] 3.3 Verify the merge button in the header reads "Merge" (not "Close worktree") while `pr` is pending, and updates when it resolves
      req: review-a-change-in-one-tab/3

## 4. ProposalStep parallel fetch

- [x] 4.1 In `ProposalStep`, fire `useFileContents` for the first `present` artifact as soon as the change listing resolves, not after `useState` picks it
      req: review-a-change-in-one-tab/3
      files: src/frontend/features/folders/ProposalStep.tsx
- [x] 4.2 Verify the reader pane shows a skeleton while the file contents are loading, not `<Loading />`
      req: review-a-change-in-one-tab/3
      files: src/frontend/features/folders/ProposalStep.tsx

## 5. Verify

- [x] 5.1 Switch from Overview to Change and confirm the header paints before 100ms with cached data
      req: review-a-change-in-one-tab/3
- [x] 5.2 Confirm step marks appear when `useTask` resolves without remounting the header
      req: review-a-change-in-one-tab/3
- [x] 5.3 Switch between steps inside Change and confirm the header and rail do not remount
      req: review-a-change-in-one-tab/3
- [x] 5.4 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` and confirm a full green run
      req: review-a-change-in-one-tab/3
