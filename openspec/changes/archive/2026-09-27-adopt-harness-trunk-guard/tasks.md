## 1. Costura del guard de trunk

- [x] 1.1 `BuildingBlocks/World/IRepositoryGuard.cs` — interface con `EnsureInstalled(repositoryPath)`, resultado estilo `WorktreeResult` (Ok/Failure accionable, nunca excepción); doc de la seam: el único write a la hook configuration de un repo
      req: the-world-seam/1, adopt-the-harness/2
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IRepositoryGuard.cs
- [x] 1.2 `Infrastructure/Git/GitRepositoryGuard.cs` — escribe `.githooks/pre-commit` (rechaza commit en `master|main` con remedio; detached HEAD pasa; `AIORCH_UNGUARDED=1` pasa), `git config core.hooksPath .githooks`, idempotente (re-lee y solo reescribe si difiere), chmod +x; `core.hooksPath` ajeno → `Failure` con frase, no pisa
      req: adopt-the-harness/2
      files: src/shared/AiOrchestrator.Infrastructure/Git/GitRepositoryGuard.cs
- [x] 1.3 Registro en `World.cs` en bloque propio al final
      req: the-world-seam/1
      files: src/shared/AiOrchestrator.Infrastructure/World.cs
- [x] 1.4 Tests `InfrastructureTests` sobre un repo temporal real (`git init`): instala hook + `core.hooksPath`; idempotente; repara hook borrado; respeta `core.hooksPath` ajeno; y bash sobre el mismo repo: commit en branch pasa, commit en master exit 1 con remedio, detached HEAD pasa, `AIORCH_UNGUARDED=1` pasa
      req: adopt-the-harness/2
      files: src/tests/AiOrchestrator.InfrastructureTests/RepositoryGuardTests.cs

## 2. Auto-worktree en StartRun

- [x] 2.1 `StartRun.cs` — derivar `change/<task-key>` (`#` fuera, sanitizado como el validador de `CreateWorktree`) cuando `WorktreeName` es nulo/vacío, y llamar `Ensure`; reescribir la doc del param `WorktreeName`; `Run.WorktreeName` siempre lleno
      req: run-a-task/3
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/StartRun.cs
- [x] 2.2 Tests funcionales (`Runs.FunctionalTests`): start sin `WorktreeName` → run con worktree `change/<n>` en `<folder>/.worktrees/change/<n>`; dos starts de la misma tarea reusan el worktree; `Ensure` que falla → run rechazado con la frase de git
      req: run-a-task/3
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/DerivedWorktreeTests.cs
- [x] 2.3 Revisar fixtures de runs que commitean en trunk del repo bajo test; si existen, crear branch en el setup del fixture
      req: run-a-task/3
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs

## 3. Guard al registro y al arranque

- [x] 3.1 `AddFolder.cs` y `ChooseFolder.cs` — llamar `EnsureInstalled` al persistir (solo `IsRepository`); el `Failure` va en la respuesta y no bloquea el registro
      req: choose-a-folder/1, adopt-the-harness/2
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/AddFolder.cs
- [x] 3.2 Hosted service en Server — walk de folders registrados en startup re-ejecutando `EnsureInstalled`; fallo a logs, sin retry loop
      req: choose-a-folder/1
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/TrunkGuardWalk.cs, src/modules/Workspace/AiOrchestrator.Modules.Workspace/WorkspaceModule.cs
- [x] 3.4 Tests: registro de repo con guard que falla → folder almacenado y `Failure` en respuesta (mock del guard); folder no-repo no llama al guard
      req: choose-a-folder/1, adopt-the-harness/2
      files: src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/TrunkGuardRegistrationTests.cs, src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/WorkspaceFixture.cs

## 4. Manifiesto y adopción

- [x] 4.1 Manifiesto en el build del server — lista explícita: `.harness/` (loop con `gate` vacío, tripwires, `protected-paths`), `.ai/commands/aio/`, las skills que nombran, los cinco enlaces de runtime, `.githooks/pre-commit` (mismo fichero que el guard)
      req: adopt-the-harness/1
      files: src/shared/AiOrchestrator.Infrastructure/Harness/HarnessManifest.cs, src/shared/AiOrchestrator.Infrastructure/AiOrchestrator.Infrastructure.csproj
