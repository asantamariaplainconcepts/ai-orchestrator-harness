## ADDED Requirements

### Requirement: Un corpus versionado de decisiones reales, generado nunca escrito a mano
El repositorio SHALL mantener un corpus de decisiones e incidentes en `docs/eval/corpus.json`,
generado por un script reproducible desde las fuentes que ya existen — los snapshots de dispatch
(#344), los veredictos de gate (#336) y las entradas de telemetría (#314) — nunca a mano: un
corpus tecleado envejece en la primera semana. Cada entrada SHALL nombrar su issue, su momento,
la máquina que decidió y los facts. La unión con telemetría lleva el coste observado y dice
`unknown` donde no lo hay — nunca cero.

#### Scenario: El corpus se regenera idéntico
- **WHEN** el generador corre dos veces sobre las mismas fuentes
- **THEN** el JSON difiere a lo sumo en metadatos de generación, y cada entrada conserva su
  issue, momento, máquina y facts

#### Scenario: El coste desconocido no se inventa
- **WHEN** una entrada del corpus no tiene telemetría que unir
- **THEN** su coste dice `unknown`, no `0`

### Requirement: Comparar dos políticas es replay en lote, sin efectos
La comparación de la política vigente contra una candidata SHALL correrse como el replay de
#344 aplicado a cada entrada del corpus — cero procesos, cero escrituras, ninguna fuente
externa leída. La respuesta SHALL ser un diff tipado: cuántas entries, cuáles cambian contra
qué guards, y el coste observado/desconocido de las entries afectadas. Cada comparación SHALL
nombrar sus cinco identificadores — evaluador, modelo (ninguno: decisión pura), rúbrica,
dataset (versión + hash) y muestras — o reconoce que no hay número.

#### Scenario: Una política candidata muestra su diff
- **WHEN** se compara la máquina vigente contra una candidata sobre el corpus
- **THEN** la respuesta nombra cada entrada que cambiaría y el guard responsable, sin lanzar
  nada ni escribir a ningún proveedor

#### Scenario: Sin cinco identificadores no hay número
- **WHEN** un plano de medición no puede nombrar evaluador/modelo/rúbrica/dataset/muestras
- **THEN** no produce una cifra — el plano probabilístico del Agent queda declarado con los
  cinco requisitos vacíos, no con un número inventado

#### Scenario: Un diff favorable no toca la política
- **WHEN** la comparación muestra que la candidata mejoraría alguna métrica
- **THEN** `lifecycle.json` no cambia por ello: la política evoluciona por PR revisada con la
  comparación adjunta, y un score alto jamás anula Hold, claim o gate fallida
