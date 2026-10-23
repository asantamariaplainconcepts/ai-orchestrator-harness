# Tasks

## 1. El corpus

- [x] 1.1 `docs/eval/` + generador reproducible: lee los dispatches con snapshot de la DB del
      checkout, los veredictos del sink, y une telemetría por change — emite `corpus.json`
      (entries: issue, when, machine, facts, decision, cost {observed|unknown}). Script del
      repo (node o dotnet — el más barato de mantener: node, ya hay .telemetry tooling mjs).
      req: policy-corpus/1
      files: scripts/collect-corpus.mjs, docs/eval/corpus.json, docs/eval/README.md
- [x] 1.2 Al menos una regeneración idéntica (dos corridas, diff vacío modulo metadatos) y el
      corpus commiteado con las entradas reales disponibles hoy.
      req: policy-corpus/1
      files: docs/eval/corpus.json

## 2. El comparador

- [x] 2.1 `PolicyComparison` (Runs use case): `GET /api/eval/policy` — cuerpo con la máquina
      candidata; corre `LoopSimulator.Replay` sobre cada entrada del corpus; respuesta tipada
      `{ entries, changed[], cost }` con los cinco identificadores (evaluator: LoopInterpreter,
      model: none — pure decision, rubric: guard diff, dataset: corpus hash, samples: count).
      req: policy-corpus/2
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Features/Runs/UseCases/ComparePolicy.cs
- [x] 2.2 Tests: corpus con N entries contra máquina idéntica → cero cambios; candidata que
      endurece un guard → las entries que dependen de él nombradas; coste unknown donde no hay
      telemetría.
      req: policy-corpus/2
      files: src/tests/AiOrchestrator.Modules.Runs.FunctionalTests/PolicyComparisonTests.cs

## 3. Verificación

- [x] 3.1 En vivo: corpus generado de este checkout — **7 entradas reales** (los dispatches con
      snapshot de los ticks del watcher de hoy), regeneración idéntica verificada (hash
      b4ca99ca445eb66e estable); comparación viva contra máquina candidata endurecida (noneOf
      LiveRun): 7 entradas, 7 cambios nombrados con guards [Room, Held, LiveRun], cinco
      identificadores en la respuesta, coste declarado unknown (7) — cero procesos, cero
      escrituras.
      req: all
- [x] 3.2 Gate verde: 15/15, 1099 tests (+2 PolicyComparisonTests), 8 assemblies, summary corrobora 1099/1099.
      req: all
      files: (no files — the run is the evidence)
- [x] 3.3 `openspec validate "acp14-policy-corpus" --strict` clean.
      req: all
      files: openspec/changes/acp14-policy-corpus/
