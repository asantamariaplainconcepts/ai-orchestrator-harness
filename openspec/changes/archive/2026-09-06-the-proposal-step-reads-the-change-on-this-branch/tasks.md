## 1. The read

- [x] 1.1 `GetOpenSpecChange` — one query slice beside the others, `GET
      /api/folders/{id:guid}/openspec-change?worktree=`, `[Requires(WorkspacePermissions.Read)]`,
      resolving the directory through `WorkingRoot.Resolve` so an unowned path is not-found.
- [x] 1.2 The unarchived changes: the immediate children of `openspec/changes/` that are not `archive`,
      read out of `IWorkspaceProbe.Files`, in name order. No new probe method.
      Directory entries are read as well as files, because `ls-files --directory` collapses a
      wholly-untracked change into one entry with nothing under it.
- [x] 1.3 The artifact list per change: the three conventional names first, present or not, then every
      other Markdown file found under it, ordinal. Presence is whether the file answered, not whether
      the tree listed it.
- [x] 1.4 The three readings of a Markdown file, each `null` where the file does not carry that kind of
      thing at all: lines, `- [x]`/`- [ ]` counts, and `### ` headers under a `## ADDED` / `## MODIFIED`
      section. Fenced blocks skipped, `^` anchored.

## 2. The screen

- [x] 2.1 `ProposalStep.tsx` — the panel: the change's path in mono, its artifacts with their meta, the
      chosen one rendered beside them through `Prose`, and `Open worktree in editor`.
- [x] 2.2 The validation line: the declared `openspec-validate` command's own line, and its recorded
      result through `CommandsTab`'s `CheckLine` — one rendering of one recorded fact rather than a
      second vocabulary for it. Four absences said as four sentences — no change here, no such
      artifact, no declared command, never run here.
- [x] 2.3 `ChangeTab.tsx` — **one line**, the `"proposal"` slot at 452, plus its import. The other four
      slots byte-identical.
- [x] 2.4 `contracts.ts`, `queries.ts` (`keys.openspecChange`, `useOpenSpecChange`) and a `proposal.*`
      block appended to the end of `en.ts`. None of the four reformatted.
- [x] 2.5 `.harness/commands.json` — one appended entry, `openspec-validate`, no `gate`.

## 3. Tests

- [x] 3.1 `OpenSpecChangeTests` in the Workspace functional suite, 14 tests, driving `StubProbe` with a
      written tree: one change, two changes, none at all, an archive-only directory, an unowned path,
      an untracked change, and two worktrees reading their own.
- [x] 3.2 Every absence assertion anchored to something present in the same read, and each proved by
      mutation. Four mutations, each killing exactly what it should:
      `Present: false` → `true` kills `An_artifact_the_change_does_not_carry_is_a_row_saying_so`;
      dropping `isDelta ?` kills `A_file_that_is_not_a_delta_reports_neither_count`;
      dropping `total > 0 ?` kills `An_artifact_with_no_checkboxes_reports_no_task_count`;
      `Names(listed).Take(0)` kills 13 of the 14 — including
      `A_directory_with_no_change_answers_an_empty_list_and_not_an_error`, which is the proof that its
      empty list is not vacuous. Reverted, and 14/14 green again.
- [x] 3.3 The counts checked against hand-counted real material, on the running app. #292's live
      worktree carries `a-file-in-the-diff-says-what-it-serves`: the screen said `proposal.md · 67
      lines`, `design.md · 170 lines`, `tasks.md · 75 lines · 14 of 17 done`,
      `specs/annotate-what-a-file-serves/spec.md · 177 lines · 8 added · 0 modified`. By hand: `wc -l`
      67/170/75/177, one `## ADDED` section and no `## MODIFIED`, eight `^### Requirement:`, fourteen
      `- [x]` and three `- [ ]`. Every number matches.

## 4. The gate and the evidence

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` —
      657 passed, 2 failed, 7 assemblies. The two are `PhoneTests.A_folders_overview_fits_a_phone` and
      `A_folders_review_tab_fits_a_phone`, ledger D10: this worktree's own directory name,
      `agent-aa175b61453b32f6f`, needs 192px in a 390px window. Not this change's, and not loosened.
      657 + 2 = 659 = the branch's 645 plus this change's 14.
- [x] 4.2 `openspec validate "the-proposal-step-reads-the-change-on-this-branch" --strict` — valid.
- [x] 4.3 Both themes, on the running app, and the bundle: `index-*.js` 322.92 kB → 329.11 kB raw,
      **77.43 → 78.76 kB gzip (+1.33 kB)**; first screen 178.91 → 180.20 kB gzip, inside the 200 kB
      budget. `ProseBody` unchanged at 47.54 kB gzip — the lazy boundary held.

## 5. What the running app said

- [x] 5.1 Observed on a real AppHost (`aspire start --isolated`, stopped with `aspire stop`), against
      this repository's own worktrees. **Check 1**: this branch's own change read back as
      `proposal.md · 130 lines`, `design.md · 188 lines`, `tasks.md · 44 lines · 0 of 15 done`,
      `specs/read-the-change-on-a-branch/spec.md · 134 lines · 5 added · 0 modified`.
      **Check 4**: `epic/288` answered *"This branch has written no change under openspec/changes."* —
      a sentence, with the directory named, and a 200.
      **Check 5**: a second unarchived change written into this directory showed both, neither marked
      as the one, and the second — untracked, so collapsed by `ls-files --directory` — still read its
      artifacts by name and said `design.md · not present`.
      **Check 6**: the declared command run through the product recorded `exit 0` on `71ed8a2` in
      `epic/288`, drawn as `exit 0 · on this commit`; with a malformed change in place the terminal
      exited **1** and the product recorded `exitCode: 1`, drawn in red. Same answer both ways.
- [x] 5.2 Two defects the code did not show. The artifact name was truncated from the left, copied from
      `DiffTab` — right for a file path, wrong for `specs/<capability>/spec.md`, which rendered as
      `…erves/spec.md`. It wraps now. And the reader opened on nothing; it opens on the first artifact
      that exists, the way `DiffTab` opens on the first file.
- [x] 5.3 The measured 404 confirmed again: `?worktree=<the checkout's own path>` is a 404, and the
      checkout is read by omitting the parameter. The panel passes a worktree row, so it never meets it.
