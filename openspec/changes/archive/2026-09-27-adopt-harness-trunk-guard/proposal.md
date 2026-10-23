## Why

"Todo trabajo vive en worktrees" es hoy costumbre, no ley: un run sin worktree trabaja sobre el trunk del folder, y nada impide commitear en `master`/`main` — ni a un agente ni a una persona en su propia terminal. Y montar el harness en otro repositorio es copiar a mano `.harness/`, nueve comandos, las skills que los nombran y cinco enlaces de runtime (#364). Este cambio hace de la regla una ley en tres capas y convierte la instalación del harness en un botón, con otro que verifica que sigue entero.

Diseño conversado y aprobado: `docs/superpowers/specs/2026-09-27-trunk-sin-commits-y-adopt-the-harness-design.md`.

## What Changes

- **Auto-worktree**: un run sin `WorktreeName` ya no trabaja en el checkout — el handler deriva `change/<task-key>` y asegura el worktree con `Ensure` (crea-o-adjacha). El checkout deja de ser un destino de trabajo.
- **Costura nueva `IRepositoryGuard`**: la app instala y repara un hook `pre-commit` en `.githooks/` de cada folder registrado (activado con `git config core.hooksPath .githooks`), que rechaza el commit directo en `master`/`main` con el remedio en la frase. Detached HEAD pasa; `AIORCH_UNGUARDED=1` es el escape explícito; un `core.hooksPath` ajeno (husky) no se pisa.
- **Reparación al registro y al arranque**: `AddFolder`/`ChooseFolder` llaman al guard (el fallo no bloquea el registro; el `Failure` va en la respuesta), y un hosted service recorre los folders en cada arranque re-instalando lo que falte.
- **Adopción del harness (#364)**: botón **Adopt the harness** en el estado vacío del tab AI workflows. Un manifiesto que viaja con el build del server declara todo lo que se copia — `.harness/`, los nueve comandos, las skills que nombran, los cinco enlaces de runtime **y `.githooks/pre-commit`**. Nunca sobrescribe, deja cambios sin commitear, el `gate` del loop llega vacío, y sin `openspec/` la respuesta lo dice.
- **Botón Verify**: caso de uso `VerifyHarness` que recorre el manifiesto contra la checkout y el guard, y devuelve una lista de checks ok/missing con remedio. No repara.

## Capabilities

### New Capabilities
- `adopt-the-harness`: copiar el harness manifestado a la checkout de un folder sin sobrescribir nada, y verificar después qué falta.

### Modified Capabilities
- `run-a-task`: el requisito "a run works in the directory the run row records" cambia — un run sin worktree nombrado deriva `change/<task-key>` en lugar de trabajar en el checkout; el checkout deja de ser un lugar donde un agente trabaja.
- `choose-a-folder`: registrar un folder instala el guard de trunk; el fallo de instalación no bloquea el registro y viaja en la respuesta.
- `the-world-seam`: dos interfaces nuevas — `IRepositoryGuard` y `IHarnessAdopter` — con implementación en Infrastructure y registro en `World.cs`.

## Impact

- **Runs**: `StartRun.cs` — bloque de `workingDirectory`, doc del param `WorktreeName`, `Run.WorktreeName` siempre lleno.
- **Workspace**: `AddFolder.cs`, `ChooseFolder.cs` (llamada al guard), use cases nuevos `AdoptHarness.cs` y `VerifyHarness.cs`, botones en `LoopGraph.tsx`.
- **Infrastructure**: `Git/GitRepositoryGuard.cs`, `Harness/HarnessAdopter.cs` + manifiesto y ficheros que copia, hosted service de reparación en Server.
- **BuildingBlocks**: `World/IRepositoryGuard.cs`, `World/IHarnessAdopter.cs`.
- **Frontend**: `LoopGraph.tsx`, `contracts.ts`, `queries.ts`, `en.ts` — bloques nuevos junto al screen dueño.
- Sin migraciones: todo son ficheros y llamadas.
- Verificado que nada existente rompe: `MergeChange` aterriza vía `gh pr merge --squash` (el commit en trunk lo hace el vendor) y `land` en el loop pushea desde el worktree; nada de la app commitea en el checkout principal.
