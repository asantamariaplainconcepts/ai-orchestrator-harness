## 1. Seam

- [x] 1.1 Add `History` to `IWorkspaceProbe` and the `WorkspaceHistory` record (commits with short hash, subject, author, date; the trunk name measured against; truncation marker) in `src/shared/AiOrchestrator.BuildingBlocks/World/IWorkspaceProbe.cs`
      req: the-world-seam/4, read-the-commits-ahead/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IWorkspaceProbe.cs
- [x] 1.2 Implement `GitWorkspaceProbe.History` beside `Position` in `src/shared/AiOrchestrator.Infrastructure/Git/GitWorkspaceProbe.cs`: one `git log <trunk>..HEAD --format` process, NUL-separated fields, capped, null where `Position` answers null
      req: the-world-seam/4, read-the-commits-ahead/1, read-the-commits-ahead/3, read-the-commits-ahead/4
      files: src/shared/AiOrchestrator.Infrastructure/Git/GitWorkspaceProbe.cs, src/shared/AiOrchestrator.Infrastructure/Workspace/WorkspaceProbe.cs
- [x] 1.3 Tests in `src/tests/AiOrchestrator.InfrastructureTests/GitPositionTests.cs` (or a sibling file): two ahead lists two with hash/subject/author/date; zero ahead is empty-not-null; no trunk is null-not-empty; a branch past the cap is truncated; a non-repository is null
      req: read-the-commits-ahead/1, read-the-commits-ahead/2, read-the-commits-ahead/3, read-the-commits-ahead/4
      files: src/tests/AiOrchestrator.InfrastructureTests/GitHistoryTests.cs

## 2. Use case

- [x] 2.1 Read the history for the scoped working directory in `GetFolder` and carry it on `Response` — per request, never stored, in `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/GetFolder.cs`
      req: read-the-commits-ahead/1, read-the-commits-ahead/2, read-the-commits-ahead/4
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/GetFolder.cs
- [x] 2.2 Workspace functional coverage over a real checkout: the `history` entry is present for a worktree ahead of the trunk and absent (null) for one with no trunk
      req: read-the-commits-ahead/1, read-the-commits-ahead/2
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/WorktreeHistoryTests.cs, src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/WorkspaceFixture.cs

## 3. Frontend

- [x] 3.1 `history` on the worktree detail in `src/frontend/shared/http/contracts.ts`, and the folder data wiring through `getFolder` in `src/frontend/shared/http/client.ts` if the mapping lives there
      req: read-the-commits-ahead/1, read-the-commits-ahead/2
      files: src/frontend/shared/http/contracts.ts
- [x] 3.2 Copy in `src/frontend/shared/i18n/en.ts`, grouped by the folder screen's block: tab label, "Nothing ahead of the trunk", "no trunk to be ahead of", truncated-list sentence, column labels
      req: read-the-commits-ahead/2, read-the-commits-ahead/3
      files: src/frontend/shared/i18n/en.ts
- [x] 3.3 `CommitsTab.tsx` in `src/frontend/features/folders/`: rows of short hash, subject, author, relative date, composed from `shared/ui/kit.tsx`, with the two empty states as sentences
      req: read-the-commits-ahead/1, read-the-commits-ahead/2, read-the-commits-ahead/3, read-the-commits-ahead/4
      files: src/frontend/features/folders/CommitsTab.tsx
- [x] 3.4 `commits` in `fixedTabs` in `src/frontend/features/folders/FolderDetailScreen.tsx` — `scope: "worktree"`, `git: true` — and its render site keyed on the resolved working directory
      req: read-the-commits-ahead/2
      files: src/frontend/features/folders/FolderDetailScreen.tsx

## 4. Gate

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` passes, every assembly counted; `openspec validate "commits-ahead-of-trunk" --strict` passes
      req: read-the-commits-ahead/1, read-the-commits-ahead/2
      files: src/tests/AiOrchestrator.InfrastructureTests/GitHistoryTests.cs
- [x] 4.2 The issue's check observed on the running product: a worktree 2 ahead lists two rows; one on the trunk says "Nothing ahead of the trunk"; a folder with no remote says so; a non-repository has no tab
      req: read-the-commits-ahead/1, read-the-commits-ahead/2, read-the-commits-ahead/3, read-the-commits-ahead/4
      files: src/tests/AiOrchestrator.InfrastructureTests/GitHistoryTests.cs, src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/WorktreeHistoryTests.cs
