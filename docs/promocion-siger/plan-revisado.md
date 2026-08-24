# Plan revisado — integración PortalDigital ↔ SIGER ↔ HondurasÁgil

Sustituye el orden de fases de `plan.md`. Las tareas de `plan.md` **no se descartan**: se
reubican y se les suman frentes nuevos. `diseno.md` sigue vigente salvo por el reparto de
propiedad de campos, que D-17 rehace.

Origen: acuerdos con jefatura de HondurasÁgil y PortalDigital.

---

## 1. La meta

Que la información de PD y la de SIGER terminen siendo la misma, **sin haber perdido lo que
SIGER tenía antes**, y pudiendo consultarlo cuando haga falta.

De ahí salen las tres reglas que gobiernan todo el plan: una sola fuente de verdad por ficha
en cada momento (D-17), una foto de lo original antes de tocar nada (D-18), y un historial de
cada cambio posterior (D-15).

---

## 2. Cifras del inventario

Medidas contra `TramitesEstado_Ensayo`. Son la razón de varias decisiones.

| Dato | Valor |
|---|---|
| Fichas SIGER | **1 057** |
| Fichas sin categoría, sin modalidad, sin tiempo y sin costo | **1 032** (97,6 %) |
| Fichas publicadas hoy | 50 |
| Fichas en `Aprobado`/`Completo` | 303 |
| Fichas con `SolUrl` cargada | **1** |
| Trámites de expediente | 240 |
| Trámites de expediente enlazados a una ficha | **1** |
| Instituciones | 86 (45 activas) |
| Instituciones con sitio web cargado | **0** |

Tres lecturas:

1. **El llenado masivo es el problema dominante.** 1 032 fichas × 4 campos.
2. **El expediente cubre hoy el 23 % del inventario y enlaza el 0,1 %.** Un diseño que exija
   pasar por el expediente antes de tocar una ficha bloquearía el 99,9 % del trabajo pendiente.
3. **1 056 de 1 057 fichas están hoy libres de PD**, así que el llenado en lote puede operar
   sobre casi todo el inventario sin conflicto con nadie.

---

## 3. Decisiones cerradas

| # | Decisión |
|---|---|
| **D-01** | PortalDigital es la fuente principal de información para HondurasÁgil. |
| **D-02** | «Pasar a SIGER» escribe en la tabla `TramitesSiger` **local de PD**. No hay integración con un sistema SIGER externo. |
| **D-03** | HA lee de PD salvo que el trámite solo exista en SIGER. Si existe en ambos, manda PD. Todo sale en **una sola lista**. |
| **D-04** | La institución gana una URL base de SOL. El trámite solo guarda el tramo final. |
| **D-05** | Editar un trámite que ya está en PD se hace **siempre en el expediente**. |
| **D-06** | Al importar, el usuario elige el expediente destino, **o** el bucket «Trámites Importados de SIGER» de esa institución. |
| **D-07** | «Pasar a SIGER» crea una **versión nueva**. La anterior no se borra. Se muestra la más nueva. |
| **D-08** | Quién controla PD **selecciona manualmente** qué trámites se publican en HA. |
| **D-09** | PD tiene una pantalla que lista todo lo publicado en HA, para administrarlo. |
| **D-10** | La publicación es **manual pura y no bloquea**. La regla de estado queda como *advertencia*. |
| **D-11** | Los pasos del proceso siguen siendo propiedad de SIGER. **No** se mapean con el flujo del expediente. |
| **D-12** | El contenido se edita en el expediente. `EstadoSiger` es lo único que se sigue editando **solo** en la ficha. |
| **D-13** | El trámite captura solo el tramo final de la URL SOL, con `sol.gob.hn/<URL de la institución>/` como prefijo fijo en pantalla. |
| **D-14** | Las URLs SOL completas ya cargadas **no se tocan**, y solo las usan los trámites que nunca pasaron por PD. |
| **D-15** | El historial es una **tabla de fotos** de la ficha y sus hijos. La fila viva es la última versión. |
| **D-16** | «Quitar de HA» **despublica**, no borra. |
| **D-17** | **Bloqueo condicional.** Si la ficha ya está en PD, sus campos de contenido quedan **bloqueados en la ficha** y solo se editan en el expediente. Si no está en PD, se editan en la ficha. Nunca en los dos lugares a la vez. |
| **D-18** | Antes de tocar nada se guarda una **foto del inventario SIGER original**, completa y permanente. |
| **D-19** | Al final hay una fase de **llenado asistido** de los campos que falten. |

