# retro-log-telemetry Specification

## Purpose
TBD - created by archiving change retro-log-gets-real-numbers. Update Purpose after archive.
## Requirements
### Requirement: A phase push is an explicit step in the command, not a runtime hook
Each `/aio:*` command that reaches a phase boundary — a draft PR opened, the gate passing or
failing, a hold applied, a retro entry written — SHALL invoke the `push-session-report` skill as a
named step in its own prompt text. This step SHALL live in the single command source under
`.ai/commands/aio/` that is symlinked into every runtime, so it runs the same way regardless of
which runtime is executing the command, and SHALL NOT depend on a runtime-specific hook mechanism
that only one runtime can fire.

#### Scenario: A phase boundary under one runtime
- **WHEN** `/aio:propose` reaches the step where it opens the draft PR, run under Claude Code
- **THEN** the command's own next step invokes `push-session-report` before it stops, per the
  step written into `.ai/commands/aio/propose.md`

#### Scenario: The same command under a different runtime
- **WHEN** the same command file is read and followed by a different runtime (for example
  opencode)
- **THEN** the push step is still there to follow, because it is text in the shared prompt and not
  a registration only Claude Code reads

### Requirement: The pushed cost figures come from the session's own transcript, not from prose
The `push-session-report` script SHALL derive cost, token and elapsed-time figures by reading the
current session's own transcript file mechanically rather than by asking the calling agent to state a
number. It SHALL count usage the way the app's `ClaudeUsageReader` does: rows carrying the same message
and request id are one message, merged by the **maximum** of each count, because the transcript
repeats a message's usage once per content block. Cost SHALL be priced from the same table as
`ModelPrices.cs`, resolving a model id by **longest prefix**; a session that used a model the table
does not know SHALL keep its tokens and report no cost. Where no transcript can be found (a different
runtime, or a session that predates this capability), the script SHALL record that explicitly rather
than a zero or an omitted field.

#### Scenario: A transcript is found
- **WHEN** the script runs at the end of a phase for a session whose transcript exists
- **THEN** the pushed record carries summed cost, token and elapsed-time figures read from that file

#### Scenario: A message split into blocks is one message
- **WHEN** the transcript holds two assistant rows with the same message and request id
- **THEN** their usage is counted once, at the larger of the two figures

#### Scenario: A dated or point-release model id is priced
- **WHEN** the transcript names `claude-opus-5-5`
- **THEN** it is priced at the `claude-opus-5` rate rather than reported as unknown

#### Scenario: No transcript exists for this runtime
- **WHEN** the calling command is being followed under a runtime that keeps no per-session
  transcript file
- **THEN** the pushed record says no cost figure was available for that push, and does not report a
  zero

### Requirement: A retro-log entry never reports a fabricated or silent figure
The step that produces a change's retro-log entry SHALL either report a real cost/time figure read
from the issue's `aio:state` comment — the sum of its per-session figures — or SHALL state explicitly
that no figure was ever pushed for that change. It SHALL NOT invent a number, and SHALL NOT omit the
figure without saying why — silence and fabrication are the two failures this capability exists to
close off, and both read, from the entry alone, as indistinguishable from a change that simply cost
nothing to measure.

#### Scenario: A change's phases all pushed cleanly
- **WHEN** every phase of a change pushed real figures into the issue's comment
- **THEN** the retro-log entry for that change carries cost and time as numbers, the sum of one
  figure per session

#### Scenario: One session pushed for several phases
- **WHEN** a change's phases come from two sessions, one of which pushed three times
- **THEN** the reported total is that session's latest figure plus the other session's, because each
  push replaced its session's figure rather than adding a line

#### Scenario: No push ever landed for this change
- **WHEN** the issue has no `aio:state` comment — every phase's push step was skipped, unpublished,
  or predates this capability
- **THEN** the retro-log entry says explicitly that no telemetry was pushed for this change, instead
  of a time/cost line

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

### Requirement: Every push lands in one comment on the issue, which holds facts and never the state
`push-session-report` SHALL require the issue number and SHALL write each push into **one comment on
that issue**, opened by `<!-- aio:state v1 -->` and holding a JSON block with the change name, the
pull request, one usage figure per session id and one entry per phase — its name, timestamp, session
id, status, note, and the reason when no usage figure was available. The script SHALL create the
comment on an issue's first push and edit that same comment on every later one; nothing else SHALL
write it.

A session's figure SHALL be **replaced** on each push, never added to, because every push re-reads
that session's transcript from its first message. Phase entries SHALL only be appended.

The comment SHALL NOT carry the issue's lifecycle state or anything a transition reads: the `status:*`
labels remain the state of record. Where the comment cannot be read or written, the script SHALL
print that it is unpublished, with the reason and the record it would have written, and SHALL NOT
fall back to a file in the checkout.

#### Scenario: The record survives the worktree
- **WHEN** `/aio:sync` has merged a change and removed its worktree, and `/aio:review` runs later
- **THEN** `collect-usage` reads every phase the change pushed from the issue's comment

#### Scenario: Two pushes from one session are counted once
- **WHEN** `/aio:implement` pushes at the gate and again when it marks the PR ready, from one session
- **THEN** the comment holds one figure for that session — the later one — and two phase entries

#### Scenario: Two sessions are summed
- **WHEN** a change's phases were pushed from two sessions
- **THEN** the total is the sum of the two sessions' figures

#### Scenario: GitHub cannot be reached
- **WHEN** `gh` is not signed in or the request fails
- **THEN** the push prints `UNPUBLISHED` with gh's reason and the record, exits non-zero, and writes
  no file

#### Scenario: A runtime that exposes no session id
- **WHEN** the script runs where no session id is available to read
- **THEN** the phase entry records the session id as absent and says no usage figure was available,
  and no session figure is written

### Requirement: A fault in a step reaches whoever maintains the step
A command or skill whose instruction was wrong — a missing prerequisite, an instruction that did not
match what was there, a workaround it forced — SHALL be reported with `.harness/aio/feedback`, and
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

