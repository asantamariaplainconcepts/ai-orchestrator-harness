# Tasks

## 1. El query de Runs

- [x] 1.1 `GetAttention` (use case, `GET /api/runs/attention`): agrega por task key desde
      `run_dispatches` (State pending/unknown + task key + repo), eventos de control (último
      `hold` por run), y manifiestos del sink (verdict `inconclusive`, SHA del candidato).
      Cada fila: TaskKey, Provider/Host/Repository, Cause (typed), CandidateSha, RunId/EffectId
      (acción = destino). Unknowns dichos, nunca omitidos.
      req: attention-queue/1
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/GetAttention.cs
- [x] 1.2 Tests del query: dispatch pending → fila reconciliación; run held → fila revisión;
      manifiesto inconclusive → fila verificación con SHA; nada → vacía; DB fallida → unknown
      con motivo.
      req: attention-queue/1
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/AttentionQueryTests.cs

## 2. La composición en el host

- [x] 2.1 `AttentionQueue` en el Server host: `GET /api/attention` — self-origin a
      `/api/tasks` (held) y `/api/runs/attention`, unión por task key (más repo cuando
      existe). Filas tipadas; fuentes caídas → `unknown` con razón, no cola vacía.
      req: attention-queue/1
      files: src/root/AiOrchestrator.Server/Attention/AttentionQueue.cs,
             src/root/AiOrchestrator.Server/Program.cs (registro + map)

## 3. La pantalla

- [x] 3.1 `contracts.ts`/`queries.ts`/`en.ts` bloques propios: `useAttention`; fila con causa,
      SHA, botón-enlace al destino. Loading/error/empty/success; ambos temas; teclado; 390 px.
      req: attention-queue/1
      files: src/frontend/shared/http/contracts.ts, src/frontend/shared/http/queries.ts,
             src/frontend/shared/i18n/en.ts, src/frontend/features/tasks/AttentionQueue.tsx
- [x] 3.2 La cola se muestra en la pantalla Tasks (debajo del inbox o como sección propia —
      la composición menos invasiva de la pantalla existente).
      req: attention-queue/1
      files: src/frontend/features/tasks/TasksScreen.tsx

## 4. Verificación

- [x] 4.1 En vivo (AppHost real): `GET /api/runs/attention` devolvió las causas de los smoke
      runs históricos (4 run-held + 1 run-failed, cada una nombrando su runId); la composición
      `GET /api/attention` del host unió ambas fuentes con 0 unknownSources; el mirror del
      worktree no tenía helds que cruzar (la rama mirror-held quedó cubierta por su test de
      unión en `AttentionQueue`). Gate corrió además 3 tests de derivación (dispatch →
      reconciliation, held → review con runId, done → ausente).
      req: all
- [x] 4.2 Gate verde: 15/15, 1086 tests (+3 AttentionQueryTests), 8 assemblies, summary corrobora 1086/1086. Typecheck y lint del frontend limpios.
      req: all
      files: (no files — the run is the evidence)
- [x] 4.3 `openspec validate "acp12-attention-queue" --strict` clean.
      req: all
      files: openspec/changes/acp12-attention-queue/
