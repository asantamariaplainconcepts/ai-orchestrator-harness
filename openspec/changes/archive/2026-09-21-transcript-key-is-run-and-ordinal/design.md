# Design

Una decisión, y el precedente que la respalda.

## 1. Dejar de heredar, no extender BaseEntity

El issue planteaba dos caminos: `RunLogLine` deja de heredar, o `BaseEntity` admite claves
alternativas. **El primero.** El segundo tocaría BuildingBlocks y a todos sus herederos para
resolver un caso que ya tiene respuesta en casa: `RunDispatch` — mismo módulo, misma base SQLite —
es una entidad sin herencia que declara su clave y `ValueGeneratedNever`, y lo hace desde #329 sin
coste. La herencia aquí no aporta nada que `(RunId, Ordinal)` no sea ya: el constructor de
`RunLogLine` nunca tocó el `Id`, ninguna consulta lo lee, y el GUID v7 "que indexa bien" pierde su
razón de ser cuando la clave es compuesta de una columna que ya existía y una posición.

`IdsAreDomainGenerated` no deja de llamarse: recorre las entidades buscando `Id` de tipo `Guid`, y
las que no lo tienen no lo necesitan. La convención queda intacta para las entidades que sí la
usan — el requirement de `local-persistence` sigue verdad palabra por palabra.

## 2. La clave compuesta es el índice que ya existía

`HasKey(new { RunId, Ordinal })` en vez de `HasKey(Id)` + `HasIndex(RunId, Ordinal).IsUnique()`.
En SQLite la clave primaria COMPUESTA no es rowid-alias: EF la declara como índice único
`sqlite_autoindex` — exactamente el que ya existe hoy como `IX_run_log_lines_RunId_Ordinal`. El
ahorro es el índice del `Id`: el `sqlite_autoindex_run_log_lines_1` de 22 MB desaparece con la
columna, y el índice único con nombre pasa a ser la clave. **Un índice menos dos**:
`sqlite_autoindex_1` (22,2 MB, el de la columna `Id`) fuera; `IX_run_log_lines_RunId_Ordinal`
(22,3 MB) promovido a clave. El fichero baja ~15 %.

## 3. La migración reconstruye y copia, contando

SQLite no puede ALTER una clave primaria: la migración crea `run_log_lines_new` con la forma
nueva, `INSERT INTO ... SELECT` (sin el `Id` — ya no existe columna), borra la vieja, renombra,
y deja los índices de lectura como estaban. El contador vive en el propio chequeo del issue:
`SELECT COUNT(*)` antes y después. La tabla no se vacía ni se resetea; los ordinales ya son
únicos por run desde `RunSink`, y la migración se niega a correr si no lo son (`SELECT` de
duplicados primero) — un fallo ahí es un bug de escritura que el esquema nuevo no debe
silenciar.
