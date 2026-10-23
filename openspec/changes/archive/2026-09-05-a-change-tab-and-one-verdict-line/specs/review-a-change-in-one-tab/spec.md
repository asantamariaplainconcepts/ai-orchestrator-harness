## ADDED Requirements

### Requirement: A worktree's strip is five fixed tabs and the first of them is the change

A worktree SHALL draw five fixed tabs — **Change**, Runs, Files, Chat, Telemetry — plus whatever
sessions are open beside them. There SHALL be no tab called Issue, no tab called Review and no tab
called Checks in a worktree's strip: what those three answered is a step of the change, and reading
three tabs to assemble one judgement is the thing this replaces.

**A checkout's strip SHALL keep Review and Checks.** They are withdrawn from the worktree only. A
checkout somebody edits by hand has a real diff, and a check's result belongs to the directory it ran
in — so the tabs are rescoped and not removed, and no strip other than the worktree's changes.

The tab a worktree rests on SHALL be Change, and it SHALL be a tab that strip actually offers: a
resting place the strip does not contain sends the screen straight back to it.

#### Scenario: The worktree strip
- **WHEN** the folder screen is scoped to a worktree of a repository
- **THEN** the fixed tabs are Change, Runs, Files, Chat and Telemetry, and none of them is Issue,
  Review or Checks

#### Scenario: The checkout keeps what the worktree gave up
- **WHEN** the same folder is read on its checkout
- **THEN** Review and Checks are still on the strip, with the diff and the checks they always drew

#### Scenario: A link to a tab this scope no longer has
- **WHEN** a link names a tab the scope does not offer, including one this change removed
- **THEN** the screen resolves to Change rather than to a blank panel

### Requirement: The change is five ordered steps, drawn as a rail and counted in a footer

The Change tab SHALL draw an ordered rail of five steps — **Proposal**, **Code**, **Tests**,
**Evidence**, **The app** — one panel for the selected step, and a footer that moves between them and
says how many are done out of how many there are.

The rail and the footer SHALL **navigate only**. Selecting a step SHALL NOT record anything, and the
screen SHALL NOT claim a step is done that nobody marked; where the steps' state is not stored, every
step SHALL read as not looked at and the count SHALL say so.

**How many steps are done SHALL be read from one place.** The number beside the tab and the number in
the footer SHALL come from the same source, so the strip and the footer can never disagree about how
much is left.

#### Scenario: Five steps, in order, none of them claimed
- **WHEN** the Change tab opens on a worktree
- **THEN** five numbered steps are drawn in order, none is marked done, and the footer says none of
  five is done

#### Scenario: The footer moves and stores nothing
- **WHEN** the reader presses forward and then back through the rail
- **THEN** the panel follows the selection and nothing about the worktree has been recorded

#### Scenario: One count, two places
- **WHEN** the steps outstanding change
- **THEN** the tab's counter and the footer's progress both change, because they read one source

### Requirement: The change header names the issue and states its facts without judging them

The Change tab's header SHALL name the issue the worktree is about — its number and its title — and
beneath it SHALL state the change's facts as one mono line. The facts and the wording of an unknown
are the `merge-gate` capability's; this requirement is that the header is **where** they are said and
that the header says nothing else about readiness.

The header SHALL carry the screen's actions in **one place**, and no fact SHALL disable one.

The issue SHALL be resolved the way every other surface on this screen resolves it — from the most
recent run in that directory, and otherwise from the number the branch proposes and the mirror
confirms — so the header and the facts beneath it can never name different issues for one worktree.

#### Scenario: The header over a resolved issue
- **WHEN** the worktree's task resolves
- **THEN** the header names that issue's number and title, with the facts as one line beneath it

#### Scenario: The header over a worktree whose issue does not resolve
- **WHEN** no run and no branch resolves a task for this worktree
- **THEN** the header says the issue is not known and still states every fact it can, rather than
  drawing nothing

#### Scenario: Nothing in the header is a verdict
- **WHEN** a check has failed and the tree is dirty
- **THEN** the header states both and offers every action it always offered
