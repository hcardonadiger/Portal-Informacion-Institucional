# Plan revisado — integración PortalDigital ↔ SIGER ↔ HondurasÁgil

Sustituye el orden de fases de `plan.md`. Las tareas de `plan.md` **no se descartan**:
se reubican y se les suman frentes nuevos. `diseno.md` sigue vigente salvo por el reparto
de propiedad de campos, que D-12 y D-17 rehacen.

Origen: acuerdos con jefatura de HondurasÁgil y PortalDigital.

---

## 1. Cifras del inventario

Medidas contra `TramitesEstado_Ensayo`. Son la razón de varias decisiones de este plan.

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

Tres lecturas que gobiernan el plan:

1. **El llenado masivo es el problema dominante.** 1 032 fichas × 4 campos. Es exactamente el
   trabajo que el comentario de `CapturaLote.cshtml.cs` describe como «meses de trabajo humano
   insufrible».
2. **El expediente cubre hoy el 23 % del inventario, y enlaza el 0,1 %.** «PD es la fuente de
   verdad» es una meta, no un estado. Cualquier diseño que exija pasar por el expediente
   *antes* de poder tocar una ficha bloquea el 99,9 % del inventario.
3. **D-14 protege una sola fila.** La rama de «URL heredada» existe, pero es para una ficha.

---

## 2. Decisiones cerradas

| # | Decisión |
|---|---|
| **D-01** | PortalDigital es la fuente principal de información para HondurasÁgil. |
| **D-02** | «Pasar a SIGER» escribe en la tabla `TramitesSiger` **local de PD**. No hay integración con un sistema SIGER externo. |
| **D-03** | HA lee de PD salvo que el trámite solo exista en SIGER. Si existe en ambos y hay conciliación, manda PD y se ignora SIGER. Todo sale en **una sola lista**. |
| **D-04** | La institución gana una URL base de SOL. El trámite solo guarda el tramo final. |
| **D-05** | Editar un trámite de SIGER se hace **siempre en el expediente**. Si no tiene conciliación, primero se importa. |
| **D-06** | Al importar, el usuario elige el expediente destino, **o** el bucket «Trámites Importados de SIGER» de esa institución. |
| **D-07** | «Pasar a SIGER» crea una **versión nueva**. La anterior no se borra: queda en historial. Se muestra la más nueva. |
| **D-08** | Quién controla PD **selecciona manualmente** qué trámites se publican en HA. |
| **D-09** | PD tiene una pantalla que lista todo lo publicado en HA, para administrarlo. |
| **D-10** | La publicación es **manual pura y no bloquea**. La regla de estado queda como *advertencia*, nunca como impedimento. |
| **D-11** | Los pasos del proceso siguen siendo propiedad de SIGER. **No** se mapean con el flujo del expediente. |
| **D-12** | El contenido se edita en el expediente. `EstadoSiger` es lo único que se sigue editando **solo** en la ficha. |
| **D-13** | El trámite captura solo el tramo final de la URL SOL. La pantalla muestra `sol.gob.hn/<URL de la institución>/` como prefijo fijo, y al lado el textbox donde el usuario termina la dirección. |
| **D-14** | Las URLs SOL completas ya cargadas **no se tocan**, y solo las usan los trámites que nunca pasaron por PD. |
| **D-15** | El historial es una **tabla de fotos** de la ficha y sus hijos. La fila viva es la última versión. |
| **D-16** | «Quitar de HA» **despublica**, no borra. |
| **D-17** | **Los campos de llenado masivo se editan en ambos lados**: en la ficha (captura en lote) y en el expediente. Es la excepción a D-12, y se protege con la regla de no arrasar descrita abajo. |

### D-17 y por qué necesita una regla de protección

Editar el mismo campo en dos lugares abre la puerta a la **pérdida silenciosa**: alguien llena
200 fichas en lote, después alguien más pasa un expediente a SIGER, el pase sobrescribe en
bloque y el trabajo en lote desaparece sin error, sin aviso y sin rastro. Con 1 032 fichas por
llenar, ese riesgo no es teórico.

D-17 se sostiene con dos reglas baratas:

1. **El pase nunca arrasa con vacío.** Si el expediente no tiene valor para un campo, el pase
   deja lo que la ficha ya tenga. Llenar y actualizar, sí; blanquear, nunca. Esto por sí solo
   elimina el caso frecuente, porque el llenado en lote pone datos donde no había.
