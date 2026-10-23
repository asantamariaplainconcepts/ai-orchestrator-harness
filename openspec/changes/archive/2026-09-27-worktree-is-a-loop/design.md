# Design — the worktree is the loop's instance

## Context

Three facts make this change cheap, and one makes it honest.

The backend already did the inversion: #365's `LoopWorkflowPlanReader` resolves a Run's plan from
`.harness/issue-loop.yaml`, and its own doc says "a controlled Run is one segment of the declared
loop." Every Run a folder dispatches carries `WorktreeName`, `IsTransition`, a dispatch row with its
decision snapshot, cost, and activity. The frontend still routes people to `/runs/:runId` as a
destination, keeps a global History of all runs, and gives the worktree a strip that names neither
the loop nor where the journey stands.

Second: the worktree-to-issue-to-position join already exists in pieces. `RunSummary.workingPath`
+ not-finished is the sidebar's liveness; "most recent run in this directory" is ChangeTab's issue
resolution rule; `OverviewTab` asks "waiting / running / could start" — the journey question in 
another accent. Nothing new is computed; what is missing is one screen that asks it on purpose.

Third: `Worktree.State` was a dead column removed deliberately — nothing ever wrote it. The design
decision ("what runs here is not a fact this record holds; teach Workspace to answer liveness is
a World-seam decision") is recorded in `Worktree.cs`'s own comment. This change inherits that
decision rather than reversing it: position is projected from Runs at read time. The Rails joins
the UI already makes (`useRuns(folderPath, workingPath)`) stay on the UI side of the schema line,
which is where `ARCHITECTURE.md` put them.

The honest cost: two deletions (`runs/:runId` as a destination, the History screen) remove
answers nobody asked for. The History table arose when a Run was the product's central noun and
its audit trail needed a home screen. With the worktree as the unit of reading, "all runs on this
machine" is a log, and this product does not ship a log viewer on purpose.

## Goals and Non-Goals

- Goals: entering a worktree answers "where does the journey stand, what is the agent doing" in
  one screen; a run is entered through the loop, never as a destination; History dies; vocabulary
  updated once at merge.
- Non-Goals: backend changes of any kind; teaching Workspace a position query (the UI projects from
  Runs, no seam opened); `ScreenFrame` retirement; the Tasks screen demotion; provenance for
  pre-journey runs (anything exact before a run run by the loop machinery).

## Decisions

### Position is projected from Runs at read time, never stored

`loopAt(worktree)` = the latest `IsTransition` run in that directory with its dispatch's node, or —
nothing running — the node the *last* landed transition reached (hold label semantics reused read-only).
The number of sources remains what the sidebar join already needed: one `useRuns(folderPath,
workingPath)`. If two screens want richer joins (`Run` grouped by transition) that is `ListRuns`
gaining a query shape, a backend change opened on its own ticket — **not** cross-schema reach, the
exact thing `Worktree.cs`'s comment warns about the UI committing accidentally.

Accept the projection's limits and say them on screen: runs started by hand (typed prompt,
`IsTransition = false`) are drawn dimmed on the journey as *off-loop* — exactly the #333 honesty
(what ran is not necessarily a machine step) that field already records.

### The journey header, drawn from parts that exist

```
┌─ #42 · refine ✓ ── propose ✓ ── ▓implement▓ ● ── sync ───────────┐
│    r1 · 12m · $0.84   r2 · 31m · $3.10   r3 live · AgentActivity │
└────────────────────────────────────────────────────────────────────┘
```

- Machine: `LoopGraph`'s nodes (from the declared loop, already read by `useIssueLoop`), drawn
  horizontally as a strip, not a graph — the graph's edges carry routing the strip doesn't need;
  `LoopGraph`/`LoopInspector` fold into the header's implementation and stop being separate
  surfaces.
- Position mark: from `loopAt` above. `●` = live run, clicking it opens that run's terminal in the
  pane `RunPane` already provides (`?run=` deep link through `openRun`, minus the route).
- Walked transitions: the `IsTransition` runs of this worktree, each `rN` stamped with the facts
  `RunSummary` already carries.
- Below: `ChangeTab` unchanged in shape; its step panels each gain a provenance line ("produced by
  r2 · /aio:propose · 31m · $3.10") from the same runs list — zero new queries, a `find` over data
  the screen already holds.

### One run surface: RunView embedded, `/runs/:runId` deleted

`RunDeepLink` already resolves `?run=<id>` to a global tab and drops the param — the route was
half-redundant. `/runs/:runId` becomes: resolve the run's `workingPath` + folder, navigate to that
folder's screen scoped to the worktree, hand `?run=` to the existing tab mechanism. The
`RunScreen.tsx` full-page shell (its header shell, its tabs wiring, its Steps column/strip and
their responsive logic) is deleted; `RunView` + `AgentTerminal` remain. Everything the dead code
did that nothing else does dies with it: that is the point of having the embedded view first.

### History dies; spend reporting moves, the query stays

`GET /api/runs/spend` is untouched — spend is a fact about a folder's work and belongs beside the
folder's other facts. The History screen's consumption of it (windowed total above a global table)
dies with the screen; a future change may surface spend beside the Overview facts if somebody asks
for it. Deleting a screen without a replacement for its orphaned facts is the mistake this avoids
by keeping the API.

### Vocabulary and docs, once, at merge

`README.md`'s vocabulary block and `AGENTS.md`'s "What this is" keep every locked word; the
hierarchy sentence changes: *a worktree is the instance of a loop for one issue; a run is one
transition of it by an agent.* Design.md's copy rules make this cheap. Done in the same change so
docs and code land together — the loop's own rule.

## Risks and Trade-offs

- **Deep links to `/runs/:id` break** — mitigated exactly as `/workflows` was: redirect to the
  owning worktree screen, not 404.
- **Two sources of "where is it"** if a label says one node and the run dispatch says another —
  the run's dispatch wins (it is the observed fact); the label is advisory. Same rule the
  controlled machinery already applies (#361's fact-checks).
- **Worktrees from before the loop** (hand runs, pre-`IsTransition` history) show a journey with
  no marks — correct and stated, not hidden: the header names "no transitioned run" rather than
  guessing from labels.

## Migration Plan

1. Journey header on the worktree strip, read-only (machine + position + walked list).
2. `ChangeTab` provenance lines.
3. Deep-link re-route; delete `/runs/:runId` route + `RunScreen` shell + Steps column/strip.
4. Delete `HistoryScreen` + rail item; move/keep spend API as-is.
5. Vocabulary/doc update + retro entry.
