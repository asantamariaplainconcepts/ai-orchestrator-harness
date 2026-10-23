## Context

The Change tab (`ChangeTab.tsx`) mounts when `shown === "change"` in `FolderDetailScreen`. On first mount it fires:

- `useTask(resolved?.key, repoPath(detail.remoteUrl))` — may hit the mirror or trigger a GitHub sync (2–5s cold).
- `useWorktreePullRequest(detail.id, worktree.path)` — GitHub API call (1–3s).
- `VerdictRow` calls both again — cache hits, no extra cost.
- The selected step panel (`ProposalStep`, `CodeStep`, etc.) fires its own query and returns `<Loading />` until it resolves.

The header's first render depends on `task.data?.labels` (for `stepMarksFromLabels`) and `pr.data?.pullRequest?.state` (for the merge button). Both are `undefined` while pending, which the code already handles — `stepMarksFromLabels(undefined)` returns an empty set, `merged` is `false`. **The header can paint today; what blocks it is the step panels' `if (isPending) return <Loading />`.**

The blank screen has two causes:

1. Each step panel returns `<Loading />` — a full-panel spinner — instead of its skeleton layout.
2. `FolderDetailScreen` swaps tabs synchronously: the old tab unmounts, the new one mounts and returns `<Loading />`, and the screen is blank until the new tab's queries resolve.

## Goals / Non-Goals

**Goals:**
- Change tab header paints in <100ms with cached data.
- Step panels draw their layout skeleton while pending, not a spinner.
- Tab switch uses `useTransition` so the previous tab stays visible while the new one mounts.

**Non-Goals:**
- No new API endpoints or query batching.
- No backend changes.
- No change to the query keys or cache strategy.
- No change to what data the Change tab reads — only when it blocks paint.

## Decisions

### 1. Step panels draw skeletons, not `<Loading />`

**Decision:** Each step panel's `isPending` branch returns a lightweight skeleton of its layout — the heading bar, a row of muted placeholders — instead of `<Loading />`.

**Why not `<Loading />`:** A spinner replaces everything. The header, the rail and the footer vanish, and the screen is blank. A skeleton keeps the structure visible and tells the reader what is coming.

**Alternative considered:** `<Suspense>` at the `FolderDetailScreen` level. Rejected because the app has no `Suspense` boundary today and introducing one changes the loading model for every tab, not just Change.

### 2. `useTransition` on tab switch

**Decision:** `FolderDetailScreen` wraps `setTab` in `startTransition`. React keeps the previous tab visible while the new one mounts, so there is no blank gap.

**Why `useTransition` and not `useDeferredValue`:** The tab is a discrete state change, not a value to defer. `startTransition` marks the update as non-urgent, which is exactly "don't blank the screen while the new tab is mounting."

**Alternative considered:** `keepMounted` — mounting every tab once and hiding the inactive ones. Rejected because terminals measure themselves at zero width when hidden, and a worktree with five tabs open would hold five terminal sessions.

### 3. `ProposalStep` fetches first artifact in parallel

**Decision:** `ProposalStep` reads the change listing and, once it has the first `present` artifact, fetches its contents. Today the listing resolves, then `useState` picks the first, then `useFileContents` fires — all sequential. The fix is to fire `useFileContents` with the first artifact's path as soon as the listing has it, which React Query already does via `enabled`.

**Why not batch:** The listing and the file are different endpoints and different cache keys. Parallel `useQuery` is the model the app already uses; batching would be a new pattern.

## Risks / Trade-offs

- **Skeleton flicker:** If the query resolves in <50ms the skeleton may flash for one frame. Acceptable — it is the same shape as the content, and a flash of structure is less jarring than a spinner.
- **`useTransition` delays the tab highlight:** The tab button may not show as pressed until the new tab is ready. Mitigated by calling `setTab` inside `startTransition` — React highlights the tab immediately and defers the content swap.
- **Stale data in the previous tab:** While the transition holds the old tab, its data may be stale. Acceptable — the old tab was what the person was reading, and the new tab replaces it as soon as it is ready.
