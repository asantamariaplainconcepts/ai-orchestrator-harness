## MODIFIED Requirements

### Requirement: Una cola accionable por Task, compuesta del estado real
El sistema SHALL exponer una cola de atención agregada por task key: cada fila nombra la causa
tipada de su bloqueo, el SHA del candidato cuando existe, y la acción disponible como destino
verificable. La causa SHALL derivarse del estado persistido — dispatch `pending`/`unknown` del
journal, hold del recorrido del run, verdict `inconclusive` del manifiesto, hold del mirror —
**en el backend**; la pantalla muestra, no decide. Datos desconocidos SHALL presentarse como
`unknown` con su razón, nunca como cola vacía.

La composición entre módulos SHALL vivir en el host — el único sitio que conoce todos los
módulos — leyendo sus APIs self-origin, el mismo patrón del watcher. Ninguna regla de
autorización SHALL vivir en React, y la cola no autoriza nada: sus acciones navegan a donde ya
se actúa.

La cola responde al presente: una fila cuya task ya no está en el mirror abierto SHALL drenar
— no se muestra — porque la causa ya no bloquea nada que este proceso pueda actuar. El mirror
abierto es la autoridad de "qué está abierto", y el drain vive en el join del host, no en las
consultas de los módulos.

#### Scenario: Un dispatch sin resolver es una fila
- **WHEN** un dispatch del journal queda `pending` o `unknown`
- **THEN** la cola nombra su task con la causa tipada de reconciliación abierta y enlaza al efecto

#### Scenario: Un run en hold es una fila
- **WHEN** el último evento de control de un run es `hold`
- **THEN** la cola nombra su task con la causa de revisión pendiente y enlaza al run

#### Scenario: Un veredicto inconclusive es una fila
- **WHEN** el manifiesto de la gate de un merge quedó `inconclusive`
- **THEN** la cola nombra su task con la causa de verificación incompleta y el SHA del candidato

#### Scenario: Un hold del mirror sin actividad local
- **WHEN** una task está held y no tiene dispatch, run ni manifiesto
- **THEN** la cola nombra su task con la causa held del mirror, no la omite ni la inventa

#### Scenario: Una fila cuya task ya no está abierta drena
- **WHEN** una fila vendría del Runs (dispatch, run o gate) y la task no aparece en el mirror
  abierto — cerrada, resuelta o fuera del scope
- **THEN** la cola no muestra la fila: lo que bloqueaba ya no está abierto, y "no rows" sigue
  significando "nada bloqueado ahora"

#### Scenario: Un bloqueo real con la task abierta permanece
- **WHEN** una fila vendría del Runs y la task sigue en el mirror abierto
- **THEN** la cola muestra la fila con su causa y su destino — el drain no toca lo que aún bloquea

#### Scenario: El mirror ilegible no drena
- **WHEN** la mitad del mirror no puede leerse
- **THEN** la respuesta lo dice con su razón y ninguna fila se descarta — un drain sobre un
  desconocido escondería un bloqueo real el día exacto en que nada está calmado

#### Scenario: Datos desconocidos
- **WHEN** una fuente no puede leerse
- **THEN** la respuesta lo dice con su razón — nunca una cola vacía que leería como "nada bloqueado"

#### Scenario: La cola no autoriza
- **WHEN** se actúa desde una fila
- **THEN** la acción navega al run, a la issue o al dispatch — la cola no elimina holds, no
  aprueba candidatos, no re-lanza runs
