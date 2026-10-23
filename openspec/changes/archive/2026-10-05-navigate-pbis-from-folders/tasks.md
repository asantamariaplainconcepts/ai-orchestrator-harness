## 1. Shared frontend files — contracts, queries, copy

- [x] 1.1 Append the rail PBI-row contract to `contracts.ts` (the row's key, title, loop position,
state, hold and live-marker fields derived from the inbox read and the Connector's position),
appended to the PBI-screen block, not reordered.
      req: app-shell-and-navigation/2, pbi-inbox/1
      files: src/frontend/shared/http/contracts.ts
- [x] 1.2 Append the rail's per-folder PBI reduction query to `queries.ts` (a reduction of
`usePbis` by `folderFor`, the same join `usePbisHere` uses, exposed for the rail), appended to the
PBI-screen block.
      req: app-shell-and-navigation/2, pbi-inbox/1
      files: src/frontend/shared/http/queries.ts
- [x] 1.3 Append the rail and History copy to `en.ts` (search placeholder, no-match, empty, error,
hold badge, live marker, `Live` label and count, History headers and grouping labels), grouped by
the PBI-screen block, not alphabetical.
      req: app-shell-and-navigation/2, pbi-inbox/1
      files: src/frontend/shared/i18n/en.ts

## 2. Rail — PBIs under each folder, not branches

- [x] 2.1 In `Sidebar.tsx`, replace the `branches.map` under each folder with a reduction of the
inbox (`usePbis` + `folderFor`) to that folder's active PBIs; draw each row with key, title, loop
position and state from the Connector's read, never re-parsing labels in the UI.
      req: app-shell-and-navigation/2, pbi-inbox/1
      files: src/frontend/app/Sidebar.tsx