### D-17 — el bloqueo condicional

Reemplaza la idea anterior de editar en ambos lados con reglas de protección. Es mejor: en vez
de convivir con el riesgo de pisarse, lo elimina.

**La regla:** una ficha está «en PD» cuando existe un trámite de expediente que la apunta
(`ExpedienteTramite.TramiteSigerId`). Ese solo predicado decide quién manda:

| Estado de la ficha | Dónde se editan sus campos de contenido |
|---|---|
| Sin trámite de expediente que la apunte | **En la ficha.** Captura en lote y llenado asistido operan aquí. |
| Con trámite de expediente que la apunte | **Solo en el expediente.** En la ficha quedan de solo lectura, con enlace al expediente. Los datos viajan al pasar a SIGER. |

**Por qué encaja tan bien:** es exactamente el mismo predicado que ya gobierna la lectura en
D-03. «Se trae de PD si existe, si no de SIGER» y «se edita en PD si existe, si no en SIGER»
son la misma frase. Una sola regla para leer y para escribir.

**Y resuelve las dos presiones a la vez:**

- No hay meses de trabajo insufrible: hoy 1 056 de 1 057 fichas están libres, así que el
  llenado en lote y el asistido operan sobre casi todo el inventario.
- No hay edición en dos lugares: en ningún momento un campo es editable desde dos pantallas,
  así que no hay nada que se pueda desactualizar.

**Es barato:** `ExpedienteTramite.TramiteSigerId` ya tiene índice, así que el predicado del
bloqueo no cuesta nada. Y no depende de `ConciliacionesSiger` —la tabla que la Fase 2 va a
reparar—, porque `TramiteSigerId` viaja en el DTO del expediente y sobrevive a los guardados.

**Los tres grupos de campos** que salen de D-17 y D-12:

| Grupo | Campos | Dónde se editan |
|---|---|---|
| **Contenido** | Nombre, descripción, objetivo, dirigido a, categoría, modalidad, tiempo, costo, requisitos, entregables, lugares, vigencia, temporalidad, observaciones DIGER, si está en SOL, tramo de la dirección | Según el bloqueo de D-17 |
| **Propio de SIGER** | `EstadoSiger`, `Codigo`, pasos del proceso (D-11) | Siempre en la ficha. Nunca se bloquean. |
| **Curaduría y operación** | Publicación en HA, `EsPopular`, tareas de digitalización | Siempre en la pantalla de administración. Nunca se bloquean. |

Ese tercer grupo resuelve de paso lo que estaba abierto en P-09 y P-10: no son contenido del
trámite, así que no entran al bloqueo ni al versionado.

### D-18 — la foto original, y por qué urge

La meta dice «sin haber perdido la información de antes de SIGER». Eso **no** lo garantiza el
historial de D-15, porque ese historial arranca en el primer pase desde un expediente. Para
cuando eso ocurra, la captura en lote y el llenado asistido ya habrán modificado 1 032 fichas.

El único momento en que lo original está íntegro y garantizado es **antes de empezar**.

Por eso D-18 es una copia completa y permanente del inventario SIGER —fichas y sus seis
colecciones hijas— tomada de una sola vez, antes de cualquier otra escritura. Es una migración
sencilla, sin riesgo de diseño, y es lo que hace que la meta se pueda cumplir. Después, D-15
cubre todo lo que pase de ahí en adelante, y la foto original queda como la versión cero.

### Cómo D-13 y D-14 conviven

