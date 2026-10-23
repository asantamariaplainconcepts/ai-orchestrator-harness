## Why

Switching to the Change tab leaves the screen blank for several seconds before painting. The header blocks on two network calls — `useTask` (mirror, may trigger a sync) and `useWorktreePullRequest` (GitHub API) — and each step panel replaces its `isPending` state with `<Loading />`, which mounts a spinner instead of the skeleton of what is coming. The dev waits, then the screen appears all at once.

## What Changes

- The Change tab header paints immediately with cached data (diff counts, branch, worktree name) and fills in `useTask` and `useWorktreePullRequest` results when they arrive, without blocking the first render.
- Each step panel (Proposal, Code, Tests, Evidence) draws a skeleton of its layout while its query is pending, not a full-screen `<Loading />` that replaces the header and rail.
- `FolderDetailScreen` wraps the tab switch in `useTransition`, so the previous tab stays visible while the new one mounts — no blank gap.
- `ProposalStep` fetches the first artifact's contents in parallel with the change listing, not after it resolves.

## Capabilities

### New Capabilities

### Modified Capabilities

- `review-a-change-in-one-tab`: the header and step panels shall paint incrementally — cached data first, pending data as it arrives — instead of blocking the first render on all queries.

## Impact

- **Frontend only.** No backend changes, no new endpoints, no new queries.
- `ChangeTab.tsx` — header does not block on `useTask` or `useWorktreePullRequest`.
- `ProposalStep.tsx`, `CodeStep.tsx`, `TestsStep.tsx`, `EvidencePanel.tsx` — `isPending` draws a skeleton, not `<Loading />`.
- `FolderDetailScreen.tsx` — `useTransition` on `setTab`.
- No shared-file changes (`contracts.ts`, `queries.ts`, `en.ts`).
