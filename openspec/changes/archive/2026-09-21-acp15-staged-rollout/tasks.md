# Tasks

## 1. El modo observation

- [x] 1.1 `LabelWatcher`: modo `observation` — el cálculo completo (wave, elegibilidad, machine,
      workflow resuelto) con log por task (qué habría hecho), SIN TryAcquire/ReserveIntent/
      StartRun/worktree. La escalera documentada en el summary de la clase.
      req: staged-rollout/1
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 1.2 `PilotTask`: en `dispatch-claims`, sólo la task nombrada despacha; las demás pasan
      por el camino de observación (log, sin efecto).
      req: staged-rollout/2
      files: src/root/AiOrchestrator.Server/Watching/LabelWatcher.cs
- [x] 1.3 Docs de operación: la escalera con qué mira una persona en cada peldaño, cómo se
      sube/baja, y OFF-es-drenaje. (docs/runbooks o README sección — lo más barato que el repo
      ya sostiene.)
      req: staged-rollout/1
      files: docs/controlled-workflows.md

## 2. Tests

- [x] 2.1 `observation` con elegibles: cero api.StartRun/TryAcquire/ReserveIntent; el log
      lleva la decisión (patrón de watcher tests con HarnessApi fake).
      req: staged-rollout/1
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs
- [x] 2.2 `PilotTask` con dos elegibles: solo la nombrada lanza; la otra queda en log sin
      llamada a StartRun.
      req: staged-rollout/2
      files: src/tests/AiOrchestrator.Server.FunctionalTests/LabelWatcherTests.cs

## 3. Verificación

- [x] 3.1 El Tick real verificado por test contra el registry real en tmp (BareRegistry
      bootstrapped): `observation` con una ready elegible produce la línea 8431 y
      **cero POSTs** — ni /api/runs, ni /coordination/dispatch-intents, ni claims (Wire lo
      aserta vacío); `PilotTask` con dos ready: exactamente 1 POST /api/runs (el piloto) y la
      otra en 8431. La verificación sobre el repo vivo la hará el primer uso real de la
      escalera — el modo existe para eso y el log es el mismo 8431 que el test aserta.
      req: all
- [x] 3.2 Gate verde: 15/15, 1101 tests (+2), 8 assemblies, summary corrobora 1101/1101.
      req: all
      files: (no files — the run is the evidence)
- [x] 3.3 `openspec validate "acp15-staged-rollout" --strict` clean.
      req: all
      files: openspec/changes/acp15-staged-rollout/
