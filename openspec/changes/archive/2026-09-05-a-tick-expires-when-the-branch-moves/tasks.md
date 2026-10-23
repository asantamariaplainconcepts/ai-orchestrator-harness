## 1. The row

- [x] 1.1 `Domain/ReviewStep.cs` — the aggregate, with the five known step keys on it and the doc
      comment saying why it is its own set and not a collection under `Worktree`
- [x] 1.2 `Persistence/WorkspaceDbContext.cs` — `DbSet<ReviewStep>`, `review_steps`, widths, and the
      unique index on `(FolderId, WorkingPath, StepKey)`
- [x] 1.3 Generate the migration — `dotnet ef migrations add ReviewStepsAreWorktreeState` against the
      Workspace module, `--output-dir Persistence/Migrations`. Never hand-written, and the
      `*.Designer.cs` snapshot copy comes with it

## 2. The use cases

- [x] 2.1 `WorkspacePermissions.cs` — `Review = "workspace:review"` appended, with the reason on it
- [x] 2.2 `WorkspaceModule.cs` — granted to `ProjectRole.Member` beside the other five
- [x] 2.3 `FolderErrors` — `NoSuchStep` and `StepNotComparable` appended at the end of the class
- [x] 2.4 `UseCases/GetReviewSteps.cs` — `GET /api/folders/{id:guid}/review-steps`, `[Requires(Read)]`,
      `WorkingRoot.Resolve`, one `Since` per marked step, four answers on the wire
- [x] 2.5 `UseCases/MarkReviewStep.cs` — `PUT /api/folders/{id:guid}/review-steps`,
      `[Requires(Review)]`, unknown step refused, unreadable head refused, `done: false` deletes

## 3. The screen

- [x] 3.1 `contracts.ts` — the review-step shape, appended; nothing existing changes
- [x] 3.2 `queries.ts` — `keys.reviewSteps`, `useReviewSteps`, `useMarkReviewStep`
- [x] 3.3 `en.ts` — a `changeStep.*` block at the end, wording taken from `folders.check.*`
- [x] 3.4 `ChangeTab.tsx` — `stepsDone` becomes a hook over the query; `changeStepsLeft` becomes
      `useChangeStepsLeft` and still derives from it; the rail's circle and second line say the state;
      the panel header carries the sentence and the mark control. **The five slots at 338–342 are not
      touched**
- [x] 3.5 `FolderDetailScreen.tsx` — three lines: the import, the hoisted hook beside `runsHere` /
      `tasksHere`, and the counter

## 4. Proof

- [x] 4.1 A functional test file for the two use cases: the four answers, both refusals, the
      not-this-folder's-path refusal, unmark, mark-twice-is-one-row, and two directories independent
- [x] 4.2 Every absence assertion gets a positive anchor from the same read, then is proved by
      mutation — force the branch, require the absence assertions to fail, revert, confirm an empty
      diff, re-run green
- [x] 4.3 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` green,
      with Workspace's count grown and no other assembly moved
- [x] 4.4 Run the app and observe checks 1–4 of the ticket: three marks survive a restart, five marks
      plus one commit takes the counter back to five, a rebase past a marked commit reads as the third
      state, and two worktrees are independent
