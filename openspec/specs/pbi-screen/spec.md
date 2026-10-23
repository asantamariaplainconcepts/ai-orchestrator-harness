# pbi-screen Specification

## Purpose
The PBI's screen answers where this PBI is and what is missing. It is the unit of navigation: every
entry of the rail, the PBIs screen and the Loop screen opens it, and a run is read inside it, never as a
screen of its own. Reverse-engineered from the shipped code (`src/frontend/features/pbi/PbiScreen.tsx`,
`HeaderAction.tsx`, `tabs.tsx`, `tabs/JourneyTab.tsx`, `journey.ts`), not negotiated ahead of it.

## Requirements
### Requirement: The PBI has its own route and five tabs
The PBI SHALL be at `/pbis/:folderId/:pbiId/:tab`, where the tab is Journey, Issue, Proposal, Review or
Files, in that order, and an unknown or missing tab SHALL open the Journey. Each tab SHALL load on its
own. The tab marked SHALL be the one the PBI's state names with `tab:` in the loop (`GET
/api/loop/definition`), with "now", or "needs you" when the PBI is held; a state without `tab` marks
none. No state name is written in the screen: in this repository's loop `proposal-review` marks
Proposal, and `code-review` and `merge-review` mark Review.

#### Scenario: A held PBI in merge-review
- **WHEN** a PBI held in `merge-review` is opened
- **THEN** the Review tab carries "needs you"

#### Scenario: The marked tab follows the loop
- **WHEN** a PBI in `proposal-review`, in `code-review` or in `in-progress` is opened and not held
- **THEN** Proposal carries "now", Review carries "now", and no tab is marked, in that order

### Requirement: The open tab does not wait for the PBI
Once `GET /api/folders` names the repository, the screen SHALL ask at the same time for the PBI (`GET
/api/pbi`), its runs, its journal lines, its folder's loop, what the open tab reads from the vendor (the
issue and its children, the proposal, or the review) and the open tab's chunk. None of them SHALL wait
for `GET /api/pbi`; the tab paints once the PBI and its own read have both answered.

#### Scenario: The PBI answers last
- **WHEN** the Proposal tab is opened and `GET /api/pbi` has not answered
- **THEN** `GET /api/pbi/proposal`, the runs, the journal, the loop and the tab's chunk have all been asked for

### Requirement: The other tabs are read while one is open
Once the PBI has answered and the browser is idle (`requestIdleCallback`, at most a second later), the
screen SHALL ask in the background for the chunks of Issue, Proposal and Review and for what each
reads from the vendor, under the same query keys their own hooks read, so a click on one paints from
the cache. Files and the recordings SHALL NOT be read ahead. A background read that fails SHALL show
nothing; the tab asks again when it opens.

#### Scenario: Proposal paints from the cache
- **WHEN** the Journey of a PBI has painted and a few seconds later Proposal is clicked
- **THEN** `GET /api/pbi/issue`, `/api/pbi/children`, `/api/pbi/proposal` and `/api/pbi/review` and the
  three chunks were already asked for, and the click asks for no new proposal

### Requirement: What the vendor answered stays until the person asks again
The PBI's vendor reads (the PBI with its pull request, the issue, its children, the proposal and the
review) SHALL stay on screen once read: not polled, not read again on window focus, nor on a change of
tab. Entering the PBI again, from another screen or another PBI, SHALL show what was kept at once and
read the kept reads again behind it. A refresh button in the header, labelled "Read #<id> again from
<vendor>", SHALL read all five again with `Cache-Control: no-cache`, so the server skips its ten-second
copy. A write from the screen SHALL still read them again. The runs, the journal and the loop SHALL keep
their polling, and a new journal line about the PBI SHALL read `GET /api/pbi` again behind the screen,
so the header follows the loop without polling the vendor.

#### Scenario: The loop moves the PBI while it is open
- **WHEN** the Journey is open and the journal's next poll brings a new line about the PBI
- **THEN** `GET /api/pbi` is asked once more and the header shows the new state, with no revisit and no refresh

#### Scenario: Back on a PBI
- **WHEN** a PBI is opened, the person goes to another PBI, and comes back while `GET /api/pbi` has not answered
- **THEN** the header shows the PBI already read, and `GET /api/pbi` has been asked again

#### Scenario: The refresh button
- **WHEN** the refresh button is pressed, by pointer or keyboard
- **THEN** `GET /api/pbi`, `/api/pbi/issue`, `/api/pbi/children`, `/api/pbi/proposal` and `/api/pbi/review` are each sent with `Cache-Control: no-cache`, and the runs are not

### Requirement: The header says the state, the hold and the pull request
The header SHALL show the PBI's state, or "no state"; when it is held, "Hold · <reason>" with the reason
of the engine's latest hold at that state, or "Hold · put on by a person" when the engine did not put it
on; and the pull request as "PR #<n>" with whether it is a draft, ready for review, approved or merged.

#### Scenario: A hold the engine put on
- **WHEN** the gate was red in `verifying`
- **THEN** the header reads "Hold · El gate está en rojo: …" with the gate's own why

### Requirement: One button, whose words depend on the state
The header SHALL offer at most one action, decided in this order: *Release hold* when the PBI is held —
released at once when the hold leads to a state, with "The loop goes on to <next>." as its hint, and
otherwise through a dialog that asks for the state; *Stop run* when a loop run of the PBI is live,
after a confirmation that says the run ends failed and the loop holds the PBI; *Move to…* when the PBI
is open in the vendor, has no sub-issues, and has no state or one the loop does not declare, through a
dialog that asks for the state; and nothing otherwise. A closed PBI is reopened in the vendor, and an
epic is closed by the engine when its last sub-issue closes, so neither SHALL be offered *Move to…*. A refusal SHALL be shown as "Nothing changed. <reason>".

#### Scenario: A PBI the loop is working on
- **WHEN** a PBI in `in-progress` is not held and has no live loop run
- **THEN** the header shows no action button

#### Scenario: An open PBI in done
- **WHEN** a PBI is in `done` and its issue is open
- **THEN** the button is *Move to…*

#### Scenario: A refined PBI
- **WHEN** an open PBI is in `needs-refinement`
- **THEN** the button is *Move to…*

#### Scenario: A closed PBI
- **WHEN** a PBI is in `done` and its issue is closed
- **THEN** the header shows no action button

#### Scenario: An epic
- **WHEN** an open PBI with no state has sub-issues
- **THEN** the header shows no action button

### Requirement: Terminal and Explore with agent open a session on the PBI
Beside the button, *Terminal* SHALL open a shell on the PBI at once, and *Explore with agent* SHALL open
a dialog with the agent, the model and an optional command. The agent list SHALL show every agent the
machine has an adapter for, with those whose CLI is not on the PATH shown and disabled; the folder's
default agent SHALL be preselected, else the first usable one. The model list SHALL follow the chosen
agent, with the folder's model added first when it is not in the list. The command field SHALL suggest
`<the state's command> <id>`. Both SHALL open a manual run as the sessions capability says, and the new
session SHALL open in the tray once it appears.

