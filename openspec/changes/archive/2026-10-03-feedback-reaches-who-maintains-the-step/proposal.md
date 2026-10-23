## Why

A fault in a harness step — a command whose instruction did not match what was there — was pushed as
a `degraded`/`failed` note into the `aio:state` comment of whichever issue was being worked. That
issue is about something else, it closes, and the note surfaced only if somebody wrote that change's
retro. The same broken instruction met in ten issues was ten notes nobody connected.

Expo's skills solve the same problem the other way round: each skill ends with *Submitting Feedback*,
a command that sends the fault to whoever maintains the skill, and a separate eval-candidate path when
an agent failed repeatedly or the user took over.

## What Changes

- **`.harness/scripts/feedback --about <step> --subject <instruction> --note <fact> [--issue <n>] [--eval]`**:
  one issue per step and subject, titled `[<step>] <subject>` and labelled `harness-feedback`; the same
  title gets a comment instead of a second issue. `--eval` adds `eval-candidate` and asks for the test
  or spec scenario that would have caught it. `--issue` names the issue being worked and records a
  pointer in its `aio:state` comment, so the retro still sees it.
- **Destination**: `feedback.repository` in `.harness/config.json`, else this repository — an adopted
  repository can point at the harness's upstream on purpose.
- **`push-session-report` is cost only**: `--status` and `--note` are refused with a pointer to
  `feedback`. A red gate is the change's failure; it already halts and holds.
- Every `/aio:*` command that may write to GitHub, and every harness-owned skill, ends with
  *Submitting feedback*. `status` (read-only) and `candidate` (forbidden from touching issues) do not.
- `collect-usage` lists friction from the feedback pointers; phases pushed before keep their notes.
- Also in this branch: the three changes merged in #415 are archived, which #415 skipped.

## Capabilities

### Modified Capabilities

- `retro-log-telemetry`: friction goes to the step's maintainer; the phase record carries cost only.

## Impact

- `.harness/scripts/feedback` (new, shipped), `push-session-report` script and skill,
  `issue-state.mjs` totals, `collect-usage`, `retro-entry`, six skills and seven commands,
  `CONTRIBUTING.md`, `AGENTS.md`.
