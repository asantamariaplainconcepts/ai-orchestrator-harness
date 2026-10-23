# loop-journal Specification

## Purpose
What the loop decides, a person can read: every decision the engine takes is one line of a journal with
its reason, and the engine's present (whether it runs, when it last looked, what is live) is readable
beside it. The Journey and the Loop screen show these and deduce nothing. Reverse-engineered from the
shipped code (`src/modules/Loop/AiOrchestrator.Modules.Loop/Journal/` and `Features/`), not negotiated
ahead of it.

## Requirements
### Requirement: Every decision is one line, written once
The engine SHALL write each decision to `loop_journal` as one line with its time, repository, PBI, the
state it was taken from, its kind (`Moved`, `Held`, `Released`, `Started` or `Waited`), the state it
leads to when there is one, and why. Every script a state runs SHALL be a `Ran` line from that state,
with no state it leads to and the why `<script>: <its why>`, a script that passes included (#593); a
script that waits SHALL leave only the `Waited` line, so a wait stays one line however many passes it
lasts. A person turning a folder's engine on or off SHALL be a `Switched`
line of its repository with an empty PBI, `on` or `off` as from and to, and the reason in words. Lines SHALL never be changed. A wait SHALL be written only when the
previous line for that PBI is not the same wait from the same state with the same reason, so a PBI that
waits for an hour is one line and not sixty. A script of a state SHALL be a `Started` line from that
state to itself with the why `running <script>`, written and saved before the script runs, unless the
PBI's lines since its last transition already hold that start, so a script that waits every pass is
announced once beside its one wait.

#### Scenario: A script's start is a line before its result
- **WHEN** a PBI in `verifying` runs `gate`
- **THEN** a `Started` line from `verifying` to `verifying` says `running gate` while the gate runs, and
  the line its result leads to comes after it

#### Scenario: A script that keeps waiting is announced once
- **WHEN** `file-overlap` waits on the same PBI for three passes
- **THEN** the journal holds one `Started` line `running file-overlap` and one `Waited` line for it

#### Scenario: A hold is journalled with its reason and where it leads
- **WHEN** `proposal-review`'s score answers `high`
- **THEN** a `Held` line from `proposal-review` to `in-progress` carries the hold's reason and the score's why

#### Scenario: A script that passes is a line
- **WHEN** `verifying` runs `gate`, which answers green, and `evidence`, which answers that the diff does
  not touch `src/frontend/`
- **THEN** two `Ran` lines from `verifying` carry `gate: <its why>` and `evidence: <its why>`, before the
  `Moved` line to `code-review`

### Requirement: Every script's output is kept and its line names it
The engine SHALL write everything a script printed to a file of its own,
`<scripts directory>/<owner>/<name>/<pbi>/<script>-<utc time>.log`, where the scripts directory is
`Loop:ScriptsDirectory` or, unset, `scripts` beside the SQLite file next to Runs' `runs` (#638). The
script's `Ran` line SHALL carry the file's path as `output`, whatever the script answered; every other
line's `output` SHALL be null, and the why SHALL stay the script's own sentence.
`GET /api/loop/journal/{id}/output` SHALL answer the file of that line as `text/plain`; 404
`Journal.NotFound` for a line that does not exist, and 404 `Output.NotFound` for a line that ran no
script or whose file was removed. The Journey SHALL draw the link on the `Ran` line.

#### Scenario: A red gate can be read from the Journey
- **WHEN** `gate` answers red on PBI 7 after printing `✗ ci-test failed`
- **THEN** the `Ran` line `gate: <its why>` names a file under `owner/repo/7/` that holds every line
  `gate` printed, the `Held` line after it names none, and the Journey's `log` link on the `Ran` line
  opens `GET /api/loop/journal/{id}/output` with that text

#### Scenario: The file is gone
- **WHEN** the file a `Ran` line names was removed from disk
- **THEN** the line still reads as written, and its output route is 404 `Output.NotFound` saying the path was removed

#### Scenario: Turning a folder off is a line
- **WHEN** a person turns the engine off for `owner/repo`
- **THEN** a `Switched` line of `owner/repo` goes from `on` to `off`, saying its live runs finish and
  nothing new starts

#### Scenario: A repeated wait is one line
- **WHEN** a PBI waits on `WIP 4 of 4` for three passes
- **THEN** the journal holds one `Waited` line for it

### Requirement: The journey travels with the issue as one comment the engine edits
After saving a step that wrote a `Moved`, `Held` or `Released` line for a PBI, the engine SHALL write
that PBI's journey to its issue through the vendor (#696): one comment starting with
`<!-- aio:journey -->` and holding a table `When | State | Kind | To | Agent or script | Result | Why |
Took | Cost | Recording`, with one row per `Moved`, `Held` and `Released` line of the PBI and one per
finished run of it, the loop's and the ones a person opened (#697), oldest first. A transition's row SHALL name the scripts its state ran since the
previous transition, from the `Ran` lines a script wrote as `<script>: <why>`, and never from the engine's
own `Ran` line such as "the run failed (Stopped): …" (#710); a run's row SHALL have the kind `run`, or `manual` for one a person opened, its
state or `no state`, its start as when, its command, CLI and model, its outcome or `failed: <cause>`, its
reason, how long it took, its cost when known, and the path of its recording on `aio/evidence` when its
session wrote one. A cell SHALL be one line with its pipes escaped and a terminal's colour codes left
out, which GitHub would give back as the characters `^[` (#710). No row
SHALL be written for a `Started`, `Waited`, `Ran` or `Switched` line. The comment SHALL be created at
the PBI's first transition or the first end of one of its runs, and edited at every later one, found by
its marker among the issue's comments, so an issue keeps one journey comment however many transitions and runs it has. The rows SHALL
be built from this instance's journal and runs. A comment reads back as written when the two differ only by what GitHub
changes when it saves (#710): `\r\n` for `\n`, spaces at the end of a line, and blank lines at either
end. A comment the vendor does not write or keep, or a vendor
that cannot edit one, SHALL NOT undo or delay the transition, which is saved first: it SHALL be a `Waited`
line from the state the PBI left, "the journey comment was not written: <why>", and a person's own move
or release SHALL still answer its own line.

No comment SHALL be created or edited for a PBI whose folder has no `.aio/version`, read at the
moment of the write (#697), whether a transition or a run's end asked for it: a client's repository
opened as a folder gets no comment from the app, which a debug line in the log says, and no setting
changes it.

#### Scenario: Three transitions, one comment
- **WHEN** a PBI is held in `verifying` on a red gate, released by a person to `merge-review`, and moved
  to `retro` on a low score
- **THEN** its issue has one `aio:journey` comment, with the same id after the third transition as after
  the first, and three rows in that order: held from `verifying` by `gate, evidence`, released to
  `merge-review`, moved to `retro` by `diff-risk`

#### Scenario: A finished run is a row
- **WHEN** the run of `retro` ends with `next` and the engine moves the PBI to `merging`
- **THEN** the comment has a `run` row of `retro` with `/aio:review <n> · claude-code · <model>`, the
  result `next` and the run's reason, before the `moved` row to `merging`

#### Scenario: A run a person opened is a row when it ends
- **WHEN** a terminal a person opened on a PBI with no state ends after 1m 35s with a recording
- **THEN** Runs tells the loop, which writes the PBI's comment between two passes with a `manual` row of
  `no state`, `1m 35s` and `<pbi>/<run id>/session.cast`; a comment the vendor does not keep is a warning
  in the log, and the next transition writes it whole again

#### Scenario: A folder without the harness
- **WHEN** a run a person opened ends on a PBI of a folder with no `.aio/version`
- **THEN** its issue gets no `aio:journey` comment; once the folder has the file, the same end writes one

#### Scenario: GitHub gives the comment back with its own line endings
- **WHEN** the journey comment is edited and reads back with `\r\n` and spaces at the end of its lines
- **THEN** it is written, and no `Waited` line says otherwise

#### Scenario: The engine's own ran line names no script
- **WHEN** a PBI's `Ran` lines before a transition are "the run failed (Stopped): …", `claim: …` and
  `risk-score: …`
- **THEN** the transition's row names `claim, risk-score`

#### Scenario: The vendor refuses the comment
- **WHEN** GitHub refuses the journey comment of a PBI the engine moves to `retro`
- **THEN** the PBI is in `retro`, and its newest journal line is a `Waited` line from `merge-review` that
  says the journey comment was not written and why

### Requirement: The journal is read newest first
`GET /api/loop/journal?repository&pbi&limit` SHALL answer the lines newest first, 100 by default and
between 1 and 500, each as `{id, at, repository, pbi, from, kind, to, why, output}`. A PBI named without its
repository SHALL be refused as "A PBI is named with its repository."

#### Scenario: One PBI's journal
- **WHEN** the Journey asks for the journal of `owner/repo` and PBI 495
- **THEN** it gets only that PBI's lines, newest first

### Requirement: The engine's present is readable
`GET /api/loop/present` SHALL answer whether the engine is enabled (at least one folder is on and the
interval is above zero) and its interval as stored or configured, the last pass
(when, how many folders and PBIs it read, what it skipped, what it wrote, and its failure) or null since
the process started, when the next pass is due, the live loop runs with their last output and whether
they are stale, the scripts a pass is running now with their repository, PBI, state, script, since when
and whether they are stale (running longer than the threshold a silent run is held to, from
`runs_settings` or `Runs:StaleAfter`), each folder with its vendor, repository, its engine switch (`on`), WIP limit and the
problem that keeps it out, and the newest journal line. A folder without `.aio/loop.yaml` SHALL
carry the problem "No loop declared. Add .aio/loop.yaml to the repository."

#### Scenario: The strip after a restart
- **WHEN** the host has just started and no pass has run
- **THEN** the present's last pass is null and the Loop screen says "no pass since the server started"

#### Scenario: A running gate is in the present
- **WHEN** a pass is running `gate` on PBI 7 in `verifying`
- **THEN** `liveScripts` lists `owner/repo`, `7`, `verifying`, `gate` and when it started, and lists
  nothing once the gate has answered

#### Scenario: A switch changes the next read
- **WHEN** a person turns `owner/repo` on through `PUT /api/loop/engine`
- **THEN** the next `GET /api/loop/present` lists `owner/repo` with `on` true, `enabled` true, and the
  `switched` line as the newest

### Requirement: A repository's loop is readable as it was declared
`GET /api/loop/definition?repository` SHALL answer the folder's loop as read: its name, agents, states
with their plan and transitions, and undeclared states, or the problem that made it unreadable. A
repository no folder holds SHALL be 404 `Loop.NoFolder`.

#### Scenario: The Journey draws the loop's states
- **WHEN** the PBI screen asks for the definition of its repository
- **THEN** it gets the declared states in order, which the Journey lists top to bottom
