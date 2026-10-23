# run-the-declared-loop Specification

## Purpose
TBD - created by archiving change loop-runs-the-yaml. Update Purpose after archive.
## Requirements
### Requirement: The harness executes the loop a checkout declares in issue-loop.yaml
The system SHALL read `.harness/issue-loop.yaml` at load and SHALL execute it as the loop its tasks
follow — reading it always from the Folder's checkout, never from a worktree. The `LabelWatcher`
tick SHALL decide which node a task is at from the loop, not from `lifecycle.json`. A checkout
declaring no loop SHALL automate nothing: no Run starts for it, and the tick says why, on the same
terms a folder with no declared machine is silent today.

#### Scenario: A checkout without a loop automates nothing
- **WHEN** a Folder's checkout declares no `.harness/issue-loop.yaml`
- **THEN** no Run starts for any of its tasks, and the tick records the reason

#### Scenario: The loop is read from the checkout, not a worktree
- **WHEN** a task's Run executes in a worktree whose checkout declares one loop and the worktree
  holds another
- **THEN** the loop executed is the checkout's, because workbranches carry the loop as a fact of
  the trunk they branched from

### Requirement: An invalid loop starts nothing and names the error
A loop that fails validation SHALL start no Run, and the refusal SHALL name the node and the field
at fault. Validation SHALL check, at load: unique node ids; every `depends_on` names a known node;
no cycles in `depends_on`; every `$step.output.field` a `when`, `with` or `bash` references is
declared in that step's `output_format`; no `$INPUTS.x` inside a `bash` node, where inputs arrive
as `$INPUTS_X`; and no quoted `"$step.output"` inside a `bash` node.
`interactive` SHALL be accepted on the document (Archon requires it; the harness treats an
`approval` as its pause).

#### Scenario: An undeclared output reference is refused with step and field
- **WHEN** a loop references `$overlap.output.files` and `overlap`'s `output_format` declares no
  `files`
- **THEN** the loop starts nothing and the refusal names `overlap` and `files`

#### Scenario: An unknown depends_on is refused at load
- **WHEN** a node depends on an id no node carries
- **THEN** the loop is refused before any Run starts, naming both ids

#### Scenario: A cycle is refused
- **WHEN** `depends_on` closes a cycle
- **THEN** the loop is refused at load, naming the cycle's nodes

### Requirement: Exactly four node types, and what each does
A node SHALL be one of `command`, `bash`, `approval` or `cancel`; any other type SHALL be refused
at load naming the node and the type found. A `command` node is an agent step of a controlled Run:
it launches the declared command against the fixed candidate. A `bash` node is a fact step: it runs
its script and parses its typed output. An `approval` node applies `status:holding` and ends the
Run; a person removing the label is the approval, and the next Run resumes at the following node.
A `cancel` node ends the Run and moves the label its `x-harness.status` names. Nothing SHALL retry:
a failed node ends the Run, applies the hold, and says why.

#### Scenario: A fifth node type is refused at load
- **WHEN** a node carries a type other than `command`, `bash`, `approval` or `cancel`
- **THEN** the loop is refused before any Run starts, naming the node and the type

#### Scenario: Removing a hold is the approval
- **WHEN** a Run parks at an `approval` node and a person removes `status:holding`
- **THEN** the next Run resumes at the node after the approval, not by re-running the approval

### Requirement: Routing is depends_on plus when over typed outputs
A node SHALL run when its `depends_on` are satisfied and its `when` holds. The `when` expression
SHALL compare `$step.output.field` values with `==`, `!=`, `<`, `>`, `<=`, `>=`, combined with `&&`
and `||`. The `risk` node's `x-harness: { decides: model }` routing stays routing over its typed
output — the score is a model's, the branches a repository's.

#### Scenario: A false when skips nothing silently
- **WHEN** a node's `when` does not hold
- **THEN** the branch below it does not run, and the Run ends where the loop's remaining
  dependencies take it

#### Scenario: A typed comparison routes on a fact
- **WHEN** `$gate.output.green == 'true'` is evaluated
- **THEN** the branch runs only on the parsed boolean, not on a substring of the transcript

