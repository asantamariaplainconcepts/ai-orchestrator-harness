# loop-screen Specification

## Purpose
The Loop screen answers what waits for a person, what is moving, and what the engine is doing: the
engine's present at the top, one row per open PBI in three bands, and the engine's last decisions with
their reasons. It reads what the engine wrote, and its one write is each folder's engine switch. Reverse-engineered from the shipped
code (`src/frontend/features/loop/LoopScreen.tsx`, `loopBands.ts`), not negotiated ahead of it.

## Requirements
### Requirement: The strip is the engine now
The screen at `/loop` SHALL open with a strip that says, for each folder of the engine's present, its
engine switch under "Engine · N of M folders on"; whether the engine looks ("every N s · last X ago",
"no pass since the server started", or "Off. Nothing moves on its own." when no folder is on) with the
way to Settings for the interval and stale threshold; the runs live now with the WIP of each folder as
live against its limit; and the engine's last decision, a `switched` line among them. A folder that is
on but whose loop cannot be read SHALL be named with its problem, because the engine skips it.

#### Scenario: Every folder is off
- **WHEN** the host runs without `Loop:Enabled` and nobody turned a folder on
- **THEN** the strip says "Engine · 0 of N folders on" and "Off. Nothing moves on its own.", and names no
  configuration key or restart

### Requirement: A folder's engine is switched from the strip
Each folder in the strip SHALL have a switch (`role="switch"`, named "Engine for <folder>") that sends
`PUT /api/loop/engine` and then reads the present again, so what it shows is the next read, not the
press; it SHALL work from the keyboard and in both themes. A folder that is off and whose loop cannot be
read SHALL keep its switch focusable but refusing (`aria-disabled`), with the problem beside it as its
description; a folder that is on keeps its switch, so it can be turned off. A refused or failed switch
SHALL be said under the strip with the server's reason. These are the screen's only writes.

#### Scenario: On by keyboard, off by mouse
- **WHEN** a person focuses a folder's switch, presses Space, then clicks it
- **THEN** two PUTs go out, and after each the switch, the count and the last decision show the next read

#### Scenario: A folder without a loop
- **WHEN** a folder has no `.aio/loop.yaml`
- **THEN** its switch is off and refusing, says "No loop declared. Add .aio/loop.yaml to the
  repository.", and pressing it sends nothing

### Requirement: Every open PBI is in one of three bands
Each open PBI SHALL be one row, filtered by folder when one is chosen, in one band: "Needs you" when it
is held, with the hold's reason, or when its loop run is stale, with how long it has been silent, or
when a script the engine runs on it is stale;
"In motion" when a loop run or a session of it is live and writing, with how long ago it last wrote,
or when the engine is running one of its state's scripts, with "Running <script> · <time>";
"The rest" otherwise, with its next step from the loop, the
engine's latest wait for it, or that it is outside the loop. Each row SHALL say its folder, PBI and
state, and open the PBI's Journey.

#### Scenario: A held PBI comes first
- **WHEN** one PBI is held and another has a live run
- **THEN** the held one is under "Needs you" with its reason, and the other under "In motion"

#### Scenario: A running gate is in motion
- **WHEN** the engine has been running `gate` on a PBI for 3 minutes
- **THEN** its row is under "In motion" with "Running gate · 3m", and under "Needs you" once the gate
  runs past the stale threshold

#### Scenario: A stale run needs you until it writes
- **WHEN** a PBI's loop run has been silent past `Runs:StaleAfter`
- **THEN** its row is under "Needs you" with "stale, silent for <time>" and no pause, nothing is written
  at the vendor, and the row returns to "In motion" on the first poll after the run writes again

### Requirement: The last decisions are the journal, with their reasons
Below the bands, the screen SHALL list the engine's 20 latest journal lines: time, PBI, from which
state to which or that it held, started a run, waited or ran a script, and why. With no lines, it SHALL say the
engine's first pass writes there. A `ran` line that carries its output SHALL show a "log" link beside
its row, not inside it, to `GET /api/loop/journal/{id}/output` in a new tab, as the Journey does.

#### Scenario: A red gate's log
- **WHEN** the journal has the ran line of a gate that answered red, with the file its output went to
- **THEN** the line shows a "log" link named "Everything gate printed" to `/api/loop/journal/<id>/output`, and the row still opens the Journey

#### Scenario: A release by a person
- **WHEN** a person released a hold a minute ago
- **THEN** the list shows the line as released, from the held state to its target, with "a person released the hold"
