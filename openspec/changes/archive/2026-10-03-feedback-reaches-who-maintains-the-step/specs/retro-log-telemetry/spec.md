## ADDED Requirements

### Requirement: A fault in a step reaches whoever maintains the step
A command or skill whose instruction was wrong — a missing prerequisite, an instruction that did not
match what was there, a workaround it forced — SHALL be reported with `.harness/scripts/feedback`, and
every `/aio:*` command allowed to write to GitHub and every harness-owned skill SHALL end with a
*Submitting feedback* section naming it. The script SHALL keep one open issue per step and subject,
titled `[<step>] <subject>` and labelled `harness-feedback`, adding a comment when that title is
already open rather than opening a second. `--eval` SHALL add `eval-candidate` and ask for the test or
spec scenario that would have caught the fault. The destination SHALL be `feedback.repository` in
`.harness/config.json`, or the working repository when unset. With `--issue`, the issue being worked
SHALL be named in the report and SHALL get a pointer in its `aio:state` comment. Where gh refuses, the
script SHALL say UNPUBLISHED with gh's reason and the report.

#### Scenario: The same fault found twice is one issue
- **WHEN** two runs report the same step and subject
- **THEN** there is one `harness-feedback` issue with both reports

#### Scenario: Repeated failure becomes a case to keep
- **WHEN** a report carries `--eval`
- **THEN** its issue carries `eval-candidate` and asks for the test or scenario that would have caught it

#### Scenario: An adopted repository reports upstream on purpose
- **WHEN** `feedback.repository` names another repository
- **THEN** the issue opens there, and the report names the worked issue with its repository

## MODIFIED Requirements

### Requirement: A retro entry offers a change's friction notes instead of asking cold
Where the issue's `aio:state` comment holds feedback pointers — the harness-feedback issues raised
while the change was worked — or, from before those existed, `degraded`/`failed` phase entries, the
step that appends the change's retro-log entry SHALL surface them as candidate "what didn't work"
reflections before asking a human to supply one from memory. A human SHALL still be free to add, edit
or reject them — this SHALL NOT auto-write a reflection point without a human's confirmation.

#### Scenario: A change logged one piece of friction
- **WHEN** a feedback report was raised with `--issue` while the change was worked
- **THEN** `/aio:sync`'s retro-entry step shows it, with its link, as a candidate reflection instead
  of only asking an open question

#### Scenario: A change logged no friction
- **WHEN** a change has no feedback pointers and no `degraded`/`failed` phases
- **THEN** the retro-entry step asks the human as it does today, with no candidate to offer

## REMOVED Requirements

### Requirement: A step that hits friction pushes a note instead of a silent workaround
**Reason**: the note landed on the worked issue, which is about something else, so the same fault met
in several issues was never seen as one.
**Migration**: `.harness/scripts/feedback`, described by "A fault in a step reaches whoever maintains
the step"; `push-session-report` refuses `--status` and `--note` and names it.