#### Scenario: An agent that is not installed
- **WHEN** the dialog opens on a machine without `claude` on the PATH
- **THEN** Claude Code is listed as not on PATH and cannot be chosen, and the dialog says no agent is available

### Requirement: The Journey lists the loop's states with what happened in each
The Journey SHALL list the loop's declared states top to bottom, then the undeclared ones, then any
state named only by a run, each marked visited, current or not reached, with its plan (agent and
command, its scripts, or its score). A score's band and reason SHALL come from the engine's latest
line for that state. Under each state SHALL be its runs, loop or manual, with agent, command, duration,
cost and outcome or failure; a run's row SHALL unfold to its end record as written, its reason and
problems, the state it led to, its tokens, and its recording to play. A live run SHALL say how long it
has run and how long ago it wrote, and offer its terminal. A script the engine is running SHALL be a
row under its state shaped as a live run's, with the script's name and how long it has run, and no
recording or terminal. The current state SHALL show the hold's note
and the engine's lines for it.

#### Scenario: A held state says how it gets released
- **WHEN** the current state is held with a hold that leads to `retro`
- **THEN** the Journey says the PBI waits for a person, and that Release hold or an approval at the vendor moves it to `retro`

### Requirement: An epic has no journey of its own
A PBI with sub-issues SHALL be shown as an epic. Its Journey SHALL list no states: it SHALL say in one
sentence that an epic has no journey of its own, that each of its sub-issues walks the loop, and link
to the Issue tab, where their graph is. Its header SHALL read "epic · <open> of <total> open", counting
the sub-issues not closed, or "epic · closed" once the epic is closed, in place of the state or "no
state". A PBI without sub-issues SHALL keep its states.

#### Scenario: An epic with two open sub-issues
- **WHEN** a PBI with two open sub-issues opens on its Journey
- **THEN** the Journey says an epic has no journey of its own and links to the Issue tab, and the header reads "epic · 2 of 2 open"

#### Scenario: A PBI without sub-issues
- **WHEN** a PBI without sub-issues opens on its Journey
- **THEN** the Journey lists the loop's states and the header says no epic

### Requirement: With nothing of its own, the Journey reads the issue's journey comment
When this instance's `/api/runs` and `/api/loop/journal` answer nothing for a PBI, the Journey SHALL be
drawn from the rows of the issue's `aio:journey` comment, as `GET /api/pbi/issue` marks it with the
harness kind `journey` (#696): each `run` and `manual` row as a finished run under its state, with its
command, CLI, model, outcome or failure, reason, duration and cost, and its recording played from
`aio/evidence` at the path the row names (#697), and each `moved`, `held` and `released` row as the
engine's line from its state. Above the states it SHALL say "From the issue, as another instance wrote
it", with a link to the comment. When this instance has a run or a line of the PBI, the Journey SHALL be
drawn from those alone. An issue without the comment, or one that cannot be read, SHALL leave the Journey
as this instance has it.

#### Scenario: An instance with an empty database
- **WHEN** the runs and the journal are empty and the issue's thread carries an `aio:journey` comment
  whose rows run `/aio:implement` in `in-progress` and move the PBI through `verifying`, `code-review`
  and `merge-review` to `retro`
- **THEN** the Journey marks those four states visited and `retro` current, shows the run with its
  duration and cost under `in-progress` and each move under its state, below "From the issue, as another
  instance wrote it"
