## 1. The server reads which annotated file is a test

- [x] 1.1 `Features/Folders/TestClaims.cs` — the recognition rule (a `.test.`/`.spec.` infix, a base
      name beginning `test_`/`test-`, a base name ending `_test`/`_tests`/`-test`/`-tests` or the
      capital `Test`/`Tests`, or a `__tests__` segment) and the roll-up from requirement id to the
      annotated test paths claiming it. Every suffix carries a boundary, because a bare `test`
      suffix calls `latest.ts`, `contest.cs` and `protest.py` tests
      req: which-requirements-have-a-test/1, which-requirements-have-a-test/2
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/TestClaims.cs
- [x] 1.2 `UseCases/GetAnnotations.cs` — `AnnotatedFile.IsTest` and `Response.Requirements`, both
      appended. No new route, permission, error code or `World` seam
      req: which-requirements-have-a-test/1, which-requirements-have-a-test/2
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/GetAnnotations.cs

## 2. The screen

- [x] 2.1 `contracts.ts` — the two new fields, appended; nothing existing changes
      req: which-requirements-have-a-test/2
      files: src/frontend/shared/http/contracts.ts
- [x] 2.2 `en.ts` — a `testsStep.` block at the end. Never `gate.*`, which #289 deleted
      req: which-requirements-have-a-test/3, which-requirements-have-a-test/7
      files: src/frontend/shared/i18n/en.ts
- [x] 2.3 `features/folders/testClaims.ts` — the pure reading: the two numbers a check's own sentence
      has no room for, the two silences it draws as nothing, and the split of claimed requirements
      into covered and not. Beside `facts.ts`. The sentence itself is `CommandsTab.CheckLine`, reused
      the way #291's validation row reuses it, so the four `since` answers and the tone rule are
      stated once
      req: which-requirements-have-a-test/3, which-requirements-have-a-test/5, which-requirements-have-a-test/6
      files: src/frontend/features/folders/testClaims.ts
- [x] 2.4 `features/folders/TestsStep.tsx` — what this change added, what the declared checks last
      said, and the two silences
      req: which-requirements-have-a-test/3, which-requirements-have-a-test/4, which-requirements-have-a-test/5, which-requirements-have-a-test/7, which-requirements-have-a-test/8
      files: src/frontend/features/folders/TestsStep.tsx
- [x] 2.5 `ChangeTab.tsx` — **one slot**: the `"tests"` line. The five slots are not folded into a
      table. Being the last of the five, this also removes `Todo`, whose own comment says it goes
      with the last slot — a conflict-free rebase over #291 and #295 left it unread and `tsc`
      refused it
      req: which-requirements-have-a-test/8
      files: src/frontend/features/folders/ChangeTab.tsx

## 3. Proof

- [x] 3.1 `src/tests/.../TestCoverageTests.cs` — the recognition rule over four ecosystems and its
      three lookalikes, a requirement with a test, a requirement with none, an annotated test that
      left the diff, a change that annotated nothing, and one test named under two tasks. Six tests;
      Workspace 108 to 114
      req: which-requirements-have-a-test/1, which-requirements-have-a-test/2
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/TestCoverageTests.cs
- [x] 3.2 Every absence assertion has a positive anchor from the same answer, then a mutation proof.
      **Five mutations run, five caught, none survived**: `IsTest` always true (4 of 6 failed), the
      suffix boundary dropped so a bare `test` matches (1), the roll-up skipping the requirements no
      test claims (1), the roll-up taking only the diff's matched claims (1), and #292's closed-task
      check forced open (1). Each reverted to an empty diff and re-run green
- [x] 3.3 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green apart from ledger D10, re-run after the rebase onto `ff54eef`. **706 tests**, Workspace
      122 → **128** and no other assembly moved: Infrastructure 315, Tasks 108, Workflows 66, Runs
      62, Architecture 18, EndToEnd 7 of 9. The two EndToEnd failures are D10 —
      `"agent-a00483df6c62b8972" needs 203px and has 172px in a 390px window` — and were not
      touched. Runs 62/62, so D9's flake did not appear
- [x] 3.4 Run the app and observe the checks. **Observed on a running AppHost against this
      worktree**, and it paid twice. The panel read *1 test annotated · 2 of 8 requirements the
      tasks claim have a test*, with the six uncovered ids listed — honestly two, because six of the
      eight requirements are frontend and this repository has no frontend test runner (D19). The
      three check rows drew as *Build exit 0 · on this commit · 8s · 1m ago* (green), *Frontend
      gates not run here* (muted), *Unit + functional exit 1 · on this commit · 64s · 23s ago*
      (red), and `0 of 5 steps done` throughout — the machine marked nothing. **Two findings:** the
      plural read *11 test annotated* because `tCount` prepends the count and the string carried one
      too (fixed); and `lastCheck.summary` is `› exit N`, the line `ProcessCommandRunner` prints
      itself, so the ticket's `128 passed` is not obtainable from the record today — reported, not
      worked around
- [x] 3.5 Both themes, and the bundle growth in KB. Light and dark both read: the panel's only inks
      are `--foreground`, `--muted-foreground`, `--border`, `--card`, `--pc-warning-700` and
      `pc-badge--outline`, and the pass/fail ramp is inside `CheckLine` rather than restated here.
      Index chunk 345.68 → **351.79 kB** raw and 82.62 → **83.75 kB** gzip (+6.11 / +1.13);
      first screen 183.97 → **185.08 kB** gzip of the 200.00 budget. No new chunk, CSS byte-identical
