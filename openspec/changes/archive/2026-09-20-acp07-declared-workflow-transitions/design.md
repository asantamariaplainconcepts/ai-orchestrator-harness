# Design

Three decisions, each one reading the codebaseline forced.

## 1. The effect's payload becomes `Workflow`, and the interpreter reads it as a reference

`LoopEffect(StartRun, "/aio:propose #{key}")` becomes `LoopEffect(StartRun, "aio-implement-verified")`
— same member of the same closed enum, one payload with a different meaning and a migration rule:
the file that carries a prompt where a workflow key belongs is refused with the expected shape
named (`control.loop.effect-workflow-required`), because a transition that says "run prose" is a
transitiion that says nothing the product can verify. `{key}` interpolation dies here by
demotion: the workflow's steps already interpolate `<n>` at the admission
(`step.Label.Replace("<n>", command.TaskKey)`), and the run's title already carries the task key —
so the prompt's one responsibility in the loop machine was being a string the
machine cannot check. The stub prompt passes through only `not-a-transition` runs, which never
touch the machine.

The launch plan (`LabelLaunchPlan`) carries the workflow key in its candidate instead of the
prompt: `Observed` logs the workflow it would start (`candidate for aio-implement-verified`), and
the dispatch posts it as `WorkflowId`. `Prompt` goes from the watcher's Start entirely — a
transición has nothing free-form about it.

## 2. The workflow lives three lines from the machine that names it

`aio-implement-verified` in `.harness/workflows.json`:

- step 1: `aio.implement` (the agent step — the admission demands exactly one first)
- step 2: `controlled-ci` (a gate that already exists, one-shot, `RunsHere`)
- step 3: hold (final — the admission demands it)

Both files declared, both versioned in the same checkout, both read through their own seams. The
naming convention (state `ready-for-implementation` → workflow `aio-implement-verified`) is
deliberately not a rule — the machine names its workflow per transition, explicitly, and another
repository may name a different one for the same state. What is a rule: the workflow the machine
names must exist and be controlled, or nothing launches.

The resolution happens where the repository is known: the watcher resolves against the folder's
own `IWorkflowPlanReader` (as the admission will re-resolve at start — the plan is verified twice,
by the resolver and by the admission, same as the label is read twice). Unresolvable is a typed
stop, not a fallback: `workflow-missing` logs the key it tried and waits for the next tick.

## 3. `not-a-transition` is a carry-the-task flag, not a new run kind

The run's row already has its `TaskKey`; what it lacks is whether it speaks for the loop. The
watcher's dispatch sets `IsTransition: true` on its Start (it already knows — it went through the
machine); the UI's start sets it from the same source (a task in the loop's states, without a
person typing free-form); a prompt typed by hand leaves it false — the run exists, the terminal
is a terminal, but the loop does not see it: `InFlight` counts transitions, `status:in-progress`
counts transitions and the WIP with them. This is honest labeling carried in one column, not a
new run type, not a second state machine.

## What this deliberately does not decide

- **No gate on the final candidate** (ACP-08 owns it) — the gate this adds is the plan's own
  `controlled-ci`, which #320's chain already runs and records.
- **No dynamic workflow selection** — the machine declares which workflow; the code never guesses
  by title similarity or step count.
- **No legacy migration of existing prompt-bearing lifecycle files beyond refusal with the remedy
  named** — this repository's own file migrates in the same commit; any other file is somebody
  else's checkout deciding on its own time.
