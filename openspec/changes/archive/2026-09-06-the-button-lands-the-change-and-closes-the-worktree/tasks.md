## 1. The seams that were short — written down before anything was touched

- [x] 1.1 `World/IWorktreeManager.cs` — `RemoveBranch(repositoryPath, branch, ct)`, with the
      `<summary>` saying why the forcing form is the only one that works on a squash-merged branch and
      what makes it safe (the pull request observed merged first)
- [x] 1.2 `Infrastructure/Git/GitWorktreeManager.cs` — `git branch -D <branch>` run in the checkout,
      git's own sentence returned through the existing `Explain`
- [x] 1.3 `World/IPullRequestLookup.cs` — `IPullRequestMerge` and `MergeOutcome`, a **separate**
      interface beside the read-only one, with the `<summary>` naming the house rule it follows
      (`IWorktreeManager` kept apart from `IWorkspaceProbe`)
- [x] 1.4 `Infrastructure/Git/GhPullRequestLookup.cs` — implements it too: one
      `gh pr merge <n> --squash`, the same spawn discipline as `Find` (login-shell PATH, no pager, no
      terminal prompt, both streams before the wait, the timeout)
- [x] 1.5 `Infrastructure/World.cs` — one registration line for the new interface. **Reported**: the
      epic's rule is no `World.cs` line, and the module-side precedent does not apply because this
      implementation spawns a CLI and lives in Infrastructure

## 2. One reading of the change directory, shared rather than copied

- [x] 2.1 `Workspace/.../GetOpenSpecChange.cs` — hoist the unarchived-name walk and the
      below-a-directory test out of `Handler` into `internal static` members of the slice, so the
      guard and the panel read the change directory the same way. No behaviour change

## 3. The queue

- [x] 3.1 `Workspace/.../MergeChange.cs` — `POST /api/folders/{id:guid}/merge`,
      `[Requires(WorkspacePermissions.ManageWorktrees)]`, `IScopedToProject`, the worktree named in
      the body
- [x] 3.2 the two guards, in order and before anything else: an unarchived change (naming the
      command), then a working copy git reports as dirty (saying what it showed)
- [x] 3.3 the pull request read once — none, unreachable and closed refuse; `MERGED` skips the merge
      and is reported as `alreadyMerged`
- [x] 3.4 the squash-merge, then the declared stop line of every live session in that directory, then
      the removal without force, then the row, then the branch
- [x] 3.5 `GetFolder.cs` — `FolderErrors.ChangeNotArchived`, `MergeRefused`, `WorktreeNotRemoved`
      appended at the end of the class, each with the reason it is its own code

## 4. The screen

- [x] 4.1 `shared/http/contracts.ts` — the response type beside the folder ones
- [x] 4.2 `shared/http/queries.ts` — `useMergeChange`, immediately after `useRemoveWorktree`,
      invalidating `keys.folders` and `keys.terminals(folderId)` the way its neighbour does
- [x] 4.3 `shared/i18n/en.ts` — a fresh `mergeAction.` block appended at the end. **`gate.*` is not
      resurrected** and the tombstone comment above it is not touched
- [x] 4.4 `features/folders/MergeChangeDialog.tsx` — `Dialog` + `variant="destructive"`, the
      unfinished-checklist precondition named in words, and the server's sentence rendered on refusal
- [x] 4.5 `features/folders/ChangeTab.tsx` — **one more child of the existing actions box**, opening
      the dialog. The five panel slots are not touched, and nothing in the box is disabled

## 5. The guard is pinned by committed tests

- [x] 5.1 `Workspace.FunctionalTests/WorkspaceFixture.cs` — `StubWorktrees` gains the branch
      deletion; a `StubPullRequestMerge` beside `StubPullRequests`, registered and reset with the rest
- [x] 5.2 `Workspace.FunctionalTests/MergeChangeTests.cs` — the unarchived-change guard (including the
      collapsed untracked directory), the dirty tree, no pull request, the vendor's refusal, the
      stop-then-close order, the removal refusal naming the step, the retry that does not merge twice,
      and the branch reported rather than failed
- [x] 5.3 every absence assertion anchored against something present in the same read, then proved by
      mutation: force the guard off, require the tests to fail, revert, confirm an empty diff, green

## 6. The gate and the running app

- [x] 6.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` —
      the two `PhoneTests` failures are D10 and are not loosened
- [x] 6.2 observed live against a **scratch repository created for it**, never a real worktree and
      never anything belonging to another agent

## 7. What the running app changed, after the tasks above were written

- [x] 7.1 **Guard two read the wrong question, and only pressing the button showed it.**
      `IWorkspaceProbe.Diff` answers *what this branch changed*, measured from the merge base and by
      its own design, so that work an agent already committed still shows in the Diff tab. It is
      therefore non-empty for every branch worth merging: the first live press refused a worktree
      whose `git status` was clean, naming the archived change it had itself just moved. Changed to
      `WorkspaceState.UncommittedFiles`, which is the number the header's Tree fact already states
- [x] 7.2 A directory git cannot describe is refused rather than read as clean — the same read's null
- [x] 7.3 Two more tests and two more mutations: turning the corrected guard off kills 1, and putting
      the original bug back kills 3. The `land-a-change` delta rewritten to say which reading is
      required and why the other one is wrong
- [x] 7.4 Observed against a scratch repository created for it, with its own isolated AppHost: the
      unarchived change refuses and names `/aio:sync`; archiving it makes the same worktree pass both
      guards and reach the pull-request lookup; a dirty tree refuses with the count; and the primary
      checkout answers 404, because it holds no worktree row for anything to act on
- [x] 7.5 **The merge itself was not observed.** A scratch repository has no remote, so `gh` answers
      `no git remotes found` and the queue honestly stops there. Merging for real would have meant
      merging something real, which this was told not to do — so the squash, the removal, the branch
      delete and the retry are covered by committed tests and argued, not watched
