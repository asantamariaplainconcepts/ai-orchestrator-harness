## REMOVED Requirements

### Requirement: A checkout declares its loop, and the harness obeys the declaration
**Reason**: the loop a checkout declares is now `.harness/issue-loop.yaml`, executed by the backend;
`lifecycle.json` stops being read and is deleted with `HarnessLifecycleFile` and `LabelLaunchPlan`
(#365). The launch decision moves to `run-the-declared-loop`.
**Migration**: the states-and-guards machine is superseded by the declared loop's nodes and
`x-harness.status` labels — this repository already carries its loop as
`.harness/issue-loop.yaml` (#363), so nothing is lost by the deletion.

### Requirement: One declaration may carry several machines over the same states
**Reason**: with the file deleted there are no machines to name; a checkout declares one loop, and
what differed between `staged` and `ship` is the loop's own routing (`when`, holds) rather than two
machines over one set of states.
**Migration**: none needed — this repository's requirement was satisfied by editing one file, and
that remains true of `issue-loop.yaml`.

### Requirement: An unreadable declaration makes the harness start nothing
**Reason**: restated for the loop as "An unreadable loop makes the harness start nothing". The
declaration it guarded was `lifecycle.json`'s machine, deleted by #365; the scenario "No file
declares no machine" becomes "No file declares no loop".
**Migration**: none — the same refusal, read from `.harness/issue-loop.yaml`.

### Requirement: The declared vocabulary is closed, and nothing read from it executes
**Reason**: restated for the loop as "The loop's vocabulary is closed, and nothing read from it
executes". Its `startRun` paragraph and the two scenarios about a lifecycle effect carrying a prompt
or naming an undeclared workflow key have nothing left to describe: #365 deletes the effect and the
workflows it named.
**Migration**: none — a node's command is the loop's own, and an unknown node type is refused at load.

## ADDED Requirements

### Requirement: An unreadable loop makes the harness start nothing
Where the declared loop is absent, larger than its bound, unparsable, or names anything outside the
vocabulary the product understands, the system SHALL treat the Folder as declaring no loop and
SHALL start no Run for it. It SHALL NOT fall back to `lifecycle.json`, to a built-in machine, or to
the workflows of `workflows.json`. The refusal SHALL be reported with its reason and the file it
was read from, because the symptom — nothing happens — is otherwise indistinguishable from the
watcher being switched off.

#### Scenario: No file declares no loop
- **WHEN** a Folder has no declared loop
- **THEN** no Run starts for any of its tasks, and the reason is recorded

#### Scenario: A malformed file is not an empty one
- **WHEN** the declared loop is mid-edit, carries conflict markers, or does not parse
- **THEN** the Folder is treated as declaring no loop rather than as declaring no guards

#### Scenario: A declaration from a newer product is refused, not guessed
- **WHEN** the loop names a node type or expression this product does not understand
- **THEN** it is refused with that term named, rather than that condition being treated as false

### Requirement: The loop's vocabulary is closed, and nothing read from it executes
The node types a loop may carry, the comparisons a `when` may apply, and the tokens an interpolation
may substitute SHALL each be drawn from a fixed set the product defines — four node types, the
typed-output comparisons of `run-the-declared-loop`, and the declared inputs. The system SHALL NOT
evaluate expressions, code, or templates supplied by the file beyond substituting those tokens. The
file arrives from a repository through a pull request and SHALL be bounded and parsed on the same
terms as every other declared file — a size limit, a comment-tolerant parser, and a malformed
document answered as no declaration.

#### Scenario: A declaration cannot describe an action the product does not offer
- **WHEN** a declaration names a node type outside the fixed set
- **THEN** it is refused, and no part of the file is executed to find out what the node meant

#### Scenario: The same bounds as its sibling files
- **WHEN** the declared loop exceeds the size a declaration can reasonably be
- **THEN** it is refused on the same terms `.harness/commands.json` and `.harness/config.json` are
