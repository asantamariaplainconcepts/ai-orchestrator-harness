# Tasks: label-sits-the-proposal

## 1. The loop declares the hop

- [x] 1.1 `.harness/issue-loop.yaml` — `refine` gains `x-harness: { status: spec-ready }` with the
      comment that says what it means; `claim` loses its `x-harness: { status: in-progress }` (the
      bash body, the branch push, stays)
      req: run-the-declared-loop/Resumption checks the previous node's facts, not its label alone
      files: .harness/issue-loop.yaml
- [x] 1.2 Verify against reality: `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      — the modified yaml parses and validates (`LoopReader` tests, `LabelWatcherTests`,
      `LabelLaunchPlanTests` all green, every test assembly counted)
      req: run-the-declared-loop/An invalid loop starts nothing and names the error
      files: .harness/issue-loop.yaml

## 2. The commands write and read the label

- [x] 2.1 `.ai/commands/aio/refine.md` — on success apply `status:spec-ready` (never
      `status:ready-for-proposal` again); gaps keep going to `status:needs-refinement`. Create the
      label at the vendor first if it does not exist (`gh label create status:spec-ready`), so the
      write cannot fail on an unknown label
      req: issue-lifecycle/Six commands drive one issue from refined to retro, and none of them removes a hold
      files: .ai/commands/aio/refine.md
- [x] 2.2 `.ai/commands/aio/propose.md` — the gate requires `status:spec-ready`, accepting
      `status:ready-for-proposal` as migration for issues refined before this change; the remedy
      named on refusal is `/aio:refine`
      req: issue-lifecycle/Six commands drive one issue from refined to retro, and none of them removes a hold
      files: .ai/commands/aio/propose.md
- [x] 2.3 Verify against reality: on this branch's own issue #399 — remove and re-apply
      `status:spec-ready` with `gh`, confirm the label shows in `gh issue view`
      req: issue-lifecycle/Six commands drive one issue from refined to retro, and none of them removes a hold
      files: .ai/commands/aio/refine.md

## 3. The inbox derives the seventh state

- [x] 3.1 `LoopStatus` (`MirroredTask.cs`) gains `SpecReady = 3` between `ReadyForProposal` and
      `ReadyForImplementation`, renumbering the rest — enum values are derived-not-stored, so
      renumbering is safe
      req: task-status-and-holds/A status is derived from the labels by precedence, and holding is orthogonal
      files: src/modules/Tasks/AiOrchestrator.Modules.Tasks/Domain/MirroredTask.cs
- [x] 3.2 `ListTasks.cs` — the label map gains `["status:spec-ready"] = LoopStatus.SpecReady`; the
      kebab-case spelling at the response edge gains `spec-ready`
      req: task-status-and-holds/A status is derived from the labels by precedence, and holding is orthogonal
      files: src/modules/Tasks/AiOrchestrator.Modules.Tasks/Features/Inbox/UseCases/ListTasks.cs
- [x] 3.3 Update `LabelLaunchPlanTests`-adjacent coverage where the six are enumerated:
      `ListTasks` derivation tests for spec-ready alone, spec-ready with a further label (loses),
      and spec-ready with an earlier one (wins)
      req: task-status-and-holds/A status is derived from the labels by precedence, and holding is orthogonal
      files: src/tests/AiOrchestrator.Modules.Tasks.FunctionalTests

## 4. The board renders the lane

- [x] 4.1 `contracts.ts` — `TaskStatus` gains `"spec-ready"` after `"ready-for-proposal"` (append
      beside the task screen's block; the three shared files are append-only neighbours)
      req: task-status-and-holds/A status is derived from the labels by precedence, and holding is orthogonal
       files: src/frontend/shared/http/contracts.ts
- [x] 4.2 `TasksScreen.tsx` — the lane list, `statusLabel`, `statusIcon` (CircleDot-dotted ramp:
      reuse the ramp comment, pick the glyph the ramp suggests at position three), `statusColor`
      (neutral-to-info midpoint), `command` (`tasks.do.propose`) and `workflowFor` (`/aio:propose`)
      gain their `spec-ready` entries
      req: task-status-and-holds/A status is derived from the labels by precedence, and holding is orthogonal
      files: src/frontend/features/tasks/TasksScreen.tsx
- [x] 4.3 `en.ts` — `tasks.status.specReady` copy (grouped with the task screen's block), and the
      lane count comment six → seven where it stands
      req: task-status-and-holds/A status is derived from the labels by precedence, and holding is orthogonal
      files: src/frontend/shared/i18n/en.ts
- [x] 4.4 Verify against reality: `aspire start --isolated --apphost
      src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`, and on #399 (carrying
      `status:spec-ready` after task 2.3's re-apply): the board shows a seven-column grid, #399 in
      the spec-ready lane, its button says Propose and selects `/aio:propose`; `aspire stop`
      req: task-status-and-holds/A status is derived from the labels by precedence, and holding is orthogonal
      files: src/frontend/features/tasks/TasksScreen.tsx

## 5. The gate

- [x] 5.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      passes on what is about to be merged — the count printed is every assembly in
      `src/AiOrchestrator.slnx`, zero failures — and `openspec validate label-sits-the-proposal
      --strict` passes
      req: run-the-declared-loop/An invalid loop starts nothing and names the error
      files: .harness/issue-loop.yaml
- [x] 5.2 `gh pr checks` reports zero checks on the draft PR this change opens, and the check line
      on the issue is filled with what was observed, in numbers — assemblies counted, lanes seen,
      the label re-applied
      req: issue-lifecycle/GitHub owns the state of the work and the repository owns facts about the code
      files: .ai/commands/aio/propose.md