- Si el trámite tiene tramo → `URL base de la institución` + `tramo`.
- Si no tiene tramo → la URL completa heredada, tal cual (D-14). Hoy: una ficha.
- Si tiene tramo pero la institución no tiene URL base → **no se emite enlace**. Nunca componer
  a medias: sería un enlace roto en producción.

La API pública sigue emitiendo la **URL absoluta** en todos los casos, para que HA no cambie.

---

## 4. Qué sobrevive de lo ya hecho

**La Fase 1 completa (4 commits en `Jamil`).** Cero reescritura:

- `ModalidadNormalizador`, `CodigoPromovido` — intactos.
- `IdSiger` nulable + índice único filtrado + migración `SigerIdOpcional` — **reforzado**.
- `ReglaPublicacion` — sobrevive, cambia de papel: de determinante a advertencia (D-10).

**Las tareas 5–17 de `plan.md`** se reubican en las fases 5, 6 y 8. Ninguna se descarta.

Tamaño: de 17 tareas a unas **37**. Cuatro hechas, ~33 por delante.

---

## 5. Fases

**Orden de ejecución: 0 → 2 → 3 → 9 → 5 → 6 → 7 → 4 → 8.**

La numeración se mantiene estable para no romper las referencias ya conversadas. Dos fases
cambian de lugar respecto a lo que se venía diciendo, y por razones concretas:

- **La Fase 0 va antes que todo** porque la foto original solo se puede tomar íntegra una vez.
- **La Fase 9 (llenado asistido) conviene adelantarla** — ver la nota en su sección.
- **La Fase 4 se corre al final** porque hoy ninguna de las 86 instituciones tiene dirección
  cargada; hasta que alguien las reúna, entregaría enlaces compuestos para cero trámites.

---

### Fase 0 — La foto del SIGER original (~1 tarea)

**Entrega:** D-18. Copia completa y permanente de las 1 057 fichas y sus seis colecciones hijas.

**Va primera y no se puede posponer.** Cualquier escritura anterior a esta foto es información
original perdida para siempre. Es la fase más barata del plan y la que sostiene la mitad de la
meta.

---

### Fase 1 — HECHA

`IdSiger` opcional, índice filtrado, regla de publicación unificada, normalizador y generador
de código. 217 pruebas en verde.

---

### Fase 2 — Detener la pérdida silenciosa de conciliaciones (~3 tareas)

**Entrega:** que una decisión de conciliación sobreviva a que alguien guarde el expediente.

**El defecto, verificado en código:** `ExpedienteMapper.Aplicar` llama a `LimpiarHijos()`, que
hace `_tramites.Clear()`, y vuelve a agregar los trámites desde cero — cada guardado borra y
reinserta los `ExpedienteTramite` con Id nuevo. `ConciliacionesSiger` tiene FK a ese Id con
`OnDelete(DeleteBehavior.Cascade)`. El enlace sobrevive porque `TramiteSigerId` viaja en el DTO;
las decisiones **Descartado** y **ProponerFichaNueva** solo viven en esa tabla y desaparecen. La
bandeja cuenta como pendiente todo lo que tiene `Decision is null`, así que un trámite
descartado a mano regresa a la bandeja al siguiente guardado — justo lo que el comentario de la
propia entidad dice que existe para evitar.

*Ruta de código leída completa; no ejecutada contra base. Primer paso: medir el daño real.*

**Camino recomendado:** rekeyar `ConciliacionSiger` sobre `(ExpedienteId, TramiteIndex)`. Ese
índice **ya existe** en `ExpedienteTramite`, así que el cambio es barato.

**Nota:** el bloqueo de D-17 **no** depende de esta reparación, porque usa `TramiteSigerId`, que
sobrevive. La Fase 2 protege la bandeja de conciliación, no el bloqueo.

---

### Fase 3 — Control de publicación en HA + pantalla de administración (~4 tareas)

**Entrega:** D-08, D-09, D-10, D-16.

Arregla además un defecto vivo: 303 fichas en Aprobado/Completo y solo 50 publicadas, porque la
bandera solo se recalcula al editar.

1. Bandera manual de publicación. `ReglaPublicacion` pasa a alimentar una advertencia que
   **no bloquea** (D-10).
