## Why

The header carries global chrome that belongs to the rail or to the screen underneath, not to the row it occupies. The search is 220px of hint text with nothing behind it, "New run" navigates to a screen one rail row away, and four icon buttons duplicate destinations the rail can hold. Meanwhile the workflow editor lives on a global route far from where workflows are used, and the 520px breakpoint exists solely to fold icons that no longer belong in the header.

Issue: #311.

## What Changes

- **Remove the search hint and "New run" from the header.** Nothing behind either: no command palette, no handler, and "New run" is a navigation the rail already offers one row above.
- **Move the attention chip (amber circle, agents blocked), the holds counter (amber badge), and the refresh button to the rail's logo block** — the "AI Orchestrator · local agent runs" area beside the app name.
- **Embed the workflow editor in the project's "AI workflows" tab.** `WorkflowsTab.tsx` moves from catalogue-only to the full master/detail editor (list, step edits, holds, reorder, rename, delete, create, SourceNotice, import/export). `WorkflowsScreen.tsx` is removed.
- **Remove the `/workflows` route.** A URL typed by hand redirects to `/tasks` rather than 404 — the screen is gone but the path was a published link in `WorkflowsTab.tsx`'s "Edit workflows" button and may survive in a bookmark.
- **Move history, settings and theme to the bottom of the rail**, beside the "this machine" card.
- **Remove the 520px breakpoint (`headerCompactBelow`) and its overflow menu.** Nothing is left to fold: the four icons that were folded are in the rail, and the search and "New run" are gone.

## Capabilities

### New Capabilities

_None — this is a relocation of existing chrome, not new behaviour._

### Modified Capabilities

- `app-shell-and-navigation`: the header stops carrying global navigation and attention state; the rail takes the attention chip, the holds counter, the refresh control, history, settings and theme. The `/workflows` route is removed (redirect to `/tasks`). The 520px breakpoint and its overflow menu are removed. The six routes become five.
- `author-a-workflow`: the workflow editor is authored inside the project's "AI workflows" tab rather than on a global screen. The spec's references to "the Workflows screen" change to the tab.

## Impact

- `src/frontend/app/AppShell.tsx` — the header loses search, "New run", attention chip, holds counter, the four icon links, the divider, `moreChoices`, `iconLink`, `headerCompactBelow`. The `titles` table loses the `/workflows` row.
- `src/frontend/app/Sidebar.tsx` — gains the logo block (attention chip, holds counter, refresh) and the bottom block (history, settings, theme beside "this machine"). Calls `useTasks`, `useRuns`, `useLiveRuns`, `useAllTerminals` that today the shell holds.
- `src/frontend/features/folders/WorkflowsTab.tsx` — becomes the full editor, moved from `WorkflowsScreen.tsx` (not copied).
- `src/frontend/features/workflows/WorkflowsScreen.tsx` — removed.
- `src/frontend/app/routes.tsx` — `/workflows` route replaced with a redirect to `/tasks`.
- `src/frontend/shared/i18n/en.ts` — `app.search`, `app.newRun`, `app.more` removed (no consumer); new keys for the rail's bottom block appended beside the rail's existing block.

No backend changes, no `contracts.ts` or `queries.ts` changes.
