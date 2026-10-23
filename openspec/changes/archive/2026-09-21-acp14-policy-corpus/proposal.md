# Why

La política (`lifecycle.json`, el graph de guards) ha cambiado tres veces sin medir contra las
anteriores: cada cambio es fe razonada. El replay de #344 ya puede re-evaluar decisiones
registradas contra una máquina candidata — cero procesos, cero escrituras — pero no hay un
**corpus**: un conjunto versionado de decisiones reales contra el cual comparar una política
candidata y ver qué cambiaría. La telemetría de #314 lleva coste real desde hace 30+ entradas;
los adversarios de la epic (#321) viven dispersos como tests que no se leen como corpus.

# What Changes

- **`docs/eval/` como corpus versionado**: un índice (`corpus.json`) con las decisiones e
  incidentes reales — cada entrada un objeto `{ issue, when, machine-revision, facts, decision }`
  derivable de lo que ya existe: los snapshots de dispatch (#344), los veredictos de gate
  (#336) y las entries de telemetría. El generación se hace con un script reproducible
  (`scripts/collect-corpus.mjs` o equivaliente — decide la propuesta), nunca a mano.
- **El comparador**: `LoopSimulator` de #344 ya es el motor — este change añade el corredor por
  lotes: para cada entry del corpus, replay contra la política vigente y contra la candidata,
  diff agregado (cuántas decisiones cambian, cuáles y por qué guard).
- **Los tres planos separados y medidos por separado**: invariantes del motor (los adversarios
  de #321 ya existen como tests — este change los nombra y los indexa, no los duplica),
  aceptación/journeys (el corpus de decisiones), calidad probabilística del Agent (fuera de
  alcance hoy: sin evaluador/rúbrica/dataset no hay número — declarar los cinco requisitos y
  parar).
- **Un score alto nunca anula Hold, claim o gate fallida** — la comparación informa, la política
  sigue siendo PR revisada con la comparación adjunta (diff de decisiones + coste de las
  entries afectadas desde #314).

# Capabilities

### ADDED

- `policy-corpus`: corpus versionado de decisiones, comparación de políticas sin efectos, y
  los tres planos separados.

# Impact

- **Code:** `LoopSimulator` reuse (Runner), un endpoint o CLI script para correr la
  comparación (`GET /api/eval/policy?candidate=...` — decide), un test de contrato del corpus
  (entrada válida, diff fundado).
- **Docs:** `docs/eval/corpus.json` + README del corpus.
- **Sin migraciones.**
- **Not in scope:** eval continuo en cada push, leaderboard, agentes cambiando su política
  activa, plane 3 (calidad probabilística) — sus cinco requisitos quedan declarados vacíos.
