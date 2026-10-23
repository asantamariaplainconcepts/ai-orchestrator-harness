# Tasks

## 1. The expected head on the seam

- [x] 1.1 `IPullRequestMerge.Squash(path, number, expectedHead)`: `GhPullRequestLookup` reads
      the PR's `headRefOid` before the request (Fields grows it), refuses typed
      `candidate-changed` with both heads, merges at the vendor, and on an uncertain response
      re-reads the PR — `MERGED` + expected head = confirmed by observation, never a second
      request.
      req: merge-gate/6
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IPullRequestLookup.cs,
             src/shared/AiOrchestrator.Infrastructure/Git/GhPullRequestLookup.cs
- [x] 1.2 Tests of the seam's outcomes with the `gh` stub pattern the file already uses:
      head-divergence refusal pre-request, lost-response-settled-by-read, vendor refusal
      pass-through.
      req: merge-gate/6
      files: src/tests/AiOrchestrator.InfrastructureTests/

## 2. The grant admission in MergeChange

- [x] 2.1 Task resolution reuse for the worktree (its own resolution path), canonical grant read
      via `IControlRegistry.Grants(task, now)`, and the typed refusals: `grant-missing`,
      `grant-expired`, `grant-revoked`, `candidate-changed`, each naming the
      approve-candidate remedy. The expected head passed to the seam is the grant's candidate.
      A folder declaring no registry pin has no grants and no merge — the claims' own opt-in.
      req: merge-gate/4, merge-gate/5, controlled-workflow-authority/2
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/MergeChange.cs
- [x] 2.2 Functional tests of each refusal against wired registries (the same bare-registry
      fixtures the watcher tests use), and of the successful path: live grant, expected head,
      one merge, typed response.
      req: merge-gate/4, merge-gate/5
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/

## 3. The journaled close

- [x] 3.1 The merge effect in the Coordination journal: operation `merge`, effectId derived from
      task identity + `merge`, recorded pending before the merge request, settled started/failed
      after response-or-observation. `MergeChange` wraps its existing resumable steps in the
      journal's vocabulary — the cleanup failure rows persist visibly pending.
      req: merge-gate/7
      files: src/modules/Workspace/.../MergeChange.cs,
             src/modules/Coordination (operation vocabulary + reserve/outcome reuse)
- [x] 3.2 `/aio:sync`'s mechanical half consumes the endpoint: the prompt keeps the person's
      steps (hold-release, archive, retro, milestone decision) and stops narrating git merge
      mechanics it no longer performs.
      req: merge-gate/6
      files: .ai/commands/aio/sync.md
- [x] 3.3 The retry-resume test: merge landed + cleanup failed + retry → re-entry reads MERGED,
      resumes cleanup, merges nothing.
      req: merge-gate/7
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/

## 4. Verification

- [x] 4.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green, with the full test count and every assembly named.

      15/15 steps. **1078 tests, 0 failures across 8 assemblies** (16 over #336's 1062); the
      pipeline's own `ci-summary.json` corroborates: 8 assemblies, 1078 passed, 1078 listed. New:
      5 admission tests + 5 journal/resume tests (Workspace), 6 expected-head/observation tests
      (Infrastructure).
- [x] 4.2 `openspec validate "acp10-verified-merge" --strict` clean. Validated.
