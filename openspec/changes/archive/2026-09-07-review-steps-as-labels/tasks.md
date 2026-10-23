## 1. Backend — remove the Workspace review-step model

- [x] 1.1 Delete `Domain/ReviewStep.cs` — the aggregate
- [x] 1.2 Delete `Features/Folders/UseCases/GetReviewSteps.cs` — the query use case and its route
- [x] 1.3 Delete `Features/Folders/UseCases/MarkReviewStep.cs` — the command use case and its route
- [x] 1.4 Remove `DbSet<ReviewStep>` and the `Entity<ReviewStep>` block from `Persistence/WorkspaceDbContext.cs`
      req: review-steps-per-worktree/3, review-steps-per-worktree/4
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Persistence/WorkspaceDbContext.cs
- [x] 1.5 Remove the `ReviewStepsAreWorktreeState` migration, its `.Designer.cs`, and the snapshot entity
      req: review-steps-per-worktree/3, review-steps-per-worktree/4
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Persistence/Migrations/WorkspaceDbContextModelSnapshot.cs
- [x] 1.6 Remove `FolderErrors.NoSuchStep` and `FolderErrors.StepNotComparable` from `GetFolder.cs`
      req: review-steps-per-worktree/3
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/GetFolder.cs
- [x] 1.7 Remove the `Review` constant from `WorkspacePermissions.cs` and its grant from `WorkspaceModule.cs`
      req: review-steps-per-worktree/3
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/WorkspacePermissions.cs, src/modules/Workspace/AiOrchestrator.Modules.Workspace/WorkspaceModule.cs

## 2. Frontend — read steps from labels, write through SetTaskLabels

- [x] 2.1 Remove `ReviewStepMark` and `ReviewSteps` from `contracts.ts`
      req: review-steps-per-worktree/2
      files: src/frontend/shared/http/contracts.ts
- [x] 2.2 Remove `useReviewSteps`, `useMarkReviewStep`, and the `reviewSteps` key from `queries.ts`
      req: review-steps-per-worktree/2
      files: src/frontend/shared/http/queries.ts
- [x] 2.3 Rewrite `useStepMarks` in `ChangeTab.tsx` to read from `task.data?.labels` filtered for `reviewed:`
      req: review-steps-per-worktree/2, review-a-change-in-one-tab/1
      files: src/frontend/features/folders/ChangeTab.tsx
- [x] 2.4 Rewrite `stepsDone` to return the set of step IDs whose label is present
      req: review-steps-per-worktree/2, review-a-change-in-one-tab/1
      files: src/frontend/features/folders/ChangeTab.tsx
- [x] 2.5 Replace the mark button's mutation from `useMarkReviewStep` to `useSetTaskLabels`, passing `add: ["reviewed:<step>"]` or `remove: ["reviewed:<step>"]`
      req: review-steps-per-worktree/3
      files: src/frontend/features/folders/ChangeTab.tsx
- [x] 2.6 Invalidate the detail cache (`["tasks","by-key",key,repository]`) on label-write success so the Change tab re-reads
      req: review-steps-per-worktree/3
      files: src/frontend/shared/http/queries.ts

## 3. Frontend — remove the freshness model

- [x] 3.1 Remove `markSaid` from `ChangeTab.tsx`
      req: review-a-change-in-one-tab/2
      files: src/frontend/features/folders/ChangeTab.tsx
- [x] 3.2 Simplify the rail glyph: green tick for marked, muted ordinal for unmarked — no amber state
      req: review-a-change-in-one-tab/1, review-a-change-in-one-tab/2
      files: src/frontend/features/folders/ChangeTab.tsx
- [x] 3.3 Simplify the rail's second line: the hint until marked, then "marked" — no commit sentence
      req: review-a-change-in-one-tab/1, review-a-change-in-one-tab/2
      files: src/frontend/features/folders/ChangeTab.tsx
- [x] 3.4 Remove the `changeStep.adrift`, `changeStep.here`, `changeStep.behindOne`, `changeStep.behind`, `changeStep.on` keys from `en.ts`
      req: review-a-change-in-one-tab/2
      files: src/frontend/shared/i18n/en.ts
- [x] 3.5 Keep `changeStep.mark`, `changeStep.unmark`, `changeStep.marking`
      req: review-a-change-in-one-tab/1
      files: src/frontend/shared/i18n/en.ts

## 4. Tests — update and remove

- [x] 4.1 Delete `ReviewStepTests.cs`
      req: review-steps-per-worktree/3
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/ReviewStepTests.cs
- [x] 4.2 Delete `RailExpiryJourneyTests.cs`
      req: review-a-change-in-one-tab/2
      files: src/tests/AiOrchestrator.EndToEndTests/RailExpiryJourneyTests.cs
- [x] 4.3 Update `AnnotationTests.cs` to reflect the deleted `ReviewStep.cs` reference
      req: review-steps-per-worktree/3
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/AnnotationTests.cs

## 5. Gate

- [x] 5.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` passes
- [x] 5.2 `openspec validate "review-steps-as-labels" --strict` passes
