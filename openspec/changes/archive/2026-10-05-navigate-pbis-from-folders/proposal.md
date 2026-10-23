## Why

The projects rail lists, under each folder, the folder's **branches**, not its PBIs. A row is a
`Branch` that opens the folder scope with `?worktree=`, `Live` filters branches that have a run, and
the only text search that exists is over branch names. There is no global History: `/history`
redirects to `/pbis`, and the closest thing — `useClosedJourneys` — groups runs by `run.key` alone,
local to one folder's checkout, so two folders whose PBIs share a remote key collapse into one and
a journey closed in another folder is invisible here. The PBI's voyage — the unified destination
the rail, the inbox and a future `Loop` entry all open — is being built by #450 (`pbi-journey-by-stages`);
this change makes the rail and History reach it, and stops the folder's `Change` tab from competing
with it.

## What Changes

- **The rail lists PBIs under each folder, not branches.** Each folder in the `Projects` group shows
  its **active PBIs** as rows — key, title, loop position, state — ordered by attention required,
  then activity, then the rest. A row does **not** depend on a worktree existing and opens the
  **same voyage** the inbox and the `Loop` entry open. Until #450 lands, the row opens what the
  detail offers today (`PbiDetailPane`/`PbiWalk`); once #450 lands, it opens the journey spine.
- **The rail's text search finds PBIs by key and title**, not branches. The `nav.filterProjects`
  field today matches `${folder.name} ${branch.name}`; it SHALL match `${folder.name} ${pbi.key}
  ${pbi.title}` against the PBIs each folder holds.
- **`Live` identifies live runs over PBIs, not branches**, and distinguishes a **hold** (a person is
  waited on) from a **run in progress**: the hold reads on the row as a hold badge, a live run as a
  live marker, and `Live` narrows to PBIs with a run that has not finished.
- **A global History screen is added.** `/history` ceases to redirect and becomes a screen that
  groups runs by the **full identity of the PBI** — source, host, repository and key together — the
  same identity join `PbiWalk` already does (`run.key === task.key && run.folderPath === repo.path`),
  not the `run.key`-alone grouping of `useClosedJourneys`. A journey closed in one folder stays open
  from History after its worktree is removed, and two PBIs that share a remote key under different
  folders or repositories keep separate histories.
- **Overview and Files stay in the folder's context; Change moves to the PBI's.** The folder keeps
  `overview` (the disk's PBI sections) and `files` (the disk's tree); the `change` tab leaves the
  folder's tab strip and is reached as part of the PBI's voyage, so a journey is not duplicated
  between a folder tab and the PBI.
- **Old links resolve or explain, never silently reselect.** A `?worktree=` link SHALL resolve to its
  PBI when the relation is unambiguous, or show an explanation; it SHALL NOT select another PBI that
  shares a key or path, the same rule `pbi-details` already enforces for reads and writes.
- **Empty states, filter-with-no-match and read errors carry distinct messages**, and keyboard and
  back/forward navigation work across the rail, History and the voyage.

## Capabilities

### New Capabilities

(None. The rail, the search, Live, History and the link resolution are the read side of
capabilities that already exist; the delta modifies how navigation reaches them, not the
capabilities themselves.)

### Modified Capabilities

- `app-shell-and-navigation`: the rail's `Projects` group lists PBIs under each folder (not
  branches), the text search finds PBIs by key and title, `Live` narrows to PBIs with a live run
  and distinguishes a hold, `/history` is a screen rather than a redirect, and the folder's tab
  strip loses `Change` (kept by the PBI's voyage).
- `pbi-inbox`: the global History screen reuses the inbox's identity resolution and its
  board/filters remain global; the rail's per-folder PBI rows are a view of the same inbox read,
  not a second source.
- `pbi-details`: the voyage a row opens is the PBI's detail/journey — the destination #450 centres
  — and a closed PBI stays reachable from History by full identity after its worktree is gone; old
  `?worktree=` links resolve to the PBI or explain, never reselecting another by shared key or path.
- `the-narrow-design-set`: the rail with PBI rows, the global History screen and the PBI voyage
  reached from a row are covered narrow surfaces at 390×844 in both themes, asserting no required
  control ends past the right edge and the document does not scroll sideways.

## Impact

- **Frontend — rail and routing:** `src/frontend/app/Sidebar.tsx` (the `Projects` group: PBI rows
  under each folder, key/title search, `Live` over PBIs, hold-vs-run distinction, reduced `…` menu),
  `src/frontend/app/routes.tsx` (the `/history` route becomes a screen; old `?worktree=` resolution),
  and a new `History` screen reusing the full-identity join `PbiWalk` already computes.
- **Frontend — folder context:** `src/frontend/features/folders/FolderDetailScreen.tsx` and
  `OverviewTab.tsx` keep Overview and Files; `Change` leaves the folder's tab strip for the PBI's
  voyage. `ClosedJourneys.tsx` stays as the folder-local reading it is; History is global and does
  not reuse its `run.key`-alone grouping.
- **Frontend — voyage destination:** the row's destination is the PBI's voyage that #450
  (`pbi-journey-by-stages`) builds. Until #450 merges, the row opens the `PbiDetailPane`/`PbiWalk`
  reading of today; the delta declares the destination contract, not a second journey.
- **Frontend — shared files:** `contracts.ts`, `queries.ts` and `en.ts` gain the rail's PBI-row
  contract, the History grouping and their copy, appended to each screen's block and not reordered.
- **Backend:** only in what the new navigation asks to read — the per-folder PBI view is a
  client-side reduction of the inbox read over the folder's remotes (`folderFor`), as `OverviewTab`'s
  `usePbisHere` already does; History's runs-by-identity read is the one #450 adds behind `read-a-run`
  (its task 1.1), reused here rather than re-served. No migration, no module crossing another's
  schema: paths, not foreign keys.
- **Seam:** anything reaching outside this process goes through `BuildingBlocks/World/` and
  `AddWorld()`; if a new reach is needed it is registered there. The rail and History compose above
  existing seams (`usePbis`, `useRuns`, `folderFor`, the Connector's position read).
- **Depends on #450 (`pbi-journey-by-stages`)** — declared and blocking. The "same voyage" a rail
  row opens is the one #450 builds; until it lands, the row opens today's detail. Tasks that depend
  on #450's spine are ordered after it in `tasks.md`.
