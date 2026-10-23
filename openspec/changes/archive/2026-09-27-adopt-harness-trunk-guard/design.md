## Context

El spec conversado (`docs/superpowers/specs/2026-09-27-trunk-sin-commits-y-adopt-the-harness-design.md`) nació de un grilling de tres preguntas (a quién alcanza la regla, dónde para a una persona, qué pasa con un run sin worktree) y fusionó dos esfuerzos: la regla de trunk y #364. Los hechos del código que lo sostienen:

- `StartRun.cs` acepta `WorktreeName` nulo y hoy significa "el agente trabaja en el checkout y ve el trabajo sin commitear" — el bloque de `workingDirectory` alrededor de la línea 318.
- `IWorktreeManager.Ensure` ya tiene la semántica crea-o-adjacha que el segundo run de una tarea necesita (documentada en la propia interface: la propuesta y la implementación de un mismo cambio deben aterrizar en el mismo worktree).
- Nada de la app commitea en el checkout principal: `MergeChange` aterriza con `gh pr merge --squash` a través de `IPullRequestMerge`, y el nodo `land` del loop pushea desde el worktree.
- `#363` ya marcó pasos vacíos en el editor del loop — la adopción copia el `gate` vacío y el editor existente lo exige rellenar.
- `World.cs` es append-only por bloques (regla 1 de colisiones de AGENTS.md).

## Goals / Non-Goals

**Goals:**

- Ningún agente trabaja jamás en el trunk: un run sin worktree nombrado deriva `change/<task-key>` y lo asegura.
- Ningún commit directo en `master`/`main`, para nadie: agente, terminal de la app, o persona en su terminal propia.
- Adoptar el harness completo — loop, comandos, skills, enlaces y la regla — en un botón, y verificar que sigue entero en otro.
- La app instala y repara la regla: al registrar el folder, en cada arranque, y al adoptar.

**Non-Goals:**

