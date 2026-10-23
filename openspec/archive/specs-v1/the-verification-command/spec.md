# the-verification-command Specification

## Purpose
The command a project declares as the one that shows a change working, and the two ways it is reached:
pressed by the person the loop stopped for, or run by the plan as a step of the run itself. The loop's
one deliberate stop used to hand somebody a diff and nothing to open — the command was declared, and
finding it meant knowing which tab of which worktree it was in. What makes this executable is a seam
that had been cut and unused since 19 August: `WorkflowPlanStep.RunsHere` already meant "this product
runs the step", and the plan already recognised one, in order to stop. Now it runs it, and the process
ending is the step ending — zero advances, anything else stops the run naming the step and its code,
and nothing retries. What is deliberately absent is any claim that the application is up. The product
cannot know: the launcher detaches to ppid 1 and exits, the address is different on every start, and
the exit code it leaves behind is unchanged by the application going down. The link the command prints
is what proves it, when clicked, and nothing here prints a state it did not observe.
## Requirements
### Requirement: A project declares which of its commands is the verification command

A checkout SHALL be able to mark exactly one of the commands in its own `.harness/commands.json` as
the one that shows a change working. The mark SHALL be a flag on the entry, read from the file rather
than from any stored row, so that a branch which changes it changes it for that branch.

The flag SHALL default to **not marked**. A folder where nobody has marked anything SHALL be reported
as having no verification command, and MUST NOT have one inferred for it from any other property of
its commands.

Only the **checkout's** file SHALL be read for this, never a worktree's.

#### Scenario: A declared verification command is read back
- **WHEN** a checkout's `.harness/commands.json` marks one entry as the verification command
- **THEN** that entry's key, name and command line are what the product reports as the verification command

#### Scenario: Nothing marked is said rather than guessed
- **WHEN** a checkout's `.harness/commands.json` marks no entry
- **THEN** the product reports that the folder declares no verification command
- **AND** no command is offered in its place

### Requirement: A plan step naming a folder command is executed by this product

A run's plan step whose key resolves to one of the folder's declared commands SHALL be executed by
this product rather than handed to the agent. The command line SHALL be resolved **when the run
starts**, in the same place the task key is substituted, and carried with the step — so a command
edited or removed after a run started SHALL NOT change what that run executes.

The step SHALL be over when the process is over.

- An exit code of **zero** SHALL end the step as succeeded and advance the plan exactly as an agent
  going idle does.
- Any **other** exit code, and **no exit code at all**, SHALL end the step as failed and stop the run.
  The run's output SHALL name the step, the command key and what it exited with, and an absent code
  SHALL be reported as absent rather than as a number.
- A stopped plan SHALL NOT be retried, and no step after the failed one SHALL be dispatched.

The command's output SHALL be written to the run's own transcript as the run's output.

#### Scenario: A zero-exit step advances the plan
- **WHEN** a plan step that this product runs exits with code 0
- **THEN** that step is recorded as succeeded
- **AND** the next step of the plan is dispatched

#### Scenario: A non-zero step stops the run and says so
- **WHEN** a plan step that this product runs exits with code 3
- **THEN** that step is recorded as failed
- **AND** the run's output names the step, its command key and the code 3
- **AND** no step after it is dispatched, and the step is not run again

#### Scenario: A step whose process gave no ending
- **WHEN** a plan step that this product runs ends with no exit code
- **THEN** the step is recorded as failed and the run's output says there was no ending to read

#### Scenario: The run executes the plan it started with
- **WHEN** the folder's declaration of a command changes while a run using it is in flight
- **THEN** that run executes the command line it resolved when it started

### Requirement: Resolving a command key crosses a seam, and says "cannot say" with null

Resolving a folder command key to a command line SHALL happen across an interface in the world seam,
implemented by the module that owns the command rows and registered by that module.

The seam SHALL answer `null` for every case it cannot resolve — an unknown folder, an unknown key, a
key with no command line, and a declared working directory that resolves outside the checkout — and
MUST NOT answer a partial or guessed line for any of them.

The layer that runs processes MUST NOT read the command rows itself.

