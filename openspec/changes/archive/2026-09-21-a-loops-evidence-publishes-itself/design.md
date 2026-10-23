# Design

Una decisión: **el comando publica; el producto no se entera.**

## 1. La convención es el contrato, y ya existe

El paso 4 empareja `evidence-<n>-*.png` contra el cuerpo de la issue (`evidence.ts:matchesIssue`),
donde el vendor deja las URLs `user-attachments/assets/…` cuando algo se adjuntó. Al comando no le
hace falta ninguna API de producto: **renombrar a la convención y adjuntar a la issue** es todo lo
que hay entre el fichero que Chrome escribió y el panel que ya lo lee. El comentario que `gh issue
comment --attach` escribe queda en el cuerpo de la issue — el mismo sitio que el espejo guarda
(`GetTask`) y que el paso 4 parsea. Un solo camino de publicación, y es el del vendor.

Lo que no existe — y no se inventa aquí — es atadura línea-del-check ↔ evidencia. El cuerpo del
comentario lleva las imágenes numeradas y el SHA; la línea del check referencia el comentario por
su número. Emparejar captura con frase concreta sigue siendo del paso 4 de la persona.

## 2. La app decide el endpoint, no el comando

`aspire describe` es el único origen de la URL del server — el mismo reposo que `capture.sh` ya
toma (`capture.sh` pregunta por el recurso `server` y falla con el remedio si no está). El paso 9
pregunta `aspire ps`; con el AppHost arriba usa `aspire describe` para leer el endpoint… **y no lo
necesita**: la publicación es `gh issue comment` con `--attach`, que no pasa por el producto. La
aplicación entra en este cambio por una sola razón — decidir "¿estamos en el caso bueno o en el
degradado?", y para eso basta `gh auth status` y el exit de `gh`:

- `gh auth status` falla → credencial ausente → degradado, dicho.
- `gh --attach` no existe (gh < 2.99.0) → degradado, dicho, con la versión encontrada.
- app parada → irrelevante para publicar (el vendor no es la app); **pero** `capture.sh` mismo
  exige el AppHost arriba para el set de 1440 — el paso 8 ya degrada solo si la app no está,
  porque no puede capturar lo que no corre. El NarrowSet (EndToEnd) no la necesita.

Se corrige la premisa del issue aquí: la app viva no es la publicación, es la captura. El remedio
de "app parada" lo escribe el paso 8 — captura 390-only, dicho — y el paso 9 publica lo que haya.

## 3. Sin publicar es un estado escrito, no un silencio

La línea del check gana su forma fija: `Evidence: <n> images against <short-sha>` o
`Evidence: <n> images UNPUBLISHED in .harness/evidence/ (gh <reason>)`. Quien lee la issue bajo un
hold sabe si la prueba llegó o quedó en el worktree — y el borrado del worktree no la borra:
`.harness/evidence/` está dentro del worktree, gitignorado, y ese es el trato del issue original —
lo durable es lo publicado; lo local es borrable.
