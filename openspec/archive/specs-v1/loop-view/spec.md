# loop-view Specification

## Purpose
TBD - created by archiving change loop-view-background-work. Update Purpose after archive.
## Requirements
### Requirement: One global screen answers what the loop is doing across every folder

The system SHALL give a person a single screen that answers "what is moving in the background,
across every folder" without opening a terminal or filling in an execution form. It SHALL be
reachable from the rail as `Loop`, between `PBIs` and `All projects`, on the route `/loop`, inside
the same shell and served the same way as the other routes — a client route asked for directly
SHALL come back as the application, and moving to it SHALL be the router rather than a page load.

The screen SHALL draw one row per PBI that is in the loop — a PBI the vendor answers as open in
`GET /api/pbis`. A row SHALL carry, at least: the folder it belongs to; the PBI's identity (its key
and title); its current position in the loop; its activity right now; and the next step or the
reason it is waiting. A folder or a worktree that carries no open PBI SHALL NOT be drawn as a row
on its own — the screen is a view of the work, not a list of the machines that do it.

The screen is a read side. It SHALL compose the existing reads — it SHALL NOT add a scheduler, a
watcher, a new endpoint, or a second store of the loop's state, and it SHALL NOT start, stop,
resume or authorize anything. What a row can reach (a terminal, a run's transcript, the PBI's
journey) is where the action happens; the screen navigates to those rather than acting.

#### Scenario: A deep link to `/loop` is served the application
- **WHEN** `/loop` is requested directly from the browser
- **THEN** the host returns the application and the client resolves the route

#### Scenario: Moving to Loop is not a page load
- **WHEN** a person moves from the inbox to Loop
- **THEN** the document is not re-fetched

#### Scenario: The screen is a read side
- **WHEN** the screen is open and a person reads a row
- **THEN** no run is started, resumed or provisioned by reading, and a row's opening action
  navigates to a destination that already exists

#### Scenario: A folder with no open PBI is not a row
- **WHEN** a folder is configured but carries no open PBI in `GET /api/pbis`
- **THEN** the screen draws no row for that folder

### Requirement: A row is the composition of three existing reads, joined by the PBI's identity

A row SHALL be one PBI, composed from the three reads it is built from — `GET /api/pbis` (the PBI's
position and its hold), `GET /api/runs?live=true` (the run in motion for it, if any), and
`GET /api/attention` (the block on it, if any) — and SHALL be joined **by the PBI's identity**, the
source and repository and key together, the same identity the PBI detail and the run's own walk
already join by. A PBI SHALL NOT be counted twice for the runs that worked it, or for a copy of its
work: several runs of one PBI are one row's activity, not several rows, and a run whose PBI cannot
be attributed to a repository by that identity SHALL NOT be attached to another PBI that happens to
carry the same number.

The row's **position** SHALL be the PBI's loop position as the Connector reads it — the `PbiStatus`
served by `GET /api/pbis` — not a position re-derived from a label on the screen. A position the
vendor could not map (`unknown`) SHALL be drawn as `unknown`, with the vendor's own reason, and
SHALL NOT be read as `backlog`. The row's **next step or reason for waiting** SHALL come from the
declared loop the checkout reads — the same `journeyFor` projection the header and the walk draw
from, reused rather than re-implemented — or, where the attention queue names a block on the PBI,
from that block's typed cause.

#### Scenario: Two repositories, one number, one row each
- **WHEN** two repositories in one scope each carry a PBI numbered 12, and only one has a run
- **THEN** the run is attached to the PBI of the repository it names, and the other PBI's row is
  not offered that run as its own

#### Scenario: Several runs of one PBI are one row's activity
- **WHEN** a PBI has one run that has not finished and two that have
- **THEN** the screen draws the PBI once, with the unfinished run its current activity and the
  finished runs its history, rather than three rows

#### Scenario: A position the vendor could not map is drawn unknown
- **WHEN** a PBI's position read is `unknown` with a reason
- **THEN** the row draws `unknown` with that reason, and does not read it as `backlog`

### Requirement: The screen orders attention required, then work in motion, then the rest

The screen SHALL draw its rows in three bands: **attention required** (a PBI the attention queue
names a block on — a hold, a failure, an unresolved dispatch, an inconclusive gate), then
**work in motion** (a PBI with a run that has not finished), then **the rest** (every other open
PBI within the visible set). The bands SHALL be ordered in that sequence so a person's eye lands on
what needs a person first.

A hold with **no live process** SHALL NOT be drawn in the work-in-motion band: a hold is a state
that waits on a person, not an activity, and drawing it "in motion" would read as a run that is
moving for no reason. A PBI that is held and also has a live run stays in the attention band — the
attention is the louder fact — and the run is still its activity.

#### Scenario: Attention required leads the screen
- **WHEN** one PBI has an unresolved gate and another has a live run and a third is waiting in the queue
- **THEN** the gated PBI is drawn first, the running PBI second, the waiting PBI after

