## 1. The PBI screen

- [x] 1.1 Add `features/pbis/pbiLink.ts`: `pbiPath(key, folderPath)` building `/pbis/<encoded key>?folder=<path>`, and `pullRequestFor(task, pbis, folders)` joining an issue to its PR by GitHub's closing keywords within one repository. The frontend has no unit runner; the join is exercised by the end-to-end test of 5.3.
      req: pbi-details/4, loop-view/1
      files: src/frontend/features/pbis/pbiLink.ts
- [x] 1.2 Give `PullRequestReview` an `initialStep` and an `embedded` prop: embedded skips `useScreenHeading` and the PR header's title; export it. The direct route keeps opening on Code with its heading.
      req: review-a-remote-pull-request/1
      files: src/frontend/features/pbis/PullRequestReview.tsx, src/frontend/app/heading.ts
- [x] 1.3 Add `features/pbis/PbiScreen.tsx`: the routed `PbiScreen` (reads `:key` and `?folder=`, resolves the repository with `folderFor`, takes the inbox row first and `usePbi` as the read of record, names absence, error and unattributed) and the `PbiView` body (heading, five tabs, Journey resting).
      req: pbi-details/2
      files: src/frontend/features/pbis/PbiScreen.tsx
- [x] 1.4 The heading: key and title, position chip, hold badge, PR chip or "no PR found", and the one action — `ReleaseHold`, *Stop run* through `useStopRun` for a live run joined by key and repository, or `PbiLoopCommandButton`.
      req: pbi-details/3
      files: src/frontend/features/pbis/PbiScreen.tsx
- [x] 1.5 The tabs: Journey (`PbiSpine` and, folded at its foot, the launch form moved out of `PbiDetailPanel`, absent when held); Issue (body, assignees, labels, thread); Proposal, Code review and Evidence (embedded `PullRequestReview` on `proposal`, `code`, `evidence`, or the "where the PBI stands" sentence before a PR). The code-review position marks the Code review tab.
      req: pbi-details/1, pbi-details/2
      files: src/frontend/features/pbis/PbiScreen.tsx, src/frontend/features/pbis/PbisScreen.tsx, src/frontend/features/folders/TabButton.tsx, src/frontend/features/pbis/PbiWalk.tsx
- [x] 1.6 Register `pbis/:key` in `routes.tsx`, lazy like `/loop`, after the static `pbis/pull-requests` routes; make the `/tasks` redirects keep `location.search`.
      req: app-shell-and-navigation/1
      files: src/frontend/app/routes.tsx

## 2. Every entry opens the screen

- [x] 2.1 Rail rows (`Sidebar.tsx`), Loop rows (`LoopScreen.tsx`) and History groups (`HistoryScreen.tsx`) navigate with `pbiPath`.
      req: loop-view/1, pbi-inbox/1
      files: src/frontend/app/Sidebar.tsx, src/frontend/features/loop/LoopScreen.tsx, src/frontend/features/history/HistoryScreen.tsx
- [x] 2.2 The folder Overview's rows navigate with `pbiPath` instead of opening `PbiDetailPane` beside the list.
      req: pbi-inbox/1
      files: src/frontend/features/folders/OverviewTab.tsx, src/frontend/features/folders/FolderDetailScreen.tsx, src/frontend/features/folders/PbiDetailPane.tsx
- [x] 2.3 The inbox: Table and Board rows navigate with `pbiPath`; the Split pane draws `PbiView` for its selection in place of `PbiDetailPanel`.
      req: pbi-inbox/1
      files: src/frontend/features/pbis/PbisScreen.tsx
- [x] 2.4 *Needs a person* (`AttentionQueuePanel.tsx`) groups rows by provider, host, repository, key and cause, keeping the newest, with the group's identity as React's key.
      req: pbi-inbox/2
      files: src/frontend/features/pbis/AttentionQueuePanel.tsx

## 3. Loop rows

- [x] 3.1 A Loop row names the folder by `folder.name` and carries the joined PR.
      req: loop-view/1
      files: src/frontend/features/loop/LoopScreen.tsx

## 4. Tray and rail

- [x] 4.1 Move `SessionsBar` below the content box in `AppShell`, add the *Sessions · N* label and remove its `+` menu and the explore dialog it hosted.
      req: run-a-session/1
      files: src/frontend/app/SessionsBar.tsx, src/frontend/app/AppShell.tsx
- [x] 4.2 The rail: drop *Create worktree* from the Projects `+` and the new-worktree icon from the folder row; add the folder row's `…` menu with *Explore idea* (the existing `ExploreDialog`) and *Terminal here* (`startSession` with `kind: "shell"`).
      req: app-shell-and-navigation/4
      files: src/frontend/app/Sidebar.tsx, src/frontend/app/AppShell.tsx, src/frontend/shared/ui/kit.tsx
- [x] 4.3 The folder header: remove *Run a PBI here* and the worktree count from the subtitle.
      req: app-shell-and-navigation/2
      files: src/frontend/app/AppShell.tsx, src/frontend/features/folders/FolderDetailScreen.tsx
- [x] 4.4 Rail A, chosen on the design canvas (#471): no tagline and no chips above the navigation; `PBIs` with its count, `Loop` with one "N need you" badge, `History` in place of `All projects`; a pause mark on a held row; the foot in one line. Copy nothing reads any more removed.
      req: app-shell-and-navigation/3, app-shell-and-navigation/5, app-shell-and-navigation/6, app-shell-and-navigation/7
      files: src/frontend/app/Sidebar.tsx, src/frontend/shared/i18n/en.ts

## 5. Copy, docs and tests

- [x] 5.1 Add the screen's copy to `en.ts` in one block for this change; remove keys nothing reads any more.
      files: src/frontend/shared/i18n/en.ts
- [x] 5.2 `README.md` and `AGENTS.md`: a worktree is the launcher's scratch for a run, not the instance of the loop; a PBI is what the person navigates.
      files: README.md, AGENTS.md
- [x] 5.3 Update the end-to-end tests that assert the moved controls, and add one that opens a PBI from the rail while `/pbis` is mounted and from Loop, and lands on the same screen.
      req: app-shell-and-navigation/1, pbi-details/2, pbi-details/3, pbi-details/4, loop-view/1
      files: src/tests/AiOrchestrator.EndToEndTests/PbiScreenTests.cs

## 6. Verification

- [x] 6.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`, and name what it counted.
- [x] 6.2 Run the product: open `#412` (held), an issue with a joined PR, a PR-shaped PBI and a closed PBI from History; check the heading action, the five tabs and the tray, in both themes and at 390 through the narrow set.
- [x] 6.3 Retake and publish the evidence batch (`.harness/scripts/evidence`), and keep the spec deltas true to what landed.
      files: openspec/changes/pbi-screen-and-session-tray/specs/app-shell-and-navigation/spec.md, openspec/changes/pbi-screen-and-session-tray/specs/run-a-session/spec.md
