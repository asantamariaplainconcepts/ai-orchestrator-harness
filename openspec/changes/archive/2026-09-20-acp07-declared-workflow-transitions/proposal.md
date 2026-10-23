# Why

The watcher's dispatch (#329) sends `WorkflowId=string.Empty` — and the Runs admission it must
satisfy refuses exactly that (`Run.DispatchControlled`: "An effect identity requires a controlled
Workflow"). The machine's transition effect carries a **prompt**, not a workflow; there is no
controlled workflow that implements a transition; and the start prompt the person's UI offers
shares nothing with the watcher's door. The contract (ACP-07): a transition resolves its Workflow
explicitly, never degrades to prompt-only, and manual and automatic travel the same door.

# What Changes

- **The effect names a Workflow.** `LoopEffect`'s payload changes from a prompt string to a
  workflow key (with `{key}` interpolation staying for the legacy form nothing uses); the
  interpreter and the launch plan carry the key through; the vocabulary stays closed — a file
  naming anything else is refused, and this repository's `.harness/lifecycle.json` migrates its
  two transitions to `aio-implement-verified`.
- **A controlled workflow per transition.** `.harness/workflows.json` gains
  `aio-implement-verified` (`controlled: true`): one Agent step (`aio.implement`), the
  `controlled-ci` gate, a final Hold — the shape the admission already enforces. The state
  machine and the workflow live in the same declared family of files, three lines apart in the
  same checkout.
- **The watcher resolves the plan before it dispatches.** `HarnessApi.StartRun` is sent a real
  `WorkflowId` from the effect; a plan the reader cannot resolve is a typed `workflow-missing`
  stop for that task this tick — logged, zero writes, the next tick re-decides. Never a
  prompt-only fallback.
- **One door.** The UI's start for a loop task offers the same workflow the machine would
  launch, and a prompt-free run (a person typing instructions by hand) starts marked
  `not-a-transition`: it counts for no in-progress, no WIP, no claim — the loop does not see it.

# Capabilities

### Modified Capabilities

- `declared-loop-machine`: the effect vocabulary names workflows; the migration is explicit and
  the old prompt-carrying shape is refused naming the key it should carry.
- `dispatch-observation` / `durable-controlled-dispatch`: the dispatch sends the resolved
  workflow; `workflow-missing` is a typed stop; manual and watcher share one door.
- `declared-workflow-plans`: the controlled-execution round-trip requirement covers the new
  workflow; nothing else changes about plans.

# Impact

- **Code:** `IDeclaredLoopMachine` (+`HarnessLifecycleFile` parser and this repo's
  `lifecycle.json`), `LabelWatcher`/`HarnessApi` (workflow resolution and the typed stop),
  frontend start-dialog logic for loop tasks (offer the transition's workflow; mark
  prompt-only runs `not-a-transition` — visible flag on the run, excluded from WIP counts the
  mirror already does), `en.ts` block.
- **Specs:** deltas for the three named capabilities.
- **No migration**: everything travels in declared files and run rows that already exist.
- **Not in scope:** rewriting CLI adapters; rewriting `.ai/commands/aio/*` (the workflow's steps
  still name them); the final-candidate gates (ACP-08's); determinism.