- [x] 4.2 `BuildingBlocks/World/IHarnessAdopter.cs` + `Infrastructure/Harness/HarnessAdopter.cs` — copia ficheros del manifiesto: nunca sobrescribe (existente → saltado y nombrado), deja todo sin commitear, en la checkout y nunca en un worktree; respuesta con copiados/saltados/falta-`openspec/`
      req: adopt-the-harness/1, adopt-the-harness/2
      files: src/shared/AiOrchestrator.BuildingBlocks/World/IHarnessAdopter.cs, src/shared/AiOrchestrator.Infrastructure/Harness/HarnessAdopter.cs, src/shared/AiOrchestrator.Infrastructure/Git/GitRepositoryGuard.cs
- [x] 4.3 Registro en `World.cs` tras el del guard
      req: the-world-seam/1
      files: src/shared/AiOrchestrator.Infrastructure/World.cs
- [x] 4.4 `AdoptHarness.cs` (Workspace) — `POST /api/folders/{id:guid}/harness/adopt`, permiso `workspace:configure`; ejecuta `EnsureInstalled` después de copiar; la respuesta nombra el caso commit-sobre-master (worktree o `AIORCH_UNGUARDED=1` una vez)
      req: adopt-the-harness/1, adopt-the-harness/2
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/AdoptHarness.cs
- [x] 4.5 Tests: repo de prueba sin harness → adoptar deja exactamente los ficheros del manifiesto sin commitear; repo con ficheros existentes → intactos y nombrados; re-adopt no cambia nada; sin `openspec/` → dicho en respuesta, no creado
      req: adopt-the-harness/1, adopt-the-harness/2, adopt-the-harness/3
      files: src/tests/AiOrchestrator.InfrastructureTests/HarnessAdoptionTests.cs, src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/AdoptHarnessTests.cs, src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/WorkspaceFixture.cs

## 5. Verify

- [x] 5.1 `VerifyHarness.cs` (Workspace) — `GET /api/folders/{id:guid}/harness/verify`, permiso `workspace:read`; recorre manifiesto + guard: ficheros, cinco enlaces, hook instalado, `core.hooksPath`, `openspec/`, `gate` no vacío; cada check ok/missing con remedio; escribe nada
      req: adopt-the-harness/4
      files: src/modules/Workspace/AiOrchestrator.Modules.Workspace/Features/Folders/UseCases/VerifyHarness.cs, src/shared/AiOrchestrator.Infrastructure/Harness/HarnessAdopter.cs, src/shared/AiOrchestrator.BuildingBlocks/World/IHarnessAdopter.cs
- [x] 5.2 Tests: todo presente → lista ok; fichero borrado + hook desinstalado → nombrados con remedio; verify no escribe nada (mtime/hash antes y después)
      req: adopt-the-harness/4
      files: src/tests/AiOrchestrator.InfrastructureTests/HarnessAdoptionTests.cs, src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/VerifyHarnessTests.cs, src/tests/AiOrchestrator.Modules.Workspace.FunctionalTests/WorkspaceFixture.cs

## 6. UI

- [x] 6.1 `LoopGraph.tsx` — botón Adopt the harness en el estado vacío; botón Verify siempre visible; listas de checks con remedio; kit.tsx + tokens, sin branching por tema
      req: adopt-the-harness/1, adopt-the-harness/4
      files: src/frontend/features/folders/LoopGraph.tsx
- [x] 6.2 `contracts.ts`, `queries.ts` — tipos y queries de adopt/verify junto al bloque del screen dueño (regla 3 de colisiones)
      req: adopt-the-harness/1, adopt-the-harness/4
      files: src/frontend/shared/http/contracts.ts, src/frontend/shared/http/queries.ts
- [x] 6.3 `en.ts` — copy nuevo en el bloque del screen dueño, agrupado por pantalla
      req: adopt-the-harness/1, adopt-the-harness/4
      files: src/frontend/shared/i18n/en.ts
- [x] 6.4 Verificar con la app real (`aspire start --isolated`): adoptar en un repo de prueba, ver skips, verify, borrar fichero, verify de nuevo
      req: adopt-the-harness/1, adopt-the-harness/4
      files: src/frontend/features/folders/LoopGraph.tsx

## 7. Gate

- [x] 7.1 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj` — verde completo, el count de assemblies completo
      files: openspec/changes/adopt-harness-trunk-guard/tasks.md
