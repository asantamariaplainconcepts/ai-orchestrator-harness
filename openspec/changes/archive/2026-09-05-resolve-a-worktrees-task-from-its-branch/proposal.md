## Why

A worktree only knows which issue it is for if somebody started a run in it from the app, and this
repository's own loop never does. `taskFor(runsHere)` reads the task key off the most recent run in
the directory; `/aio:propose` creates the worktree from a terminal, so nothing records a run.

Measured on this machine: the three worktrees alive right now — `change/275`, `change/262`,
`change/264` — have **zero runs** between them, while 26 historical `change/*` worktrees have some,
from when work was started with the app's button. So on every worktree the loop currently produces,
the Issue tab draws *"No run has worked here yet"*, the merge gate's Seam tile reads unknown, and the
comparison #149 built compares nothing.

## What Changes

- A worktree with no run resolves its task **from its branch**, and the answer says it was derived.
- The number is **proposed by the branch and confirmed by the mirror**: it is used only when the
  inbox already holds that task for this folder's repository. No branch-naming convention is encoded
  anywhere.
- **A run always wins.** Measured: 3 of the 26 worktree/key pairs disagree — `change/174` ran `#104`
  twice, `change/6` ran `#104`, `change/182` ran `#174`. Derivation is a fallback for a worktree with
  no run, never a replacement for one that has.
- Both readers — the Seam tile and the Issue tab — take the same answer from the same place, as they
  already do.

## Capabilities

### New Capabilities

- None.

### Modified Capabilities

- `merge-gate`: the Seam tile's task input gains a second source, and the rule for which one wins is
  written down. The tile's "unknown until both inputs are in" behaviour is unchanged; what changes is
  that the task input stops being unknown in the ordinary case.

## Impact

- **`src/frontend/features/folders/MergeGate.tsx`**: `taskFor` takes the branch and the mirror's
  tasks, and returns where the key came from rather than only the key.
- **`src/frontend/features/folders/IssueTab.tsx`**: says when the key was derived.
- **`src/frontend/shared/i18n/en.ts`**: one appended block.
- **No backend, no migration, no contract change.** `Worktree.branch` is already on the wire and the
  inbox is already read by `useTasks`.
- **Not touched**: `FolderDetailScreen.tsx` — neither `fixedTabs` nor the `Tab` union, both of which
  `change/264` is in flight over.
