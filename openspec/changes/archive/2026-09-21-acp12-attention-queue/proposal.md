# Why

Existen tres writers de atención que nadie agrega: el watcher escribe attention comments en la issue
(`IAttentionCommentWriter`), `ProjectDiff` publica el diff de tests del merge, y el gate del Run
deja su veredicto en la ficha de Ejecución. Quien llega a la consola no tiene una cola: abre Task
por Task. El issue #246 cerró su primera entrega (reasons tipadas); #247 la cola única — queda por
construir la fila que **agrupa por Task** con su causa y su acción.

# What Changes

- **`Reason` en el wire**: `GetRunControl`/`ListDispatchIntents` ya llevan estado por task; un
  query nuevo en Runs — `GET /api/runs/attention` — agrega **por task key** lo que Runs sabe:
  dispatch `pending/unknown` (reconciliación abierta), hold del recorrido (último evento `hold`),
  verdict `inconclusive` del manifiesto en el payload del control, y el comentario de atención no
  confirmado. Cada fila: task key/repo, causa tipada (el `AttentionReason` existente, sin
  inventar un enum rival — regla del propio #246), SHA del candidato cuando lo hay, acción
  disponible.
- **La cola compone en el host**: `/api/attention` en el Server host — el único sitio que conoce
  todos los módulos (Program.cs:45, el mismo argumento de `LabelWatcher`) — leyendo self-origin a
  `/api/tasks` (held/blocked) y `/api/runs/attention`. **El backend determina la razón; la
  pantalla la muestra** — nada de reglas en React.
- **El enum `AttentionReason` crece** donde los writers actuales ya calzan: nada — los reasons
  existentes (`HeldForReview`, `BlockedNeedsInput`, `FailedNeedsPerson`) cubren lo que hoy se
  escribe; lo nuevo no son reasons sino **estados agregados** (pending, unknown, inconclusive) que
  la cola lee de su fuente. No se añade vocabulario vacío hasta que haya un writer que lo use.
- **UI**: cola dibujada desde el diseño #260 — filas por Task con causa, acción y estado
  Loading/error/empty/success; ambos temas, teclado, 390 px.

# Capabilities

### ADDED

- `attention-queue`: una cola accionable por Task, compuesta del estado real, sin reglas en React.

# Impact

- **Code:** enum sin cambio; `GetAttention` (Runs, nuevo); `AttentionQueue` en el host
  (`Server/Attention/`); `queries.ts`/`contracts.ts`/`en.ts` bloques propios; pantalla nueva
  bajo la lista de tasks.
- **Sin migraciones** — todo se lee de lo persistido.
- **Not in scope:** autorizar desde la cola (los botones navegan a donde ya se actúa),
  notificaciones push (la cola es pull), duplicar #260's recomendador.
