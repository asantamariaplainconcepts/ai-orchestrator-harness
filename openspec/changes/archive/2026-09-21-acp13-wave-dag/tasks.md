# Tasks

## 1. Depends leído del body

- [x] 1.1 `TaskRow` de `HarnessApi` (y `WatchedTask`) ganan `DependsOn` — parse de la sección
      `## Depends` (bullets `\`- #n\``) server-side en BuildingBlocks junto a
      `LabelLaunchPlan`; null/ausente = sin depends; truncado = no-confiable, dicho.
      req: wave-scheduling/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs,
             src/root/AiOrchestrator.Server/Watching/HarnessApi.cs
- [x] 1.2 `LabelWatchList`: función pura que dado (tasks con DependsOn, statuses) devuelve las
      elegibles — cerradura transitiva, ciclos con keys en el resultado, referencias rotas
      dicha. (Names: `WaveElegibility.Compute`.)
      req: wave-scheduling/1
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs

## 2. Orden por superficie y congelación

- [x] 2.1 Seams del body server-side (misma regla de sufijo que `seam.ts`), y orden entre
      candidatos disputados: mayor número de pares disputados primero; llegada nunca desempate.
      Sin disputa: orden del mirror.
      req: wave-scheduling/2
      files: src/shared/AiOrchestrator.BuildingBlocks/World/LabelLaunchPlan.cs
- [x] 2.2 `LabelWatcher`: ciclos/referencias rotas al log (una vez por tick); congelación tras
      doble invalidación — attention comment `FailedNeedsPerson` con motivo y exclusión del set.
      req: wave-scheduling/3
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 2.3 `Launch` procesa el conjunto elegible en el orden disputado; la reserva de capacidad
      es la existente (`TryAcquire`), sin contador nuevo.
      req: wave-scheduling/2
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs

## 3. Tests

- [x] 3.1 `WaveElegibility`: C tras solo A cerrada no corre; ciclo bloquea con keys; referencia
      rota excluye y dice; sin depends invariante; cerradura recalcula cuando B cierra.
      req: wave-scheduling/1
      files: src/tests/AiOrchestrator.InfrastructureTests/LabelLaunchPlanTests.cs
- [x] 3.2 Orden disputado: A (1 fichero compartido con B y C) sale primero; seams disjuntos no
      reordenan; parseo de Depends del body (bullets, null, truncado).
      req: wave-scheduling/1, wave-scheduling/2
      files: src/tests/AiOrchestrator.InfrastructureTests/LabelLaunchPlanTests.cs
- [x] 3.3 Watcher: dependencia no satisfecha produce cero dispatch para C (loop de aserciones de
      watcher existing pattern).
      req: wave-scheduling/1
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs

## 4. Verificación

- [x] 4.1 Con las issues REALES de esta wave: `## Depends` declarada en #347 (→ #344) y #348
      (→ #346, #347); el código real ejecutado contra sus bodies: 347 depende de [#344] y 348
      de [#346, #347] leídos exactamente; elegibilidad calculada con los statuses de hoy —
      #348 fuera (346/347 abiertas), #347 dentro (#344 done), #346 sin depends igual que hoy;
      surface leída de la sección Seam real de 348 (docs/eval/…). Ciclo y referencia rota
      cubiertos por sus tests (#x↔#y con keys en el reason; #999 no sostenida).
      req: all
- [x] 4.2 Gate verde: 15/15, 1097 tests (+11), 8 assemblies, summary corrobora 1097/1097.
      req: all
      files: (no files — the run is the evidence)
- [x] 4.3 `openspec validate "acp13-wave-dag" --strict` clean.
      req: all
      files: openspec/changes/acp13-wave-dag/