2. **El diff decide lo demás.** Cuando ambos lados tienen valor y difieren, la pantalla de
   confirmación del pase (PR-07) lo muestra antes de escribir, señalando los campos que se
   tocaron directamente en la ficha después del último pase. Basta una marca de tiempo de
   «última edición directa» para poder señalarlos.

Con eso, D-17 es seguro sin renunciar a la velocidad del llenado en lote.

**La alternativa que se descartó** era mover el llenado en lote al expediente y conservar una
sola fuente de verdad. Es más limpio en teoría, pero exigiría importar ~1 032 fichas a
expedientes **antes** de poder llenar nada, cuadruplicando el módulo de expedientes de golpe
con contenedores artificiales. El costo no lo justifica.

### Cómo D-03 se vuelve una sola consulta

Los tres casos de D-03 no son tres caminos de código. Se resuelven **al escribir**, no al leer:

- Solo en SIGER → la ficha `TramitesSiger` tal como se importó.
- Solo en PD → ficha promovida, con `IdSiger` nulo (ya construido en la Fase 1).
- En ambos → la ficha que PD **sobrescribió** al pasar a SIGER.

Resultado: `TramitesSiger` sigue siendo la única superficie de lectura, HA no cambia una
línea, y todo sale en la misma lista. Si se resolviera al leer, habría que mezclar dos
esquemas en cada consulta y el filtro `SoloFichasCompletas` dejaría de funcionar.

**Consecuencia:** un trámite conciliado cuyo contenido se editó en el expediente pero **aún no
se ha pasado a SIGER** sigue viéndose en HA con el contenido viejo. Es deliberado: un borrador
sin revisar no debe llegar al ciudadano.

### Cómo D-13 y D-14 conviven

Una sola regla, en un solo lugar:

- Si el trámite tiene tramo → `URL base de la institución` + `tramo`.
- Si no tiene tramo → la URL completa heredada, tal cual (D-14). Hoy: una ficha.
- Si tiene tramo pero la institución no tiene URL base → **no se emite enlace**. Nunca componer
  a medias: sería un enlace roto en producción.

La API pública sigue emitiendo la **URL absoluta** en todos los casos, para que HA no cambie.

---

## 3. Qué sobrevive de lo ya hecho

**La Fase 1 completa (4 commits en `Jamil`).** Cero reescritura:

- `ModalidadNormalizador`, `CodigoPromovido` — intactos.
- `IdSiger` nulable + índice único filtrado + migración `SigerIdOpcional` — **reforzado**.
- `ReglaPublicacion` — sobrevive, cambia de papel: de determinante a advertencia (D-10).

**Las tareas 5–17 de `plan.md`** se reubican en las fases 5, 6 y 8. Ninguna se descarta.

Tamaño: de 17 tareas a unas **34**. Cuatro hechas, ~30 por delante.

---

## 4. Fases

**Orden de ejecución: 2 → 3 → 5 → 6 → 7 → 4 → 8.** La numeración se mantiene estable para no
romper las referencias ya conversadas, pero la Fase 4 se corre al final: hoy **ninguna** de las
86 instituciones tiene una URL cargada, así que hasta que alguien reúna esas direcciones la
Fase 4 entregaría enlaces compuestos para cero trámites.

### Fase 1 — HECHA

`IdSiger` opcional, índice filtrado, regla de publicación unificada, normalizador y generador
de código. 217 pruebas en verde.

---

### Fase 2 — Detener la pérdida silenciosa de conciliaciones (~3 tareas)

**Entrega:** que una decisión de conciliación sobreviva a que alguien guarde el expediente.

**Por qué va primera:** D-05 convierte la conciliación en el **interruptor que decide todo el
flujo de edición**. Si las decisiones se evaporan, el enrutamiento es no determinista: el mismo
trámite se editaría en el expediente un día y se importaría de nuevo al siguiente, **creando un
duplicado**. La Fase 7 no se puede construir encima de esto.

**El defecto, verificado en código:** `ExpedienteMapper.Aplicar` llama a `LimpiarHijos()`, que
hace `_tramites.Clear()`, y vuelve a agregar los trámites desde cero — cada guardado borra y
reinserta los `ExpedienteTramite` con Id nuevo. `ConciliacionesSiger` tiene FK a ese Id con
`OnDelete(DeleteBehavior.Cascade)`. El enlace sobrevive porque `TramiteSigerId` viaja en el DTO
y se reescribe; las decisiones **Descartado** y **ProponerFichaNueva** solo viven en esa tabla y
desaparecen. La bandeja cuenta como pendiente todo lo que tiene `Decision is null`: un trámite
descartado a mano regresa a la bandeja al siguiente guardado, que es exactamente lo que el
comentario de la propia entidad dice que existe para evitar.

