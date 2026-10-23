## ADDED Requirements

### Requirement: La activación es una escalera, y cada peldaño añade un solo comportamiento
El watcher SHALL gobernarse por una escalera de modos: `disabled` (nada nuevo; lo vivo drena a
su fin — nadie mata procesos ni borra worktrees), `observation` (todo el cálculo, ninguna
ejecución), `observe-claims` (adquiere sin lanzar), `dispatch-claims` (lanza). Cada peldaño
superior SHALL diferir del anterior en exactamente un comportamiento. En `observation`, cada
tick SHALL registrar las decisiones que habría tomado — elegibles, bloqueadas con su motivo, el
orden disputado y el workflow que habría arrancado — sin crear claim, intent, run ni worktree.

#### Scenario: Observation decide sin ejecutar
- **WHEN** el watcher corre en `observation` con tasks elegibles
- **THEN** el log nombra cada decisión que habría tomado y ningún Agent, claim, intent o
  worktree se crea

#### Scenario: OFF drena
- **WHEN** el modo es `disabled` y hay runs vivos
- **THEN** no se despacha nada nuevo y los vivos siguen hasta su fin — sin matar procesos ni
  borrar worktrees

#### Scenario: Cada peldaño añade uno
- **WHEN** se sube un peldaño
- **THEN** exactamente un comportamiento nuevo aparece, y volverse a bajar lo quita

### Requirement: El piloto de una Task es la única Task que pasa de decidir a hacer
En `dispatch-claims` con `LabelWatcher:PilotTask` en la configuración, SOLO la task nombrada
SHALL despacharse; las demás tasks elegibles SHALL tratarse como en `observation` — decididas
en log, sin efecto. Una vez retirada la clave de piloto, el comportamiento es el del peldaño
completo.

#### Scenario: PilotTask recorta el dispatch
- **WHEN** `PilotTask` nombra `#350` y `#350` y `#351` son elegibles
- **THEN** `#350` se despacha y `#351` queda observada en log, sin efecto

#### Scenario: Sin PilotTask, el peldaño completo
- **WHEN** la configuración no declara `PilotTask`
- **THEN** el dispatch es el del peldaño `dispatch-claims` sin recorte
