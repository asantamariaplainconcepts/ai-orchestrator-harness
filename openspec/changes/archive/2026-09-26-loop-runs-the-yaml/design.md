## Context

Issue #365: one loop, executed. The proposal deletes the Workflows module (~3,000 lines), the
lifecycle machine and the Postgres plans, and makes the backend run `.harness/issue-loop.yaml`.
What already exists and is kept: `RunLauncher.Controlled` + `ControlledPlan` (516 lines) — fixed
candidate, steps against it, hold, per-step trace; `IDeclaredLoopFile` — writes the loop file
atomically from the checkout since #363; the loop editor on the AI workflows tab; `LabelWatcher`'s
tick loop; the controlled execution contract in `controlled-workflow-execution`.

## Goals and Non-Goals

- Goals: the backend executes the declared loop; load-time validation that refuses to start an
  invalid loop; four node types; typed agent outputs; label-based resumption with fact-checks
  (#361); the WIP limit declared by the loop; delete the module, the machine and `workflows.json`.
- Non-Goals: Archon's engine (only its format); mid-run resume (a Run ends at an approval or a
  cancel — resumption starts a new Run); retries; `script`, `loop`, `wait`, `prompt` nodes and
  sub-workflows; adopting the harness in a folder without a loop (#364).

## Decisions

### The interpreter composes the machinery that already exists, and is new

`LoopRunner` (new, beside `RunLauncher.Controlled` in `Agents/`) reads the validated loop and
drives the existing controlled Run: a `command` node becomes the agent step of a controlled Run
against a fixed candidate; a `bash` node becomes its gate step (exit-code-and-scope logic
unchanged, only now scheduled by the loop rather than by a plan's rows); `approval` and `cancel`
are terminal, as they are today. The Run's trace keeps each node's inputs, typed output and error
— observed on the Run, not re-read from the declaration. This honors the spec's
`controlled-workflow-execution` snapshot rule: editing the loop file after a Run ends must not
rewrite its record.

### A node's four types are the closed vocabulary, and router steps are facts

`bash` is deterministic; `command` routes on its `output_format`-validated result (as `risk` does:
`decides: model` because a model wrote the score, deterministic because the routing is typed).
`approval` ends the Run with `status:holding` — a person removing the label is the approval, which
resumes at the next node in a new Run; `cancel` ends it moving a label. Nothing retries; a failed
node applies the hold and the reason, on the terms the manual loop already uses.

### The WIP limit moves to the loop's x-harness, and both readers read it there

`wipLimit` leaves `.harness/config.json` (which keeps the label names) and is declared by the loop's
workflow-level `x-harness: { wip: 4 }`. Both the automatic path and `/aio:implement`'s WIP gate read
the same declaration: the automatic path through `Load()`'s validated limit, the written gate
through reading the same file, as `run-the-declared-loop`'s scenario "One number, two enforcements"
requires. An unreadable limit stops a launch (a folder that automates nothing is the same failure
as today's unreadable config, and carries the same refusal to read absence as permission).

### Validation is the whole loop's contract, enforced at load, never at run

The 6 checks (unique ids, known `depends_on`, acyclic, declared `$step.output.field`, `$INPUTS_X`
in bash, unquoted outputs) are repo-knowledge. `Load()` validates against the YamlDotNet-parsed
graph before any Run starts, and the message names the node and the field — in the terms #356's
failure taught: the writer and the validator must agree, and here the writer is the editor of #363,
which already refuses to save an invalid loop, so the backend's validator is the second reader and
the error is the same shape. The parser is a new dependency, YamlDotNet, behind
`IDeclaredLoopFile`, which gains a `Read` returning the validated loop. `interactive:` is accepted
on the document and ignored (Archon demands it; an `approval` is the harness's pause).

### Resumption decides from labels, verified by facts

Label-based resume follows ADR-0005 (GitHub owns state) and the issue's own text: resume at the
node after the one whose `x-harness.status` is the issue's current label, or after the released
approval — then verify the previous node's facts before starting. For `implement` the fact is: the
OpenSpec change exists and its branch exists, checked as a fact (the file on disk, the branch in
`git branch --list`), because #361 showed a hand-applied `status:ready-for-implementation` will
otherwise seat an agent at a step without its input. Missing facts: no Run starts, and the
refusal names the fact. This reviewer-added condition lives in the issue's own comment (2026-09-25)
and its check.

### Deletion is a migration this issue owns

The Workflows module's tables are dropped by a migration this change owns while open (AGENTS.md's
collision rule: one issue owns a module's migrations at a time). The module, `HarnessLifecycleFile`,
`LabelLaunchPlan`, `HarnessWorkflowFile`, `WorkflowsTab`'s step editor, and their tests go in one
delete. What stays on the AI workflows tab: the loop editor (which writes through
`IDeclaredLoopFile`) and the declared commands list ("Run a task here"). #356 closes unfixed —
the file it describes stops existing.

## Risks / Trade-offs

- **One file gains power**: the loop changes automation by pull request. Same trust model as
  `commands.json` (declared file, reviewed diff), and YAGDotNet parsing behind a size bound with
  the same failure-closed reading as every sibling seam.
- **Deletion volume**: ~4,700 lines out. The trace, the editor and the interpreter are the surface
  that remains — the good set, in the issue's words.
- **`script`/`risk` bow out of the interpreter's node types** (the loop file today carries
  `script` + `runtime: bun` nodes): the four types are the product's; the loop file's nodes that
  cannot be typed as one of the four are refused at load, and this repository's own hard facts
  (gate, tripwires, evidence) either become `bash` nodes or are not resumable by the loop — the
  loop file of #363 is already the definition, and what its nodes mean is decided by this spec's
  four types, naming anything else in the refusal.
- **Validation of `when`**: parsed, not evaluated — comparisons over typed outputs are the closed
  set (`==, !=, <, >, <=, >=, &&, ||`), never a script engine.

## Migration Plan

1. Delete `.harness/lifecycle.json` in this change (with `HarnessLifecycleFile`); the repository
   already carries its loop as `.harness/issue-loop.yaml` (#363 merged in `31f1bae`), and the
   interpreter starts reading it.
2. Delete `.harness/workflows.json`, the Workflows module, its rows and its migration.
3. `HarnessConfigFile` drops `wipLimit`; the loop's `x-harness.wip` is the limit, and
   `.harness/config.json`'s comment stops claiming it.
4. `LabelWatcher` swaps `IDeclaredLoopMachine` for the loop-based decision.
5. Docs: `issue-lifecycle` spec delta as written; `CONTRIBUTING.md`'s `wipLimit` sentences update.

## Open Questions

None blocking. #364 (adopting the harness in a folder without a loop) is separate work named by
the issue as deliberately not this change's.
