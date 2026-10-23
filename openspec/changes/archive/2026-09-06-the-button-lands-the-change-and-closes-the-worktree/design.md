## Context

`/aio:sync` lands a change. Eleven steps, of which nine are git and two are judgement. This slice
takes the nine and leaves the two, and the whole of the difficulty is in three places: where the
merge seam lives, how a half-finished queue is resumed without a column to remember it in, and what
is allowed to hold the button back.

## Goals / Non-Goals

- **Goals.** One endpoint that runs the mechanical queue for one worktree. A guard that refuses an
  unarchived change and names the command that is missing. A mid-queue failure that leaves the merge
  done, says where it stopped, and does not merge twice on retry.
- **Non-goals.** Reimplementing the command. Writing the retro. Archiving. Closing the issue or the
  milestone. Retrying. Any use of `--force` or of a recursive delete.

## Decisions

### The merge goes through the pull request, because that is what closes the issue

`gh pr merge <n> --squash`, run in the worktree, is `sync.md` step 7 and it is what makes the PR's
`Closes #<n>` fire. A local `git merge --squash` and a push would land the same tree on `master` and
leave the issue open, because the keyword is in the pull request's body and not in any commit.

**"Without this code writing anything to the vendor" is about the issue, not about the merge.** The
ticket's own Seam says it: *"closing the issue does not cross here, because the vendor does it via
the `Closes`"*. So there is no `gh issue close`, no label and no comment anywhere in this slice, and
check 2 is verified by looking at who closed the issue — the pull request, not this process.

### Resumability is read from the world, never stored

Check 6 wants a retry that resumes and **does not merge twice**. The obvious shape is a column
recording how far the queue got. There is no migration slot in this epic, and there does not need to
be one: **the pull request already holds the answer.** `PullRequestSummary.State` is the vendor's own
word, and `IPullRequestLookup` already reports `MERGED` because it asks with `--state all` — a
decision #235 took for a different reason and which pays here.

So the queue asks the pull request first. `MERGED` means step 1 is already done and is skipped, and
the response says so out loud (`alreadyMerged`). This is `GetOpenSpecChange`'s argument applied to a
different fact: *"nothing is cached and nothing is stored — a stored answer is wrong the moment
somebody edits a file"*. A stored step counter would be wrong the moment somebody merged by hand.

### The order of the queue, and why the row goes where it does

1. Resolve the worktree row → its path and its branch.
2. **Guard: an unarchived OpenSpec change** → refuse, naming `/aio:sync`.
3. **Guard: a dirty working copy** → refuse, saying what git reported.
4. Ask the pull request. No pull request, one that cannot be asked about, or one that is `CLOSED` →
   refuse. `MERGED` → skip step 5.
5. **Squash-merge.**
6. **Stop the environment** — the declared stop line of every live session in that directory, then
   close them.
7. **Remove the worktree**, `force: false`. A refusal here is the check-6 case: it returns saying the
   merge is done and that the worktree was not removed. **The row survives**, so a retry re-enters at
   step 4, reads `MERGED`, and resumes.
8. Remove the row and announce it. The directory goes before the row, which is
   `worktree-lifecycle`'s existing rule and the reason a probe cannot put it back.
9. **Delete the local branch.** A failure here is **not** a refusal: the merge is done, the directory
   is gone and the row is gone, so there is nothing left to resume and nothing to undo. It is carried
   on the successful response as git's own sentence, which is the honest shape — the alternative is
   reporting a failed operation when the only residue is a branch name.

### `git branch -D` and not `-d`, and what makes that safe

A squash-merge leaves git unable to see the branch as merged, so `-d` refuses **every time** — the
ticket says as much. `-D` is therefore the only form that works, and the safety is not in the flag:
it is that this line runs **only after the pull request has been observed in state `MERGED`** and
only for the branch of a worktree row this product owns. The commits are on `master` in the vendor
before the local ref is dropped.

### What is allowed to hold the button back

#289's action box carries a rule in a comment: *"nothing in it is ever disabled by a fact … the panel
refusing on a verdict is what this screen stopped doing."*

**The button is never disabled in the action box.** It opens a dialog, and the dialog's destructive
button is disabled by **the count of review steps the person has not ticked** — following
`RemoveFolderDialog`, which does exactly this with a client-side precondition and names what blocks
in words. Two reasons this is not the thing #289 removed:

- A tick is a person's own statement, not the product's reading of five conditions. The ticket's whole
  framing is that *"what blocks stops being the machine's opinion"*.
- It is said in words, in the place where it can be read, rather than being a greyed control with no
  explanation. `#185`'s complaint was a disabled button with no path; this one names its path.

The unarchived-change guard is a different kind again: it is a **mechanical precondition of the
queue** — merging first would split the deltas across two commits — and it is enforced by the server,
because it is about the branch and not about anybody's opinion of it.

### One reading of the change directory, not two

The guard needs "is there an unarchived change here". #291 already built that reader
(`GetOpenSpecChange`), including the awkward part: git's `ls-files --directory` collapses a
wholly-untracked change directory into one entry, which is exactly what a change an agent has just
written looks like. Its name-listing walk is hoisted out of the handler and shared, so the guard and
the panel can never disagree about what counts as unarchived. Nothing is copied.

### Two error codes for five refusals, on purpose

`Folder.MergeRefused` carries the sentence for all five pre-merge stops — dirty tree, no pull
request, one that cannot be asked about, one that is closed, and `gh`'s own refusal. They have one
remedy shape, which is to read the sentence, and this is the pattern `FolderErrors.WorktreeRefused`
and `ChatRefused` already set. `Folder.ChangeNotArchived` is its own code because its remedy is a
specific command, and `Folder.WorktreeNotRemoved` is its own because it is the one refusal that
happens **after** the merge and therefore means something different about the world.

## Risks / Trade-offs

- **Two implementations of the same queue** (here and in `sync.md`). Accepted, argued in the
  proposal, recorded as an assumption for a person to ratify, and checked by running the command on
  another change end to end.
- **A detached AppHost whose tab was closed is not stopped.** Its stop obligation is held inside
  Infrastructure where no use case can reach it. Reported as a debt rather than solved with a second
  mechanism.
- **No frontend test covers the dialog.** This repository has no frontend test framework at all. Said
  plainly rather than implied: the backend queue and both guards are pinned by committed tests; the
  dialog's precondition, its copy and its refusal rendering are argued from the code and from the
  running app.
