# Tasks

## 1. El paso 8 deja las capturas donde el paso 4 las lee

- [x] 1.1 `.ai/commands/aio/implement.md` paso 8: tras retomar y commitear el registro de diseño,
      copiar las capturas nuevas/modificadas a `.harness/evidence/` renombradas
      `evidence-<issue>-<screen>.png` (la convención que `evidence.ts:matchesIssue` empareja),
      con la lista exacta de lo copiado mostrada. Un cambio sin capturas no copia nada y no dice
      nada.
      req: the-narrow-design-set/5
      files: .ai/commands/aio/implement.md
- [x] 1.2 El paso 8 degrada dicho si el AppHost no está: `capture.sh` ya falla con el remedio; el
      texto del comando dice que ese es el comportamiento (1400 sin app, 390 siempre).
      req: the-narrow-design-set/5
      files: .ai/commands/aio/implement.md

## 2. El paso 9 publica en vez de describir

- [x] 2.1 Paso 9: con capturas en `.harness/evidence/`, componer UN comentario — las imágenes
      embebidas y el SHA corto contra el que se observaron — y publicarlo con
      `gh issue comment <n> --body-file <body> --attach <img>...`. Chequear primero `gh auth status`
      y que `gh` soporte `--attach` (2.99.0): cualquiera de los dos fallos degrada.
      req: evidence-from-the-issue/3, the-narrow-design-set/5
      files: .ai/commands/aio/implement.md
- [x] 2.2 La línea del check del issue pasa a llevar la forma fija:
      `Evidence: <n> images against <short-sha>` publicado, o
      `Evidence: <n> images UNPUBLISHED in .harness/evidence/ (<reason>)` degradado. Números y SHA,
      no adjetivos.
      req: evidence-from-the-issue/3
      files: .ai/commands/aio/implement.md
- [x] 2.3 `ship.md` hereda por referencia: una frase que apunte a los pasos 8/9 de implement.md,
      nada duplicado.
      req: the-narrow-design-set/5
      files: .ai/commands/aio/ship.md

## 3. Verificación

- [x] 3.1 Verificado en vivo, caso por caso. **Publish real**: `gh issue comment 278
      --body-file … --attach …` publicó
      [#issuecomment-5757483702](https://github.com/asantamariaplainconcepts/ai-orchestrator-harness/issues/278#issuecomment-5757483702)
      — el vendor reescribió la marca a `user-attachments/assets/67c57d98…`, y el nombre
      `evidence-278-change-evidence-390.png` es el que `matchesIssue` empareja: el comentario
      de vuelta es, byte a byte, lo que el paso 4 lee. **gh fuera de PATH**: la rama UNPUBLISHED
      ejecutada (`PATH=/usr/bin:/bin` → `UNPUBLISHED: gh is not signed in`), ficheros en
      `.harness/evidence/`, sin fallo del comando. **Copy del paso 8**: ejecutado contra un repo
      tmp con `git status --porcelain` — copió exactamente `evidence-278-folders.png` y `git
      status` quedó limpio (`.harness/` ignorado, verificado en el repo real). **Sin capturas**:
      la rama `else` dice "no captures this change" sin inventar comentario.
      req: all
      files: (no files — la corrida es la evidencia)
- [x] 3.2 `aspire do ci --apphost src/root/AiOrchestrator.AppHost/AiOrchestrator.AppHost.csproj`
      green: 15/15 pasos, 1078 tests, 8 assemblies, summary corrobora 1078/1078.
      req: all
      files: (no files — the run is the evidence)
- [x] 3.3 `openspec validate "a-loops-evidence-publishes-itself" --strict` clean.
      req: all
      files: openspec/changes/a-loops-evidence-publishes-itself/
