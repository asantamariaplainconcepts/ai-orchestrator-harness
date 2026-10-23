## Why

Issue #365. Three definitions of how an issue advances, and none is complete: `.harness/lifecycle.json`
says which label starts which workflow, `.harness/workflows.json` says which steps a Run takes, and
`.harness/issue-loop.yaml` — the one file that describes the whole loop, shipped by #363 — is drawn
and edited on screen but executed by nobody. Two of the three carry roughly 4,700 lines
(3,013 in the Workflows module without tests or migrations, 856 in `WorkflowsTab.tsx`, 326 in
`HarnessWorkflowFile`, 278 in `HarnessLifecycleFile`, 244 in `LabelLaunchPlan`), and #356 is the
failure mode of holding two: the writer and the validator of `workflows.json` disagree and the whole
automatism blocks. The hard part already exists — `RunLauncher.Controlled` and `ControlledPlan`
(516 lines) run steps against a fixed candidate, stop at a hold and keep each step's trace.

## What Changes

- The backend executes `.harness/issue-loop.yaml`. It is read and validated at load, always from the
  checkout, never from a worktree. An invalid loop starts nothing and names the error: unique ids,
  known `depends_on`, no cycles, every `$step.output.field` declared in that step's `output_format`,
  no `$INPUTS.x` inside a `bash` (there it is `$INPUTS_X`) and no quoted `"$step.output"` in a
  `bash`.
- Exactly four node types. `command` is an agent step (the controlled Run's agent step);
  `bash` is a fact (the gate step); `approval` applies the hold and ends the Run — a person removing
  the label is the approval, resuming at the node after the released approval; `cancel` ends the Run
  and moves the `x-harness.status` label. Any other type is refused at load.
- Routing is `depends_on` plus `when` over typed outputs, with `==`, `!=`, `<`, `>`, `<=`, `>=`,
  `&&` and `||`.
- An agent's output is typed: the result file `/aio:candidate` already uses, validated against the
  node's `output_format`. No valid output, no advance — the node fails.
- State stays in the labels (ADR-0005). A Run resumes at the node after the one whose
  `x-harness.status` is the issue's current label, or after the released approval — **and only after
  checking as a fact that the previous node left what it must leave** (for `implement`: the OpenSpec
  change exists and its branch exists), because a label set by hand is not proof the step ran
  (#361). The trace stays in the Run, as today.
- The `LabelWatcher` tick decides from the loop, not from `lifecycle.json`.
- Resuming checks the previous node's facts, not its label alone (#361: a hand-applied
  `status:ready-for-implementation` dispatched an agent into `/aio:implement` with no proposal).
- **BREAKING — deletions.** `lifecycle.json`, `HarnessLifecycleFile`, `LabelLaunchPlan` and
  `workflows.json` as a Run's plan go: the Workflows module (Postgres plans, import, export,
  `WorkflowStepForCommand`, the step editor of the AI workflows tab) and the migration that drops
  its tables. #356 closes unfixed — the file it describes stops existing. What stays: launching a
  declared command over a worktree (Commands, "Run a task here") and the loop editor of #363.
- The WIP limit moves into the loop's workflow-level `x-harness` (`x-harness: { wip: 4 }`) and out of
  `.harness/config.json`, which loses `wipLimit`.

## Capabilities

### New Capabilities
- `run-the-declared-loop`: the harness executes the loop a checkout declares in
  `.harness/issue-loop.yaml` — four node types, typed outputs, `when` routing, resumption by label
  with fact-checks, and load-time validation that refuses to start an invalid loop — replacing the
  declared machine's launch decision with the declared loop's execution.

### Modified Capabilities
- `declared-loop-machine`: the launch decision stops reading `lifecycle.json`'s state-and-guard
  machine; the tick decides from the loop — a node's `x-harness.status` and its `depends_on`
  routing instead of states with `allOf`/`noneOf` guards. The declared machine file and its
  vocabulary are deleted.
- `declared-workflow-plans`: `.harness/workflows.json` stops being a Run's plan — import, export,
  source comparison and the Postgres plans it seeds are deleted with the Workflows module. The
  step editor follows. #356's writer/validator disagreement ends by removing the file.
- `issue-lifecycle`: the WIP limit is read from the loop's `x-harness`, not from
  `.harness/config.json`; a folder without `issue-loop.yaml` automates nothing.
- `author-a-workflow`: the Workflows screen loses the step editor and the plan rows it edited; the
  loop editor remains.

## Impact

- **New**: the loop interpreter beside `RunLauncher.Controlled` in
  `src/shared/AiOrchestrator.Infrastructure/Agents/`, and a YAML parser (new dependency,
  YamlDotNet) behind `IDeclaredLoopFile`, which today only writes and gains a `Read`.
- **Changed**: `LabelWatcher` (decides from the loop), the WIP gate (reads the limit from the loop),
  `HarnessConfigFile` (loses `wipLimit`), `World.cs` (appended registrations) and
  `WorkflowsTab.tsx`.
- **Deleted**: the Workflows module, `.harness/lifecycle.json`, `.harness/workflows.json` and their
  tests; a migration drops the module's tables — this issue owns the Workflows migrations while
  open.
- **Not opened here**: adopting the harness in a folder without a loop (#364), Archon's engine (only
  its format), mid-run resume, retries, `script`/`loop`/`wait`/`prompt` nodes and sub-workflows.
