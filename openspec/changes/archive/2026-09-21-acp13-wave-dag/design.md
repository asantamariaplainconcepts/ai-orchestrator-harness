# Design

Tres decisiones.

## 1. `Depends` se lee del body con el trato que `parseSeam` enseñó

La sección `## Depends` con bullets (`- #347`) es el contrato — lo mismo que la sección Seam ya
es. Las tres respuestas de `parseSeam` son las de aquí: la sección dice, null es ausencia, y un
body truncado哈佛 es un `truncated` que hace la respuesta no-confiable. El parseo vive en
BuildingBlocks al lado de `LabelLaunchPlan` (que es su consumidor) — no en el frontend ni en el
 módulo Tasks: el watcher es el lector.

Las referencias van por key con el formato del propio issue (`#347`); el mirror lista la key tal
cual la task llega (`#347`), así que la igualdad es directa sobre lo que el vendor ya resolvió.
Una referencia que el mirror no sostiene es una fila con `broken` — elegibilidad cero y log, nunca
un `FileNotFoundException` del grafista mental.

## 2. El conjunto elegible se calcula desde el mirror completo, cada tick

Nada de estado entre ticks: la elegibilidad es una función pura de (tasks, depends) → eligibles.
DFS desde cada task; si encuentra un ciclo, todas las tasks del ciclo quedan inelegibles y el
motivo se loggea con las keys del ciclo (una vez por tick, no por task). Cerrada = status `done`/
`closed` en el mirror — el vocabulario que `LabelWatcher` ya lee.

## 3. Superficie disputada: la sección Seam ya lo dice

La sección Seam del body (bullets con paths) es la superficie declarada — ya existe como contrato
del.issue y el parseo más pequeño posible la lee aquí server-side (sufijo por `/`, como el
frontend). Entre los candidatos de un tick: si dos o más comparten al menos un path de seam, el
que más pares disputados tiene se lanza primero. Sin disputa: el orden del mirror. **La llegada
nunca es desempate**: la observación del PoC (5 castigos a la misma rama) es la razón de la regla.

La congelación por doble invalidación reusa el attention writer de #329: tras dos invalidaciones
de baseline (#336 ya las registra en el manifiesto), la task recibe `FailedNeedsPerson` con el
motivo y queda fuera del conjunto elegible hasta acción humana (quitar el comentario/label de
congelación es la acción).
