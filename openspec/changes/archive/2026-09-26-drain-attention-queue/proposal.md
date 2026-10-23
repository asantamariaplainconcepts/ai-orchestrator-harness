## Why

The attention queue answers "what needs a person now" from every `held`/`failed` run ever recorded,
with nothing that ever retires a row (#357). A task that failed weeks ago, whose issue is closed and
whose run nobody restarted, keeps its row forever — on the machine that reported the bug, 19 of the
20 rows are historical noise, and the queue's purpose is lost in them.

## What Changes

- A row whose task is **not in the open mirror drains**: the `AttentionQueue` join in the host drops
  every row whose task key the mirror's open task list does not carry. The mirror already is the
  authority on "what is open" — the queue just never looked at it.
- Draining is a join, not a mode: the rows are filtered at composition, so `no rows` keeps meaning
  "nothing blocked now", and the unknown-source rule is untouched — a mirror that cannot be read is
  a stated unknown, never an empty queue.
- `GetAttention` still lists every cause unfiltered; the join stays in the host, which is the only
  place that knows both modules exist. No screen work, no migration.

## Capabilities

### New Capabilities

- none

### Modified Capabilities

- `attention-queue`: the "one row per blocked task" requirement gains the drain rule — a row whose
  task is no longer in the open mirror is not shown. The queue responds to **now**, not to history.

## Impact

- `src/root/AiOrchestrator.Server/Attention/AttentionQueue.cs` — the join with the mirror's open
  task list, which is where the drain lives.
- `src/tests/AiOrchestrator.Server.FunctionalTests/` — server-side tests of the composition.
- No change to `GetAttention`'s causes, to the panel, or to the Runs module's queries.
