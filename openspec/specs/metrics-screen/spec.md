# metrics-screen Specification

## Purpose
The Metrics screen of a folder answers how the loop works its repository: what each step of the loop
costs and how long it takes, what people did at each hold, how long a PBI takes from its label to
done and how often it goes back, and what changes how those numbers read. It reads what the engine
wrote, what Runs kept and what the vendor says, and writes nothing. Its shape follows the Platform's
Project Health board. Written with the code of #734 (`src/modules/Loop/AiOrchestrator.Modules.Loop/Features/ReadMetrics.cs`,
`MetricsReading.cs`, `src/frontend/features/metrics/`).

## Requirements
### Requirement: One read per folder, period and epic
`GET /api/loop/metrics?repository=<owner/name>&days=<7|30>&epic=<id>` SHALL answer the four blocks for
the folder whose repository it names, over the last 7 or 30 days or all of it when `days` is absent,
and for the sub-issues of one epic alone when `epic` is given. Another number of days SHALL be refused
as a validation failure, a repository no folder holds SHALL be 404 with the remedy, and a loop that
cannot be read SHALL be 409 with its problem. A vendor that cannot be read SHALL not fail the read: the
blocks that need it are empty and a note says why.

#### Scenario: The period and the epic narrow every block
- **WHEN** the read asks for `days=30&epic=9`
- **THEN** only runs, holds, moves and merges of the last 30 days of PBIs whose parent is #9 are counted

### Requirement: The agents, by step of the loop
For each state whose agent ran in the period, the screen SHALL show the loop's runs and how many ended
with an outcome; the median and p90 of the ended runs' duration; and the cost and tokens in total and
per run, the per-run figures over the runs that said them. A bar chart SHALL show the cost by step and
another the median and p90 of duration. Below, a table SHALL list the period's runs with their step,
PBI, start, duration, result and cost, each PBI opening its Journey. A run a person opened by hand
SHALL not be counted. Percentiles SHALL interpolate linearly between the nearest ranks, as the Platform's
collector does.

#### Scenario: A run without a cost is not a run that cost nothing
- **WHEN** a step's only ended run has no cost
- **THEN** its cost is $0.00, its cost per run is none, and a note says the totals are a floor

### Requirement: People, at each hold
Each hold of the period SHALL be grouped by its reason: the reason the loop declares for it, a run's
failure by its cause, a script's failure by its name, or `max_loops` used up. Each hold SHALL end as
released unchanged, released after a person's commit, sent back (a request for changes, or a release
or move to a state above), closed (the PBI closed while held), or still held, drawn as one stacked bar
per reason with the time in hold, median and longest, of the ended ones. A reason whose every ended
hold was released unchanged SHALL say "Released unchanged N of N: this hold caught nothing". A
person's commit SHALL be one on the PBI's `change/<n>` branch that carries no mark of the loop, in its
committer or a trailer (commit-marks), whichever instance worked the PBI. A commit older than the
earliest marked commit of the pull requests read SHALL be neither a person's nor the loop's, because
the loop marked nothing then.

#### Scenario: A hold released with no commit
- **WHEN** a hold is released and no commit of a person lands on the branch between the hold and the release
- **THEN** it counts as released unchanged

#### Scenario: The loop's commits are not a person's
- **WHEN** a commit on the branch is committed by `aio run <id>` or `aio script <name>`, or ends with an `Aio-Run` or `Aio-Script` trailer
- **THEN** it does not make the release, or the merged pull request, a person's

#### Scenario: A person's commit on a PBI another instance worked
- **WHEN** a merged pull request of a PBI this journal does not know has a commit without a mark
- **THEN** it counts as merged with a person's commit

#### Scenario: A commit from before the marks
- **WHEN** a merged pull request's only commit is older than every marked commit
- **THEN** it counts neither with a person's commit nor as all the loop's, and a note names its PBI

### Requirement: The decisions of a person and the merged pull requests
For each state with a `back` and a hold in its `on` (the plan and the merge), the screen SHALL count
the PBIs that left it forward approved as they were after a hold, approved after a person asked for
changes, or let through by the score without a person. It SHALL count the merged pull requests of
`change/<n>` branches that carry a person's commit against those that are all the loop's.

#### Scenario: Changes requested on the plan
- **WHEN** a person requests changes on a PBI's proposal and it later leaves `proposal-review` forward
- **THEN** it counts as approved after changes

### Requirement: The flow
The screen SHALL show, for the PBIs that reached done in the period, the p50, p75 and p90 from their
first journal line to done; each epic from its first sub-issue's first line to its close; the PBIs that
went back and how many times (a move or release to a state above the one it left, or a hold that led
nowhere in a state that runs only scripts, such as a red gate, that the PBI later left forward); and
the PBIs to done per week, Monday first.

#### Scenario: Percentiles come from the data
- **WHEN** two PBIs reached done in 200 and 240 minutes
- **THEN** the p50 is 220 minutes and the p90 236

### Requirement: The notes
The screen SHALL end with notes that change how a number reads, each worded from its kind: runs without
a cost, runs still live, holds still open, runs a person opened, the date of the repository's first run,
no runs yet, pull requests older than the ones read, commits before the earliest marked one ("Commits
before <date> carry no mark and are not classified"), and a vendor that could not be read with what it
said. A note about runs or PBIs SHALL list them.

#### Scenario: Holds still open
- **WHEN** a PBI is still held
- **THEN** a note says one hold is still open and that its time is not in the median or the longest

### Requirement: Every number lists what it counts
Every count on the screen SHALL be a button that opens a dialog listing the PBIs or runs it counts,
each opening its PBI's Journey. The Loop screen SHALL link to the Metrics of each folder with a loop,
or of the chosen folder; the screen's address SHALL carry the period and the epic; and it SHALL read in
both themes and at 390 wide with no sideways scroll of the page, its tables scrolling in their own box.

#### Scenario: From a number to its PBIs
- **WHEN** a person presses the number of PBIs that reached done
- **THEN** a dialog lists them by number and title, and one opens its Journey
