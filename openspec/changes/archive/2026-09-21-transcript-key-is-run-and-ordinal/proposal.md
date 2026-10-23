# Why

La clave de una línea de transcript cuesta dos índices de ~22 MB cada uno sobre las mismas filas.
`run_log_lines` lleva `Id` (GUID v7 en TEXT) como clave primaria — herencia de `BaseEntity`, que el
constructor de `RunLogLine` ni siquiera toca — y `(RunId, Ordinal)` único al lado. Ninguna consulta
en el módulo lee `line.Id`. El fichero SQLite del checkout principal mide 148 MB; el transcript es
el 91,5 %, y 44 MB de eso son dos índices de una clave que nadie consulta, pagados en cada
inserción de las 12.773 líneas que escribe un run.

# What Changes

- **`RunLogLine` deja de heredar `BaseEntity` y su clave pasa a ser `(RunId, Ordinal)`** — lo que
  de verdad la identifica, lo que todos los lectores ya usan. El precedente está en casa:
  `RunDispatch` es una entidad del mismo módulo que no hereda y declara `EffectId` directamente,
  con `ValueGeneratedNever`. Sin `Id`, no hay GUID en TEXT, no hay autoíndice: **~22 MB fuera**.
- **El índice único existente se convierte en la clave primaria compuesta** — el índice que
  `GetRun` (cola por ordinal), `RunLogRetention` (borrado de cabeza por ordinal) y `RunSink`
  (siembra su contador del máximo) ya usan. No se añade índice nuevo; se quita el que sobra.
- **Migración de datos, no de esquema solamente**: la migración reconstruye la tabla con la clave
  compuesta y copia las filas — `COUNT(*)` idéntico antes y después. El módulo Runs reclama esta
  migración mientras el cambio esté en vuelo (regla 2 de *What collides*).
- **Lectores intactos**: `RunSink`, `GetRunTranscript`, `GetRun`, `RunLogRetention` no cambian. Si
  alguno tuviera que cambiar, la forma elegida está mal — dice el propio issue.

# Capabilities

### Modified

- `local-persistence`: el requirement de las ids producidas por el dominio gana su contrapartida —
  una entidad cuya identidad es compuesta no hereda `Id` que no significa nada; `RunDispatch` ya
  era el precedente, `RunLogLine` se le une.

# Impact

- **Code:** `Domain/RunParts.cs` (la entidad y su constructor), `Persistence/RunsDbContext.cs`
  (la clave), una migración nueva del módulo Runs.
- **No migration collisions fuera de Runs**; este change reclama las migraciones de Runs.
- **Sin cambios de API ni de pantalla**: misma respuesta, mismo transcript, mismas 408.756 filas.
- **Not in scope:** política de retención (14 días / 20.000 líneas), compresión o agrupamiento en
  bloques, `tasks` (1,2 MB), reabrir ADR-0002.