*Ruta de código leída completa; no ejecutada contra base. Primer paso: medir el daño real.*

**Camino recomendado:** rekeyar `ConciliacionSiger` sobre `(ExpedienteId, TramiteIndex)`, la
identidad estable que ya reconoce `plan.md`.

---

### Fase 3 — Control de publicación en HA + pantalla de administración (~4 tareas)

**Entrega:** D-08, D-09, D-10, D-16.

**Por qué va aquí:** no depende de nada, y arregla un defecto vivo — 303 fichas en
Aprobado/Completo y solo 50 publicadas, porque la bandera solo se recalcula al editar. D-05 lo
empeora: si se deja de editar en la ficha, esas fichas **jamás recalcularán su bandera**.

1. Bandera manual de publicación. `ReglaPublicacion` pasa a alimentar una advertencia que
   **no bloquea** (D-10).
2. Migración y relleno **conservador**: las 50 publicadas hoy siguen publicadas; las demás
   quedan **sin publicar**, listadas como *candidatas*. El relleno no debe autopublicar fichas
   sin revisar.
3. Pantalla «Publicado en HondurasÁgil». **Ya existe el 70 %**: `Siger/Index.cshtml.cs` tiene
   filtro `Publicado` Sí/No y contador `TotalPublicados`. Falta publicar/despublicar
   —individual y en lote—, la advertencia y la lista de candidatas.
4. Permiso propio para publicar, distinto del de editar una ficha.

---

### Fase 4 — URL SOL compuesta (~3 tareas) · *se ejecuta al final*

**Entrega:** D-04, D-13, D-14.

**Bloqueada por datos, no por código:** 0 de 86 instituciones tienen dirección cargada. Alguien
tiene que reunir las URLs de las 45 instituciones activas antes de que esta fase entregue algo.
Esa recolección puede arrancar en paralelo desde hoy.

1. URL base de SOL en `Institucion`. Ojo: setters privados y factoría validadora;
   `RegistrarContacto` ya valida URL absoluta con la misma regla. Es cambio de dominio, no solo
   una columna.
2. Tramo final en el trámite, y composición en un solo lugar con la regla de tres ramas.
   Normalizar barras ahí, no en cada uso.
3. La pantalla muestra el prefijo fijo `sol.gob.hn/<URL institución>/` junto al textbox (D-13).

**Riesgo:** `TramiteSiger.SolUrl` hoy se expone en el catálogo público como URL absoluta y
`SoloFichasCompletas` la evalúa. Si el campo pasa a guardar solo el tramo sin componer en la
salida, se rompen los enlaces SOL de HA. Además el predicado de completitud pasa a cruzar dos
tablas, porque «tiene dirección» ya no se responde mirando una sola columna.

---

### Fase 5 — El expediente aprende a guardar todo lo que SIGER guarda (~8 tareas)

**La base de todo:** si el expediente no puede guardar un campo, ese campo no se puede editar
del lado del expediente.

Tareas 5–9 de `plan.md`: categoría, modalidad de catálogo cerrado, gratuidad, las dos tablas
hijas (entregables y lugares), su siembra desde `DocEntregado`/`Horario`/`Telefono`/`DirSede`,
la conversión de las 240 modalidades y la UI.

**Más lo que D-12 agrega:** vigencia del documento, temporalidad, observaciones DIGER, si está
en SOL y el tramo de la dirección.

**Riesgo:** la conversión de las 240 modalidades debe correr **antes** de aplicar el CHECK.

---

### Fase 6 — De PD a SIGER: promover, actualizar y versionar (~6 tareas)

**Entrega:** D-07, D-15, D-17, más las tareas 10–15 de `plan.md`.

**Por qué van juntos:** promover y actualizar son la misma operación —escribir del expediente
hacia la ficha— una creando y otra actualizando. Si el versionado aterriza después de los
primeros pases, esos quedan como agujeros sin historial.

1. Historial como tabla de fotos de la ficha y sus hijos (D-15).
2. Promover: expediente → ficha nueva. Nace como versión 1.
3. «Pasar a SIGER»: la regla de no arrasar con vacío (D-17), el diff contra lo publicado
   señalando lo editado en la ficha, confirmación, escritura y versión nueva.
4. Ver el historial y una versión anterior.

---

### Fase 7 — De SIGER a PD: importar (~5 tareas)

