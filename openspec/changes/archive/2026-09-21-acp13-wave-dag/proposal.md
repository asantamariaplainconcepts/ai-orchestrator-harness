# Why

`LabelWatcher` lanza una Task por tick sin noción de wave: no hay dependencias entre Tasks, no hay
conjunto elegible transitivo, y dos Tasks del mismo tick pueden disputar los mismos ficheros
compartidos. La capacidad la reserva `IControlRegistry` (ACP-04) — pero por Task, no por conflicto
de superficie. En la tirada del PoC de 24 tickets, la llegada castigó cinco veces a la misma rama:
llegar primero no es lo mismo que integrar primero.

# What Changes

- **`Depends` declarado en la issue, leído del body**: la sección `## Depends` con bullets
  nombra las tasks que deben cerrarse antes (`- #347` o `- asantamariaplainconcepts/repo#347`).
  El watcher la parsea al listar — mismo material que `parseSeam` lee en el frente, mismo
  trato de "sección dice / null es ausencia / truncado es dicho".
- **Ciclo y referencias rotas detectados al calcular**: una dependencia de una task que no
  está en el mirror es una referencia inválida — se dice, se excluye, no se ignora; un ciclo
  bloquea a todo su conjunto con el motivo en el log (y en `Launch` nunca entra).
- **El conjunto elegible es transitivo y cerrado**: una task es elegible cuando todas sus
  dependencias están cerradas en el mirror — la cerradura se calcula desde el estado completo
  del mirror, nunca incremental (lección L7: nada de "lo que vi en el tick anterior").
- **Superficie disputada decide el orden dentro del tick**: entre candidatos elegibles que
  disputan ficheros (sección Seam del body), el que más tareas del conjunto toca se lanza
  primero — la llegada NO es desempate por defecto. Seams disjuntos = sin reorden.
- **Una sola reserva**: la del registry de ACP-04; el DAG no abre contador propio. Con WIP 1
  nunca dos; con 2, dos con seams disjuntos conviven.
- **Congelación por doble invalidación**: una rama cuya evidencia (baseline del trunk) se
  invalidó dos veces se congela con motivo visible (attention comment `FailedNeedsPerson` con
  el motivo) en lugar de revalidarse indefinidamente.

# Capabilities

### ADDED

- `wave-scheduling`: dependencias declaradas, conjunto elegible transitivo, orden por
  superficie disputada, y congelación por invalidación repetida.

# Impact

- **Code:** `LabelLaunchPlan` (depends + seams del body, elegibilidad transitiva, orden
  disputado), `HarnessApi`/`WatchedTask` (leer Depends del body de `/api/tasks`), `LabelWatcher`
  (log de ciclos/referencias rotas, congelación con attention comment vía el writer de #329).
- **Sin migraciones** — todo se lee del mirror.
- **Not in scope:** fan-out ilimitado (WIP manda), exclusión por nombre de rama, DAG persistido
  (es calculado por tick desde el mirror completo).
