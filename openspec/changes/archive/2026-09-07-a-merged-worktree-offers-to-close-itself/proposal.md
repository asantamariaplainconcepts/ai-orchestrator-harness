## Why

Issue **#302**. When the pull request was merged from outside the product — from a terminal, in the
GitHub UI, by `/aio:sync` — the Change tab still draws "Merge change" against a worktree whose
change has already landed. The product already has every fact it needs: `useWorktreePullRequest`
returns the PR state, `MergeChange.cs` already skips the merge when `alreadyMerged`, and the
clean-up queue (stop, remove, branch) already runs without it. What is missing is the button that
reads that state and says the right thing.

## What Changes

- **The Change tab reads the pull request state it already loads.** `VerdictRow` calls
  `useWorktreePullRequest`; `ChangeTab`'s button does not. The button now reads the same query —
  a cache hit, not a second request — and when `state` is `MERGED` it draws **"Close worktree"**
  with a different icon, instead of **"Merge change"**.
- **The dialog adapts its copy.** `MergeChangeDialog` receives whether the PR is already merged
  and says so: the title, the body and the confirm button name the close, not the merge.
- **The endpoint is unchanged.** `POST /api/folders/{id}/merge` already handles `alreadyMerged`
  by skipping the merge and running the clean-up queue. No backend change, no new seam, no new
  route.
- **The query is not polled.** The PR state loads on mount, the same as `VerdictRow`'s fact line
  already does. A PR that merges while the tab is open is seen on the next open, which is the
  same trade the fact line makes and is not changed here.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `land-a-change`: the existing requirement "A mid-queue failure says which step it stopped at,
  and a retry does not merge twice" covers the server side of `alreadyMerged`. A new requirement
  covers the UI half: when the pull request is merged, the Change tab offers to close the
  worktree, not to merge it again.

## Impact

- **`ChangeTab.tsx`** — calls `useWorktreePullRequest` (cache hit beside `VerdictRow`'s call),
  passes `merged` to the dialog, swaps the button label and icon.
- **`MergeChangeDialog.tsx`** — receives `alreadyMerged`, adapts its title, body and confirm
  button copy.
- **`en.ts`** — new `mergeAction.close*` keys appended beside the existing `mergeAction.*` block.
- **No backend change.** `MergeChange.cs`, the contracts, the queries hook — all unchanged.
