## ADDED Requirements

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

## MODIFIED Requirements

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
Where the issue's `aio:state` comment holds one or more `degraded`/`failed` phase entries, the step
that appends the change's retro-log entry SHALL surface their notes as candidate "what didn't work"
reflections before asking a human to supply one from memory. A human SHALL still be free to add, edit
or reject them — this SHALL NOT auto-write a reflection point without a human's confirmation.

#### Scenario: A change logged one piece of friction
- **WHEN** a change's `/aio:propose` phase pushed one `degraded` note about a missing prerequisite
- **THEN** `/aio:sync`'s retro-entry step shows that note as a candidate reflection instead of only
  asking an open question

#### Scenario: A change logged no friction
- **WHEN** a change has only `ok` phase entries
- **THEN** the retro-entry step asks the human as it does today, with no candidate to offer

## REMOVED Requirements

### Requirement: Every push is one line, tagged with the change, the phase and the session
**Reason**: `.telemetry/executions.jsonl` lived in the worktree and was deleted with it at merge, so
`/aio:review` and the corpus found nothing to read.
**Migration**: The record is the issue's `aio:state` comment, described by "Every push lands in one
comment on the issue". Old files are no longer read; their figures were overcounted.