#### Scenario: An unknown key resolves to nothing
- **WHEN** a command key no folder declares is resolved
- **THEN** the seam answers null

#### Scenario: A command whose directory escapes the checkout
- **WHEN** a declared command's working directory resolves outside the checkout it was started in
- **THEN** the seam answers null rather than a line to run there

### Requirement: A plan step this product cannot run is refused when the plan is imported

Importing a declared plan SHALL classify a step naming a key the folder cannot run as **refused**,
and SHALL record on that step which of the two reasons applied:

- the key is not a prompt key and the folder declares no command by that key;
- the folder declares that key as a command that does not end by itself, which has no ending for a
  step to wait on.

A refused step MUST NOT be filed as a prompt for the agent. The import SHALL report how many steps it
refused, alongside what it added, updated and left unchanged.

A run that reaches a refused step SHALL stop and name the key, rather than typing it at the agent.

#### Scenario: A key the folder does not declare
- **WHEN** a declared plan names `tests` and the folder declares no command with that key
- **THEN** the imported step is recorded as refused, with the reason that no such command is declared
- **AND** the import's response counts it as refused

#### Scenario: A key the folder declares as long-running
- **WHEN** a declared plan names a command the folder declares as one that stays up
- **THEN** the imported step is recorded as refused, with the reason that it has no ending to wait for

#### Scenario: A prompt key is not refused
- **WHEN** a declared plan names a dotted prompt key such as `aio.implement`
- **THEN** the step is filed as a prompt for the agent exactly as before

#### Scenario: A run stops at a refused step
- **WHEN** a run reaches a step that was refused at import
- **THEN** the run stops and its output names the step's key
- **AND** the key is not sent to the agent

### Requirement: The change tab's fifth step offers the verification command

The fifth step of a change SHALL show the verification command the folder declares, its key and its
command line as declared, and SHALL offer a control that runs it in that working directory and opens
a terminal for it.

The step SHALL NOT state whether the application is running, SHALL NOT display an address it did not
observe, and MUST NOT probe any address to decide what to draw. What the command prints is the only
thing that says where the application is.

Where the folder declares no verification command, the step SHALL say so and offer nothing to press.

#### Scenario: The declared command is shown and can be run
- **WHEN** the fifth step is opened on a worktree of a folder that declares a verification command
- **THEN** the command's name and declared line are shown
- **AND** pressing the control opens a terminal running that command in that worktree

#### Scenario: Nothing is claimed about the application
- **WHEN** the fifth step is drawn, whether or not the application is running
- **THEN** no statement that the application is up, and no address, appears in the step

#### Scenario: A folder with no verification command
- **WHEN** the fifth step is opened on a folder that declares no verification command
- **THEN** the step says the folder declares none, and offers no control that runs anything

### Requirement: A toggle makes the verification command a step of a workflow's plan

The fifth step SHALL offer a toggle that adds the verification command to a workflow's plan as a step
this product runs, and removes it again.

Turning it on SHALL append the step to that workflow's stored plan and SHALL write the folder's
declared plans out to `.harness/workflows.json`, so that the next run of that workflow executes it
without anybody pressing anything. Turning it off SHALL remove the step and write the file again.

The toggle SHALL report the state it read rather than the state it hopes for: on when that workflow's
plan already holds a step naming the verification command, off otherwise.

Where the file cannot be written the change SHALL be refused, and the refusal SHALL name the file.

#### Scenario: Turning it on
- **WHEN** the toggle is turned on for a workflow of a folder that declares a verification command
- **THEN** that workflow's plan holds a step naming that command
- **AND** `.harness/workflows.json` holds that step too

#### Scenario: The next run starts it
- **WHEN** a run of that workflow is started after the toggle was turned on
- **THEN** the run executes the verification command as a step of its plan

#### Scenario: Turning it off
- **WHEN** the toggle is turned off
- **THEN** neither the stored plan nor `.harness/workflows.json` holds a step naming that command

#### Scenario: A folder with no verification command
- **WHEN** the toggle is turned on for a folder that declares no verification command
- **THEN** the change is refused and nothing is written

