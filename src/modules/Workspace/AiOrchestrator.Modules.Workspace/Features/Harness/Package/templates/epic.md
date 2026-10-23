<!-- La plantilla de una épica: una issue padre con sub-issues, cada una escrita con issue.md. Las líneas de ayuda son comentarios HTML y no salen en la issue. -->

## Objetivo

<!-- Qué es cierto cuando termina la última sub-issue, y por qué importa a quien lo usa. De dónde sale la épica: lo que se observó, con fechas y números. -->

## El grafo

<!-- Un bloque mermaid con flowchart LR: la épica es el nodo e<número>, y cada sub-issue un nodo i<número> cuyo texto es #<número> y lo que hace en pocas palabras. Debajo del bloque, una línea que diga qué significa una flecha, como «Una flecha es "va después de"». Una flecha sale de los ficheros que dos sub-issues comparten en su Dónde cae. -->

```mermaid
flowchart LR
```

## Sub-issues

<!-- Una fila por sub-issue, en el orden del grafo. -->

| Issue | Qué | Dónde cae |
| --- | --- | --- |

## Criterios de aceptación

<!-- Lo que tiene que ser cierto para cerrar la épica: una casilla por criterio, como - [ ] seguida de lo que se comprueba. -->

## Lo que no es

<!-- Lo más cercano que la épica deliberadamente no hace. -->