#### Scenario: A hold without a process is not work in motion
- **WHEN** a PBI is held and has no run that has not finished
- **THEN** it is drawn in the attention-required band, and the work-in-motion band does not name it

#### Scenario: A held PBI with a live run stays in the attention band
- **WHEN** a PBI is held and also has a run that has not finished
- **THEN** it is drawn in the attention-required band, and its activity still names the live run

### Requirement: The folder filter narrows the rows, and the counters count the visible set

The screen SHALL let a person filter to one folder, and the counters SHALL count **the set of rows
drawn after the filter** — not the whole set — so that a filter that hides every row reads as the
visible set's count, not as a stale whole-set number. A row that leaves the visible set (because a
folder filter now excludes its folder) SHALL NOT still be counted. The screen SHALL NOT change the
`GET /api/pbis` contract: the server keeps answering over the whole set with the whole-set
`sourceCounts` and `kindCounts` it already answers, and the screen's visible-set counters are the
screen's own composition over the rows it draws.

#### Scenario: Filtering to a folder redraws the counters over the visible set
- **WHEN** a person filters the screen to one folder
- **THEN** the counters reflect the bands of the rows still drawn, and a row excluded by the filter
  is not counted

#### Scenario: The server's whole-set counts are left alone
- **WHEN** the screen filters to a folder
- **THEN** `GET /api/pbis` still answers with its whole-set `sourceCounts` and `kindCounts`,
  unchanged, and the screen does not ask it to filter by folder

### Requirement: Loading, empty, error and result are four named states, and a failed read is not a result

The screen SHALL name its state, and the four SHALL NOT collapse into one another: **loading**
while the reads are on their way; **empty** when every read answered and there is nothing in the
loop to show; **error** when a read failed, with the failure's own words; and the **result** — the
rows. A read that failed SHALL NOT be drawn as an empty result, and **zero PBIs SHALL NOT be shown
as a valid result when a read was not answered**: a screen that says "nothing running" while a read
was unreachable is a screen that lies calmly. The attention queue's own `unknownSources` — the
sources it could not read — SHALL be the screen's signal of a partial read, and a row that the
attention queue names from a source it could not read SHALL be drawn as `unknown` with that reason
rather than dropped as if the queue were empty.

#### Scenario: A partial read is not drawn as an empty result
- **WHEN** the attention queue answers but one of its sources could not be read
- **THEN** the screen names that source as unreadable and does not present the rows it did read as
  a complete, calm, empty queue

#### Scenario: A failed runs read is an error, not zero runs
- **WHEN** `GET /api/runs?live=true` fails while the screen is open
- **THEN** the screen names the failure and does not draw the work-in-motion band as confirmed empty

#### Scenario: A genuinely empty loop reads empty
- **WHEN** every read answers and there is no open PBI in the loop
- **THEN** the screen names that there is nothing, with none of the four states read as an error

### Requirement: Entering a PBI from Loop and from the inbox is the same journey

A row's link SHALL open **the same PBI journey** the inbox and the PBI detail open, so that the
same PBI read from two places carries the same identity, the same navigation, and the same context
of where its work stands. The destination a row points at SHALL be the PBI-keyed destination that
#450 fixes in the PBI detail — the journey addressed by the PBI's identity and reached from the
journey's own surface — and SHALL NOT be a worktree-keyed destination that names the directory the
run happened to work in: a worktree is a place, and a link to a PBI's journey is the PBI, not the
place one of its runs worked in.

This dependency SHALL be honoured at build time: the screen's row link SHALL open #450's destination
as #450 defines it, and the screen SHALL be opened only after #450 has landed, so the row points at
a destination that exists rather than one the screen invents.

#### Scenario: From Loop and from the inbox, the same journey
- **WHEN** a person opens a PBI's journey from the inbox and opens the same PBI's journey from a
  Loop row
- **THEN** both open the same journey, carrying the same PBI identity and the same navigation back

#### Scenario: A row points at the PBI, not at a worktree
- **WHEN** a row opens a PBI's journey
- **THEN** the destination is addressed by the PBI's identity (the journey #450 names), and is not
  a link to the working directory one of the PBI's runs used

### Requirement: A row names its folder and its PR, and opens the PBI screen

A row SHALL name the folder by the folder's name rather than its path, and SHALL carry the PR joined
to the PBI by the rule the PBI screen uses, when there is one. Opening a row SHALL navigate to the
PBI's own screen, `/pbis/:key?folder=<path>`, the same destination the rail, History and the inbox
open.

#### Scenario: The folder is named

- **WHEN** a row belongs to the folder registered as `ai-orchestator-new`
- **THEN** the row reads `ai-orchestator-new`, not the folder's absolute path

#### Scenario: A row opens the PBI screen

- **WHEN** a row is opened
- **THEN** the PBI screen for that key and folder is drawn, with the Journey tab selected

