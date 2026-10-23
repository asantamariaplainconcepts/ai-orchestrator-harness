# Design: check-the-claim-in-its-worktree

## Context

The #361 guard in `LabelWatcher.Dispatch` (LabelWatcher.cs:351-363) reads two facts before
seating an agent on `implement`: the OpenSpec change (`facts.OpenSpecChange`) and the branch
(`facts.BranchExists`). Both are answered against `candidate.Folder.Path` — the primary checkout,
whose state is `master`. But `/aio:propose` writes `openspec/changes/<name>/` on the branch it
creates, whose working position is the worktree `.worktrees/change-<n>` — where the dispatch
itself is about to seat the agent. So the guard asks the wrong directory, every tick, forever:
measured on `master` with #397, refusal `no OpenSpec change in openspec/changes/` with the branch
pushed and the artifacts sitting in the worktree one level below.

The neighboring rule is correct and stays: the **loop file** is read from the checkout, never a
worktree's copy (#365) — because a workbranch carries the loop as a fact of the trunk it branched
from. The claim's facts are the opposite: they are facts **of the branch**, and the branch's
working position is the worktree.

Issue: #400. Seam: `src/root/AiOrchestrator.Server/Watching/` +
`src/shared/AiOrchestrator.Infrastructure/Agents/LaunchFacts.cs` (behind
`BuildingBlocks/World/ILaunchFacts.cs`).

## Goals / Non-Goals

**Goals**

- An issue whose propose ran in its worktree dispatches when its hold is released.
- A propose done from another machine (worktree absent, `origin/change/<n>` pushed) dispatches too.
- The genuine refusal — no change in either place — still refuses, with the same remedy named.

**Non-Goals**

- Not changing where `/aio:propose` writes (the branch/worktree is right; the branch is the claim).
- Not reading the loop file from a worktree (#365 stays).
- Not touching `BranchExists`, `RepositoryIdentity`, `LabelLaunchPlan`, WIP, holds.
- Not the PR-as-task mirror noise (#398 in `backlog`) — its own issue if it hurts.

## Decisions

### D1: The watcher asks for the working path, not the folder

`LabelWatcher.Dispatch` computes the claim's working path — `Path.Combine(folder.Path,
".worktrees", worktreeName.Replace('/', '-'))` when the directory exists, else the folder path —
and hands **that** to `facts.OpenSpecChange`. The slash rule (`change/397` → `change-397`) is
repeated as a private helper rather than imported from the Runs module's `RunWorkingDirectory`:
that type is internal to Runs, and a BuildingBlocks seam cannot reach into a module — a second
copy with a comment naming its twin is the established bargain in this codebase
(`RepositoryIdentity`'s header says the same of `src/frontend/shared/repo.ts`).

The helper lives beside the watcher (Server, `Watching/`) where its only caller is.

### D2: `OpenSpecChange` keeps its contract; the fallback is the caller's file read

`ILaunchFacts.OpenSpecChange(checkoutPath, changeName)` does not change shape: it enumerates
`openspec/changes/` under the path it is given, and the watcher now gives it the path that can
hold the change. The remote-branch fallback — the propose-from-another-machine case — is added to
`LaunchFacts` as a **second read inside the same call**: when the directory enumeration finds no
change and `refs/remotes/origin/change/<n>` resolves, the change's presence is read from the ref
with `git ls-tree --name-only origin/change/<n> -- openspec/changes/` (the branch name is derived
from the change-name argument the watcher already passes as `null` — so the fact gains one
parameter: the claim's branch name, `change/<n>`, which `Dispatch` already knows as
`candidate.WorktreeName`).

Split as it is, the fact stays one read with one fallback, in the World implementation that owns
git — the watcher does not shell out, respecting the seam.

### D3: The composed path is only consulted when it exists

The watcher checks `Directory.Exists` before handing the worktree path (a hand-made branch claim
on a machine where the worktree was never created falls back to the primary path and then to the
ref, both of which are the honest answers for that machine). No worktree is ever created by the
watcher — creating one is the run's own business (`StartRun`), not the tick's.

### D4: Tests mirror the three cases

- Worktree present with the change → dispatch (the current `InstalledFacts(false)` refusal test
  gains a passing sibling against the worktree path).
- Worktree absent, ref holds the change → dispatch (a `LaunchFacts` test with a real temp git
  repo, alongside the existing `GitHistoryTests` style).
- Neither → the existing refusal stands, unchanged message.

The LabelWatcher wire tests assert on which path the facts were asked about (the fake
`InstalledFacts` records its arguments), not on the filesystem, so the watcher's tests stay
socket-free as before.

## Risks / Trade-offs

- [Recomputing the worktree path in two places can drift] → both copies say where the other is;
  `RunWorkingDirectory`'s own header already documents the slash rule as the silently-wrong-when-
  retyped part.
- [Reading a change from a ref could see a stale proposal] → the branch is the claim; if it moved
  since, the run that starts on it works on what the branch holds — the same truth the run itself
  sees when it checks the branch out.
- [A directory named like a worktree but belonging to something else] → the path is only read for
  the fact; the run's admission (`StartRun`) owns creating or reusing worktrees and keeps its own
  checks.

## Migration Plan

In-memory read path only; no schema, no persisted state. Rollback is a revert.

## Open Questions

None blocking.
