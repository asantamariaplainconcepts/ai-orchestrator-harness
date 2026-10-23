## Context

The projects rail (`src/frontend/app/Sidebar.tsx`) lists, under each folder, the folder's
**branches**: a row is a `Branch` whose destination is `scopeHref(folder, branch)` →
`/folders/{id}?worktree={path}`, `Live` (`liveOnly`) filters branches with a run, and the only text
search is `nav.filterProjects` matching `${folder.name} ${branch.name}`. There is no global
History: `/history` is `<Navigate to="/pbis" replace />` (`routes.tsx:49`), and `useClosedJourneys`
groups runs by `run.key` alone, local to one folder's checkout, so two PBIs sharing a remote key
collapse and a journey closed in another folder is invisible.

The PBI's **voyage** — the unified destination a rail row, the inbox and a future `Loop` entry all
open — is the one #450 (`pbi-journey-by-stages`) builds: a spine of stages from the declared loop,
runs grouped under their stage, the Connector's position drawn, surviving the worktree. The
runs-by-identity read #450 adds (its task 1.1, behind `read-a-run`) is the join this change's
History reuses: `PbiWalk` already computes `run.key === task.key && run.folderPath === repo.path`
client-side, and that full-identity join is the contract History groups by — not the `run.key`-alone
join of `useClosedJourneys`.

The rail's per-folder PBI view already exists as a client-side reduction in `OverviewTab.tsx`
(`usePbisHere`), which reduces the global inbox (`usePbis()`) to a folder by matching `origin`
against the folder's remotes (`folderFor`). The server has no folder filter (a stated fact in
`pbi-inbox`); the rail reuses that reduction rather than asking for one.

Stakeholders: the person, whose work is PBIs, not branches; the parallel work on #450, whose voyage
is this change's destination; the parallel refine/propose of #453 (sessions tray) and #454 (PR
review), neither of which this change pre-empts.

## Goals / Non-Goals

**Goals:**
- The rail's `Projects` group shows, under each folder, that folder's active PBIs (key, title, loop
  position, state) as clickable destinations, ordered by attention required → activity → the rest, a
  row independent of a worktree's existence and opening the same voyage the inbox opens.
- The rail's text search finds PBIs by key and title; `Live` narrows to PBIs with a live run and
  distinguishes a hold from a run in progress.
- A global History screen groups runs by the PBI's full identity (source, host, repository, key), so
  a closed journey stays reachable after its worktree is removed and two PBIs sharing a remote key
  keep separate histories.
- The folder's `Change` tab moves to the PBI's voyage; Overview and Files stay in the folder's
  context.
- Old `?worktree=` links resolve to their PBI when unambiguous or explain; they never select another
  PBI by shared key or path.

**Non-Goals:**
- Building the voyage spine itself — that is #450. This change reaches it as a destination; until
  #450 merges, the row opens the `PbiDetailPane`/`PbiWalk` reading of today.
- The global `Loop` entry — that is #451.
- The sessions tray / `Session` contract — #453.
- PR review/edit without checkout, and comments/verdicts — #454/#455.
- Removing the creation/deletion commands of worktrees or their mechanical management — this change
  moves navigation destinations, not the controls.
- The retired controls' final home (Explore idea, Terminal here) — those ride with the
  sessions/launcher issue; the rail's `…` menu is reduced here to what the folder still owns.

## Decisions

### Decision 1 — The rail's per-folder PBI rows are a client-side reduction of the inbox, not a new server read
The rail reduces the global inbox read (`usePbis()`) to each folder's PBIs by matching each PBI's
`origin` against the folder's remotes via `folderFor`, exactly as `OverviewTab`'s `usePbisHere`
already does. The server has no folder filter (a `pbi-inbox` requirement), and adding one would
duplicate the inbox's identity resolution and its counts. The rail's rows are a view of the same
read the inbox draws, in vendor order, with the same vendor fields — no product-added folder
attribution on the row.

**Why not a per-folder server read:** the inbox is filtered server-side because the counts must agree
with the list; a folder filter is explicitly not a filter the server holds, and the folder→PBI
relation is resolved from the PBI's `origin` against the folder's remotes, a join `folderFor` already
performs. A second read would create a second source of the same PBIs and a second counts answer.

