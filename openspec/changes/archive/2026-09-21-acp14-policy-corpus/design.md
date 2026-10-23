# Design

Dos decisiones.

## 1. El corpus se genera, never a mano

`docs/eval/corpus.json` es un artefacto derivado de lo que ya vive en las DBs del checkout y en
`.telemetry/`: para cada dispatch con snapshot (#344) un objeto `{ issue, when, machine, facts,
decision }`; para cada veredicto de gate (#336) su entrada. El generador es el **replay runner
mismo invertido**: un script que lee las fuentes y emite el JSON — reproducible, versionado, y
discutible en un diff como cualquier spec. A mano sería un corpus que envejece en la primera
semana.

Las entries de telemetría (#314) se unen por `change` con la decisión del mismo issue: el diff
de políticas puede entonces decir no solo "cuáles decisiones cambian" sino "qué cambios de la
historia reciente habrían esperado o corrido distinto". Coste desconocido sigue siendo
desconocido — cero es inventar.

## 2. El comparador ES el simulador de #344, corrido por lotes

Ni motor nuevo ni adaptación: `LoopSimulator.Replay(snapshot, candidate)` por cada entrada,
agregado en un diff tipado — `{ entries, changed: [{issue, from, to, guards}], cost: {observed,
unknown} }`. La respuesta nombra evaluador (LoopInterpreter), modelo (ninguno: decisión pura),
rúbrica (diff de guards), dataset (corpus version + hash), muestras (entrada count) — los cinco
requisitos del plano 3 aplicados a la comparación determinista que sí sabemos hacer hoy; el
plano probabilístico queda con sus cinco requisitos declarados y vacíos, sin número.

El mismo invariante de ACP-14 va en la puerta: un diff favorable (menos espera, más
elegibilidad) nunca toca `lifecycle.json` — la política cambia por PR con la comparación
adjunta en el cuerpo, como cualquier spec.
