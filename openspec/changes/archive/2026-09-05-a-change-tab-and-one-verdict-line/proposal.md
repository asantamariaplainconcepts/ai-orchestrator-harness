## Why

Approving a worktree is seven tabs and five cards. The strip draws `Issue · Runs · Files · Review ·
Checks · Telemetry · Chat`, and above it `MergeGate.tsx` mounts a verdict of five conditions — Base,
Merge, Checks, Working tree, Seam — each in its own tile with its own sentence underneath. Whoever
approves assembles the judgement from at least three places, and **none of the five cards says
whether the change works**.

The panel also carries the rule the canvas draws the opposite of: *"Every action SHALL stay available
at every verdict"* is written about a machine's opinion of five conditions, when what should block is
the person's own checklist.

Issue: https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/289 — `design`,
`enhancement`, `status:ready-for-proposal`, under epic
[#288](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/288). Blocked by
**#283**, merged at `17e4da9`: the mono line reads the issue's title and the Seam fact reads its
body, and with the mirror cut at 4,000 characters more than half of this repository's recent issues
would give a line that lies.

Measured on this branch at `17e4da9`, in `FolderDetailScreen.tsx`:

| | |
| --- | --- |
| fixed tabs in the table | **10** (the "seven" are the worktree filter of it) |
| the worktree strip today | `issue, runs, files, diff, commands, telemetry, chat` |
| the checkout strip today | `overview, tasks, runs, files, diff, commands, workflows, chat` |
| `diff` and `commands` scope | **`both`** — a checkout has a real diff and real checks |
| `homeFor(worktree)` | **`"issue"`** — a tab this change removes |
| the sixth fact the row wants | `useWorktreePullRequest`, called only in `IssueTab.tsx:74` |

## What Changes

- **One tab, `Change`, and the verdict in one line.** The worktree strip becomes
  `Change · Runs · Files · Chat · Telemetry` plus the session tabs. `Issue`, `Review` and `Checks`
  leave *the worktree strip*.
- **`Review` and `Checks` are not deleted — they are rescoped to `checkout`.** Both are `scope:
  "both"` today for reasons the table's own doc records: #18 added the diff for a checkout somebody
  edits by hand, and a check's result belongs to the directory it ran in (#147). Deleting the rows
  would break a view this change is not about.
- **The five verdict cards become one mono row** in the change header, beside the issue number and
  its title: `pull request #236 · draft · 25 files · +1034 −360 · 1 ahead, 0 behind · no conflicts ·
  3 checks green · seam all inside · tree clean`. **All five facts survive, and a sixth arrives** —
  the pull request, whose read moves out of `IssueTab.tsx` deliberately.
- **Nothing in the row is ever a tick.** Every fact has its own words for "not known", including the
  three distinct unknowns #283 established for the Seam (`could not be read` is not `declares no
  seam`). A fact with no source is stated as absent, never rounded to green.
- **The aggregate verdict goes.** `verdict()` and its four sentences — *Ready to merge*, *Not ready
  to merge*, *Nothing has checked this*, *Cannot merge yet* — are removed. What blocks stops being
  the machine's opinion over five conditions and becomes the person's own checklist, said as
  `N review steps left`.
- **A rail of five steps and a footer with progress.** Proposal, Code, Tests, Evidence, The app. In
  this change the rail and the footer **navigate and store nothing**; each panel is a placeholder
  naming the ticket that fills it.
- The tab carries a counter of the steps still to do. **The count's source is #290's**; here it is
  derived from one named function that today answers "none done", because nothing on this screen has
  ever been able to record one.
- **The `merge-gate` requirements are rewritten, not ignored** — the five-tile requirement and the
  never-disables-an-action requirement both.

Not breaking on the wire: no contract, endpoint, migration or `World` seam changes. The whole change
is the frontend and two spec deltas.

## Capabilities

### New Capabilities
- `review-a-change-in-one-tab`: a worktree's change is one tab with a header that states the facts,
  a rail of five ordered steps, a panel per step and a footer that counts them — replacing a strip
  where the question the screen exists for was answered by reading three tabs and five tiles.

### Modified Capabilities
- `merge-gate`: the five drawn conditions become one line of facts that judges nothing — one
  requirement MODIFIED and renamed. The requirement about the verdict's wording is **REMOVED and
  replaced by an ADDED one**, not modified: half of it described a machine judging whether a change
  may land, and that half is deleted rather than reworded; the half that survives — no action is
  disabled by a fact — is carried into the new requirement in the same words. Six requirements
  before, six after. The four that describe where the facts come from — position against the trunk,
  a check's result outliving its session, only a declared check counting, a worktree's task resolved
  from runs first — are untouched, because the sources are exactly what this change keeps.

## Impact

- `src/frontend/features/folders/FolderDetailScreen.tsx` — the `FixedTab` union's fixed arm,
  `fixedTabs`, `homeFor`, `fillsHeight`, the `MergeGate` render site, the `IssueTab` render site.
  The dynamic arm of `Tab` is left intact and contiguous for `change/264`.
- `src/frontend/features/folders/MergeGate.tsx` — the five tiles, the two headers, the disclosure and
  the aggregate verdict all leave; the six facts and one mono row arrive. `gateCommands` is untouched
  and stays exported.
- `src/frontend/features/folders/ChangeTab.tsx` — **new**: header, rail, panel, footer.
- `src/frontend/features/folders/TabButton.tsx` — one optional `tone` on the count; absent is today.
- `src/frontend/features/folders/PullRequestView.tsx` — its reason-to-key mapper is exported so the
  row does not write a second one.
- `src/frontend/shared/i18n/en.ts` — the whole `gate.*` block is replaced by a `change.*` block.
- `src/frontend/features/folders/{IssueTab,DiffTab,CommandsTab}.tsx` — **not touched**. Two stay
  mounted on the checkout; `IssueTab` stops being reachable and its content moves in #291–#295.
- `openspec/specs/merge-gate/spec.md` — two requirements, via the delta.

## Assumptions recorded

Written unattended, with nobody to ask. Each is a decision taken here and a thing a person may
overturn.

1. **The mono row carries six facts, not the five the ticket's example string shows.** That string —
   `pull request #236 · draft · 25 files · +1034 −360 · 1 ahead, 0 behind · no conflicts · tree
   clean` — silently drops Checks and Seam, while the sentence above it says *"the same five facts,
   one line, losing none"*. The sentence wins: Checks and Seam are in the row.
2. **`Re-run checks` stays.** The canvas draws no such button, and #293's Tests step is where it
   belongs — but withdrawing an action before its replacement exists is the irreversible direction.
   It sits in the change header's action slot, which is also where #296 appends `Merge change`.
3. **The disclosure and the narrow stacking leave with the tiles.** `tilesAbove = 560`,
   `useNarrowContainer` and the collapsed summary existed because five tiles did not fit at 390px. A
   row of text wraps. Narrow width is **#297's** and is explicitly not measured here.
4. **Rail state is derived from one function that answers "none done".** `stepsDone()` in
   `ChangeTab.tsx` is the single place #290 replaces with its query — the tab counter and the
   footer's `0 of 5 steps done` both read it, so there is one number and not two.
5. **The sidebar's `clean · N steps left` is not drawn here.** #290's own issue says the sidebar
   string and the tab counter come from it, and the sidebar is not in this ticket's Seam. Left as a
   debt addressed to #290.
6. **The `gate.*` i18n block is replaced rather than extended.** One prefix, `change.`, owns every
   string this screen says. `gate.*` disappears, so no later ticket can append into a block that no
   longer exists.
