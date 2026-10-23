# end-records Specification

## Purpose
A run ends with a command: the agent says its outcome with `aio done`, and the control plane reads that
record instead of guessing from the transcript. The record is untrusted input, so an agent that forgets
to call it, or writes something the schema refuses, fails its run with that cause rather than leaving
the PBI hanging. Reverse-engineered from the shipped code (`.aio/aio`,
`.aio/schemas/done.v1.json`, `src/modules/Runs/AiOrchestrator.Modules.Runs/Features/PbiRuns/EndRecords.cs`),
not negotiated ahead of it.

## Requirements
### Requirement: aio done writes one record and says whether it did
`.aio/aio done <outcome> [--reason <text>] [--problems <text>]...` SHALL write
`{version: 1, outcome, reason?, problems?, at}` atomically to `$AIO_DONE_FILE`, or to
`.aio/done.json` at the top of the git checkout when that is unset, overwriting what was there. It
SHALL answer one JSON line `{written, why}` and exit 0 when written, 1 when the file could not be
written, and 2 when the arguments are refused and nothing is written. An outcome SHALL be lower case,
digits and dashes; `blocked` SHALL require `--reason`.

#### Scenario: blocked without a reason
- **WHEN** an agent calls `aio done blocked`
- **THEN** nothing is written and the script exits 2 saying the reason is required

### Requirement: The record is validated against its schema
The control plane SHALL validate the record against `done.v1.json`: no other properties, `version` 1,
`outcome` matching `^[a-z][a-z0-9-]*$` up to 64 characters, `at` a date-time, a reason and each problem
between 1 and 4000 characters, and at most 100 problems. The script and the schema SHALL agree, and a
test SHALL fail when they do not.

#### Scenario: A hand-edited record with an extra field
- **WHEN** the end record carries a property the schema does not declare
- **THEN** the run fails with `InvalidEndRecord`

### Requirement: The record decides how the run ended
When a loop run's session ends, the record SHALL be read as follows: no file is `NoEndRecord` ("The
agent ended without calling aio done"); invalid JSON or a schema failure is `InvalidEndRecord`; a
record written before the run started is `NoEndRecord`, because it is an earlier run's; `blocked` is
the failure `Blocked` with the agent's reason; an outcome the state does not know is `UnknownOutcome`,
naming the outcomes it does; anything else is the outcome, with its reason and problems. A manual run's
record SHALL NOT be parsed: a manual run has no outcome.

#### Scenario: A leftover record from an earlier run
- **WHEN** the worktree still holds a `done.json` whose `at` is before this run started
- **THEN** the run ends with `NoEndRecord`, not with that record's outcome

#### Scenario: An outcome the state does not know
- **WHEN** the reviewer's run in `code-review` ends with `aio done ready`
- **THEN** the run fails with `UnknownOutcome`, naming `rejected` and `approved`