- No se configura branch protection en el vendor.
- No se bloquea `git checkout` — git no ofrece hook para ello; lo bloqueado es el commit directo en trunk. Los merge commits automáticos (`git pull`) no pasan por `pre-commit` y se aceptan.
- No se actualiza un harness ya adoptado (lo excluía el propio #364).
- Verify no repara nada.
- No se bloquean terminales en el checkout principal: se bloquea el commit, no la lectura ni el merge.

## Decisions

### 1. El hook es `pre-commit` en `.githooks/`, no en `.git/hooks/`

Dentro del repo, activado con `git config core.hooksPath .githooks`. Versionable, visible en diffs, y heredado por cada worktree — git comparte config entre worktrees del mismo repositorio y un `core.hooksPath` relativo se resuelve desde el root de cada worktree. La alternativa, `.git/hooks/`, es invisible al diff, se pierde al re-clonar y no viaja con las ramas.

El hook rechaza cuando `git symbolic-ref --short HEAD` resuelve a `master|main`, con el remedio en la frase (`git worktree add … -b change/<n>`). Detached HEAD pasa — rebase y cherry-pick en un worktree son trabajo legítimo; la regla bloquea la rama, no los estados intermedios. `AIORCH_UNGUARDED=1` es el escape: verboso a propósito, no un toggle silencioso. Trunks con otro nombre (`trunk`, `develop`): el hook es texto en el repo, editable — `master|main` es la convención por defecto, no una lista configurable desde la app.

Alternativa descartada: prohibir el checkout en la app. Git no expone `pre-checkout`, y vigilar cada terminal es una carrera perdida; el commit es el gesto que la regla prohíbe y `pre-commit` es el único hook duro que existe.

### 2. La regla vive en una costura: `IRepositoryGuard`

Interface en `BuildingBlocks/World/`, implementación `GitRepositoryGuard` en `Infrastructure/Git/`, registro en `World.cs` en bloque propio al final. `EnsureInstalled(repositoryPath)` es idempotente y auto-reparable: lee el hook existente, lo reescribe si falta o difiere, hace chmod +x; si ya está correcto no toca nada. Devuelve Ok/Failure con frase accionable, nunca excepción — el estilo `WorktreeResult`.

Un `core.hooksPath` ajeno (husky, lefthook) **no se pisa**: se devuelve fallo con la frase "this repository already delegates hooks to X — move that hook's trunk check into .githooks/pre-commit". Respetar la configuración existente de una persona es la mejor opción de las malas.

Quién la llama: `AddFolder` y `ChooseFolder` al persistir (el fallo **no bloquea** el registro — no instalar el hook es un hecho sobre la máquina, no una razón para no tener el folder en el inbox), y un hosted service en Server que recorre los folders registrados en cada arranque. Un fallo de reparación queda en logs, no en un loop.

### 3. El auto-worktree deriva, no exige

Un run sin `WorktreeName` deriva `change/<task-key>`: el `#` fuera (`#482` → `change/482`), sanitizado con las mismas reglas del validador de `CreateWorktree` (sin `..`, sin prefijo `-`; carácter no válido → `-`). El branch derivado va a `Ensure` con `BaseRef ?? "HEAD"` — nada se rechaza, nadie tiene que elegir. El segundo run de la misma tarea aterriza en el worktree que el primero hizo, que es exactamente por lo que `Ensure` existe como llamada distinta de `Create`.

`Run.WorktreeName` queda siempre lleno; la respuesta no cambia de forma porque el frontend ya pinta `worktree <name>` y `WorkingPath` cuando existe. El campo de la Request sigue: quien nombra branch explícito (watcher, `/aio:*`) lo mantiene.

Fallos de `Ensure` → run rechazado con `RunErrors.WorktreeRefused` y la frase de git pasada por `Explain`. Mismo camino que hoy.

### 4. El manifiesto es la única lista de lo que se adopta — y el hook es uno más

Un `HarnessManifest` en el código, junto a los ficheros que copia, declara todo: `.harness/` (loop — con el `gate` vacío —, tripwires, `protected-paths`), `.ai/commands/aio/` (nueve comandos), las skills de `.ai/skills/` que esos comandos nombran, los cinco enlaces de runtime apuntando a `.ai/`, y `.githooks/pre-commit` — el mismo fichero que la capa 2 escribe. Viaja con el build del server. No se deduce recorriendo carpetas: una lista derivada es una lista que discrepa.

`IHarnessAdopter` (misma estructura de costura que el guard) escribe los ficheros: nunca sobrescribe (existente → saltado y nombrado en la respuesta), deja todo sin commitear, escribe en la checkout y nunca en un worktree. Adoptar ejecuta también `EnsureInstalled` — el folder adoptado nace con la regla.

Adoptar sobre master deja ficheros sin commitear que el hook recién instalado bloquearía commitear ahí: el orden de la adopción (copiar primero, `EnsureInstalled` después) hace que el caso normal sea que el primer commit de la propia adopción se haga con `AIORCH_UNGUARDED=1` una vez, conscientemente — o desde un worktree. La respuesta del botón lo nombra.

### 5. Verify informa, no repara

`GET /api/folders/{id}/harness/verify` recorre el manifiesto contra la checkout y el guard: cada fichero presente, los cinco enlaces resueltos, el hook instalado y `core.hooksPath` apuntando bien, `openspec/` existe, `gate` del loop no vacío. Cada check ok/missing con remedio — el remedio siempre es "adopta de nuevo" o "instala el hook", porque reparar ya es trabajo de la adopción y del arranque. Un botón que toca nada no necesita deshacer nada.

## Risks / Trade-offs

- [El hook solo cubre el repo donde está instalado, y `git commit --no-verify` lo salta] → Es la regla dura que git ofrece, no una coraza. Branch protection en el vendor es el otro extremo y queda fuera; la frase del remedio y el escape verboso hacen la intención explícita.
- [Un `core.hooksPath` ajeno deja el folder sin regla] → El guard lo devuelve como `Failure` visible (en la respuesta del registro y en logs de arranque), no lo pisa en silencio.
- [Derivar `change/<task-key>` colisiona con branches que ya existen con otro contenido] → `Ensure` adjacha al branch existente, que es el comportamiento buscado para la acumulación; si el path está ocupado por algo que git no lista como worktree, el run se rechaza con la frase de git.
- [Fixtures de tests que hoy commitean en trunk del repo bajo test] → El gate lo dirá; si existe alguno, el ajuste es del fixture (crear branch en el setup), no de la regla.
- [La copia de skillsidentifica mal qué comandos nombran] → El manifiesto es una lista explícita con dueño (el build), no una deducción en runtime; revisarla es un diff.

## Migration Plan

Sin migraciones — todo son ficheros y llamadas. Despliegue: nada cambia hasta que alguien adopta o abre el dialogo de start; el hosted service instala el guard en folders ya registrados en el primer arranque tras la actualización. Rollback: revertir el commit; el hook instalado en folders es texto inerte sin la app detrás (y `AIORCH_UNGUARDED=1` lo neutraliza sin tocar nada).

## Open Questions

Ninguna — el grilling cerró: sujeto (todos), dónde (pre-commit local), runs sin worktree (auto-crear), terminales en trunk (permitidos, commits bloqueados), instalación (la app instala y repara), y todo a la vez con #364 + Verify.
