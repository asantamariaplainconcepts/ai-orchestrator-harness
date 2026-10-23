# Design

Dos decisiones.

## 1. La razón se deriva donde viven los datos, se compone donde se conocen los módulos

Runs ya persiste por task key: `run_dispatches` (estado `pending/unknown` = reconciliación
abierta), los eventos de control (el `hold` del recorrido), el manifiesto del merge (payload del
sink de #336, verdict `inconclusive`). Un query en Runs — `GET /api/runs/attention` — lee **lo
suyo** y devuelve filas tipadas por task key: esa es la mitad que falta en cada cola previa. La
composición con Tasks (held del mirror) no puede ser cross-module: la hace el **host**, que es el
únito sitio que conoce los dos (Program.cs lo escribió para `LabelWatcher`; el mismo argumento,
gastado la segunda vez, que es cuando un argumento se vuelve patrón).

`/api/attention` en el host lee self-origin `/api/tasks` y `/api/runs/attention` y une por
`taskKey`. El resultado es una lista de filas — cada una con su causa tipada, su SHA cuando hay, y
la acción que el frontend ofrece como **enlace** (aprobar→issue del vendor, revisar→run, etc.):
la cola navega, no autoriza.

## 2. El vocabulario no crece antes que su writer

`AttentionReason` tiene tres miembros y tres writers que los usan. Añadir ahora
`PublicationPending` o `GrantMissing` sería vocabulario sin productor — y la regla del repo
(AF: un estado sin nadie que lo escriba es una promesa vacía) es también la del propio #246:
"un enum rival" es peor que ninguno. La cola lee **estados agregados de su fuente** (dispatch
state, event kind, verdict) y los mappea al reason existente que mejor lo nombra; el día que un
writer nuevo escriba una razón nueva, el enum y la cola crecen en el mismo change que lo usa.
