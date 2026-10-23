# Spec Delta

## Purpose
What a command's phase cost, and what went wrong while running it, pushed deterministically by an
explicit step in the command itself, and carried through to the retro-log entry that closes the
change it was spent on — or an honest statement that no figure was ever pushed for it.

## ADDED Requirements

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
The `push-session-report` script SHALL derive cost, token and active-time figures by reading the
current session's own transcript file mechanically — summing what is already recorded there —
rather than by asking the calling agent to state a number. Where no such transcript can be found
(a different runtime, or a session that predates this capability), the script SHALL record that
explicitly rather than a zero or an omitted field.

#### Scenario: A transcript is found
- **WHEN** the script runs at the end of a phase for a session whose transcript exists
- **THEN** the pushed record carries summed cost, token and active-time figures read from that file

#### Scenario: No transcript exists for this runtime
- **WHEN** the calling command is being followed under a runtime that keeps no per-session
  transcript file
- **THEN** the pushed record says no cost figure was available for that push, and does not report a
  zero

### Requirement: A step that hits friction pushes a note instead of a silent workaround
Where a command's own instructions turned out to be wrong, a prerequisite was missing, or the agent
had to invent a workaround to continue, it SHALL push a record with `--status degraded` or
`--status failed` and a one-line `--note` describing the fact — not an adjective — the moment it
happens, rather than only at the phase's normal end and rather than silently continuing as if
nothing were amiss. `--status ok` SHALL carry no note; a push supplying `--note` without `degraded`
or `failed` SHALL be refused, so a finding is never buried inside a record that reads as fine.

#### Scenario: A missing prerequisite is reported
- **WHEN** a command step depends on a CLI that is not installed and no file in the repository says
  how to install it
- **THEN** the agent pushes a `--status failed` record noting the missing prerequisite before or
  instead of silently working around it (for example, installing it from a guessed package name
  without recording that the guess was needed)

#### Scenario: An instruction that didn't match reality
- **WHEN** a step's written instruction produces a result the command did not expect, and the agent
  adapts to continue
- **THEN** it pushes a `--status degraded` record with a one-line note of what the instruction said
  versus what was true

#### Scenario: A clean phase carries no note
- **WHEN** a phase completes exactly as its command describes
- **THEN** its push uses `--status ok` and carries no `--note`

### Requirement: Every push is one line, tagged with the change, the phase and the session
`push-session-report` SHALL append exactly one JSON line per invocation to
`.telemetry/executions.jsonl`, carrying at minimum the change name, the phase name the calling
command supplied, the id of the session it read, a timestamp, the status, and either the
transcript-derived cost figures or the explicit "unavailable" marker. A line already written SHALL
NOT be edited or removed by a later push — `.telemetry/executions.jsonl` is append-only, the same
rule `docs/process/retro-log.md` already follows. Because the figures are summed from the
transcript's first message on every push, two lines from one session overlap rather than partition
that session, and the session id SHALL be what a reader uses to tell those apart.

#### Scenario: Two phases of the same change both push
- **WHEN** `/aio:propose` and later `/aio:implement` both push for change `<name>`
- **THEN** `.telemetry/executions.jsonl` carries two lines for `<name>`, one per phase, and neither
  overwrites the other

#### Scenario: Two pushes from the same session
- **WHEN** `/aio:implement` pushes at the gate and pushes again when it marks the PR ready, both
  from one session
- **THEN** both lines carry the same session id, and the later line's figures include the earlier
  line's rather than covering only the work done between them

#### Scenario: A runtime that exposes no session id
- **WHEN** the script runs where no session id is available to read
- **THEN** the line records the session id as absent rather than omitting the field or inventing one

### Requirement: A retro-log entry never reports a fabricated or silent figure
The step that produces a change's retro-log entry SHALL either report a real cost/time figure
derived from that change's lines in `.telemetry/executions.jsonl` — taking the largest figure per
session id and summing across sessions, never adding every line, which would count one session's
tokens once per push it made — or SHALL state explicitly that no figure was ever pushed for that
change. It SHALL NOT invent a number, and SHALL NOT omit the figure without saying why — silence and
fabrication are the two failures this capability exists to close off, and both read, from the entry
alone, as indistinguishable from a change that simply cost nothing to measure.

#### Scenario: A change's phases all pushed cleanly
- **WHEN** every phase of a change has a line in `.telemetry/executions.jsonl` carrying real figures
- **THEN** the retro-log entry for that change carries cost and time as numbers, one figure taken
  per session and those summed

#### Scenario: One session pushed for several phases
- **WHEN** a change's lines come from two sessions, one of which pushed three times
- **THEN** the reported total is that session's largest figure plus the other session's, not the sum
  of all four lines

#### Scenario: No push ever landed for this change
- **WHEN** a change has no lines in `.telemetry/executions.jsonl` at all — every phase's push step
  was skipped, failed, or predates this capability
- **THEN** the retro-log entry says explicitly that no telemetry was pushed for this change, instead
  of a time/cost line

### Requirement: A retro entry offers a change's friction notes instead of asking cold
Where a change has one or more `degraded`/`failed` notes in `.telemetry/executions.jsonl`, the step
that appends its retro-log entry SHALL surface those notes as candidate "what didn't work"
reflections before asking a human to supply one from memory. A human SHALL still be free to add,
edit or reject them — this SHALL NOT auto-write a reflection point without a human's confirmation.

#### Scenario: A change logged one piece of friction
- **WHEN** a change's `/aio:propose` phase pushed one `degraded` note about a missing prerequisite
- **THEN** `/aio:sync`'s retro-entry step shows that note as a candidate reflection instead of only
  asking an open question

#### Scenario: A change logged no friction
- **WHEN** a change has only `ok` pushes
- **THEN** the retro-entry step asks the human as it does today, with no candidate to offer
