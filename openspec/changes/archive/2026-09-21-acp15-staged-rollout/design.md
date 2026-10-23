# Design

Una decisión: **la escalera reusa la llave que ya existe.**

`LabelWatcher:Mode` ya gobierna el watcher (disabled/observe-claims/dispatch-claims). El modo
`observation` es el peldaño que falta entre `disabled` y `observe-claims`: ejecuta TODO el
cálculo (wave, elegibilidad, orden, incluso `TryAcquire` NO — eso ya es un efecto de
escritura remota: el claim CAS escribe en el registry). Dónde poner el corte exacto:

- **Decide**: tasks, wave, machine, guards, workflow resuelto, worktree propuesto.
- **NO ejecuta**: `TryAcquire` (escribe registry), `ReserveIntent` (escribe journal),
  `StartRun` (crea run+worktree), obviamente nada de dispatch.

Cada candidato decide en log (una línea por task: qué habría hecho y con qué workflow) —
el observador encendido sobre el repo vivo produce un registro por tick que una persona lee y
compare contra su intención. `PilotTask` recorta en `dispatch-claims`: solo la task nombrada
pasa de decidir a hacer; el resto sigue en el estado de observación (log, no efecto).

OFF (`disabled`) ya es drenaje por diseño: un tick sin despachos no toca los vivos. Se documenta.
