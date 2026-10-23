# Tasks

**Blocked by #451, #453, #455.** This change closes the server-side hold gap the loop's `merge`
node and `/aio:sync` already meet, and the scope/presentation gaps the PBI journey (#450/#463)
opened. Tasks 1–2 (the admission and the PBI-keyed entry) stand on their own against master;
tasks 3–4 (the journey step and the i18n) compose into `PbiSpine`, which is on master, and SHALL
NOT land before #451 (the `Loop` view where the merge appears as a step), #453 (`ISession`/
`IEnvironment`, consumed by the close-environment step at the merge's end) and #455 (the
publication frontier — *«Publication SHALL NOT merge»*) have merged, so the contracts they fix are
the ones this change reads. The branch holds until those three are in master; their merge is the
precondition for tasks 3–4, not a guess at their shape.

## 1. The hold refusal in the admission (merge-gate)

- [x] 1.1 Add a `HeldByPbi` case to `MergeGrantAdmission.GrantRefusal`, and read `IPbiSource.GetPosition` for the resolved `ControlPbiIdentity` before the grant lookup in `MergeGrantAdmission.Ask`; refuse typed `HeldByPbi` when `PbiPositionRead.Held` is true, short-circuiting the grant and vendor round-trips. A position read that fails SHALL be refused with the vendor's own words rather than read as not-held.
      req: merge-gate/The admission refuses a merge when the PBI it resolves to is held
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/MergeGrantAdmission.cs
- [x] 1.2 Inject `IPbiSource` into the `MergeChange` handler (it is already registered in `AddWorld()`), pass it into `MergeGrantAdmission.Ask`, and add the `HeldByPbi` branch to the handler's refusal switch with the remedy sentence naming the hold's release.
      req: merge-gate/The admission refuses a merge when the PBI it resolves to is held
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/MergeChange.cs, src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/MergeGrantAdmission.cs
- [x] 1.3 Test the admission's hold refusal against `IPbiSource` (held, unheld, unknown) and the short-circuit (a held PBI does not read the grant). Verify the typed reason carries through the handler's sentence.
      req: merge-gate/The admission refuses a merge when the PBI it resolves to is held
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/MergeChangeTests.cs

## 2. The PBI-keyed merge entry (merge-gate)

- [x] 2.1 Add a PBI-identity entry to `MergeChange` — a second route addressed by source, repository and key plus the folder — resolving the worktree from that identity and the folder as the launcher's detail, with the worktree cleaned up after the merge is confirmed. The folder-scoped `WorktreeId`-keyed route stays unchanged; the permission (`WorkspacePermissions.ManageWorktrees`) is unchanged.
      req: merge-gate/The merge is launched by the PBI's identity from the journey's done step
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/MergeChange.cs
- [x] 2.2 Confirm `MergeEffectId.Derive` derives from the same canonical PBI identity the admission resolved for both entries, so a merge launched by identity and one by worktree journal as one effect. No change to the derivation itself — verify, do not edit.
      req: merge-gate/The close is a named, resumable effect
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/MergeGrantAdmission.cs
- [x] 2.3 Test the PBI-keyed entry: a PBI with one worktree in flight is launched against it; a PBI with none is refused with the remedy; the effect id matches a worktree-scoped launch for the same worktree.
      req: merge-gate/The merge is launched by the PBI's identity from the journey's done step, merge-gate/The close is a named, resumable effect
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/MergeChangeTests.cs

## 3. The journey's done step (pbi-details) — blocked by #451, #453, #455

- [x] 3.1 Compose the merge's confirmation and result renderer into the `done` step of `PbiSpine`, offered by the step itself; the launch is addressed by the PBI's identity and the folder, and the worktree's path/branch are the launcher's detail. The worktree's `ChangeTab` keeps its own merge control.
      req: pbi-details/The journey's done step offers the merge with state and block motive
      files: src/frontend/features/folders/PbiSpine.tsx, src/frontend/features/folders/MergeChangeDialog.tsx
- [x] 3.2 Draw the step's state and the block motive from the admission's typed refusals (hold, grant, head, archive, tree); the launch is reachable rather than disabled, and the refusal comes when it is taken, per `merge-gate`.
      req: pbi-details/The journey's done step offers the merge with state and block motive
      files: src/frontend/features/folders/PbiSpine.tsx
- [x] 3.3 Name the absence for a PBI with no worktree in flight or no pull request, rather than offering a launch to nothing.
      req: pbi-details/The journey's done step offers the merge with state and block motive
      files: src/frontend/features/folders/PbiSpine.tsx

## 4. i18n and shared files (en.ts, block-appended)

- [x] 4.1 Add the `done` step's state, block-motive and result copy to `en.ts` in a block-appended `mergeStep.*` section beside the existing `mergeAction.*` block; no hardcoded copy in the step, and a neighbour's block is not tidied.
      req: pbi-details/The journey's done step offers the merge with state and block motive
      files: src/frontend/shared/i18n/en.ts
- [x] 4.2 If the PBI-keyed entry requires a contract change, append it next to the merge block in `contracts.ts` and `queries.ts`; the folder-scoped contract stays, and the three shared files are touched block-appended only.
      req: merge-gate/The merge is launched by the PBI's identity from the journey's done step
      files: src/frontend/shared/http/contracts.ts, src/frontend/shared/http/queries.ts

## 5. Canonical docs and gate

- [x] 5.1 Verify `docs/redesign.md` is not a sole source of any decision this change touches (it is absent at `89e7ce5a`); if a remaining sole-source reference is found in any doc, fold it into `README.md`/`ARCHITECTURE.md`/the specs before removing the doc.
      req: merge-gate/The admission refuses a merge when the PBI it resolves to is held
      files: docs/redesign.md
- [x] 5.2 Run the full gate: `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`. The run prints every test assembly in `src/AiOrchestrator.slnx`, zero failures; a smaller count is a subset, not a success.
      files: src/root/AiOrchestrator.AppHost/CISteps/CiPipeline.cs
- [x] 5.3 Live check against an authorized test PR: hold active blocks, grant invalid blocks, head change blocks; an admitted merge lands once and a repeat request observes one effect; cleanup and history recovery from History. Record what was observed in the issue's check, not the existence of tests.
      files: .harness/scripts/merge, src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/MergeChange.cs
