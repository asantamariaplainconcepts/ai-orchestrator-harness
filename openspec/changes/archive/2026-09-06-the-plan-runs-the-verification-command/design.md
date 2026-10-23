## Context

`WorkflowPlanStep.RunsHere` has meant *"this product executes the step rather than handing it to the
agent"* since 19 August, and nothing has ever executed one. `ImportWorkflows` files a step whose
`run` matches a `.harness/commands.json` key as `WorkflowStepKind.FolderCommand`; `WorkflowPlanReader`
turns that kind into `RunsHere: true`; and `RunPlan.cs:85` recognises it and returns
`PlanAction.Unsupported`, which stops the run. Three pieces of a mechanism with the last one missing.

The first answer written for #286 put `aspire start` into the prose of `.ai/commands/aio/implement.md`.
Everything that draft then had to invent — the address travelling in the hold's comment, `ReleaseHold`
stopping a process it had not started — followed from the product not knowing a launch had happened.
That branch (`origin/change/286`, PR #287, closed) is this change's predecessor and its measurements
are reused rather than retaken.

## Goals / Non-Goals

**Goals:**

- A project decides, in its own two files, which command shows the change working and after which step.
- The person the loop stopped for presses it from where they are already reading.
- A plan step that runs a process is over when the process is over, and its exit code is acted on.

**Non-Goals:**

- Reporting whether the application is up. Measured, decided, and argued in `proposal.md`.
- Making this a check. `.harness/commands.json` already says *"starting the app is not a statement
  about the change"*, and no exit code from this reaches any verdict.
- Reopening #94. The launcher is still a declared command; the product presses it.
- Touching any `/aio:*` prompt. A fact about one repository inside a prompt serves one step.

## Decisions

### The new port answers for a key, and the module that owns the rows implements it

```csharp
public interface IDeclaredCommandLookup
{
    Task<DeclaredCommandLine?> Resolve(
        string folderPath, string workingDirectory, string commandKey, CancellationToken ct = default);
}
```

Nothing below the modules can resolve a command key. `IFolderCommandSource` was that seam and stopped
being it at #98, which made the row the record and left `IDeclaredCommandFile` reading only the file.
So Runs asks Workspace across a port, exactly as the terminals layer *reports* an ending across
`ICheckSink` — the mirror image of the same split, and the same registration: **`WorkspaceModule.cs`,
beside `ICheckSink` at line 62, and `World.cs` gains no line.**

`null` is "cannot say", which is what `the-world-seam`'s third requirement demands. It covers an
unknown folder, an unknown key, a key with no line, and a declared directory that escapes the
checkout — because the caller does the same thing with all four: it stops and names the key.

*Alternative considered:* widening `IDeclaredCommandFile` to resolve keys. Rejected — it reads the
file, and the file is a seed somebody imports once. Resolving from it would run a line the folder's
rows no longer hold.

### The line is resolved when the run starts, not when the step is dispatched

`StartRun` already builds every `LaunchStep` up front and substitutes `<n>` there, with the reason
written beside it: *"Substituted here because this is where the task key is; the launcher types what
it is given."* The command line is resolved in the same place, for the same reason, and `LaunchStep`
carries it as **one optional field appended at the end** — absent means exactly today's behaviour.

This also settles what a run does when somebody edits the command mid-run: nothing. A run executes
the plan as it was when it started, which is what `WorkflowPlan` is already documented to be.

*Alternative considered:* resolving in the launcher at dispatch. Rejected — it puts a schema read in
the layer that owns processes, which is the line `IRunSink` and `ICheckSink` exist to hold.

### The step ends when the process ends, and the plan is driven from that ending

`PlanAction.RunHere` is appended as **5**, after `Unsupported = 4`. The enum has implicit ordinals and
is persisted through nothing, but appending is the rule either way.

`RunPlan.Idle()` advances past a resolvable `RunsHere` step and returns `RunHere` with it. The
launcher runs it through `ICommandRunner` — the seam that already spawns a folder's own commands in a
pseudo-terminal — and streams its output into the **run's own transcript**, which is where a run's
output belongs and where the person watching the run already is.

- **Zero** advances the plan exactly as an idle agent does, by asking the plan for its next move.
- **Non-zero** ends the step failed, says the step, the key and the code in the run's output, and
  stops. **Nothing retries** — nothing in this loop ever has.
- **No exit code at all** (a killed session) is treated as the non-zero case and the null is said
  rather than rounded to a number. `ICheckSink` already documents that *"null is not zero"*.

*Alternative considered:* opening a tab through `ITerminals` for the plan's step too. Rejected —
`ITerminals` has no ending to await, so the plan would have to poll a dictionary for a session to
stop being alive. `ICommandRunner` yields the exit code on its final line, which is the ending the
step is defined by. The **button** in the panel still opens a tab, because a person pressing it wants
to watch it; the two paths differ because the two situations do.

### A step the folder cannot run is refused where the plan is written

Today `ImportWorkflows` files any unmatched key as `WorkflowStepKind.Command` — an agent prompt. So a
plan naming `tests` on a folder that has not imported its commands yet becomes a line typed at an
agent, which is a plan quietly doing the wrong thing.

`WorkflowStepKind.RefusedCommand = 5` is appended, and the step's `Detail` — empty for every imported
step until now — carries **which** of the two reasons it was:

- a key that is **not dotted** and that the folder does not declare (a dotted key is a prompt:
  `.harness/workflows.json` documents both key spaces, and `aio.implement` is unambiguous);
- a key the folder declares **long-running**, which by definition has no ending for a step to wait on.

`WorkflowPlanReader` maps that kind to `RunsHere: true`, so the resolution finds nothing and the run
stops **naming the key** instead of typing it at an agent. One kind and a reason, rather than two
kinds: the run's answer is the same in both cases and only the sentence differs.

*Consequence, accepted:* the commands import must run **before** the workflow import, or every
command step is refused. That is now visible — a count in the import's own response — rather than
silently mis-filed, which is what this replaces.

### The folder declares the verification command, in the file, with no column

`.harness/commands.json` gains a fifth optional flag beside `cwd`, `kind`, `tab`, `stop` and `gate`:

```json
{ "key": "start-the-app", "name": "Start the app", "run": "…", "verify": true }
```

Read live through `IDeclaredCommandFile`, which already reads the checkout's file and already bounds
every field. **It is not imported into a row and there is no migration** — #290 holds this epic's only
Workspace migration, and the fact is about the declaration rather than about any process, so the file
is the honest place for it. The panel asks the file.

*Alternative considered:* a column on `FolderCommand`, which is what #286 proposed. Rejected here
because it serialises this change behind another ticket's migration for a boolean the file already
carries, and because a marked row would drift from a `commands.json` edited on a branch — which is
the exact drift `IDeclaredCommandFile` exists to avoid.

*Consequence, accepted:* only the checkout's file is read, never a worktree's, exactly as
`ImportCommands` does — the file is on a branch and asking a worktree would answer from whichever
branch happened to be there.

### The toggle writes the rows and the file, and it lives in Workflows

Check 2 is *"the next run of that workflow starts the app by itself"*, and a run reads its plan from
the **rows** through `IWorkflowPlanReader`. So writing `.harness/workflows.json` alone would not do
it. The toggle appends the step to the workflow's rows and then writes the file out through
`IDeclaredWorkflowFile.Write`, which is the export path that already exists.

Both halves are Workflows' — it owns the rows and it holds the file seam — so the use case is a
Workflows use case and the route is `/api/workflows/{id:guid}/workflow-step`. **It cannot use
`FolderErrors`**, which is `internal` to Workspace, so its two refusals live in `WorkflowErrors`
beside the module's own.

*Consequence, accepted:* the step is appended **last among the non-fixed steps**, before the `Done`
bookend. The plan's hold is wherever the plan already put it, and a person who wants the app up
*before* a particular hold reorders it in the workflow editor — which is the screen for that. This
change does not guess an insertion point.

### `.harness/commands.json` has a sentence that stops being true

It opens with *"Nothing here runs by itself: the Commands tab lists what this file offers and a
person chooses."* A plan step that executes a command contradicts it. The defence is that a person
still chose — they picked the workflow, and they turned the toggle on — but the sentence has to say
that rather than be left standing while the code disagrees.

## Risks / Trade-offs

- **The import ordering refuses every command step** → now counted and reported by the import rather
  than mis-filed as a prompt, which is the whole of the change to that file.
- **Pressing `Run` on a live application restarts it** → measured as leaving exactly one AppHost, so
  the label is `Run` and never `Open`. The alternative is two AppHosts over one SQLite file.
- **A failed plan step holds the run rather than failing it** → the same ending `Unsupported` has
  always produced, so a person finds the run where they already look for a stopped one. A verdict is
  not the thing being changed here.
- **`RunHere` output goes to the transcript and not to a tab** → a long build's output lands in the
  run screen. That is where somebody watching a run is; a tab would be a second place to look.

## Migration Plan

None. No schema changes. A folder that declares no `verify` command sees a panel that says so, and
every existing plan behaves exactly as it does today until somebody turns the toggle on.

## Open Questions

None. The liveness question the ticket asked to be settled by measurement is settled in
`proposal.md`, with the numbers.
