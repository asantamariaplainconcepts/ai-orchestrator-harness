## 1. The checks

- [x] 1.1 `git mv` `.harness/tripwires/tests-touched.sh`, `old-tests-new-code.sh` and `out-of-seam.sh` to `.harness/aio/checks/`, source `_lib.sh` from their own folder, and keep what each one measures
- [x] 1.2 Rename `TRIPWIRE_BASE`, `TRIPWIRE_TRUNK`, `TRIPWIRE_DECLARED`, `TRIPWIRE_CRAP` and `TRIPWIRE_CRAP_MAX` to `CHECK_*` in `_lib.sh`, `crap.sh` and the three moved checks, and rewrite the comments that say "tripwire", "Tripped" or name `all.sh`
- [x] 1.3 Delete `.harness/tripwires/` (`all.sh`, `protected-paths.sh`)

## 2. The issue's Dónde cae, shared

- [x] 2.1 Move `file-overlap`'s section parser and `declared(body)` to `.harness/aio/lib/where-it-lands.js` (CommonJS), and have `file-overlap` require it with no change in behaviour
- [x] 2.2 Confirm `package.props` embeds the new file through `aio\lib\*`, and that the existing `file-overlap` tests stay green

## 3. quality's three rows

- [x] 3.1 In `.harness/aio/quality`, read the issue body with `gh issue view <issue> --json body --jq .body`, write the declared paths as `CHECK_DECLARED` patterns (each path, plus `<path>/*`), and pass `CHECK_BASE` instead of `TRIPWIRE_BASE` to every check
- [x] 3.2 Add the rows **Tests touched** (`touched.max`), **New code on old tests only** (`old-tests.max`) and **Files outside the issue's paths** (`outside.max`), each 0 by default: exit 0 `ok`, 1 `over` above the line with the findings, 2/3 or a check that cannot run `not measured` with its last line. Update the header comment to name eight checks
- [x] 3.3 Add `touched`, `old-tests` and `outside` to `.harness/package/quality.json` (commented, with their meaning) and to `.harness/quality.json`; update the `quality.json` paragraph of `.harness/README.md`

## 4. review-evidence goes

- [x] 4.1 Delete `CISteps/ReviewEvidenceStep.cs` and `CISteps/ReviewDashboard.cs`, remove `WithReviewEvidenceStep` and its comment from `AppHost.cs`, and remove `ReviewEvidence` from `WellKnownCIStepNames.cs`
- [x] 4.2 `MutationStep.cs` reads `CHECK_TRUNK`; rewrite the comments in `MutationStep.cs`, `CoverageStep.cs` and `StepCommand.cs` that name tripwires
- [x] 4.3 Remove the `GateTests` test that runs `ReviewEvidenceStep`, and its now unused helpers and `CopyFromRepository(".harness/tripwires")`; reword `AppFixture.cs`'s comment
- [x] 4.4 Remove the `/evidence/` block from `.gitignore`; reword `AGENTS.md`, `CONTRIBUTING.md`, `package.props` (both comments), `.harness/protected-paths` and `.harness/aio/diff-risk` so none names a tripwire or `review-evidence`

## 5. Tests and the screen

- [x] 5.1 `QualityScriptTests`: the fake `gh` answers `issue view` with a body whose *Dónde cae* names `src/`; add a fake `dotnet`; the existing tests assert eight rows
- [x] 5.2 Add a test for a change that edits only `src/Thing.cs`: Tests touched and New code on old tests only are `ok` with their reason
- [x] 5.3 Add a test for a change that edits `tests/ThingTests.cs` with the fake `dotnet` failing `ThingTests.Decides`: Tests touched names the file `over`, New code on old tests only names `ThingTests.Decides` `over`, and Files outside the issue's paths names the file `over`
- [x] 5.4 Add a test for an issue body with no *Dónde cae*: Files outside the issue's paths is `not measured` with the reason
- [x] 5.5 `TestsLostOrSkippedTests.cs` sets `CHECK_BASE`
- [x] 5.6 `ScreenCaptureTests.cs` seeds the eight rows in its quality comment, so the Review tab's capture shows them in the same shape
- [x] 5.7 Build and run the tests of the touched projects (InfrastructureTests, the AppHost build, the EndToEnd tests that touch the gate and the Review tab), and `git grep -i "tripwire\|review-evidence" -- . ':!openspec/archive' ':!openspec/changes' ':!docs/process/retro-log.md'` prints nothing

## 6. Version

- [x] 6.1 Raise `.harness/package/version` from 31 to 32
