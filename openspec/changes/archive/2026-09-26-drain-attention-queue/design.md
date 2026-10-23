## Context

`AttentionQueue.Read` (host) composes two sources: the Runs module's `/api/runs/attention` (dispatch,
run and gate causes) and the Tasks module's `/api/tasks` (the mirror). Today the mirror is used only
**additively** — a held mirror task with no local activity becomes its own `mirror-held` row — while
every Runs-side row is shown regardless of the task's current state at the vendor. Nothing ever
removes a row: `runs` is an append-only history, so the queue grows monotonically with it.

`/api/tasks` (`ListTasks`) answers **open tasks only** — the inbox's own rule ("a to-do list, not an
archive"). That makes it the authority on "what is open", which is exactly the fact the queue failed
to consult.

#357 names two possible fixes: the mirror decides (a row drains when its task is not open), or time
decides (an age threshold retires failures). The mirror also carries the loop status, so "no longer
open" covers closed, moved-out-of-scope and done-at-the-vendor in one read.

## Goals / Non-Goals

**Goals:**
- A row whose task is not in the open mirror does not appear in the queue.
- Real blockers — open tasks with held/failed runs, unresolved dispatches, inconclusive gates —
  keep appearing.
- `no rows` keeps meaning "nothing blocked now"; unknown sources stay stated unknowns.
- The screen keeps drawing what arrives (`AttentionQueuePanel.tsx` unchanged).

**Non-Goals:**
- No new cause or status text for drained rows — they are gone, not annotated (#357 accepts either
  zero rows or one naming the state; zero is the simpler reading and the one the mirror's own
  vocabulary already states on the task itself).
- No change to `GetAttention` — the Runs module cannot see the mirror, and the join is the host's
  job (the pattern both existing halves already follow).
- No age threshold, no tunable, no migration, no screen work.

## Decisions

**The mirror decides, at the join, by task key.** In `AttentionQueue.Read`, keep the Runs rows only
when the mirror's task list carries the same key. The mirror is the same source the `mirror-held`
half already trusts, so both halves now read one authority instead of the queue half-trusting it.

- *Why the join and not a `GetAttention` filter:* the Runs module has no view of the mirror — a
  cross-module question in-process would break MOD001. The host already reads both APIs; the drain
  is one `HashSet` against the rows it already holds.
- *Why task key and not (repository, key):* the existing `mirror-held` join already matches on the
  bare key. Runs-side rows carry no repository for run causes (`Item.Provider/Host/Repository` are
  null for `run-held`/`run-failed`), so a repository join would silently keep every run row — the
  status quo dressed as a fix. The key is the join the queue already has.

**A mirror that cannot be read is a stated unknown, and it stops the drain.** The two reads are
already parallel and each failure is collected into `UnknownSources`. When the tasks half fails, the
open set is unknown, so **no Runs row is dropped** — draining on an unknown would hide a real
blocker on the one day the mirror hiccuped, which is the "calm on the exact day nothing is calm"
failure the spec's unknown rule exists for. The unknown is already stated in the response; the row
set just stays complete rather than filtered by nothing.

**The empty mirror is a drain, not an unknown.** An empty open set is a real answer (everything
closed) — it is the load-bearing case of this change: 19 historical failures over closed tasks
become zero rows. It is treated as a fact and drains every Runs row.

## Risks / Trade-offs

- [A task open at the vendor but not yet mirrored] → the row stays (task key missing from the open
  set). Narrow and self-healing: the mirror syncs on a sweep and the row returns with it. The
  alternative — treat a missing task as "probably closed" — is a one-way information loss.
- [Two tasks sharing a number in different repositories] → the key join cannot tell them apart, so
  one being open keeps the other's row. This is the pre-existing join's shape (#268's regression
  case lives in the mirror-side rows); the drain errs toward showing a row, never hiding one.
- [Failure anywhere in the queue list is less discoverable] → the queue gets shorter, which is the
  point; the history is still the Runs screen, which the row already links to.