2. Migración y relleno **conservador**: las 50 publicadas siguen publicadas; las demás quedan
   sin publicar, listadas como *candidatas*.
3. Pantalla «Publicado en HondurasÁgil». **Ya existe el 70 %**: `Siger/Index.cshtml.cs` tiene
   filtro `Publicado` Sí/No y contador `TotalPublicados`. Falta publicar/despublicar
   —individual y en lote—, la advertencia y la lista de candidatas.
4. Permiso propio para publicar, distinto del de editar una ficha.

---

### Fase 9 — Llenado asistido (~3 tareas) · *conviene adelantarla aquí*

**Entrega:** D-19. Completar los cuatro campos que faltan en 1 032 fichas, derivando lo obvio
y dejando marcado para revisión humana lo que no lo sea.

**Por qué conviene adelantarla:** no depende de ninguna otra fase —los cuatro campos ya existen
en la ficha— y hoy **1 056 de 1 057 fichas están desbloqueadas**, que es la condición más barata
posible para llenarlas. Si se corre al final, después de importar, cada ficha importada estará
bloqueada por D-17 y el llenado tendría que hacerse por el expediente, que es más caro.

Además hace que la Fase 3 sirva de verdad: hoy la pantalla de publicación tendría 50 fichas
publicables y casi nada más que ofrecer.

**Si prefiere dejarla al final de todos modos**, entonces el llenado debe respetar el bloqueo:
escribir en la ficha cuando esté libre y en el expediente cuando esté tomada. Es más trabajo,
pero funciona igual.

**Condición previa innegociable:** la Fase 0. El llenado toca 1 032 fichas.

---

### Fase 5 — El expediente aprende a guardar todo lo que SIGER guarda (~8 tareas)

Si el expediente no puede guardar un campo, ese campo no se puede editar una vez la ficha queda
bloqueada por D-17.

Tareas 5–9 de `plan.md`: categoría, modalidad de catálogo cerrado, gratuidad, las dos tablas
hijas (entregables y lugares), su siembra desde `DocEntregado`/`Horario`/`Telefono`/`DirSede`,
la conversión de las 240 modalidades y la UI. **Más lo que D-12 agrega:** vigencia,
temporalidad, observaciones DIGER, si está en SOL y el tramo de la dirección.

**Riesgo:** la conversión de las 240 modalidades debe correr **antes** de aplicar el CHECK.

---

### Fase 6 — De PD a SIGER: promover, actualizar y versionar (~6 tareas)

**Entrega:** D-07, D-15, más las tareas 10–15 de `plan.md`.

**Por qué van juntos:** promover y actualizar son la misma operación —escribir del expediente
hacia la ficha— una creando y otra actualizando. Si el versionado aterriza después de los
primeros pases, esos quedan como agujeros sin historial.

1. Historial como tabla de fotos, con la foto de la Fase 0 como versión cero.
2. Promover: expediente → ficha nueva.
3. «Pasar a SIGER»: diff contra lo publicado, confirmación, escritura y versión nueva.
4. Ver el historial y una versión anterior.

---

### Fase 7 — De SIGER a PD: importar y aplicar el bloqueo (~6 tareas)

**Entrega:** D-05, D-06 y la mitad de D-17. **Depende de la Fase 5.**

1. El bucket «Trámites Importados de SIGER» por institución (ver P-11).
2. Selector de expediente destino (D-06).
3. La importación: mapeo campo a campo. No se traen los pasos (D-11).
4. Guarda contra doble importación. `OrigenExternoId` ya existe en `Expediente` y ya se usa
   como clave de idempotencia en la importación de reuniones.
5. La ficha queda enlazada en el mismo acto, y con eso **bloqueada**.
6. El bloqueo en las pantallas de la ficha: campos de contenido en solo lectura, con enlace al
   expediente que ahora manda. También en la captura en lote, que debe excluir las bloqueadas.

---

### Fase 4 — URL SOL compuesta (~3 tareas) · *se ejecuta al final*

