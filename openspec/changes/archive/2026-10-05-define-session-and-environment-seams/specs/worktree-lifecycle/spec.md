## REMOVED Requirements

### Requirement: Git is asked first and the row follows
**Reason**: `worktree-lifecycle` is degraded from a first-class, UI-backed capability to an internal mechanism of the launcher. `IWorktreeManager` remains as the seam the launcher calls internally to create, reuse and retire a worktree, but the public HTTP surface (`POST /api/folders/{id:guid}/worktrees`, `CreateWorktree.cs`) and the folder dialog that calls it are removed — the launcher is the sole owner of the worktree, per epic #447. Git is still the record; the rows still follow it. The launcher's internal worktree ownership is documented under `choose-an-environment`.
**Migration**: The launcher's `StartRun` handler already injects `IWorktreeManager worktrees` and calls `worktrees.Ensure(...)`. Consumers that called the removed `POST`/`DELETE /api/folders/{id}/worktrees` endpoints move to starting a run against the agreed branch; the worktree the run needs is created/reused/retired by the launcher. Nothing that a worktree held is deleted by closing a view — the transcript, commits and pull request survive on the run.

### Requirement: A worktree branches from the repository's trunk unless the caller names a base
**Reason**: Covered by the removal above. Branching and base ref resolution move to the launcher's internal use of `IWorktreeManager.Ensure`, which already accepts a `baseRef`. The folder-facing dialog and its trunk-listing contract go; `IWorktreeManager` keeps its own `Ensure` contract.
**Migration**: The launcher's `Ensure` call carries the base ref the loop's `claim`/`prepare` already pass; nothing that named a base through the UI survives.

### Requirement: Removal is git's refusal first, and the directory goes before the row
**Reason**: Covered by the removal above. `IWorktreeManager.Remove` keeps its `force` seam — the launcher is the caller, and the safety is in the loop's `cleanup` step, which already runs after a merged pull request. The public remove endpoint and its screen offer go.
**Migration**: Removing a worktree moves to the loop's `cleanup` step (`.harness/scripts/cleanup`), which already calls `IWorktreeManager.Remove` behind the safety of a merged pull request. The manual bin next to a row in the sidebar is removed.

### Requirement: The stored worktrees are reconciled against the probe, and unreadable is not empty
**Reason**: With no public worktree UI, there is no stored row set of this product's own to reconcile against the probe. Git remains the record; the launcher reads worktrees through `IWorktreeManager` and `git worktree list`. The sidebar no longer carries a row to reconcile.
**Migration**: The sidebar's worktree rows are removed; the journey a worktree held is shown on the PBI it served and the run that worked in it, not on a row the probe reconciled.
