## Why

The Checks fact counts how many declared commands passed and on which commit, and there it stops. A
25-file change with no new test and a green suite draws the same phrase as one with fifteen:
`3 checks passed on 6d63601`.

This repository has already paid for that. `AGENTS.md` writes it down: *"Every bug this project has
found in committed code was found by running the product; none by the suite."* A green suite is a
much smaller claim than the phrase makes it look, and nothing on the screen says how small.

The same file also tells the story of 118 tests reported as a full run when there were 132 — a count
that could not tell "the lint did not run" from "there is no lint".

Issue #293, epic #288, step 3 of five. It rests on #292's annotation, #290's marks and #289's rail.

## What Changes

- **The Tests step separates three things the Checks fact says as one**: what the declared checks
  last said here, what this change added, and what was never declared at all.

- **A test file is recognised by its name, and the rule is drawn beside the count.** `AnnotationTests.cs`,
  `folders.test.ts`, `foo_test.go`, `test_foo.py`. Measured on this repository: 67 files carry a
  `[Fact]` or a `[Theory]` and **all 67 are named `*Tests.cs`**, with no file so named carrying
  neither — the rule is exact here, and it is shown on screen so it can be judged where it is not.

- **Which requirement a test covers comes from #292's annotation**, read back through the endpoint
  that already answers it. Not from a runner's output: a runner per ecosystem would be a parser per
  ecosystem, and this product already decided that a declared command returns an exit code and
  nothing else.

- **The number the step exists for**: how many of the requirements this change's `tasks.md` claimed
  have a test claiming them too, and **the ones that do not are named**, one line each. `1 of 3` is
  a fact a reader acts on; `3 passed` is not.

- **The denominator is what was claimed, and it says so.** The requirements of the spec delta are not
  read here — a requirement nobody annotated at all does not appear, and the panel states that in
  its own words rather than presenting a claimed count as the change's count.

- **`not declared` becomes a state of its own**, in the muted ink, next to a second silence it is
  not: a folder that declared no checks and a check that has never run in this directory are
  different sentences, and neither is a tick.

- **A check that failed is red with its exit code**, a check whose pass was recorded on an older
  commit says so, and a check on a commit git no longer has says that instead — the same four
  answers `FolderCommand.lastCheck.since` already carries, drawn as four.

**Not** in this change: judging the coverage (no threshold, no percentage, no colour for
"under-tested" — #281 decided that and it is inherited), parsing any runner's output, running the
suite by itself, and marking the step — marking is the person's, and this panel writes nothing.

## Capabilities

### New Capabilities

- `which-requirements-have-a-test`: how a test file is recognised, how the annotation is read for
  which requirement it covers, what the step says about the requirements nothing tested, and how a
  declared check that failed, went stale, or was never declared at all is drawn.

### Modified Capabilities

None. The annotations query gains two additive fields and answers exactly as before without them;
the Code step ignores both. A working directory with no annotations and no declared checks draws
what it drew before, in a panel that used to be a placeholder naming this ticket.

## Impact

- `src/modules/Workspace/.../Features/Folders/TestClaims.cs` (new) — which annotated path is a test,
  and the roll-up of requirement to the tests claiming it.
- `src/modules/Workspace/.../Features/Folders/UseCases/GetAnnotations.cs` — `AnnotatedFile.IsTest`
  and `Response.Requirements`, both appended. No new route, no new query key, no new permission, no
  new error code, no new `World` seam, no migration.
- `src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/TestCoverageTests.cs` (new) — the
  recognition rule, the roll-up, the uncovered requirement, and the two empty answers.
- `src/frontend/shared/http/contracts.ts`, `shared/i18n/en.ts` — appended only; a `testsStep.` block.
- `src/frontend/features/folders/TestsStep.tsx` (new), `testClaims.ts` (new, the pure reading beside
  `facts.ts`), and the `"tests"` slot of `ChangeTab.tsx`. The five slots stay five slots. The check
  row's own sentence is `CommandsTab.CheckLine`, reused as #291 reuses it.
- `ChangeTab.tsx` also loses `Todo`, the placeholder panel: this is the last of the five slots, so it
  has no caller left and `tsc` refuses it. Its `todo` field and the five `change.step.*.todo` strings
  are dead with it and are reported rather than swept.
