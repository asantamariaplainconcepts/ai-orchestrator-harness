# ADR-0007: The gate is verified from the main checkout, not from the worktree

- **Status:** Rejected (2026-10-03, see *Measured after*)
- **Date:** 2026-09-27
- **Deciders:** andoni
- **Tags:** infra, gate, worktrees

## Context

The repository's own workflow rule is one worktree per issue, and `/aio:implement` step 7 tells an
agent to run `aspire do ci` on the branch about to merge. But the Aspire CLI resolves the AppHost by
walking up from the working directory to the nearest `.git` directory — a linked worktree keeps a
`.git` *file*, so the walk continues past it and lands on the main checkout. `aspire do ci` executed
inside `.worktrees/change-388` built and tested the main checkout's `master`, reporting its numbers
as if they were the change's (three degraded telemetry records on #388, all the same fact). An
absolute `--apphost` path does not help: the `do` subcommand refuses it ("the project argument was
not specified"). AGENTS.md documents this walk-up for `aspire.config.json` only; the whole gate
inherits it.

## Decision

We will run `aspire do ci` from the main checkout only, after fetching and checking out the change's
branch there (or after the merge), and treat a gate run from inside a linked worktree as
untrustworthy — its subject is the main checkout, whatever the caller intended. While an issue is
implemented in a worktree, the verification is the step's own commands run by hand from the worktree
(`dotnet build` + `dotnet test` with coverage over the solution), recorded as such on the issue; the
full gate runs once on the branch in the main checkout before the merge.

## Consequences

- **Positive:** one honest place for the gate; no run can silently measure `master` as if it were
  the change; the worktree convention survives unchanged.
- **Negative:** `/aio:implement` step 7 as written cannot be followed from a worktree — it must be
  amended to name this two-place verification, and every implement pays a manual equivalent until it
  is.
- **Neutral:** if the Aspire CLI grows an explicit project resolution that honours a linked
  worktree, this ADR is the one to supersede.

## Alternatives considered

- **Run the gate from the worktree anyway and read its numbers as the change's** — rejected because
  it measured the main checkout: three runs confirmed the walk-up, and a gate that measures the wrong
  tree is worse than no gate.
- **Make `AppHost.cs` itself re-derive the solution from `AppHostDirectory`** — it already does; the
  AppHost of the worktree is correct. The CLI never runs it, so no AppHost-side change can fix the
  resolution step that precedes it.

## Measured after

Rejected before it was accepted, because the walk-up it rests on did not happen when measured again.
On 2026-10-03, with Aspire CLI `13.6.0`, `aspire do ci --apphost
src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` — the relative path `AGENTS.md`
already prescribes — was run from the linked worktree `.claude/worktrees/bridge-cse_01Dhhbmc…`
(its `.git` is a file) on a branch adding `WorktreePreparationTests`, a class that exists nowhere
but that branch. The run printed that class 8 times, and every one of the 93 absolute paths in its
log was under the worktree; none named the main checkout. So the gate measured the worktree.

What #388 saw is not disproved — it may have been an older CLI, or a run without `--apphost` — but
it is not what the CLI does today with the documented command, and a rule that sends every gate to
the main checkout costs each change a checkout switch to guard against it. `AGENTS.md#the-gate`
stands as written: run it from the worktree, with the `.csproj` path. If the walk-up comes back,
the evidence to look for is a test class only the branch has, missing from the run's output.

## References

- #388 — the change that found it; `docs/process/retro-log.md`, 2026-09-27 entry.
- AGENTS.md, "From a worktree, the CLI writes `aspire.config.json` into the main checkout" — the
  documented half of the same walk-up.