### Decision 2 — The row's destination is the voyage #450 builds, declared as a contract, not duplicated
The rail row opens the PBI's voyage — the detail/journey that #450 centres. This change declares the
destination contract (a row opens the PBI's voyage by full identity) and points the row at whatever
satisfies it: today's `PbiDetailPane`/`PbiWalk` until #450 lands, the spine after. The change does
not build a second journey, does not fork the detail, and does not invent a rail-owned voyage screen.

**Why a contract rather than a fork:** the detail (and #450's spine) is the single reading of a PBI;
duplicating it in the rail would make two readings to keep aligned, the failure `pbi-details`'s
"destinations resolved by full identity" requirement exists to prevent.

### Decision 3 — History groups by full identity, reusing `PbiWalk`'s join, not `useClosedJourneys`'s
`useClosedJourneys` groups by `run.key` alone and is local to one folder's checkout (it drops a run
only if its working directory is not in that folder's `branches`). History is global and groups by
**source + host + repository + key**: the same join `PbiWalk` performs
(`run.key === task.key && run.folderPath === repo.path`), and the same identity `pbi-details` enforces
for reads and writes. Two PBIs that share a remote key under different repositories or folders keep
separate histories; a journey whose worktree was removed stays open from History because the
runs-by-identity read (#450's task 1.1) does not depend on a working copy existing.

**Why not extend `useClosedJourneys`:** its grouping key is `run.key` and its scope is one folder's
checkout; widening it to global would change the grouping of an existing, folder-scoped reading
`OverviewTab` still uses, and would conflate "closed in this folder" with "closed anywhere". A
separate History screen with the full-identity join leaves the folder-local reading intact.

### Decision 4 — `Live` reads hold and run distinctly on the row
A row shows a **hold badge** when the PBI is held (a person is waited on) and a **live marker** when a
run for it has not finished; `Live` narrows to PBIs with a live marker. The distinction is the
Connector's hold versus a run's state, both already served — the row draws both, the way the
attention chip already distinguishes blocked from waiting at the rail's level. A hold is not a live
run, and a live run is not a hold; the badge and the marker are separate affordances.

**Why not collapse them:** the inbox's attention model treats "needs a person" and "is working" as
the two reasons to look; collapsing them would make a held PBI look like it is running, which it is
not.

### Decision 5 — Old `?worktree=` links resolve by relation or explain, never reselect
A `?worktree=` link resolves to its PBI when the relation is unambiguous (the worktree's folder and
branch name identify one PBI by full identity), or shows an explanation naming why it cannot. It does
not fall back to selecting another PBI that shares a key or path — the same rule `pbi-details`'s
"destinations are resolved by full identity" requirement enforces for review and execution entries.

**Why not a silent fallback:** a bookmark that used to open work X landing on work Y because they
share a key is the silent misselection the identity rules exist to prevent; an explanation lets the
person correct the link.

## Risks / Trade-offs

- **The row's destination is aspirational until #450 merges.** A row opens the voyage #450 builds;
  until then it opens today's detail. **Mitigation:** the destination is a declared contract the
  rail satisfies with whatever exists; tasks depending on #450's spine are ordered after it in
  `tasks.md`, and the row's behaviour without #450 is the detail's behaviour today, not a broken
  link.
- **The rail's per-folder reduction is client-side, like `OverviewTab`'s.** A folder with many PBIs
  across many remotes reduces on the client; the inbox read is already the bottleneck `OverviewTab`
  shares. **Mitigation:** reuse the same read, the same `folderFor` join, the same vendor order; no
  second fetch, no second source of counts.
- **History's runs-by-identity read is #450's, not this change's.** History depends on the read #450
  adds (task 1.1). **Mitigation:** the dependency is declared and blocking; History tasks are
  ordered after #450, and the full-identity join `PbiWalk` does client-side is a fallback for the
  global grouping if the served read is not yet present — but the served read is the intended path.
- **Removing `Change` from the folder's tab strip must not strand a journey.** A person who reaches
  a worktree's diff/PR/evidence from the folder today reaches it through `Change`; removing it from
  the folder moves that destination to the PBI's voyage. **Mitigation:** the voyage (which #450
  centres) carries the PR and evidence references as links to the published object and the issue, so
  the destination survives the move; the folder's `loop` and `commands` tabs, which are the
  checkout's own, stay.
- **Parallel work on `Sidebar.tsx` and the shared frontend files.** `Sidebar.tsx`,
  `contracts.ts`, `queries.ts` and `en.ts` are shared-collision surfaces; #450 may touch the shared
  files in its PBI-screen block. **Mitigation:** append to the PBI-screen block, do not reorder a
  neighbour's lines, and say any file split in the issue before doing it (AGENTS.md rule 4).
