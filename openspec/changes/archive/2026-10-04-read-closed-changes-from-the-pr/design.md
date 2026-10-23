## Context

Measured on master c0304a72 (propose time). What reads a closed change's
result locally today:

- `ChangeTab.tsx` — the worktree strip; `CodeStep.tsx` reads
  `useFolderDiff`/`useFileContents`, `ProposalStep.tsx` reads
  `useOpenSpecChange` (local OpenSpec change at `worktree.path`), the
  evidence step reads the local evidence directory.
- `FolderDetailScreen.tsx` — mounts the Change tab for worktrees the
  folder lists; fetches the diff "whether or not anybody opened the mode"
  (line 467).
- `ClosedJourneys.tsx` (#394) — reconstructs closed walks from runs whose
  directories are gone, one row per task, opening transcripts.
- `useWorktreePullRequest` already knows the PR state (`MERGED` drives the
  Close button, `ChangeTab.tsx:155`).

What already reads remotely: the #419 change `review-a-pr-without-a-worktree`
(26/31 tasks) shipped `PullRequestReview.tsx` and its comparison-fenced
readers — files, patches, contents, threads, evidence — under
`features/tasks`, with a Tasks-owned backend and a World seam. Its
capabilities live in `review-a-remote-pull-request` (in-flight change, not
yet archived).

The dependency direction: #432 builds on #431's identity rule
(source, host, repository, key) for matching a task to its PR, and on
#419's remote reader for the reading itself. #431 is proposed, not
implemented; #419 is 26/31 tasks on an unmerged branch. This proposal
declares both as contracts it assumes, and its implementation waits for
them.

## Goals / Non-Goals

**Goals:**

- One remote reading for a closed change's result: diff, files, Proposal,
  evidence — through the #419 reader, fenced to the comparison.
- Withdraw the local reconstruction for exactly that case.
- Keep open-PR review and unpublished-local-work reading intact and
  distinct.
- Remove now-ownerless local queries, hooks and effects — measured, with
  consumers checked.

**Non-Goals:**

- Deleting worktrees as an operating tool; removing the terminal,
  telemetry, journey or explicit cleanup functions.
- Modifying the loop engine or integrating new remote providers.
- Migrating evidence from issues to PRs.
- New offline persistence for merged PRs.

## Decisions

- **The closed result routes to the remote reader.** When
  `useWorktreePullRequest` resolves a MERGED or CLOSED PR for the
  worktree's change, the Change tab's steps read through the remote
  comparison the #419 reader serves — not through `useFolderDiff`,
  `useFileContents`, `useOpenSpecChange` or the local evidence readers.
  Unpublished work (no PR, or a PR with local changes ahead of it) keeps
  the local readers: that is a different question — "what is true here
  now" — and it stays.
- **Open is not closed.** An open PR with a worktree still working reads
  local-plus-remote the way it does today; the change is deliberately
  about what a **closed** result reads. The `live` run flag
  (`FolderDetailScreen.tsx:500`) says nothing about the PR, and the PR
  state says nothing about the run — #431's rule, honoured here.
- **Proposal and evidence ride the remote review's parts.** `ProposalStep`
  reads the OpenSpec change from the PR's comparison — the archived
  artefacts #419's reader already names — and the evidence step reads the
  remote evidence with its provenance SHAs. Neither completes missing
  pieces with local files.
- **Withdrawal is measured, not assumed.** Queries/effects removed are
  listed with their consumers checked (`useFolderDiff` consumers outside
  the closed-result path stay). "No consumer remains" is a claim tested by
  grep and by the build, and recorded in the issue's check.
- **Entry points keep their identity rule.** The Overview's closed
  journeys (`ClosedJourneys.tsx`) link into the remote reading the way
  #431's entries link into their destinations — by full identity, never by
  task key alone.

## Risks / Trade-offs

- **Depends on two unmerged changes.** #431's identity join and #419's
  reader are contracts this proposal names but cannot exercise; if either
  changes shape, this change follows, not leads. Rollout order is #431,
  #419 (implemented), then this.
- **Offline reading of a merged PR is lost.** Today a deleted worktree
  with a local diff in git's object store can sometimes still be read; a
  network refusal now reads as an error rather than a locally
  reconstructed diff. That is the intended honesty — the trade is named,
  not hidden behind a fallback that cannot be trusted.
- **Removal work can over- or under-reach.** The tasks require the
  removal inventory (`file-overlap`, consumers checked) before any
  deletion; the danger is the silent local query nobody notices —
  mitigated by the network-level check in the verification plan.

## Alternatives rejected

- Keep both readers and pick per query success: the ambiguity this change
  exists to remove — a merged PR with a dirty worktree would show whatever
  answered first.
- Reconstruct from the local worktree when the network fails: an expired
  comparison and a failed network call are different facts, and faking one
  as the other produces the fictional empty diff the issue's check forbids.
- Migrate evidence to PRs: out of scope; the SHAs were observed when they
  were observed.

## Error and temporal consistency

Merged, closed-without-merge, open, absence and error are five named
states in the UI; loading is a sixth. A comparison that expired reads as
expired, a truncated file list reads as truncated, and neither collapses
into "no changes". An in-flight local response cannot overwrite the remote
reading once the destination is chosen; the reverse holds too.

## Verification plan

Each item maps to a scenario in the delta specs:

1. Merged PR with clean, dirty or deleted worktree: the same remote
   reading.
2. Closed-without-merge and closed-issue-with-open-PR: honest states.
3. Error or ambiguity: no fallback, no silent selection.
4. Fork, advanced base, expired comparison, truncation: no fictional empty
   diff.
5. Direct PR entry with no Folder/Run: zero local queries — measured at
   the network level (query log or devtools), separating justified global
   polling from change-content reads.
6. Local readers failing or in flight: no effect on remote content.
7. Reopened PR: state updates without creating worktrees.
8. Removal inventory: files, hooks, queries removed and kept, with
   consumers.

Plus the transversal checks: 390×844 both themes, keyboard, and the
documented gate at implementation time.

## Questions to resolve before implementation

- Does #419's reader expose everything the closed-result steps need
  (OpenSpec artefact discovery at a pinned revision, evidence URLs), or
  does one extension need declaring?
- Which exact local queries have consumers outside the closed-result path?
  (`file-overlap` answer during implementation.)
- Does `OverviewTab`'s closed-journey row open the remote reading
  directly, or route through #431's detail entry? (Answer with #431's
  implementation.)
