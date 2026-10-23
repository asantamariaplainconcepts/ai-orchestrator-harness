## Context

The pieces exist and are not assembled. `PbiSpine` draws the journey (#450), `usePbi(key,
repository)` reads one PBI whatever its state (`pbi-inbox`), `PullRequestReview` reads a published
PR with its Proposal, Code, Checks, Evidence and Review steps (#454, #455), `PbiLoopCommandButton`
runs the loop's next command, `useStopRun` stops a run, `ReleaseHold` releases a hold, and the
sessions store already lists live sessions and hosts their panes over the content column (#453).
What is missing is the screen that composes them, and the entries that open it.

Every entry today builds `/pbis?task=<key>&folder=<path>` (`Sidebar.tsx:864`, `LoopScreen.tsx:80`,
`HistoryScreen.tsx:252`), which `PbisScreen` reads **once** as its initial selection — the reason a
rail press while `/pbis` is mounted changes the URL and not the panel.

## Goals / Non-Goals

**Goals:**

- One PBI screen, reached by the same identity from the rail, Loop, History, the folder Overview and
  the inbox.
- Its five tabs, with the review component reused rather than rebuilt.
- The tray at the bottom; the rail and the folder header without worktree creation and without the
  launch shortcut.
- The five defects #471 lists.

**Non-Goals:**

- Any wire, endpoint, migration or `World.cs` change.
- Chat as a new view: the tray opens the pane the sessions store already hosts, whose terminal and
  chat views `run-a-session` already governs.
- A fourth Loop band, or a new Loop read.
- Deciding PR #457.

## Decisions

**1. The route is `/pbis/:key?folder=<path>`.** The key travels in the path, percent-encoded (`#412`
is `%23412`), and the folder in the query, because that pair is the identity every entry already
builds and `folderFor` already resolves to a repository. React Router ranks the static
`pbis/pull-requests` above the dynamic segment, so the review deep link keeps winning. A `?pbi=`
on the folder route was the alternative; it keeps the PBI inside a folder screen whose tabs are the
disk's, which is the coupling the epic removes.

**2. The PBI is read by `usePbi(key, repository)`, the inbox row being a first answer.** The inbox
only holds open PBIs; a History group of a closed PBI has to open too. The screen takes the inbox row
when it has one, so the heading paints without waiting, and `usePbi` is the read of record.

**3. The PR of an issue is joined from the inbox, by GitHub's closing keywords.** The issue carries
no reference to its PR on the wire, and the inbox already holds every open PR of the repository as a
`GitHubPullRequest` PBI with its body. The join takes the PR PBI of the same repository (`folderFor`
of both resolves to the same folder) whose body matches `(close[sd]?|fix(e[sd])?|resolve[sd]?)
#<n>` as a word, case-insensitive. Several matches take the lowest PR number and the screen says
how many it found; none is "no PR yet", not an error. A PR-shaped PBI is its own PR. A merged PR has
left the open inbox, so a done PBI shows no PR chip — the journey's merge step already names it.

**4. Proposal, Code review and Evidence are one `PullRequestReview` each, opened on a step.** The
component gains `initialStep` and `embedded`; `embedded` skips `useScreenHeading` and the PR header's
title so the PBI owns the shell heading, and leaves the step switcher in place. Three instances
rather than one shared draft: a draft lives in the Code review tab, where review is written, and the
leave-guard (`useBlocker`) still covers it. The alternative — lifting the draft into the screen —
moves 300 lines out of a file this change does not need to restructure.

**5. One heading action, chosen in order: hold, live run, next command.** Held → `ReleaseHold`.
A live run of this PBI (`useLiveRuns` joined by key and repository) → *Stop run* through
`useStopRun`. Otherwise `PbiLoopCommandButton`, which already offers nothing for a state it does not
start. The launch form moves, whole, into a closed disclosure at the foot of the Journey tab, so
nothing a person could do yesterday is lost; it is not rendered for a held PBI.

**6. The inbox's Split pane draws the same screen body.** `PbiScreen` is split into the routed
screen and a `PbiView` body; the Split pane renders `PbiView` for its selection, and Table and Board
rows navigate to the route. That keeps the inbox's three layouts and makes "one reading" true by
construction rather than by convention.

**7. The tray is the same strip, below the outlet.** `SessionsBar` moves after the content box in
`AppShell`, gains a leading *Sessions · N* label and loses its `+`. The pane host stays where it is,
so an attached session still covers the content column and the tray stays visible under it.

**8. The folder row's `…` is a `MenuButton` with two choices.** *Explore idea* opens the existing
`ExploreDialog` for that folder; *Terminal here* calls `startSession({ folderId, kind: "shell" })`.
Both move out of `SessionsBar`, which keeps only the tabs.

**9. `/tasks` redirects through a component that keeps `location.search`.** A `<Navigate>` with a
fixed `to` drops it; the header's *Run a PBI here* is removed anyway, but a published `/tasks?…`
link deserves its query.

**10. *Needs a person* groups by provider, host, repository, key and cause.** Two failed runs of
`#185` are one blocked PBI. The newest run is the row's; React's key becomes the group's identity.

## Risks / Trade-offs

- [The PR join misses a PR whose body does not say `Closes #n`] → the screen says "no PR found for
  this PBI" and the Code review tab offers the pasted-address entry, which already exists.
- [Three review instances read the comparison three times] → React Query dedupes by key; the reads
  are one per PR.
- [End-to-end tests assert the old rail `+` and the strip's `+`] → updated in this change; the
  narrow set re-runs to catch layout drift at 390.
- [The Split pane is narrow for five tabs] → the tab row scrolls horizontally inside its own box, the
  way the folder's tab strip already does.