**Bloqueada por datos, no por código:** 0 de 86 instituciones tienen dirección cargada. La
recolección de las 45 activas puede arrancar en paralelo desde hoy.

1. URL base de SOL en `Institucion`. Ojo: setters privados y factoría validadora;
   `RegistrarContacto` ya valida URL absoluta con la misma regla.
2. Tramo final en el trámite, y composición en un solo lugar con la regla de tres ramas.
3. La pantalla muestra el prefijo fijo junto al textbox (D-13).

**Riesgo:** `SolUrl` hoy se expone en el catálogo público como URL absoluta y
`SoloFichasCompletas` la evalúa. Si pasa a guardar solo el tramo sin componer en la salida, se
rompen los enlaces SOL de HA.

---

### Fase 8 — Visibilidad y cierre (~3 tareas)

Tareas 16–17 de `plan.md`: insignia en el expediente, aviso en el detalle, filtro en el
inventario. Más actualizar `diseno.md` y `plan.md`.

---

## 6. Preguntas abiertas

**P-11 — El bucket «Trámites Importados de SIGER» y el módulo de expedientes.**
`Expediente` tiene una máquina de estados **lineal y estricta** (`CambiarEstado` lanza si el
salto no es exactamente a la etapa siguiente), arranca en `EnExploracion`, y exige `Analista`,
código, cronograma y validación. Los buckets quedarían atrapados en `EnExploracion` y
aparecerían en cada listado, conteo y tablero como si fueran levantamientos reales.
*Recomiendo marcarlos con `OrigenExternoId` y excluirlos de los listados y conteos.*

**P-13 — ¿Quién reúne las URLs de las 45 instituciones activas?** Es trabajo de datos, no de
código, y es lo único que separa a la Fase 4 de ser útil.

**P-15 — ¿Desenlazar una ficha la desbloquea?**
La pantalla de conciliación ya permite desenlazar, y hoy eso pone `TramiteSigerId = null`. Con
D-17 esa acción devolvería el mando a la ficha. Es coherente, pero conviene que sea deliberada.
*Recomiendo permitirlo con una advertencia explícita de que la ficha vuelve a editarse por su
lado.*

**P-16 — ¿El llenado asistido va adelantado o al final?** Ver la nota de la Fase 9. Adelantarlo
es más barato y hace útil a la Fase 3; dejarlo al final obliga a que respete el bloqueo.

**P-17 — ¿La captura en lote se queda como está?**
Con D-17 ya no hay conflicto: opera sobre fichas libres. Solo necesita excluir las bloqueadas.
*Asumo que se queda, sin reconstruirla contra el expediente.*

**Asumido salvo corrección:** el `Codigo` de la ficha se sigue generando del lado de SIGER; es
identidad de la ficha, no contenido del trámite.

---

## 7. Riesgos transversales

| Riesgo | Dónde |
|---|---|
| Escribir sobre el inventario antes de tomar la foto original | Fase 0 |
| Llenar 1 032 fichas después de importarlas, ya bloqueadas | Fase 9 / P-16 |
| Autopublicar fichas sin revisar en el relleno | Fase 3 |
| Buckets contaminando listados y tableros de expedientes | Fase 7 / P-11 |
| Desenlazar sin advertencia y devolver el mando sin querer | Fase 7 / P-15 |
| Enlaces SOL rotos en HA por el cambio de significado de `SolUrl` | Fase 4 |
| Componer a medias cuando la institución no tiene URL base | Fase 4 |
| Duplicados por importar dos veces la misma ficha | Fase 7 |
| Conversión de modalidades corriendo después del CHECK | Fase 5 |

Reglas vigentes de `plan.md` que siguen aplicando: `EnableRetryOnFailure` es incompatible con
`BeginTransaction` explícito; las migraciones necesitan `--output-dir Persistence\Migrations`;
`ExpedienteTramite.Id` no es estable; y el reparto de proveedor entre `Application.Tests`
(EF In-Memory, sin CHECK ni índices únicos) y `Web.Tests` (SQLite, que sí los aplica).
