# Tasks

## 1. The pipeline's structured summary

- [x] 1.1 `StepCommand` gains an optional per-line capture callback feeding a list beside the
      logger; the test step passes it, so the `Passed!`/`Failed!` lines are in memory when the run
      ends.
      req: controlled-workflow-execution/1
      files: src/root/AiOrchestrator.AppHost/CISteps/StepCommand.cs,
             src/root/AiOrchestrator.AppHost/CISteps/DotNetSteps.cs
- [x] 1.2 After a green test run, the step parses the captured lines (assembly, pass/fail/skip
      counts), invokes `dotnet test <assembly> --list-tests` per assembly on the built tree, and
      writes `artifacts/ci-summary.json` (gitignored) beside the solution: assemblies, counts,
      full test names.
      req: controlled-workflow-execution/1
      files: src/root/AiOrchestrator.AppHost/CISteps/DotNetSteps.cs, .gitignore
- [ ] 1.3 Unit coverage of the parser: the real `Passed!` line shapes (counts, durations,
      multiline assembly names) parse to the right assembly records; garbage lines are ignored,
      never guessed.
      req: controlled-workflow-execution/1
      files: src/tests (AppHost-reachable test host or a parser extracted to testable surface)

## 2. The verification manifest

- [x] 2.1 `VerificationManifest` record in BuildingBlocks/World — run, step, task identity,
      candidate HEAD/tree/base, spec pin, resolved command, exit code, inventory (assemblies,
      counts, names, digest), verdict, baseline diff. Serialized as a `ControlTraceEntry` kind
      through the existing evidence sink.
      req: controlled-workflow-execution/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IControlledWorkflow.cs
- [x] 2.2 The verdict logic in the gate's completion path (`RunLauncher.Controlled`): exit-unknown
      → inconclusive; exit ≠ 0 → fail; no summary → inconclusive:no-summary; fewer assemblies than
      the solution's `.slnx` test projects (counted from the working directory) →
      inconclusive:assemblies-missing; else pass. Recorded once per gate step.
      req: controlled-workflow-execution/1
      files: src/shared/AiOrchestrator.Infrastructure/Agents/RunLauncher.Controlled.cs
- [x] 2.3 Tests: each verdict rule through the gate's real path with fixture summaries; the
      assemblies-missing case names the absent ones; the manifest's bindings equal the candidate
      the gate observed.
      req: controlled-workflow-execution/1
      files: src/tests/AiOrchestrator.InfrastructureTests/

## 3. Baseline by SHA and the diff

- [x] 3.1 `IPullRequestLookup` (+`GhPullRequestLookup`) gains `PublishInventory(sha, inventory)`
      and `ReadInventory(sha)` — a `test-inventory` check run carrying the JSON as output.
      req: controlled-workflow-execution/3
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IPullRequestLookup.cs,
             src/shared/AiOrchestrator.Infrastructure/Git/GhPullRequestLookup.cs
- [x] 3.2 The gate resolves its merge-base (git in the working directory it already has), reads
      the baseline check run, and computes the diff (added/lost names, digests) on pass only;
      absent baseline → `baseline: absent` recorded, verdict unchanged.
      req: controlled-workflow-execution/3
      files: src/shared/AiOrchestrator.Infrastructure/Agents/RunLauncher.Controlled.cs or its
             manifest collaborator
- [x] 3.3 The diff projects once per run-and-step through `IAttentionCommentWriter` with reason
      `verification-summary`; the manifest stays the fact.
      req: dispatch-observation/3
      files: src/shared/AiOrchestrator.Infrastructure/ (attention use), en.ts if surfaced in-app
- [x] 3.4 Tests: baseline read by merge-base with two candidates sharing the trunk fact; absent
      baseline stated; the comment publishes once.
      req: controlled-workflow-execution/3, dispatch-observation/3
      files: src/tests/

## 4. Verification

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green, with the full test count and every assembly named.

      15/15 steps. **1062 tests, 0 failures across 8 assemblies**; and the pipeline's own
      `ci-summary.json` written by the new step: 8 assemblies, 1062 passed, 1062 listed — the
      summary corroborates the run it just watched, and the numbers agree with the gate's own
      count.
      req: all
      files: (no files — the run is the evidence)
- [x] 4.2 `openspec validate "acp08-verification-manifest" --strict` clean. Validated.
      req: all
      files: openspec/changes/acp08-verification-manifest/
