# Design

Tres decisiones.

## 1. El snapshot viaja con la admission, no se re-lee

El dispatch ya lleva `PlanJson` — el plan con el que el run nació, serializado en `StartRun`
desde el payload de la admission. La máquina y los facts toman el mismo camino: `LabelWatcher`
ya tiene la `DeclaredMachine` leída por tick y compone los `LoopFacts` en `LabelLaunchPlan.For` —
lo que falta es que la `Launch` los arrastre hasta el `POST /api/runs` y `RunDispatch.Admit` los
guarde (`LoopFactsJson`, `MachineJson`).

¿Por qué guardar la máquina entera y no su hash? Porque el replay necesita poder **decidir con
la vieja** — un hash obligaría a buscar la revisión en git, y las máquinas no viven en git: viven
en el working copy, editables sin commit. El snapshot ES la revisión que decidió; su tamaño es
unos cientos de bytes y la fila del dispatch ya guarda payloads mayores. El hash se guarda
también (`MachineHash`) por si la pantalla quiere decir "esta revisión", pero la fuente del
replay es el JSON.

## 2. El simulador es una función pura detrás de un endpoint

`LoopInterpreter.Decide(machine, facts)` ya es una función sin I/O — el simulador es
`Decide(snapshot.Facts, candidateMachine)` comparado con el efecto ya registrado. Sin DI, sin
estado, sin proceso. El endpoint `POST /api/runs/{id}/control/simulate` recibe la máquina
candidata en el cuerpo (o `null` para "la viva, re-leída ahora"), admite `clock` explícito como
parámetro de los facts que el tiempo afectó (`LiveRun` no es simulable — se declara
explícitamente en la respuesta como no-evaluable, con motivo) y devuelve el diff.

**Un run sin snapshot no se simula** — 404-`no-reproducible` con la razón (runs manuales, runs
anteriores a este change). Nunca un grafo vacío: la regla del issue y la de
`dispatch-observation` (unknown ≠ vacío).

## 3. La superposición la calcula el backend, la dibuja el frontend

`GetRunControl` ya deserializa definition y events; añade `decision`: los facts, el estado
reconocido, cada guard con su valor, el efecto. `RunControlView.tsx` lo dibuja encima del
recorrido existente — sin parseo en React, la regla de ACP-12 que aquí también aplica: el
backend determina, la pantalla muestra. Los guards rechazados se muestran tachados con su valor
real — no es un contrafactual, es lo que se evaluó.
