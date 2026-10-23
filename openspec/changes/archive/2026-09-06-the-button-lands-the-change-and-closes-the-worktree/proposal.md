## Why

Issue **#296**, from epic **#288**, on top of **#289**. The last step of the loop, and the one that
needs the least judgement: `/aio:sync` is eleven steps, two of them judgement and nine of them git.

The two that need it are **archiving the OpenSpec change** — so the deltas fold into
`openspec/specs/` — and **writing the retro entry**, which is prose about what the change taught and
is not derivable from the diff. `AGENTS.md` keeps the retro log for exactly that reason.

The nine that do not are: run the gate, squash-merge, stop the AppHost, `git worktree remove` on the
path git reports, and delete the local branch that a squash leaves git unable to see as merged.

And today, **running the nine mechanical ones means starting an agent** — a CLI session, in a
terminal, typing git. #289 drew a header with one actions box; there is nothing behind a merge.

## What Changes

**The command stays a command, and the product runs the queue.**

- **`POST /api/folders/{id:guid}/merge`** (`MergeChange.cs`, `workspace:manage-worktrees`) runs the
  mechanical queue for one worktree: squash-merge the pull request, stop the environment, remove the
  worktree without `--force`, delete the local branch.
- **Two guards before anything is merged.** A working copy git reports as dirty stops, saying what it
  showed. **A branch whose OpenSpec change is still unarchived stops and names the command that is
  missing** — archiving and the retro go *before* the merge so `master` gets one commit per change,
  which is why `sync.md` orders them that way, and merging first would split the deltas across two
  commits or lose them.
- **What blocks stops being the machine's opinion.** The five review steps are ticks a person put
  there. The dialog's own precondition is that count, not any fact in the header's line — so the
  action box keeps #289's rule that no action on that screen is disabled by a fact, and the reason a
  merge is held back is a person's unfinished checklist, said in words where it can be read.
- **The issue closes itself.** The pull request carries `Closes #<n>` from `/aio:propose` and a
  squash-merge closes it in the vendor. **This code makes no call about an issue** — no close, no
  label, no comment.
- **A mid-queue failure leaves the merge done, says which step it stopped at, and a retry does not
  merge twice.** Resumability is read from the world rather than stored: the pull request's own state
  is `MERGED`, so the second attempt skips the merge. **No column and no migration.**
- **`/aio:sync` is not touched.** It stays the single source of the unattended loop, it keeps the two
  steps that need judgement, and the two paths are proven to coincide rather than collapsed into one.

### The decision the ticket demands — and it is an assumption a person must ratify

The ticket names two paths to the same place and three answers. **This takes the third: both
implementations stand, and they are proven to coincide.**

`sync.md` is not modified and the command does not call the product. The reasons, in order:

1. **It is the smallest surface.** The button reaches `IWorktreeManager` and the pull-request seam.
   Making the command call the product means the unattended loop acquires a runtime dependency on a
   server being up, which is a much larger claim than this ticket is making.
2. **It is the reversible direction.** A later slice can make `sync.md` call
   `POST /api/folders/{id}/merge` and delete its own git. Un-picking that coupling once the loop
   depends on it is far harder.
3. **The duplication is bounded and checkable.** The queue is five operations in a fixed order, and
   check 7 — `/aio:sync` on another change, end to end — is what proves the two agree.

**This is recorded as an assumption, not a settled fact.** Nobody could be asked. If a person decides
the duplication is the wrong trade, the reversible direction is the one taken.

### The seam finding, written before anything was touched

**`IWorktreeManager` is short of surface, and so is the pull-request seam.** Read on the branch:

| Seam | Has | Needs |
| --- | --- | --- |
| `IWorktreeManager` | `Create`, `Ensure`, `Remove(path, force)` | **no branch deletion** |
| `IPullRequestLookup` | `Find` — and its own doc says *"read-only by construction"* | **no merge** |

So two additions, and they are deliberately not one:

- **`IWorktreeManager.RemoveBranch`** — git writing to a repository, which is what that interface's
  own summary says it is for (*"the one seam in this folder that writes to a repository"*).
- **`IPullRequestMerge`**, a separate interface beside `IPullRequestLookup` and implemented by the
  same class. Separate **because the house's own rule says so**: `IWorktreeManager` is kept apart
  from `IWorkspaceProbe` *"for exactly that reason"*, and this is the most destructive code in the
  epic. Widening a seam documented as read-only would make its first sentence false.

### The second seam finding: the declared stop line cannot be reached from a use case

The ticket's Seam says stopping the environment is *"the `stop` line the folder declares, which
`DetachedCommands` already remembers and `FolderCommandReaper` already knows how to run"*, and that
**nothing new leaves the process**.

The second half is true; the first is not reachable as written. `DetachedCommands` is internal to
Infrastructure and its only caller is the reaper, at host shutdown. **The reachable path is
`ITerminals.Stop`**, which runs the declared stop line and then ends the process group — so the stop
line does run, through a seam that already exists, and nothing new leaves the process. What is *not*
reachable is a detached app whose tab was closed: its stop obligation is held where no use case can
ask for it. That is a debt, not a thing this ticket invents a second mechanism for.

## Non-goals

- **Not a reimplementation of `/aio:sync`.** Archiving and writing the retro are judgement and do not
  come down into code.
- **Does not write the retro entry**, infer it, or leave it blank.
- **Does not archive the change.** It refuses when the change is unarchived, which is the opposite.
- **Does not close the milestone** — that is the wave's accounting, not this worktree's.
- **Does not retry.** Nothing in this loop retries.
- **No fast-forward and no merge commit.** Squash, which is what this repository does.
- **No `--force`, and no recursive delete.** Ever.
