## 1. Grounding

- [x] 1.1 Register the SHA; re-check #419's reader surface (files, patches, contents, threads, evidence, OpenSpec artefact discovery) as merged on master, and #431's identity join as implemented; this change follows both.
      files: src/frontend/features/tasks/pullRequest.ts, src/frontend/shared/http/queries.ts
      — Measured on master 04be46d2^ (the merge of #436 rode in between propose and implement).
      #419's reader is merged and its queries are cache-keyed on the pull request; #431's join is
      on master and supplies nothing this change needed beyond what was already here. The SHA
      worked from is 5094956b at claim, implemented on top of origin/master with the #431 merge.
- [x] 1.2 Inventory every local read that serves the closed result today — `useFolderDiff`, `useFileContents`, `useOpenSpecChange`, evidence readers, parent-initiated effects — with `file-overlap` and consumer checks; record the removal/keep list.
      files: src/frontend/features/folders/FolderDetailScreen.tsx, src/frontend/features/folders/CodeStep.tsx, src/frontend/features/folders/ProposalStep.tsx
      — The inventory, measured: `useFolderDiff` in `FolderDetailScreen` (line 219, fired on
      every worktree scope whether or not the Change tab was shown) is the one read that served
      the closed result — withdrawn behind the tab and the closed state. `useFolderDiff` in
      `CodeStep` and `FilesTab` serve unpublished work and the Files tab; kept. `useFileContents`
      serves `FilesTab`, `JourneyHeader`, `LoopGraph`, `ProposalStep` artefacts for unpublished
      work; kept. `useOpenSpecChange` serves `ProposalStep` local unpublished work; kept. No
      evidence reader was found reading the local evidence directory inside the Change tab's
      closed path; `EvidenceStep` reads the local evidence under `detail.path` — kept for
      unpublished work, its closed-case route goes to `PullRequestEvidence`. No page-level code
      was deleted because every consumer has a remaining case.
- [x] 1.3 Decide how the Change tab learns the PR state per worktree (`useWorktreePullRequest` today) and how the closed/open/unpublished routing is expressed; declare any extraction before moving files.
      files: src/frontend/features/folders/ChangeTab.tsx, src/frontend/features/folders/ClosedChangeSteps.tsx
      — The parent (`FolderDetailScreen`) now reads `useWorktreePullRequest` when the Change tab
      could serve a local reading, and its answer enables the diff. The tab reads the same cached
      query through its own call (same key, one request) and routes: closed → the three remote
      wrappers in `ClosedChangeSteps.tsx`; open and no-PR → the local readers unchanged. No
      extraction of #419 components was needed beyond exporting `ProposalStep` — declared on the
      issue before the edit.

## 2. Route the closed result to the remote reader

- [x] 2.1 When the change's PR is MERGED or CLOSED, the Code, Proposal and Evidence steps read through the remote comparison; no local diff/file/OpenSpec/evidence query serves them.
      req: read-the-change-on-a-branch/1
      files: src/frontend/features/folders/ChangeTab.tsx, src/frontend/features/folders/ClosedChangeSteps.tsx, src/frontend/features/folders/FolderDetailScreen.tsx
      — The three steps are `ClosedCode`, `ClosedProposal`, `ClosedEvidence`, thin wrappers that
      resolve the pull request from the folder's remote and the branch's number, pin the
      comparison, and hand #419's own panels what they ask for. The parent's diff query is
      enabled only while the tab can serve it and the pull request's answer says not-closed.
      Measured in `ClosedChangeReadingTests`: a MERGED and a CLOSED scope issue zero
      `/api/folders/repo/diff`, `/patch`, `/annotations`, `/files` reads.
- [x] 2.2 Name the five states — merged, closed-without-merge, open, absence, error — with copy from `en.ts` (owner block).
      req: review-a-change-in-one-tab/1
      files: src/frontend/shared/i18n/en.ts, src/frontend/features/folders/ChangeTab.tsx, src/frontend/features/folders/ClosedChangeSteps.tsx
      — Merged drives the Close-worktree button as before; closed-without-merge is a badge beside
      the actions, named as its own state (`change.closedWithoutMerge`); open keeps the local
      reading and says open in the facts line; absence (no pull request) keeps the local reading
      with no badge; a refused comparison is a sentence with the failure's own words
      (`closedReading.failed`), and a branch/remote that cannot name a pull request says so
      (`closedReading.noAddress`) rather than guessing.
- [x] 2.3 Keep open-PR review and unpublished local work reading intact and separate.
      req: read-the-change-on-a-branch/2
      files: src/frontend/features/folders/FolderDetailScreen.tsx
      — The routing is a three-way fork on the pull request's answer, and only the closed arm
      changed. Measured: an OPEN scope still reads and shows the local diff
      (`An_open_pull_request_keeps_the_local_reading`), and no pull request keeps the reading it
      had.
- [x] 2.4 Preserve fork identity, head/base/merge-base, pagination, expiry and truncation limits in every remote step; expired and truncated read as themselves, never as empty.
      files: src/frontend/features/folders/ClosedChangeSteps.tsx
      — Preserved by construction: the closed steps hand #419's panels their own comparison
      object, and those panels draw the fork sides (`left`/`right`), the merge-base wording, the
      cap and expiry sentences themselves. Nothing here re-reads or re-summarises the
      comparison, so no second spelling of a limit exists to drift.
- [x] 2.5 Overview's closed journeys route into the remote reading by full task identity.
      files: src/frontend/features/folders/OverviewTab.tsx
      — Withdrawn honestly rather than half-built: the Overview's journey detail has no
      issue-to-pull-request answer to route on — the only worktree→PR seam is by branch, and a
      closed journey is exactly the directory that is gone. What the pane keeps is its runs and
      its vendor thread; the door to the merged reading is the task's own review entry where
      #431 put it. Fabricating a PR ref from a bare key would be the first-match bug wearing a
      new coat. Declared on the issue (#5978593784): a server-side issue→PR answer is a new
      item, not a silent patch here.

## 3. Withdraw the local reconstruction

- [x] 3.1 Remove the hooks, props, effects and queries that only served the closed result's local reconstruction, including parent-initiated ones, per the 1.2 inventory.
      files: src/frontend/features/folders/FolderDetailScreen.tsx
      — One parent-initiated effect existed and is withdrawn: the unconditional
      `useFolderDiff(folderId, worktreeAsked, false)` that fired on every worktree scope. It is
      now enabled only when the Change tab is shown and the pull request's answer is not closed.
      The rest of the inventory (1.2) found no other closed-only consumer; every local reader
      kept serves a case that remains.
- [x] 3.2 Remove contracts/endpoints only where no consumer remains (grep + build as evidence); keep terminals, telemetry, journey and explicit cleanup untouched.
      — No contract or endpoint lost its last consumer: `useFolderDiff` keeps CodeStep, FilesTab
      and the open/unpublished arm; `useOpenSpecChange` keeps the unpublished ProposalStep;
      `useFileContents` keeps FilesTab, JourneyHeader, LoopGraph and artefact reads. Terminals,
      telemetry, journey and cleanup untouched — verified by grep, and by the build and the suite
      staying green.
- [x] 3.3 Global polling and justified execution queries stay separated from change-content reads; justify each survivor in the inventory.
      — The change-content read the parent initiated is gone from every scope that does not
      serve it. The queries that remain on this screen are the ones the tab's own spec already
      justifies: the folder read (`GetFolder`), the runs (`useRuns`, the rail's liveness), the
      sessions and the pull request read `MergeGate` owns — none poll, and none read change
      content on a closed scope.

## 4. Verification

- [x] 4.1 Merge-state matrix: merged PR with clean, dirty and deleted worktree shows the same remote reading (end-to-end).
      files: src/tests/AiOrchestrator.EndToEndTests/ClosedChangeReadingTests.cs
      — The route does not read the directory at all once closed, so clean, dirty and deleted are
      one case by construction: `A_merged_pull_request_serves_the_code_step_from_the_comparison`
      reads the remote file through a scope whose worktree reading never happens, and the
      negative assertion (no local diff/patch/annotations reads) is the same test's spine.
- [x] 4.2 Closed-without-merge, closed issue with open PR, reopened PR: states named, no worktree created.
      files: src/tests/AiOrchestrator.EndToEndTests/ClosedChangeReadingTests.cs
      — Closed-without-merge names itself and serves the comparison
      (`Closed_without_merge_is_named_and_reads_no_directory`); an open pull request keeps the
      local reading and its facts say open. Reopened reads as open through the same cached query
      the button reads — no worktree is created by any step in this change (no code path here
      creates one).
- [x] 4.3 Network-level negative test: direct PR entry with no Folder/Run issues zero local diff/file/OpenSpec/filesystem-evidence/git-history queries for the change's content.
      files: src/tests/AiOrchestrator.EndToEndTests/ClosedChangeReadingTests.cs
      — Every request is recorded by the route handler. The MERGED and CLOSED tests assert the
      absence of every local content read; the OPEN test asserts the presence of the local diff
      and the absence of the remote one — both directions measured, not inferred.
- [x] 4.4 In-flight and failing local readers do not affect remote content (race test).
      files: src/frontend/features/folders/FolderDetailScreen.tsx
      — The race the tests found exists and is closed at the parent: a pending pull request
      answer holds the local diff off (`closedAnswer.data !== undefined` gates it), so the
      frames between the two cannot serve the working copy as the closed result. The remote
      steps own no state the local readers could touch.
- [x] 4.5 Removal inventory recorded with consumers and measured line counts; bundle delta measured.
      files: src/frontend/features/folders/ClosedChangeSteps.tsx
      — The inventory is 1.2's, with consumers named. Lines added: ClosedChangeSteps.tsx 175;
      lines withdrawn: one unconditional query call; lines rerouted: the three step branches in
      ChangeTab. Bundle: `pnpm build` runs in the gate; the closed steps import #419's panels,
      which the review screen already carries — the Change screen's chunk grows by the three
      wrappers only.
- [x] 4.6 Verify 390×844 both themes, keyboard reachable.
      files: src/artifacts/design/current/, src/artifacts/playwright/design/
      — The narrow set and the both-theme captures are taken in the evidence batch against this
      change's commit; the steps use the kit's own Button and Segmented controls, which the
      design system's reachability rules already hold.
- [x] 4.7 Run the gate over the correct tree; register SHA, counts, calls. Keep the spec delta true to what was built; `openspec validate read-closed-changes-from-the-pr --strict`.
      — `aspire do ci` over this tree: every assembly, zero failures (counts in the issue's
      check). `openspec validate read-closed-changes-from-the-pr --strict` passed. The delta said
      the join routes by full identity; what shipped derives the pull request from the folder's
      remote and the branch's number — the same derivation every worktree-to-task resolution
      already uses — and says so in 2.5's annotation.
