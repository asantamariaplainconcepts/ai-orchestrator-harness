## Context

`taskFor(runsHere)` (`MergeGate.tsx:218`) is the single answer to "which task is this worktree's",
and #185 made it single on purpose: the Issue tab and the Seam tile must not derive it two possibly
different ways. It reads the task key off the most recent run in the directory.

That was right when a worktree was made by pressing "Run a task here". It is wrong for the way this
repository actually works now: `/aio:propose` creates the worktree from a terminal, and nothing
records a run. Measured — `change/275`, `change/262` and `change/264` have zero runs; 26 older
`change/*` worktrees have some.

Both callers already pass the key through `useTask(key, repoPath(remoteUrl))`, which resolves the
mirror row **by repository and key together** and answers 404 for a key the mirror does not hold.
That call is already the confirmation this change needs; it does not have to be built.

## Goals / Non-Goals

**Goals:**

- A worktree the loop created says which issue it is for, without anybody starting a run in it.
- The answer says where it came from, so a derived one is never mistaken for an observed one.
- No branch-naming convention is encoded in the product.

**Non-Goals:**

- Changing how a run records its task. `Run.TaskKey` is copied at start and stays the better answer.
- Adopting worktrees created outside the app (idea 15 of #239).
- Anything on the merge gate's verdict. Its five conditions and their rule are untouched.

## Decisions

### The branch proposes a number; the mirror confirms it

The last run of digits in the branch name is a **candidate**, and it becomes the answer only because
`useTask` finds that task for this folder's repository. `change/275` resolves because `#275` exists;
`fix/narrow-layout` proposes nothing; a `release/24` in some other project proposes `#24` and gets a
404, so nothing is drawn.

*Alternative considered:* a configurable pattern in `.harness/config.json`, beside the label names.
Rejected — it is a knob for a question the mirror can answer without one, and this product is not
only for this repository's `change/<n>`. Encoding that convention in the product would be putting the
loop inside an app that other loops are meant to use.

*Alternative considered:* trusting the branch outright. Rejected by measurement, below.

### A run always wins

Of the 26 worktrees with runs, **3 disagree with their branch**: `change/174` ran `#104` twice,
`change/6` ran `#104`, `change/182` ran `#174`. A worktree gets reused, and in those three cases
deriving from the branch would name the wrong issue over data that is already right.

So the order is: the most recent run's key, else the branch's candidate, else nothing. The Seam tile
compares a diff against what an issue declared, and comparing it against the wrong issue is worse
than not comparing at all — the tile already has an honest way to say nothing.

### The answer carries its source

`taskFor` returns the key **and where it came from**. A derived key is drawn as derived on the Issue
tab: it is a guess the mirror agreed with, not something anybody observed happening in that
directory, and the distinction is the same one this product keeps between "unknown" and "zero" in
half a dozen places.

### The branch reaches `IssueTab` as a prop

`MergeGate` already has the worktree row and its branch. `IssueTab` does not, so it gains one prop at
its call site in `FolderDetailScreen.tsx`.

**That line collides with #275**, which adds a `files` prop to the same call. Both are one-line
additions to the same JSX element, the conflict is mechanical, and *What collides* rule 4 asks that it
be said before the fact rather than discovered mid-rebase. It is said here, on both issues, and in
the pull request. Nothing else in that file is touched — not `fixedTabs`, not the `Tab` union, both
of which `change/264` is in flight over.

## Risks / Trade-offs

- **A branch number that collides with a real but unrelated issue** → the mirror confirms existence,
  not intent, so `hotfix/1234` in a repository whose `#1234` exists would resolve to it. Mitigated by
  drawing the answer as derived and by the run always winning; not mitigated further, because the
  alternative is a convention this product would have to impose.
- **Two repositories mirrored under one folder** → already handled: `useTask` takes the repository
  from `remoteUrl`, which is the rule #268 paid for.
- **A worktree reused for a second issue after this ships** → a run in it still wins, so the answer
  stays right the moment anything runs there.

## Open Questions

None.
