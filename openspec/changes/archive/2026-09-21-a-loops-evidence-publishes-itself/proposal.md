# Why

`/aio:implement` produce evidencia y la sustituye por una afirmación. El paso 8 commitea capturas
en `docs/design/current/`; el paso 9 escribe a mano una frase sobre ellas en la línea del check.
Quien revisa recibe la prosa y no la prueba — veinticuatro líneas de check escritas a mano en la
tirada del PoC, ninguna imagen. El paso 4 de la tab Change (`evidence-from-the-issue`) ya empareja
adjuntos `evidence-<n>-*` y muestra su procedencia; el producto sabe leer lo que el loop nunca
escribe.

# What Changes

- **El paso 8 gana su segunda mitad**: lo que `capture.sh` y el NarrowSet produjeron y `git status`
  muestra como nuevo o modificado se copia a `.harness/evidence/` del worktree, renombrado a la
  convención del paso 4 — `evidence-<issue>-<screen>.png`. Sin app, sin red: ficheros locales.
- **El paso 9 publica en vez de describir**: con el AppHost vivo (`aspire describe` decide el
  endpoint, no un puerto adivinado), el comando pide al producto componer y publicar el comentario
  — o usa `gh issue comment --attach` con las imágenes embebidas, la misma puerta única que el
  vendor ofrece. El comentario lleva las imágenes y el SHA contra el que se observaron; la línea
  del check dice el número de imágenes y contra qué commit.
- **Degradación dicho**: app parada, credencial ausente o vendor que no acepta adjuntos — el
  comando termina igual, los ficheros quedan en `.harness/evidence/`, y la línea del check dice
  **que están sin publicar**. El estado que el paso 4 ya sabe dibujar cuando no hay adjuntos.
- **Sin app, el comando no la arranca**: arrancar un AppHost desde un flujo es lo que dejó un
  Postgres vivo 36 minutos.

# Capabilities

### Modified

- `land-a-change`: el paso 8/9 del comando que la mitad mecánica de `/aio:sync` hereda — la
  evidencia se publica desde el comando, la prosa del check pasa a ser número y SHA.
- `evidence-from-the-issue`: el origen de los adjuntos que el paso 4 empareja — de la mano de una
  persona, a producidos por el propio loop contra un SHA.

# Impact

- **Proceso, no `src/`**: `.ai/commands/aio/implement.md` — pasos 8 y 9. Fuente única, symlinkeada
  a `.claude/commands/`, `.opencode/command/` y `.github/prompts/`; una edición.
- `.ai/commands/aio/ship.md` hereda por referencia, como hace con el resto.
- `.harness/evidence/` ya está gitignorado y su convención de emparejamiento (`evidence-<n>-*`) ya
  vive en `evidence.ts` — nada nuevo que el producto tenga que aprender.
- Ninguna migración, ningún módulo, ningún seam nuevo.
- **Not in scope**: capturar (capture.sh sigue siendo su dueño), quitar el hold, publicar el
  transcript íntegro por defecto (retención de ACP-09).
