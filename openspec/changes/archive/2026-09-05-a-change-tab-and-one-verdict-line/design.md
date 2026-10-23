## Context

`FolderDetailScreen.tsx` (1,158 lines) owns the scope, the tab strip and every tab body's render
site. `MergeGate.tsx` (735 lines) sits above the strip on a worktree and draws five condition tiles
plus an aggregate verdict. Both are hot files: this is the first ticket of epic #288 to touch
`FolderDetailScreen.tsx`, and by the epic's own rule **the first ticket to touch a hot shared file
extracts the seam**, because eight tickets follow — #290's rail state, #291–#295's five panels,
#296's header action.

Two constraints from outside this ticket bound the design:

- `change/264` (PR #265, another author's, in progress) will append `` `review:${string}` `` to the
  `Tab` union's dynamic arm. That arm is left intact, contiguous and in place.
- `pnpm format:check` is already red on `MergeGate.tsx` and `en.ts` among 24 files. Running Prettier
  on either would reformat the whole file and collide with every sibling. Edits match the surrounding
  style by hand.

There is no frontend test runner in this repository — `package.json` declares `dev`, `build`,
`preview`, `lint`, `typecheck`, `format:check` and nothing else. The gate for this change is
`tsc --noEmit`, `eslint --max-warnings=0`, `vite build` and the nine end-to-end tests, all inside
`aspire do ci`.

## Goals / Non-Goals

**Goals:**

- One worktree tab, `Change`, that is the whole approval surface.
- One mono line that carries every fact the five tiles carried, plus the pull request, and states an
  unknown as an unknown.
- A rail and a footer that navigate, and a seam where each of the eight tickets behind this one adds
  **at most one contiguous thing**.
- The `merge-gate` spec rewritten so the shipped behaviour and the written requirement agree again.

**Non-Goals:**

- No step content. Five placeholders, each naming its ticket.
- No stored ticks (#290), no merge (#296), no narrow-width measurement (#297).
- No session-tab changes, no `+` menu changes.
- No new kit primitive. The epic's headline finding is that every token and class this canvas needs
  is already vendored.

## Decisions

### 1. `Review` and `Checks` are rescoped, not deleted

`fixedTabs` rows 136 and 137 are `scope: "both"`. The ticket says the worktree strip loses them; the
checkout legitimately has both. Changing `"both"` → `"checkout"` removes them from exactly one strip
and leaves the other's eight untouched. **Alternative rejected:** deleting the rows, which would take
`DiffTab` and `CommandsTab` off the checkout as well — a view this change is not about.

### 2. `issue` is renamed to `change`, and `homeFor` follows it in the same edit

`homeFor(worktree)` returns `"issue"`. The table's own doc says *"whatever this returns must be in
`tabsFor`'s answer for the same two arguments, or `present()` sends the screen straight back here"* —
so leaving it would typecheck and then loop at runtime. `runOf`, `fillsHeight` and `present` all
match `"term:"` and `"run:"` with `startsWith`; none of those literals moves.

`chat` moves above `telemetry` in the table so the worktree strip reads
`Change · Runs · Files · Chat · Telemetry`. The checkout strip is unchanged by that move, because
`telemetry` is not in it.

### 3. The mono row carries six facts, and no fact is ever a tick

One `Fact = { text: string; tone: Tone }` per source, joined by ` · `. The six sources are the five
the tiles read — `worktree.ahead`/`behind`, `worktree.mergesClean`/`conflicts`,
`gateCommands(detail)` + `lastCheck`, `worktree.detail`, `parseSeam × compareSeam` — plus
`useWorktreePullRequest`.

Colour is carried only where something is *not* ordinary: a bad fact takes the error ink, a warning
the warning ink, and everything else — including every "ok" — stays muted mono. **That is the
mechanical form of "no ticks":** there is no green in the row to mistake for a verdict, and the words
are what say which fact is which. It also keeps DESIGN.md's rule that colour is never the only
carrier, since every fact is a sentence first.

**Alternative rejected:** keeping per-fact green. A row where three facts are green and two are grey
reads as a verdict with two abstentions, which is the thing being removed.

### 4. The three unknown states survive #283's distinction

`parseSeam` returns `ReadFromBody<SeamEntry[]>` — a value, `null`, or `unreadable`. #283 landed the
third state an hour before this change, and the tile's two Unknown reasons (`declares no seam` versus
`the mirror holds only part of this issue`) are two different sentences. The row keeps both, plus the
pull request's own two absences — `no pull request` (asked, there is none) and `pull request unknown`
(the question could not be put), which `WorktreePullRequest`'s doc says is the whole point of that
shape.

The reason sentence for an unreachable pull request comes from `PullRequestView.tsx`'s existing
mapper, exported rather than copied — a second table of six reasons is a second table to drift.

### 5. The aggregate verdict is deleted, not softened

`verdict()` and `gate.ready|notYet|unchecked|no` go. Nothing replaces them with a milder sentence:
what the header says about readiness is `N review steps left`, which counts the person's own ticks.
This is the requirement rewrite made real, and it is why the `merge-gate` delta is MODIFIED rather
than a new capability — the shipped behaviour that spec describes stops being true.

### 6. The seam left for the eight tickets behind this one

`FolderDetailScreen.tsx` mounts exactly one new thing:

```tsx
{shown === "change" ? (
  <ChangeTab
    detail={detail} worktree={scope} changed={…} changedPaths={…}
    runsHere={…} running={openTerminal.isPending} onRunChecks={…}
  />
) : null}
```

Everything else lands **inside `ChangeTab.tsx`**, which is written so each later ticket edits one
line that already exists:

- **#291–#295**: `ChangeTab.tsx` already declares all five steps and already renders five separate
  one-line panel slots, one per step id. Each ticket replaces *its own* line with its own component.
  Five tickets, five different lines, zero conflicts — which is why the placeholders are five lines
  rather than one loop.
- **#290**: one function, `stepsDone()`, is the single source of "which steps are done". The tab
  counter and the footer both read it. #290 replaces its body with its query.
- **#296**: the header has one actions slot, already holding `Re-run checks`. `Merge change` is one
  more child of it.

**Alternative rejected:** a `steps` table with a `panel` field, which five concurrent tickets would
all edit in adjacent lines of one array.

### 7. `IssueTab.tsx`, `DiffTab.tsx` and `CommandsTab.tsx` are not opened

The ticket says their content moves in their own tickets. `DiffTab` and `CommandsTab` keep their
render sites, now reached from the checkout. `IssueTab` loses its render site and becomes
unreferenced — Rollup drops it from the bundle, and #291–#295 are where its content comes back. It is
left on disk, unedited, so its own ticket owns the move.

## Risks / Trade-offs

- **An absence assertion about a deleted tab passes for free while the screen is loading.** → The
  screen's fixed strip is computed from `tabsFor`, which is pure and synchronous; the check for this
  is to name what the strip *does* draw in the same breath as what it does not, and the epic's rule
  is that every absence gets a positive anchor from the same render.
- **`IssueTab` becomes dead code for one ticket's duration.** → Reported as a debt with an owner
  rather than deleted, because deleting it would be moving another ticket's file.
- **`?tab=issue` links from before this change stop resolving.** → `present()` already handles a tab
  the scope does not have by falling to `home`, which is now `change` — the same correction it
  performs for a worktree that has gone.
- **Rescoping `diff` moves `?tab=diff` on a worktree to `change`.** → `A_folders_review_tab_fits_a_phone`
  opens `?tab=diff` on the *seeded folder*, which is a checkout, so the tab is still there.
- **Removing the disclosure removes a control.** → It disclosed the five tiles, which are gone. The
  narrow-width behaviour of what replaces them is #297's, stated in this change rather than assumed.

## Migration Plan

None. No schema, no wire contract, no persisted value. The change is a frontend render and two spec
deltas; reverting it is reverting the commits.

## Open Questions

- Whether the mono row should elide at narrow widths or wrap. Wrapping here; **#297 owns the
  measurement** and this change deliberately does not pre-empt it.
- Whether `Re-run checks` belongs in the header at all once #293's Tests step exists. Kept here so
  nothing is withdrawn before its replacement lands; #293 may move it.
