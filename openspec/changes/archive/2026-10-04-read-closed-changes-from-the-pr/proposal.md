## Why

The Change tab reads a change's artefacts — diff, files, Proposal, evidence —
from the local worktree, so a merged PR's result depends on a directory that
may since have been cleaned, edited or deleted. The remote PR reader shipped
by #419 (`review-a-remote-pull-request`) already reads code, threads and
evidence from the provider against a pinned comparison; the closed result
still does not use it.

## What Changes

- The **closed result** of a change — merged, or closed without merge — is
  read from its pull request through the existing remote reader. The local
  reconstruction of that reading is withdrawn.
- Review of **open** PRs and reading of **unpublished local work** —
  uncommitted or unpushed changes — remain distinct functions and are not
  reduced.
- Merged, closed-without-merge, open, absence and error are distinct states,
  each named.
- The remote comparison's existing guarantees stay: fork identity, head,
  base, merge-base, pagination, expiry and truncation limits, per
  `review-a-remote-pull-request`.
- Proposal content is read from the remote review, including supported
  archived artefacts; it is never completed with local files.
- Remote evidence keeps its provenance and SHA, and is not migrated.
- Hooks, props, effects and queries that exist only to reconstruct the
  local history are removed, including ones started by parent components.
- Contracts and endpoints are removed only when no consumer remains.
  Terminals, telemetry, journey and explicit cleanup functions are not
  deleted.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `read-the-change-on-a-branch`: the closed result is read through the
  remote PR reader; the local reading path that served a merged or closed
  change is withdrawn for that case.
- `review-a-change-in-one-tab`: the Change tab's steps draw from the
  remote comparison when the change is closed, with open-PR review and
  unpublished-local-work as separate reads.

## Impact

- `src/frontend/features/folders/ChangeTab.tsx`,
  `FolderDetailScreen.tsx`, entries in `OverviewTab.tsx`; the remote viewer
  under `features/tasks` only as needed.
- Contracts, queries and i18n in their owner blocks — removals only after
  consumers are checked (`file-overlap` for the palette).
- Frontend and end-to-end tests: negative tests for local reads that no
  longer happen, and regression of the operating paths that stay.
- No migration. No change to terminal, telemetry, journey or explicit
  cleanup functions.
