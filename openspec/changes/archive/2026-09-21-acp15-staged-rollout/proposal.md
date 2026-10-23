# Why

Todas las piezas del ciclo están merged — claims CAS, despacho durable, máquina declarada, gates
con manifiesto, grants que autorizan, merge journalizado, evidencia publicada, wave con DAG,
cola de atención, corpus — y nada ha corrido un ciclo completo como producto. El watcher ya
tiene modos (`disabled`/`observe-claims`/`dispatch-claims`), pero `observe-claims` todavía
adquiere y el dispatch dispara de cero a todo: no hay un escalón que **decida y muestre sin
ejecutar**, que es el que permite encender el ciclo sobre el repositorio vivo sin efecto remoto
alguno.

# What Changes

- **El modo `observation`**: el watcher decide exactamente como en `dispatch-claims` — lee wave,
  elegibilidad, machine, claims si tocan — pero **no lanza nada**: cada decisión queda registrada
  en log con lo que habría hecho (workflow, worktree, facts). Cero Agents, cero efectos remotos,
  cero worktrees creados. La puerta entre "mira" y "hace" sigue siendo la misma llave de siempre.
- **La escalera explícita y documentada**: `disabled` → `observation` → `observe-claims`
  (adquiere sin lanzar) → `dispatch-claims` (lanza). Cada peldaño es un diff de_un solo
  comportamiento — nunca dos cosas nuevas a la vez. La doc de operación nombra qué mira una
  persona en cada peldaño y cómo se baja.
- **El informe del peldaño**: en `observation`, un tick termina imprimiendo su resumen — elegibles,
  bloqueadas y por qué, órdenes disputados — ya existente como reasons del wave (event 8430);
  este change reúne esas líneas en un solo log de fin de tick legible.
- **OFF drena**: `disabled` ya detiene nuevos despachos; los runs vivos siguen hasta acabar
  (nadie los mata, nadie borra worktrees) — se hace explícito en la doc.
- **El piloto de una Task** (peldaño real de `dispatch-claims` restringido): el watcher respeta
  además `LabelWatcher:PilotTask` — si está seteado, SOLO esa task es candidata al dispatch; el
  resto se observa como en `observation`. Una issue explícita recorre el ciclo, con el resto del
  repositorio mirando.

# Capabilities

### ADDED

- `staged-rollout`: la escalera de modos, el modo observación decide-sin-ejecutar, y el piloto
  de una task.

# Impact

- **Code:** `LabelWatcher` (modo `observation`, `PilotTask`), docs de operación
  (`.harness/config`? no — config de host), tests del watcher.
- **Sin migraciones.**
- **Not in scope:** el informe desde Runs reales (eso es el hito del cierre de la epic, con la
  wave de 3), matar procesos (OFF drena), más modos automáticos.
