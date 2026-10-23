## Context

`MergeChange.cs` already handles `alreadyMerged` — the pull request's state is `MERGED`, so the
queue skips the merge and runs the clean-up half. That path exists because a mid-queue retry reads
the world rather than a stored counter. What is missing is the UI half: `ChangeTab`'s button draws
"Merge change" unconditionally, without reading the PR state `VerdictRow` below it already has.

## Goals / Non-Goals

**Goals:**
- The Change tab button reads the PR state and says "Close worktree" when it is `MERGED`.
- The dialog adapts its copy to the close, not the merge.
- The endpoint is unchanged.

**Non-Goals:**
- Polling the PR state. The query loads on mount, the same as `VerdictRow`'s fact line.
- Changing the backend. `MergeChange.cs` already does the work.
- A new route, a new seam, or a new contract type.

## Decisions

### Read the query that is already in flight, not a second one

`VerdictRow` calls `useWorktreePullRequest(detail.id, worktree.path)`. `ChangeTab` calls it again
with the same arguments — React Query deduplicates by key, so the second call is a cache hit, not a
second request. The alternative (lifting the result out of `VerdictRow`) would restructure a
component whose comment says its five steps each replace one line, and the cache hit is free.

### Branch on `state === "MERGED"`, not on `alreadyMerged` from the response

The button has to know *before the press* whether to say "Close" or "Merge", and only the PR
query carries that. The response's `alreadyMerged` is what the dialog says *after the press*, and
it is the server's own word rather than the client's. Both are kept, used at different times, and
neither is the other's source.

### The dialog's variant stays destructive

"Close worktree" removes a directory and a branch — the same queue, the same actions. The button
stays `variant="destructive"` for both labels, because the operation is the same in both cases.

## Risks / Trade-offs

- **A PR that merges while the tab is open stays "Merge change" until the next open.** This is the
  same trade `VerdictRow`'s fact line makes — the PR state in the fact line is also on mount, not
  polled. Not changed here.
- **No frontend test covers the button swap.** This repository has no frontend test framework.
  The backend `alreadyMerged` path is covered by committed tests; the label swap is argued from
  the code.
