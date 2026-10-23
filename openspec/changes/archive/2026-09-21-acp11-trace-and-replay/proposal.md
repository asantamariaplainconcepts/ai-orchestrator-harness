# Why

La pestaña Execution muestra el plan del Run y su recorrido — pero el recorrido habla del Run, no
de la transición de la Task que lo autorizó. Nadie puede ver, para un run lanzado por el loop:
qué guard permitió el arranque, con qué facts (`status`/`held`/`room`), sobre qué revisión de
`.harness/lifecycle.json` se decidió. Y no existe replay: re-evaluar las decisiones de un ciclo
con una política candidata requiere lanzar Agents — que es justo lo que ACP-14 (#347) necesita
como entrada y no puede pagar.

# What Changes

- **El run guarda la decisión que lo creó**: una fila nueva en `RunDispatch` (o columnas — la
  propuesta decide columna: `LoopFactsJson` + `MachineJson`, el snapshot completo del máquina que
  decidió) — lo que ya empieza a existir: `PlanJson` guarda el plan; falta la máquina y los facts.
- **`GetRunControl` gana la superposición**: junto al definition/trace actuales, la respuesta
  lleva los facts de la decisión, el estado reconocido, los guards evaluados
  (permitidos/rechazados con sus valores) y el efecto que disparó — derivado del snapshot y del
  `LoopInterpreter`, nunca de la declaración viva. **Editar `lifecycle.json` no reescribe el
  grafo de un run viejo** (ya lo promete el requirement existente para el plan; la máquina se le
  une).
- **Un endpoint de simulación** (`POST /api/runs/{id}/control/simulate`, o `LoopSimulator`):
  dado un run con snapshot y una máquina candidata (o `null` = la viva), re-evalúa la decisión
  con reloj explícito — **cero I/O de ejecución, cero escrituras al vendor**, puro
  `LoopInterpreter.Decide` sobre datos registrados. La respuesta es un diff: qué decidió la
  máquina vieja, qué decidiría la candidata, con los guards que cambiaron.
- **Falta de inputs → `no reproducible` con motivo** — un run sin snapshot (los antiguos, los
  manuales) no se simula; se dice y por qué.

# Capabilities

### Modified

- `controlled-workflow-execution`: el requirement "The stored definition and trace explain the
  Run" crece — el snapshot de la máquina y los facts de la decisión se guardan con el dispatch y
  se muestran; la superposición deriva del snapshot.

### Added

- `dispatch-observation` gana el requisito del replay: simulación cero-I/O con reloj explícito,
  diff de decisiones, `no reproducible` dicho.

# Impact

- **Code:** `RunDispatch` (columnas snapshot), `StartRun`/`DispatchCommand` (llevar la máquina y
  los facts desde `LabelWatcher` → `HarnessApi.RunBody` → admission), `GetRunControl` (leer y
  exponer), `RunControlView.tsx` (superposición), `LoopSimulator` (nuevo, puro), contracts/
  queries/`en.ts` en bloque propio.
- **Migración de Runs** — este change la reclama.
- **Watchers y launchers no cambian su flujo**: `LabelWatcher` ya lee la máquina por tick; la
  serializa al pedir el run (payload ya existe: `payload = Serialize(canonical)`).
- **Not in scope:** vista de dependencias entre Tasks (#250/#346), introspección en vivo del
  proceso, re-evaluar transcripts del Agent.