### Requirement: An agent's output is typed, or the node fails
A `command` node SHALL produce its result in the file `/aio:candidate` already uses, validated
against the node's `output_format` as its schema. A missing, malformed or schema-invalid result
SHALL fail the node — no advance, the reason recorded on the Run.

#### Scenario: A result that misses the schema fails its node
- **WHEN** a `command` node's result lacks a field its `output_format` requires
- **THEN** the node fails, the Run records why, and nothing below it runs

### Requirement: Resumption checks the previous node's facts, not its label alone
A new Run SHALL resume at the node after the one whose `x-harness.status` is the issue's current
label, or after a released approval — and before resuming it SHALL verify, as a fact, that the
preceding node left what it must leave. For `implement` that fact is: the OpenSpec change exists
and its branch exists. Where the fact is missing, the Run SHALL not start and SHALL say what is
missing, in the words of #361's remedy — because a label set by hand is not proof the step ran.

A work step whose completion moves the issue onward SHALL declare the label it moves: a node that
completes work and moves no label leaves the issue where it was found, and the next Run resumes at
the same node — a loop, not a progression. The declared loop's `refine` node SHALL move
`spec-ready`, and an issue carrying it SHALL resume at `spec-ready`'s dependents — the path
`overlap`, `claim`, `propose — whose first command node seats the agent on `/aio:propose`. The
`claim` node SHALL move no label: the claim within this machine is the live Run (`LiveRunKeys`),
between checkouts the branch, and a label claiming nothing collides with the node that does the
work (`implement` moves `in-progress`).

#### Scenario: A hand-applied label does not resume the loop
- **WHEN** an issue carries `status:ready-for-implementation` set by hand, with no OpenSpec change
  and no branch
- **THEN** no Run starts for it, and the refusal names what is missing

#### Scenario: A released approval resumes the next node
- **WHEN** an approval was released and the run resumes
- **THEN** it begins at the node after the approval with its facts verified, not at the loop's start

#### Scenario: A completed refine moves the issue to spec-ready, and the next run proposes
- **WHEN** an issue's refine run ends and the run moves its label to `status:spec-ready`
- **THEN** the next Run for it resumes at `spec-ready`'s dependents and seats the agent on
  `/aio:propose`, not on `/aio:refine` again

#### Scenario: A work node that moves no label is replaced, not routed around
- **WHEN** a declared work step completes and declares no `x-harness.status`
- **THEN** the declaration is refused by review — the loop's progression is its declared statuses,
  because a step that completes silently is entered again by the next Run

#### Scenario: The claim is not a label
- **WHEN** an issue carries `status:in-progress` set by the `implement` step and no other node moves it
- **THEN** the issue resumes at `implement`'s dependents, because the live Run and the branch are the
  claim and no earlier node's label stands in their way

### Requirement: The trace of every node stays on the Run
Each node's execution SHALL leave its step on the Run's trace — its inputs, its typed output, its
error if any — on the terms `RunLauncher.Controlled` already keeps, so a finished Run is inspected
without inferring a prose log.

#### Scenario: A finished Run shows each node's step
- **WHEN** a Run ends and is inspected
- **THEN** every node that ran is on the trace with its output, and the held or cancelled node is
  the last entry

### Requirement: The overlap node answers from the claimed branches and the declared seams
The declared loop's `overlap` node SHALL run `.harness/aio/file-overlap`, which SHALL treat as in
flight every `change/<n>` branch at origin other than the issue's own, SHALL read each issue's
file-set from its Seam section's bullet paths and its Task input's `seam.paths`, and SHALL answer
`clear: false` naming the issue and the paths when two file-sets can name the same file — equal, one
ending with the other on a `/` boundary, or one a directory the other sits under, compared without
case. An issue that declares no Seam, here or in flight, or that cannot be read, SHALL NOT be clear.

#### Scenario: Nothing else is in flight
- **WHEN** no other `change/<n>` exists at origin
- **THEN** the node answers clear and `claim` runs

#### Scenario: The same file written from another root
- **WHEN** this issue declares `src/frontend/shared/http/queries.ts` and one in flight declares
  `shared/http/queries.ts`
- **THEN** the node answers not clear, naming the other issue and the path

#### Scenario: A merged change stops counting
- **WHEN** a change lands and the merge script deletes its branch
- **THEN** it is no longer in flight for the next tick's overlap

