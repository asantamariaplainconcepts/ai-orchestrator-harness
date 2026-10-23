## Context

#309 asks for the commits a worktree is ahead of the trunk, beside the count `Position` already answers. The seam (`IWorkspaceProbe`) already has the trunk resolution logic in `Trunk`/`BaseRef` — the question is only how to read and carry the commit set. The screen is the folder detail's tab strip, which already has the `scope` ("both"/"checkout"/"worktree") and `git` markers this tab needs.

## Goals / Non-Goals

**Goals**

- The question "what are those two commits?" answered without leaving the worktree screen.
- The empty states said in sentences: nothing ahead, and no trunk to be ahead of, are different sentences and neither is a blank pane or a zero.

**Non-Goals** (the issue's "What it is not", unchanged)

- No git write actions — fetch, pull, push, commit, merge stay outside the product.
- No history graph — commits ahead of the trunk, not an SVG of every branch.
- No branch list or switching.

## Decisions

### 1. A separate `History` call, not fields on `Read` or `Position`

`Read` is what the folder poller calls on a timer per folder; `Position` already exists as the precedent for "two more git processes, belongs to the request that displays it". `History` follows `Position`'s shape exactly: asked by `GetFolder` per request, never stored, null for "cannot say".

Alternative — caching per worktree — rejected for the same reason `Position` is not cached: a stored fact about a directory somebody else can change is wrong the moment they change it.

### 2. One `git log` with a format string, not `rev-list` + per-commit reads

`git log --format` with a NUL-terminated field separator gives hash, subject, author name and date in one process. `rev-list <trunk>..HEAD` for the set is implicit in `git log <trunk>..HEAD`. Two-dot range, not three-dot: this is exactly "commits on this branch the trunk does not have", the definition of ahead.

Alternative — `rev-list` then `git show` per commit — rejected: N processes per worktree per request for one process.

### 3. The fields, and the cap

Short hash (what a person types to name a commit), one-line subject (`%s`, never `%B` — a body is the Files tab's job), author name (`%an`), committer date as ISO 8601 (`%cI`, parsed to `DateTimeOffset` server-side). The list is capped at a constant — 100 — beside the other caps in `GitWorkspaceProbe`: a branch can be hundreds of commits ahead and every commit becomes a row in a browser.

### 4. `WorkspaceHistory` carries a `Trunk` name and the commits

The screen needs to say *what* the commits are ahead of when it says anything — "2 ahead of origin/main" can be answered by a sentence naming the trunk. The record carries the trunk name it measured against; the commits list is the rows.

### 5. The tab is `scope: "worktree"`, `git: true`, after `change`

A worktree is where "ahead of the trunk" means anything; a checkout is the trunk or beside it. `git: true` keeps a non-repository from drawing an empty tab. Position: beside `runs` — reading a change's work (commits) belongs with reading its runs, before the files it touched. Label: "Commits".

### 6. Empty states

- Loading: the kit's `Loading` row, as every tab.
- No trunk: "There is no trunk to be ahead of" — the marker that `history` is absent on the contract, distinct from an empty list.
- Trunk with nothing ahead: "Nothing ahead of the trunk" — one row, not a blank pane.

## Risks / Trade-offs

- [A worktree hundreds of commits behind produces a long list] → capped at 100, and the cap says so in the last row rather than pretending the list ended.
- [Two more git processes per `GetFolder` for the scoped directory] → same cost class as `Position` per worktree, and `GetFolder` is requested when a screen is on display, not on the poller's timer. The list is fetched for the scoped directory only, not per worktree in the response.

## Migration Plan

Additive on both sides of the wire: a new method with a default-less interface (one implementation, updated in the same change), a new nullable field on the response. No stored data, no migration. Rollback is reverting the commit.

## Open Questions

None — the issue's seam section decided them, and the sizes (cap 100) are constants in the implementation, changed as easily there as anywhere.