- [x] 2.2 Make a row's click open the PBI's voyage by full identity (source, repository, key),
independent of a worktree's existence — reaching `PbiDetailPane`/`PbiWalk` today and the voyage
contract the `pbi-journey-by-stages` change (#450) lands when it merges.
      req: app-shell-and-navigation/2, pbi-details/3
      files: src/frontend/app/Sidebar.tsx
- [x] 2.3 Order a folder's PBIs by attention required (held/blocked), then activity (a live run),
then the rest, derived from the Connector's position and hold and the runs' states.
      req: app-shell-and-navigation/2
      files: src/frontend/app/Sidebar.tsx
- [x] 2.4 Draw a **hold badge** when the PBI is held and a **live marker** when a run has not finished
as separate affordances on the row; a held PBI with no live run shows the badge, not the marker.
      req: app-shell-and-navigation/2
      files: src/frontend/app/Sidebar.tsx

## 3. Rail — search over PBIs and Live over PBIs

- [x] 3.1 Change `nav.filterProjects` to match `${folder.name} ${pbi.key} ${pbi.title}` against the
PBIs under each folder, not `${folder.name} ${branch.name}`; hide a folder's PBIs that do not match
and show a no-match note when nothing matches.
      req: app-shell-and-navigation/2
      files: src/frontend/app/Sidebar.tsx, src/frontend/shared/i18n/en.ts
- [x] 3.2 Make `Live` narrow the rows to PBIs with a live marker (a run that has not finished), and
make `liveTotal` the count of PBIs with a live marker; a held PBI without a live run is excluded
from `Live`, because a hold is not execution.
      req: app-shell-and-navigation/2
      files: src/frontend/app/Sidebar.tsx

## 4. Folder context — Overview and Files stay; Change stays for now

- [x] 4.1 In `FolderDetailScreen.tsx`, the `change` tab STAYS on the folder's tab strip. The
proposal asked for it to move to the PBI's voyage, but the voyage #446 (#450) built draws the
PBI's loop-stages and runs by identity (`PbiSpine`) and does not carry the diff, pull-request and
evidence reading `ChangeTab` serves from the worktree; removing the tab would strand `ChangeTab`
and break the review-step rail's narrow-set and closed-change-reading tests. The tab, the
`ChangeTab` render and the diff/PR reads it needs (`useFolderDiff`, `useWorktreePullRequest`)
remain as master has them. A future change that absorbs `ChangeTab`'s reading into the voyage
will move it then.
      req: app-shell-and-navigation/3
      files: src/frontend/features/folders/FolderDetailScreen.tsx
- [x] 4.2 Leave `ClosedJourneys.tsx` (`useClosedJourneys`) as the folder-local reading it is; do not
widen its `run.key`-alone grouping to global — History is a separate screen with the full-identity
join.
      req: app-shell-and-navigation/3
      files: (no change — left intact by decision)

## 5. Routing — `/history` is a screen; `?worktree=` resolves or explains

- [x] 5.1 In `routes.tsx`, replace the `/history` redirect with a route rendering the global History
screen; keep the `/workflows`, `/tasks` and `/tasks/pull-requests` redirects; update the rail's
History item to navigate (not redirect) to the screen.
      req: app-shell-and-navigation/1
      files: src/frontend/app/routes.tsx, src/frontend/app/Sidebar.tsx, src/frontend/shared/i18n/en.ts
- [x] 5.2 Resolve a `?worktree=` query on a folder route to the PBI whose voyage owns that worktree
when the relation is unambiguous — the worktree's branch is exactly one PBI of this folder by full
identity — and the URL names no tab, and replace the folder route with the voyage. A link that names
a tab is the folder serving that tab, not a bookmark for a journey, so it stays — the `&tab=change`
that photographs the ChangeTab's rail is exactly that, and opening the voyage for it broke the
narrow-set e2e that reaches that screen through it. When the worktree named has gone, when no PBI
claims its branch, or when more than one does, stay on the folder and serve the tab the URL
asked for; never select another PBI that shares a key or path. The "explain rather than stay" half
of the proposal is amended in the spec delta: opening the voyage on anything less than a proven
one-PBI match would risk the reselection the rule exists to prevent.
      req: app-shell-and-navigation/1
      files: src/frontend/features/folders/FolderDetailScreen.tsx

## 6. History — global grouping by full identity (depends on #450)

- [x] 6.1 Build the global History screen grouping runs into journeys by the PBI's full identity —
source, host, repository and key together — reusing the same join `PbiWalk` computes
(`run.key === task.key && run.folderPath === repo.path`), not the `run.key`-alone grouping of
`useClosedJourneys`. Two PBIs sharing a remote key keep separate histories; a journey stays present
after its worktree is removed.
      req: pbi-inbox/2
      files: src/frontend/features/history/HistoryScreen.tsx
- [x] 6.2 Draw a journey's runs from the runs-by-identity read the voyage serves (the read
`read-a-run` declares, served by the Runs module — added by #450's task 1.1), not from a client-side
re-grouping of a flat list.
      req: pbi-inbox/2
      files: src/frontend/features/history/HistoryScreen.tsx, openspec/changes/navigate-pbis-from-folders/specs/pbi-inbox/spec.md
- [x] 6.3 Name the four connection states (loading, error, absence, unattributed) on a journey's
read the way `pbi-details`'s entries name them; a failed read is an error, not an absence.
      req: pbi-inbox/2, pbi-details/2
      files: src/frontend/features/history/HistoryScreen.tsx, src/frontend/shared/i18n/en.ts
- [x] 6.4 This section SHALL be realised after #450 (`pbi-journey-by-stages`) has merged: the
runs-by-identity read (task 1.1) is a dependency, and the "same voyage" the rail row opens is the
one #450 builds. Begin only when #450 is on `master`.
      req: pbi-inbox/2

## 7. Empty, no-match and error states; keyboard and back/forward

- [x] 7.1 Distinguish loading, empty, no-match and error across the rail and History, each with its
own message; an error is not empty, and empty is not no-match.
      req: app-shell-and-navigation/3, pbi-inbox/2
      files: src/frontend/features/history/HistoryScreen.tsx, src/frontend/app/Sidebar.tsx, src/frontend/shared/i18n/en.ts
- [x] 7.2 Ensure keyboard navigation and browser back/forward work across the rail rows, the History
screen and the voyage reached from a row.
      req: app-shell-and-navigation/2, pbi-details/1
      files: src/frontend/app/Sidebar.tsx, src/frontend/features/history/HistoryScreen.tsx

## 8. Narrow design set — the rail, History and the voyage at 390px

- [ ] 8.1 Add the rail with PBI rows, the History screen and the voyage reached from a row as
covered narrow surfaces: a 390×844 light/dark pair asserting no required control ends past the right
edge, the document does not scroll sideways, and the current stage's first panel row is reachable by
scrolling the application's own scroller.
- [ ] 8.2 Run the narrow capture where the product is available; stage captures in ignored
artifacts under the `evidence-452-` convention; `docs/design/current/` untouched.

## 9. Gate and check

- [ ] 9.1 Run `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
until every test assembly in `src/AiOrchestrator.slnx` passes with zero failures; record the count
observed.
- [ ] 9.2 Navigate from two folders whose PBIs share a remote key and confirm separate histories;
remove the worktree of a closed journey and reopen it from History; test old `?worktree=` links, the
search, `Live`, keyboard and back/forward, in both themes and at 390px. Record what was observed in
the check.
