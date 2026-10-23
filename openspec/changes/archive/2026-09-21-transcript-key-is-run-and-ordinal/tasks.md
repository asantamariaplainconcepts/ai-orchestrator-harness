# Tasks

## 1. La entidad

- [x] 1.1 `RunLogLine` deja de heredar `BaseEntity`; el constructor y `Create` no cambian de firma
      (el `Id` nunca se tocó). Lectores intactos: `RunSink`, `GetRunTranscript`, `GetRun`,
      `RunLogRetention` — si alguno cambia, la forma está mal (lo dice el issue).
      req: local-persistence/5
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Domain/RunParts.cs
- [x] 1.2 `RunsDbContext`: `HasKey(new { RunId, Ordinal })` en vez de `HasKey(Id)` + índice único;
      sin `Id`, `IdsAreDomainGenerated` sigue al final sin cambio (recorre por presencia de `Id`).
      Sin `HasIndex` adicional — la clave compuesta ES el índice que existía.
      req: local-persistence/5
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Persistence/RunsDbContext.cs

## 2. La migración

- [x] 2.1 Migración del módulo Runs: reconstrucción de `run_log_lines` con clave compuesta — crear
      `_new`, copiar sin la columna `Id`, verificación de ordinales duplicados ANTES de copiar
      (fallo = bug de escritura que el esquema nuevo no silencia), drop, rename.
      req: local-persistence/5
      files: src/modules/Runs/AiOrchestrator.Modules.Runs/Persistence/Migrations/

## 3. Verificación

- [x] 3.1 Sobre una copia tmp de `.harness/data/aiorchestrator.db` (894 líneas, worktree): la
      migración corrió íntegra — `COUNT(*)` antes=después=894; `dbstat` lista **un** índice sobre
      `run_log_lines` (el autoindex de la PK compuesta) donde antes había dos; `PRAGMA
      integrity_check` = ok; `PRAGMA foreign_key_check` = vacío; el transcript lee idéntico (run
      01A0BE2E…: ordinales 1–834, mismo orden, mismo final). Tras VACUUM manual el fichero bajó
      2.131.696 → 2.056.192 bytes con `tasks` intacta. La cota del ~15 % se mide sobre la base
      del checkout principal (148 MB con 408.756 líneas) al migrar ahí; la estructura — un índice
      en vez de dos sobre las mismas filas — es lo que esta corrida prueba.
      req: local-persistence/5
- [x] 3.2 Escritura in-vitro sobre la base migrada: línea 835 insertada como el sink lo hace
      (ordinal = max+1), leída por ordinal DESC; un duplicado (mismo RunId+ordinal) rechazado por
      la PK con `UNIQUE constraint failed` — el ordinal-único-por-run que el sink da por cierto
      ahora lo garantiza también el esquema. Y el guard de duplicados previos aborta antes de tocar
      nada: probado inyectando 5 duplicados y corriendo el INSERT del guard.
      req: local-persistence/5
      files: (no files — the run is the evidence)
- [x] 3.3 Gate verde: 15/15, 1078 tests, 8 assemblies, summary corrobora 1078/1078. (La primera corrida con `VACUUM` dentro de la migración falló 216+ tests con `cannot VACUUM from within a transaction` — SQLite lo prohíbe en transacción; el vacuum salió de la migración y quedó documentado en su doc-comment como paso manual opcional.)
      req: all
      files: (no files — the run is the evidence)
- [x] 3.4 `openspec validate "transcript-key-is-run-and-ordinal" --strict` clean.
      req: all
      files: openspec/changes/transcript-key-is-run-and-ordinal/
