## Why

Epic #447 closed with its nine sub-issues merged, and the visible half of the redesign never landed
(#471). Opening a PBI still answers the launch form: the detail is the inbox's right-hand panel,
the body first, the spine under it and a full **Run it** form beneath — offered even on a held PBI.
There is no screen a PBI owns, so the Proposal, Code review and Evidence readings the review
changes built live on a standalone `/pbis/pull-requests/…` page and on the worktree's Change tab,
each change having assumed the other would build the shell (#450 "no pide tabs", #454 "el
contenedor lo aporta #450"). The session tray was archived unbuilt (#453 task 10.3: *"Deferred:
the tray is not built"*), and the rail still offers to create a worktree in two places.

Measured on `origin/master` `93d7f02b` at 1480×817: pressing another PBI in the rail while `/pbis`
is open changes `?task=` and leaves the panel on the previous PBI; *Run a PBI here* goes to
`/tasks?folder=…`, whose redirect drops the query; the inbox's *Needs a person* lists `#185` twice.

## What Changes

- A PBI gets **its own screen** at `/pbis/:key?folder=<path>`, read by `usePbi(key, repository)` so
  a closed PBI opens too. Its heading carries the key and title, the Connector's position, the hold,
  and the PR when one is joined; its tabs are **Journey · Issue · Proposal · Code review ·
  Evidence**. Journey is the resting tab and the code-review position marks the Code review tab
  without replacing it.
- The heading offers **one action, the current step's**: release the hold when held; stop the run
  when one is live; otherwise the loop's next command (`PbiLoopCommandButton`). The full launch form
  stops being the content of the detail and sits folded at the foot of the Journey tab, absent when
  the PBI is held.
- The PR of an issue-shaped PBI is **joined in the browser** from the inbox it already holds: the
  pull-request PBI of the same repository whose body says `Closes #<n>` (GitHub's own closing
  keywords). No endpoint, no new field. A PR-shaped PBI is its own PR.
- **Proposal, Code review and Evidence** host the existing `PullRequestReview` component, opened on
  its matching step, with the shell heading left to the PBI. Before a PR exists they say where the
  PBI stands and offer no local diff.
- **Every entry opens that screen**: rail rows, Loop rows, History groups, the folder's Overview
  rows and the inbox's Table and Board. The inbox's Split pane draws the same component, so there is
  one reading.
- **Loop rows** name the folder by its name rather than its path, and carry the joined PR. The three
  bands stay, as `loop-view` specifies; the prototype's fourth band (*En revisión*) is not adopted.
- **The session strip moves to a fixed tray at the bottom** of the content column, labelled with the
  live count. Its `+` is removed: a run starts from the PBI's step, and a manual terminal starts
  from the folder.
- **The rail's folder row gets a `…` menu** with *Explore idea* and *Terminal here* and nothing else;
  the folder row's new-worktree icon and the Projects `+` menu's *Create worktree* are removed.
  "{agent} here" is dropped.
- The folder header loses *Run a PBI here* and the *N worktrees* count. `/tasks` keeps its query when
  it redirects to `/pbis`.
- *Needs a person* draws one row per PBI identity.
- `README.md` and `AGENTS.md` stop defining a worktree as the instance of the loop.

## Capabilities

### New Capabilities

_None._ Each behaviour has an existing home.

### Modified Capabilities

- `pbi-details`: the detail is a screen of its own with five tabs and one heading action; the clause
  that left the Review, Proposal and Evidence tabs to other changes is replaced; the PR of an
  issue-shaped PBI is joined from the inbox.
- `app-shell-and-navigation`: a `pbis/:key` route; the header no longer carries *Run a task here*;
  the rail offers no worktree creation and the folder row carries the `…` menu; the session tray is
  at the bottom; `/tasks` redirects keep their query.
- `loop-view`: a row names its folder and its PR, and opens the PBI screen.
- `run-a-session`: the live listing is drawn as a fixed bottom tray with no start affordance of its
  own.
- `review-a-remote-pull-request`: the review component opens on a requested step and leaves the
  heading to the PBI screen when it is a tab.
- `pbi-inbox`: rows open the PBI screen, and *Needs a person* is one row per PBI identity.

## Impact

Frontend only: `app/routes.tsx`, `app/AppShell.tsx`, `app/Sidebar.tsx`, `app/SessionsBar.tsx`,
`features/pbis/` (a new `PbiScreen.tsx`, `PbisScreen.tsx`, `PullRequestReview.tsx`,
`AttentionQueuePanel.tsx`), `features/loop/`, `features/history/HistoryScreen.tsx`,
`features/folders/OverviewTab.tsx`, `features/folders/FolderDetailScreen.tsx`, `shared/i18n/en.ts`,
and the end-to-end tests that assert the moved controls. No wire, endpoint, migration or
`World.cs` change. `README.md` and `AGENTS.md` change their worktree definition. The PR #457
prototype stays undecided; this change neither merges nor closes it.
