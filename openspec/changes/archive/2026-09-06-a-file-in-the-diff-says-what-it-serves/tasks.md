## 1. The prompt half

- [x] 1.1 `.ai/commands/aio/implement.md` — the annotation instruction on the step that closes a
      task: `req:` and `files:` continuation lines, closed tasks only, the id shape, and the sentence
      that a task nobody can attribute is closed without a `req:` line rather than with a guess.
      Single source; the three runtimes read it through the links that already exist
      req: annotate-what-a-file-serves/1, annotate-what-a-file-serves/2
      files: .ai/commands/aio/implement.md
- [x] 1.2 Verify the three runtimes resolve to that one file, and report — not fix — anything broken
      beside it. Nothing moved, so nothing is claimed: `.claude/commands` and `.opencode/command`
      are directory symlinks to `.ai/commands` and `.github/prompts/aio-implement.prompt.md` is a
      file symlink to the same file, all three resolving to one inode

## 2. The server reads it back

- [x] 2.1 `UseCases/GetAnnotations.cs` — `GET /api/folders/{id:guid}/annotations`,
      `[Requires(WorkspacePermissions.Read)]`, `WorkingRoot.Resolve`, the task lists found through
      `IWorkspaceProbe.Files`, read with `Contents`, matched against `Diff`. No new permission, no
      new error code, no new `World` seam
      req: annotate-what-a-file-serves/3, annotate-what-a-file-serves/5
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/GetAnnotations.cs
- [x] 2.2 The parse: closed tasks only, `req:`/`files:` continuation lines, repeats concatenate, the
      annotation ends at the next task or heading
      req: annotate-what-a-file-serves/1, annotate-what-a-file-serves/3
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/TaskAnnotations.cs
- [x] 2.3 The match: `seam.ts`'s suffix rule, restated; unmatched annotated paths answered rather
      than dropped; `truncated` when a task list was cut by the read

## 3. The screen

- [x] 3.1 `contracts.ts` — the annotations shape, appended; nothing existing changes
      req: annotate-what-a-file-serves/3, annotate-what-a-file-serves/6
      files: src/frontend/shared/http/contracts.ts
- [x] 3.2 `queries.ts` — `keys.annotations` and `useAnnotations`
      req: annotate-what-a-file-serves/3
      files: src/frontend/shared/http/queries.ts
- [x] 3.3 `en.ts` — a `codeStep.*` block at the end. Never `gate.*`, which #289 deleted
      req: annotate-what-a-file-serves/7, annotate-what-a-file-serves/8
      files: src/frontend/shared/i18n/en.ts
- [x] 3.4 `features/folders/annotations.tsx` — the short label a row carries, the map both surfaces
      read, and the one `Claim` badge both draw. Beside `seam.ts`, whose vocabulary it echoes. A
      `.tsx` rather than the planned `.ts`, because one shared badge beats two that drift
      req: annotate-what-a-file-serves/8
      files: src/frontend/features/folders/annotations.tsx
- [x] 3.5 `features/folders/CodeStep.tsx` — the file list, first row open with its patch, the rest
      behind `Show N more files`, the count of what was labelled, the stale annotated paths
      req: annotate-what-a-file-serves/4, annotate-what-a-file-serves/6, annotate-what-a-file-serves/7, annotate-what-a-file-serves/8
      files: src/frontend/features/folders/CodeStep.tsx, src/frontend/features/folders/PatchView.tsx
- [x] 3.6 `ChangeTab.tsx` — **one line**: the `"code"` slot. The five slots are not folded into a table
      req: annotate-what-a-file-serves/7
      files: src/frontend/features/folders/ChangeTab.tsx
- [x] 3.7 `DiffTab.tsx` — the file row gains the label, and the checkout path still renders. Its
      patch pane now calls the `PatchView` the Code step draws, rather than a second copy of the
      same forty lines of markup — the run screen's copy of this tab already taught that lesson
      req: annotate-what-a-file-serves/8
      files: src/frontend/features/folders/DiffTab.tsx

## 4. Proof

- [x] 4.1 A functional test file for the endpoint: a claimed file, one file under two tasks, an open
      task ignored, an unmatched annotated path reported, no annotations at all, no change at all, a
      cut task list, and the not-this-folder's-path refusal
      req: annotate-what-a-file-serves/3, annotate-what-a-file-serves/4, annotate-what-a-file-serves/5, annotate-what-a-file-serves/6
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/AnnotationTests.cs, src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/WorkspaceFixture.cs
- [x] 4.2 Every absence assertion gets a positive anchor from the same read, then is proved by
      mutation — force the branch, require the absence assertions to fail, revert, confirm an empty
      diff, re-run green. Six mutations run; two survived and both were real — a dead `archive`
      name check that the depth rule already covered, and a heading fixture that could not observe
      the rule it was for. Both fixed, then re-mutated to failing
- [x] 4.3 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green, with Workspace's count grown and no other assembly moved. Workspace 95 to 108;
      Architecture 18, Infrastructure 303, Tasks 101, Workflows 57, Runs 62 all unmoved. The two
      EndToEnd failures are ledger D10 — this worktree's own directory name, `agent-a43c...`, needs
      204px in a 390px window — and were not touched
- [x] 4.4 Run the app and observe the checks: a real annotated `tasks.md` labels its files, an
      unannotated one draws the diff whole, and a 25-file diff opens as four rows and `Show 21 more
      files`. Say plainly if it could not be observed. Observed on a running AppHost against this
      worktree: this change's own `tasks.md` labelled **13 of 72** files, the `epic/288` worktree
      with no change in progress read *nothing in this diff says what it is for* and drew all 58
      files unlabelled behind `Show 54 more files` with the first open
- [x] 4.5 Both themes, and the bundle growth in KB. Review tab seen in dark and light, the Code
      step in light; the badge is `pc-badge--outline`, whose only two colours are `--border` and
      `--foreground` and are themed. Index chunk 322.92 to 327.87 kB raw, 77.43 to 78.63 kB gzip
      (+4.95 raw, +1.20 gzip); first-screen budget 178.91 to 180.07 kB gzip of 200.00. No new chunk,
      CSS unchanged
