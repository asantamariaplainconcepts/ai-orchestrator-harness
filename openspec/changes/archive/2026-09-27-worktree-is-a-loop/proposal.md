# The worktree is the loop's instance

## Why

The product has already turned: #365 made a Run *a segment of the declared loop* (the backend reads
`.harness/issue-loop.yaml` and dispatches controlled Runs as its transitions) but the frontend and
the navigation still treat the Run as the product's central noun — a full-screen route, a global
history table, a worktree whose strip says nothing about where its journey stands. The result is
that the loop — the thing the harness exists to run — is invisible as a first-class thing, and
"what did this worktree do, what is it doing now, what is it waiting for" is a question the person
assembles by hand.

The facts are already owned by the right rows. A worktree's journey is `SELECT * FROM Runs WHERE
worktree = X AND is_transition ORDER BY started_at`: each transition wrote its command, its
terminal, its cost. `Worktree` correctly holds no state of its own (its dead `State` column was
removed on purpose) — the loop position **is a projection over the Runs**, which is a join of lists
the UI already has.

## What Changes

- **The worktree screen becomes the loop's instance screen.** The worktree strip gains a **journey
  header**: the folder's machine (nodes from the declared loop) with this worktree's position marked
  — the current transition live (its terminal reachable, one click), the transitions already walked
  grouped behind the header, each stamped with its run's facts (command, duration, cost, outcome).
  Entry a worktree = see where it stands and what the agent is doing, with zero further clicks.
- **Run is reachable only through the loop.** The `/runs/:runId` route is deleted; a run deep link
  resolves to its worktree's screen. `RunScreen`'s full-page shell dies with the route; `RunView`
  (already embedded as `RunPane`) is the only run surface. The tablet-laid-out Steps column/strip
  of the old Run screen dies with it — the journey header replaces "the steps a run took" with "the
  transitions this instance walked", which is the question that survived.
- **The History screen is deleted.** "What runs happened, everywhere" is not a question this
  product answers a screen to — the worktree's journey answers its scoped version, and an
  unscoped log of every transition is debugging, not reading. The `History` rail item goes; spend
  reporting (`GET /api/runs/spend`) stays in the API and finds a smaller home beside the folder
  facts.
- **Per-step provenance in the Change tab.** Each step panel of `ChangeTab` names the transition
  that produced it ("last run in this directory", already the rule for resolving the issue) with
  its signature — command, duration, cost — so the execution and what it mounted are one reading,
  not two screens.
- **The sidebar worktree row says the position.** The row's liveness join (already
  run-working-directory vs branch) gains the position word: the node the live run is executing, or
  the hold it waits on, from the run's dispatch.
- **Vocabulary updated once, at the merge.** README's locked vocabulary and `AGENTS.md` pointer:
  Run = *one transition of a loop by an agent*; Worktree = *the instance of a loop for one issue*.
  The words Folder / Task / Connector / Workflow / Hold / Run / Agent survive; the hierarchy
  changed under them and the doc says so.

## Capabilities

### New Capabilities
- `worktree-is-a-loop`: the worktree screen shows a worktree's journey — machine with position,
  live terminal one click away, walked transitions with their facts, per-step provenance in the
  change — and holds no state of its own doing it (projection over Runs, the form #365 left).

### Modified Capabilities
- `app-shell-and-navigation`: the route tree loses `runs/:runId` and `history`, gains nothing; the
  run deep link resolves to the owning worktree's screen; the rail's bottom loses History.
- `read-a-run`: one full-screen Run screen is deleted along with the History table that needed it;
  a run is read embedded in a worktree's journey, on the terms of `RunView` today.
- `run-a-task`: nothing backend — but the worktree-only surfaces that offered ad-hoc "start a run"
  stop being promoted entry points; the loop's admission is.
- `review-a-change-in-one-tab`: each step names the transition that produced it — the issue
  resolution rule ("most recent run in this directory") generalises to every step's provenance
  line and joins to it.
- `run-spend`: the History band is deleted with the screen; the query stays, folded into the
  folder facts where its numbers are one more fact rather than a destination.
- `worktree-lifecycle`: the worktree row and screen answer "where does this journey stand" —
  still no stored state; the liveness join gains the position word.

## Impact

- **Changed**: `FolderDetailScreen` (worktree strip + journey header), `ChangeTab` and its step
  panels (provenance line), `Sidebar.tsx` (row position, History item removed),
  `AppShell.tsx`/`routes.tsx` (routes, deep-link consumer), `read-a-run`'s UI carriers.
- **Deleted**: `RunScreen.tsx`'s full-page shell (`runs/:runId` route), `HistoryScreen.tsx`,
  `StepsColumn`/`StepsStrip`, the History rail item and i18n keys that served only them.
- **Kept, backend untouched**: `ListRuns`, `GetRun`, `GetRunTranscript`, `GetSpend`, `RunView`,
  `AgentTerminal`, `RunPane` and the whole controlled machinery — the journey header reads what
  #365's Runs already write (`WorktreeName`, `IsTransition`, dispatch, cost, activity).
- **No migrations**: everything projected; Runs owns the facts. The `Worktree` table gains no
  column — its design comment (`Worktree.cs`: state is a projection, not a fact this record holds)
  is the decision this change follows, written ahead of it.
- **Not opened here**: collapsing `ScreenFrame` to a simpler ring, retiring `agent chat` remnants
  beyond what `RunView` already carries, `/aio:*` command surface changes, `Tasks` screen
  reweighting. Those are separate changes this one makes cheaper, not does.
