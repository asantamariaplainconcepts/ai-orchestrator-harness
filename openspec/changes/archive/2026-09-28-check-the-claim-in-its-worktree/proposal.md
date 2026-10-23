## Why

The #361 fact-check — the guard that refuses to seat an agent on `/aio:implement` without the
OpenSpec change and branch a proposal owes — reads the change directory in the **primary**
checkout, but `/aio:propose` writes it on the branch it creates, materialized as the worktree
`change/<n>`. The consequence, measured on `master` with #397: an issue at
`ready-for-implementation` whose propose ran in its worktree is refused every tick with
`no OpenSpec change in openspec/changes/` ("run /aio:propose first" — when propose already ran),
and the automated implement loop never starts.

## What Changes

- `LabelWatcher.Dispatch` passes the claim's working path to the facts read — the worktree
  `change/<n>` when one exists, the primary checkout otherwise — instead of always the primary
  checkout.
- `LaunchFacts.OpenSpecChange` gains the branch fallback: when the directory it is handed holds no
  change and the checkout's `origin/change/<n>` ref exists, the change is read from that ref via
  git (`ls-tree`/`cat-file`), so a propose done from another machine — only the remote branch
  left — still satisfies the guard.
- The slash rule a branch name takes as a worktree directory (`change/397` → `.worktrees/change-397`)
  is used where the path is composed, without the watcher reaching into the Runs module for
  `RunWorkingDirectory` (internal, and cross-module on purpose).
- `BranchExists` is unchanged: `show-ref` against local and remote heads is already right.

## Capabilities

### New Capabilities

(none)

### Modified Capabilities

- `dispatch-observation`: the dispatch's read of the facts a `ready-for-implementation` claim owes
  (the OpenSpec change and its branch) is taken where those facts live — the claim's worktree, with
  the pushed branch as fallback — rather than in the primary checkout, which sits on `master`.

## Impact

- **Code**: `src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs` (the `Dispatch` facts gate);
  `src/shared/AiOrchestrator.Infrastructure/Agents/LaunchFacts.cs` (worktree/branch read);
  `src/shared/AiOrchestrator.BuildingBlocks/World/ILaunchFacts.cs` (contract, if the shape must
  say what path it was asked about); `src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs`
  and `src/tests/AiOrchestrator.InfrastructureTests/` (the facts' own tests).
- **Behaviour**: issues previously refused with `no OpenSpec change` despite a pushed branch
  become dispatchable. The refusal stays for the genuine case — no change anywhere.
- **Not touched**: `BranchExists`, `RepositoryIdentity`, `LabelLaunchPlan`, WIP accounting, holds,
  the `#365` rule that the loop file is read from the checkout and never a worktree's copy.
