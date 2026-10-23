## Why

The condition strip already says `2 ahead, 0 behind` — but the question that follows is "what are those two commits?", and answering it today means leaving the worktree screen and typing `git log` in a terminal. The screen whose whole purpose is to show a worktree's state cannot show the work on the branch.

Issue: #309.

## What Changes

- `IWorkspaceProbe` grows `Task<WorkspaceHistory?> History(string path, CancellationToken)` — the commits this branch has that the trunk does not (short hash, subject, author, date), capped by the implementation. A separate call for the same reason `Position` is: `Read` is what the poller calls on a timer, and this must not add git processes to that loop.
- `GitWorkspaceProbe` implements it beside `Position`: `rev-list` for the set, `log --format` for the fields per commit. Null answers the same cases `Position`'s null does — not a repository, no trunk, git refusing.
- `GetFolder` reads the history for the working directory the screen is scoped to, per request, never stored — the same rule `Position` follows.
- `contracts.ts` carries a `history` entry on the worktree detail; `queries.ts` and `en.ts` follow, grouped by the folder screen's block.
- A **Commits** tab on the worktree's strip — `scope: "worktree"`, `git: true` — listing hash, subject, author, relative date. Empty state is a sentence ("Nothing ahead of the trunk"), not a blank pane; no trunk is its own sentence, not zero.

## Capabilities

### New Capabilities
- `read-the-commits-ahead`: the commits a worktree's branch carries that the trunk does not, read per request and listed on the worktree screen without leaving it.

### Modified Capabilities
- `the-world-seam`: `IWorkspaceProbe` gains the `History` method — a new read on an existing seam interface, same shape as `Position` (null for "cannot say", a separate call rather than fields on `Read`).

## Impact

- `src/shared/AiOrchestrator.BuildingBlocks/World/IWorkspaceProbe.cs` — new method + `WorkspaceHistory` record.
- `src/shared/AiOrchestrator.Infrastructure/Git/GitWorkspaceProbe.cs` — implementation beside `Position`.
- `src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/GetFolder.cs` — read per request for the scoped directory.
- `src/frontend/shared/http/contracts.ts`, `src/frontend/shared/http/queries.ts`, `src/frontend/shared/i18n/en.ts` — the `history` entry, the fetch, the copy.
- `src/frontend/features/folders/CommitsTab.tsx` — new tab, composed from `shared/ui/kit.tsx`.
- `src/frontend/features/folders/FolderDetailScreen.tsx` — `commits` in `fixedTabs` (`scope: "worktree"`, `git: true`); a checkout's tabs unchanged, a non-repository does not draw it.
- Tests: `AiOrchestrator.InfrastructureTests` beside `GitPositionTests`, and the workspace functional tests over a real checkout.

No git write actions, no history graph, no branch switching — the issue's "What it is not" holds.
