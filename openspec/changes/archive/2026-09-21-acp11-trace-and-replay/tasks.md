# Tasks

## 1. El snapshot viaja con la admission

- [x] 1.1 `LabelLaunchPlan.Launch` lleva los `LoopFacts` y la `DeclaredMachine` que decidieron;
      `LabelWatcher`/`HarnessApi.StartRun` los serializan en el `RunBody`;
      `DispatchCommand`/`StartRun` los reciben; `RunDispatch.Admit` guarda `LoopFactsJson`,
      `MachineJson`, `MachineHash`. Migración de Runs (este change la reclama).
      req: controlled-workflow-execution/7
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs,
             src/root/AiOrchestrator.Server/Watching/HarnessApi.cs,
             src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs,
             src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/StartRun.cs,
             src/modules/Runs/AiOrchestrator.Modules.Runs/Domain/RunDispatch.cs,
             src/modules/Runs/AiOrchestrator.Modules.Runs/Persistence/Migrations/
- [x] 1.2 Serialización del snapshot: `DeclaredMachine`/`LoopFacts` a JSON con el mismo
      `JsonSerializerOptions` del sink (una forma, un lugar); tolerante a la lectura — un JSON
      inválido en la fila es un snapshot ausente, dicho, no un 500.
      req: controlled-workflow-execution/7
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/ControlEvidenceSink.cs

## 2. La superposición en la lectura

- [x] 2.1 `GetRunControl` extiende su respuesta con `decision`: facts, estado reconocido, guards
      evaluados con valores, efecto disparado — derivado del snapshot; `null` cuando el run no
      lleva (manual o previo), y el contrato lo dice (`decision: null` + reason).
      req: controlled-workflow-execution/7, dispatch-observation/7
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/GetRunControl.cs,
             src/frontend/shared/http/contracts.ts (bloque propio),
             src/frontend/shared/http/queries.ts (bloque propio)
- [x] 2.2 `RunControlView.tsx` dibuja la decisión encima del recorrido: los guards permitidos
      activos, los rechazados tachados con su valor, el efecto, los facts. Copy en `en.ts`
      (bloque propio). Ambos temas, teclado, 390 px — la pantalla es de la red del Change tab.
      req: controlled-workflow-execution/7
      files: src/frontend/features/runs/RunControlView.tsx,
             src/frontend/features/runs/, src/frontend/shared/i18n/en.ts (bloque propio)

## 3. El simulador

- [x] 3.1 `LoopSimulator` (puro, sin DI): dado snapshot + máquina candidata → decisión vieja,
      decisión nueva, diff de guards/efectos; `LiveRun` declarado no-re-evaluable con motivo.
      req: dispatch-observation/7
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/LoopSimulator.cs
- [x] 3.2 Endpoint `POST /api/runs/{id}/control/simulate`: cuerpo con la máquina candidata
      (`null` = la viva, re-leída del folder); 404-`not-reproducible` con motivo para runs sin
      snapshot; respuesta = diff tipado.
      req: dispatch-observation/7
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/SimulateRunDecision.cs
- [x] 3.3 Tests: reproducción exacta (misma máquina → diff vacío), máquina candidata que cambia
      la transición (diff nombra el guard), run sin snapshot → `not reproducible`, cero I/O
      (ningún seam invocado — el simulador no tiene arrastrable que lo sea).
      req: dispatch-observation/7
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/

## 4. Verificación

- [x] 4.1 Verificado en vivo contra el producto corriendo (AppHost real, DB del worktree). Run
      creado con `POST /api/runs` 201 llevando `decisionSnapshot`; `GET /control` devuelve la
      decisión completa — facts (ready-for-implementation, held=false, folder=true, room=true),
      guards con valores [(Folder,true),(Room,true),(Held,true)], fired=true, effect
      aio-implement-verified; **editado `lifecycle.json` (workflow→aio-implement-CHANGED) y la
      decisión del run no se movió**; `POST /control/simulate` con la máquina viva devolvió el
      diff exacto (candidateWorkflow=aio-implement-CHANGED + guard LiveRun differido), y con
      candidata explícita reprodujo la decisión con diff LiveRun — cero procesos, cero
      escrituras al vendor. Cleanup: filas de verificación borradas, worktree change-344-live
      eliminado, lifecycle.json restaurado.
      req: all
- [x] 4.2 Gate verde: 15/15, **1083 tests (+5 DecisionReplayTests)**, 8 assemblies, summary corrobora 1083/1083.
      req: all
      files: (no files — the run is the evidence)
- [x] 4.3 `openspec validate "acp11-trace-and-replay" --strict` clean.
      req: all
      files: openspec/changes/acp11-trace-and-replay/