**Entrega:** D-05 y D-06. Dirección nueva. **Depende de la Fase 5.**

1. El bucket «Trámites Importados de SIGER» por institución (ver P-11).
2. Selector de expediente destino (D-06).
3. La importación: mapeo campo a campo. No se traen los pasos (D-11).
4. Guarda contra doble importación. `OrigenExternoId` ya existe en `Expediente` y ya se usa
   como clave de idempotencia en la importación de reuniones.
5. La ficha queda conciliada con el trámite en el mismo acto.

**Nota:** gracias a D-17, importar **no** es requisito para completar fichas. Se importa cuando
alguien quiere trabajar un trámite a fondo, no para poder llenar cuatro campos.

---

### Fase 8 — Visibilidad y cierre (~3 tareas)

Tareas 16–17 de `plan.md`: insignia en el expediente, aviso en el detalle, filtro en el
inventario. Más actualizar `diseno.md` y `plan.md` a lo acordado aquí.

---

## 5. Preguntas abiertas

**P-09 — Tareas de digitalización: ¿al expediente o se quedan en la ficha?**
Verifiqué que **no son públicas**: no aparecen en ningún DTO del catálogo. Tienen `Estado` y
`FechaCumplimiento`, que cambian seguido. Si viven en el expediente, marcar una tarea como
completada obligaría a pasar a SIGER y **crearía una versión nueva** de toda la ficha, llenando
el historial de ruido operativo.
*Recomiendo dejarlas en la ficha junto a `EstadoSiger`, como gestión interna.*

**P-10 — `EsPopular` y la bandera de publicación: ¿expediente o pantalla de administración?**
Son palancas de curaduría del catálogo, no contenido del trámite. Si la bandera de publicación
viviera en el expediente, despublicar desde la pantalla de D-09 exigiría abrir el expediente y
pasar a SIGER, lo que contradice que «quitar de HA» sea una acción de esa pantalla.
*Recomiendo que ambas vivan en la pantalla de administración.*

**P-11 — El bucket «Trámites Importados de SIGER» y el módulo de expedientes.**
`Expediente` tiene una máquina de estados **lineal y estricta** (`CambiarEstado` lanza si el
salto no es exactamente a la etapa siguiente), arranca en `EnExploracion`, y exige `Analista`,
código, cronograma y validación. Los buckets quedarían atrapados en `EnExploracion` y
aparecerían en cada listado, conteo y tablero como si fueran levantamientos reales.
*Recomiendo marcarlos con `OrigenExternoId` y excluirlos de los listados y conteos.*

**P-13 — ¿Quién reúne las URLs de las 45 instituciones activas?** Es trabajo de datos, no de
código, y es lo único que separa a la Fase 4 de ser útil.

**P-14 — El llenado asistido: ¿entra al plan o queda para después?**
Con 1 032 fichas a las que les faltan los cuatro campos, la idea de derivar el llenado obvio a
un asistente y reservar lo complejo para revisión humana es la que mejor ataca el volumen. Si
entra, conviene que escriba por el mismo camino que la captura en lote, para que herede la
protección de D-17 en vez de inventar otra.

**Asumido salvo corrección:** el `Codigo` de la ficha se sigue generando del lado de SIGER; es
identidad de la ficha, no contenido del trámite.

---

## 6. Riesgos transversales

| Riesgo | Dónde |
|---|---|
| Pérdida silenciosa de llenado masivo al pasar a SIGER | D-17 / Fase 6 |
| Autopublicar fichas sin revisar en el relleno | Fase 3 |
| Enrutamiento de edición no determinista | Fase 2 → 7 |
| Buckets contaminando listados y tableros de expedientes | Fase 7 / P-11 |
| Historial ahogado en ruido operativo | Fase 6 / P-09 |
| Enlaces SOL rotos en HA por el cambio de significado de `SolUrl` | Fase 4 |
| Componer a medias cuando la institución no tiene URL base | Fase 4 |
| Duplicados por importar dos veces la misma ficha | Fase 7 |
| Conversión de modalidades corriendo después del CHECK | Fase 5 |

Reglas vigentes de `plan.md` que siguen aplicando: `EnableRetryOnFailure` es incompatible con
`BeginTransaction` explícito; las migraciones necesitan `--output-dir Persistence\Migrations`;
`ExpedienteTramite.Id` no es estable; y el reparto de proveedor entre `Application.Tests`
(EF In-Memory, sin CHECK ni índices únicos) y `Web.Tests` (SQLite, que sí los aplica).
