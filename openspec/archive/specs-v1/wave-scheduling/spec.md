## Purpose

A wave is what the issues themselves declare: dependencies in one section, surface in another,
eligibility computed whole from the open inbox every tick. Arrival order decides nothing a shared
surface has not already decided.

## Requirements

### Requirement: Una wave se arma desde dependencias declaradas, calculada completa cada tick
Una task SHALL poder declarar sus dependencias en su propio issue (sección `## Depends`, bullets
con la key de la task dependida). El watcher SHALL leerlas del body leído del vendor con el trato de la
sección Seam: la sección dice, la ausencia es `null`, y un body truncado hace la lectura
no-confiable dicha. Una referencia a una task que el inbox no lista SHALL excluir a la task
portadora y loggear la referencia rota. Un ciclo SHALL bloquear a todo su conjunto, con las keys
del ciclo en el log. El conjunto elegible SHALL calcularse transitivo y desde el inbox completo
de cada tick — nunca desde lo que el tick anterior vio.

#### Scenario: C depende de A y B, solo A está cerrada
- **WHEN** C declara depends en A y B, y solo A está cerrada en el vendor
- **THEN** C no es elegible — el conjunto se recalcula entero cuando B cierre

#### Scenario: Un ciclo bloquea su conjunto con el motivo
- **WHEN** A depende de B y B de A
- **THEN** ninguna de las dos es elegible y el log nombra el ciclo

#### Scenario: Una referencia que el inbox no lista
- **WHEN** C depende de `#999` y el inbox no la lista
- **THEN** C no es elegible y la referencia rota queda dicha en el log

#### Scenario: Sin depends, sin cambio
- **WHEN** ninguna task declara dependencias
- **THEN** la elegibilidad es la de hoy — status y guards, nada más

### Requirement: El orden dentro del tick lo decide la superficie disputada, no la llegada
Cuando dos o más candidatos elegibles de un tick declaran la misma superficie (paths de la
sección Seam compartidos), el watcher SHALL lanzar primero el que dispute con más tasks del
conjunto. La llegada SHALL NOT ser desempate. Seams disjuntos no reordenan nada. La capacidad
sigue siendo la reserva única de ACP-04 — el DAG no abre contador propio.

#### Scenario: Tres candidatos, uno disputa con los otros dos
- **WHEN** A toca un fichero que B y C también tocan
- **THEN** A se lanza primero y B y C quedan tras la reserva

#### Scenario: Seams disjuntos
- **WHEN** dos candidatos no comparten ningún path declarado
- **THEN** el orden es el del inbox y ambos pueden convivir bajo la reserva

### Requirement: Una rama invalidada dos veces se congela, no se revalida indefinidamente
Cuando la evidencia de una task se invalida dos veces por cambio de base (las invalidaciones que
el manifiesto de la gate ya registra), la task SHALL quedar fuera del conjunto elegible con un
attention comment tipado (`FailedNeedsPerson`) que nombra el motivo y la acción — reactivarla es
de una persona.

#### Scenario: Doble invalidación
- **WHEN** la baseline de una task se invalida por segunda vez
- **THEN** la task se congela con comentario visible y no vuelve al conjunto elegible sin
  acción humana
