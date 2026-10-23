## Why

Five scripts in `.harness/tripwires/` only run through `aspire do review-evidence`, and no loop state, agent command or workflow starts that step. Since #746 the loop measures every change with `quality` and posts the result on the issue. Three of those tripwires ask questions `quality` does not, so they move into it. The other two, and the step that ran them, are removed. Issue #758, part of epic #756.

## What Changes

- `tests-touched.sh`, `old-tests-new-code.sh` and `out-of-seam.sh` move to `.harness/aio/checks/` and follow the checks' contract. Each sources `_lib.sh` from its own folder.
- `.harness/aio/quality` gets three more rows: **Tests touched**, **New code on old tests only** and **Files outside the issue's paths**. Like the other rows, each has a line in `quality.json`, reports and blocks nothing. The third row reads the issue's *Dónde cae* the way `file-overlap` does, using a parser both scripts share in `.harness/aio/lib/`.
- `TRIPWIRE_BASE`, `TRIPWIRE_TRUNK`, `TRIPWIRE_DECLARED`, `TRIPWIRE_CRAP` and `TRIPWIRE_CRAP_MAX` become `CHECK_BASE`, `CHECK_TRUNK`, `CHECK_DECLARED`, `CHECK_CRAP` and `CHECK_CRAP_MAX` in the checks and everywhere that sets them: `quality`, `MutationStep.cs` and `TestsLostOrSkippedTests.cs`. **BREAKING** for anyone who sets the old names by hand.
- **BREAKING**: `.harness/tripwires/` is removed: `all.sh`, `protected-paths.sh` and the three that move. `aspire do review-evidence`, `ReviewEvidenceStep.cs`, `ReviewDashboard.cs`, `WellKnownCIStepNames.ReviewEvidence` and the `GateTests` test that runs the step go with it. The `/evidence/` entry in `.gitignore` goes too.
- Comments and prose that name the tripwires or `review-evidence` stop naming them: `.gitignore`, `AGENTS.md`, `CONTRIBUTING.md`, `package.props`, `.harness/protected-paths`, `.harness/aio/diff-risk`, `CoverageStep.cs`, `StepCommand.cs`, `AppFixture.cs` and `_lib.sh`.
- `.harness/package/version` goes from 31 to 32.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `change-quality`: `quality` measures eight checks instead of five. The three new ones come from `checks/`, and the issue's *Dónde cae* is the file-set the third one reads.
- `the-gate`: the `review-evidence` requirement is removed.
- `harness-package`: this repository's `.harness/` root no longer holds `tripwires/`, and `checks/` holds the five checks `quality` runs.

## Impact

- Scripts: `.harness/aio/quality`, `.harness/aio/file-overlap`, `.harness/aio/checks/*`, a new `.harness/aio/lib/where-it-lands.js`, and `.harness/aio/diff-risk` (comments only).
- AppHost: `AppHost.cs`, `CISteps/ReviewEvidenceStep.cs` and `ReviewDashboard.cs` (removed), `WellKnownCIStepNames.cs`, `MutationStep.cs`, `CoverageStep.cs` and `StepCommand.cs`.
- Tests: `QualityScriptTests.cs`, `TestsLostOrSkippedTests.cs`, `GateTests.cs`, `AppFixture.cs` and `ScreenCaptureTests.cs`, whose seeded quality comment now has eight rows.
- Package: `package/quality.json`, `package/version`, `package.props`, and `.harness/README.md`, whose `quality.json` paragraph lists the checks. The README is not in the issue's *Dónde cae*, so the outside row will name it. This repository's `.harness/quality.json`.
- The Review tab, the API and `QualityComments.cs` don't change. They already render whatever rows the comment carries.
