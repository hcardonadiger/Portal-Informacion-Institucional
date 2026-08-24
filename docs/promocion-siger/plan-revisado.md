# Plan revisado — integración PortalDigital ↔ SIGER ↔ HondurasÁgil

Sustituye el orden de fases de `plan.md`. Las tareas de `plan.md` **no se descartan**:
se reubican y se les suman frentes nuevos. `diseno.md` sigue vigente; sus decisiones
PR-01…PR-07 no fueron contradichas por jefatura.

Origen: acuerdos con jefatura de HondurasÁgil y PortalDigital.

---

## 1. Decisiones cerradas

| # | Decisión |
|---|---|
| **D-01** | PortalDigital es la fuente principal de información para HondurasÁgil. |
| **D-02** | «Pasar a SIGER» escribe en la tabla `TramitesSiger` **local de PD**. No hay integración con un sistema SIGER externo. |
| **D-03** | HA lee de PD salvo que el trámite solo exista en SIGER. Si existe en ambos y hay conciliación, manda PD y se ignora SIGER. Todo sale en **una sola lista**. |
| **D-04** | La institución gana una URL base de SOL. El trámite solo guarda el tramo final. La URL completa se **compone**: `sol.gob.hn/<base institución>/<tramo trámite>`. |
| **D-05** | Editar un trámite de SIGER se hace **siempre en PD**. Si no tiene conciliación, primero se importa a PD. |
| **D-06** | Al importar, el usuario elige el expediente destino, **o** el bucket «Trámites Importados de SIGER» de esa institución. |
| **D-07** | «Pasar a SIGER» crea una **versión nueva**. La anterior no se borra: queda en historial. Se muestra la más nueva. |
| **D-08** | Quién controla PD **selecciona manualmente** qué trámites se publican en HA. |
| **D-09** | PD tiene una pantalla que lista todo lo publicado en HA, para administrarlo (editar o quitar de HA). |

### Cómo D-03 se vuelve una sola consulta

Los tres casos de D-03 no son tres caminos de código. Se resuelven **al escribir**, no al leer:

- Solo en SIGER → la ficha `TramitesSiger` tal como se importó.
- Solo en PD → ficha promovida, con `IdSiger` nulo (ya construido en la Fase 1).
- En ambos → la ficha que PD **sobrescribió** al pasar a SIGER. PD manda porque PD escribió encima.

Resultado: `TramitesSiger` sigue siendo la única superficie de lectura, HA no cambia
una línea, y todo sale en la misma lista. Si en cambio se resolviera al leer, habría que
mezclar dos esquemas en cada consulta y el filtro `SoloFichasCompletas` dejaría de
funcionar, porque evalúa columnas de `TramitesSiger` que ya no serían los valores efectivos.

**Consecuencia que conviene tener presente:** un trámite conciliado cuyo contenido se editó
en PD pero **aún no se ha pasado a SIGER** sigue mostrándose en HA con el contenido viejo.
Eso es deliberado: un borrador sin revisar no debe llegar al ciudadano, y es lo que le da
sentido al botón. Hoy hay 240 trámites de expediente y 1 enlazado, así que casi todos
arrancarán en ese estado hasta que alguien los pase.

---

## 2. Qué sobrevive de lo ya hecho

**La Fase 1 completa (4 commits en `Jamil`).** Cero reescritura:

- `ModalidadNormalizador`, `CodigoPromovido`, `ReglaPublicacion` — intactos.
- `IdSiger` nulable + índice único filtrado + migración `SigerIdOpcional` — **reforzado**: es
  exactamente el mecanismo que sostiene «si solo existe en PD, se trae PD» (D-03).

`ReglaPublicacion` es el único punto que D-08 toca, y para demotarla de *determinante* a
*advertencia* (ver Fase 3 y P-01).

**Las tareas 5–17 de `plan.md`** se reubican en las fases 5, 6 y 8 de este documento.
Ninguna se descarta.

Tamaño: el plan pasa de 17 tareas a unas **32**. Cuatro hechas, ~28 por delante.

---

## 3. Fases

Orden elegido con dos criterios: **primero lo que detiene un daño que ya está ocurriendo**
y lo que entrega control visible sin depender de nada; después la cadena invasiva.

### Fase 1 — HECHA
`IdSiger` opcional, índice filtrado, regla de publicación unificada, normalizador y
generador de código. 217 pruebas en verde.

---

### Fase 2 — Detener la pérdida silenciosa de conciliaciones (~3 tareas)

**Entrega:** que una decisión de conciliación sobreviva a que alguien guarde el expediente.

**Por qué va primera:** D-05 convierte la conciliación en el **interruptor que decide todo
el flujo de edición** (con conciliación se edita en PD; sin ella se importa primero). Si las
decisiones se evaporan, el enrutamiento es no determinista: el mismo trámite se editaría en
PD un día y se importaría de nuevo al siguiente, **creando un duplicado**. No se puede
construir la Fase 7 encima de esto.

**El defecto, verificado en código:** `ExpedienteMapper.Aplicar` llama a `LimpiarHijos()`,
que hace `_tramites.Clear()`, y vuelve a agregar los trámites desde cero — cada guardado
borra y reinserta los `ExpedienteTramite` con Id nuevo. `ConciliacionesSiger` tiene FK a ese
Id con `OnDelete(DeleteBehavior.Cascade)`. El enlace en sí sobrevive porque `TramiteSigerId`
viaja en el DTO y se reescribe; las decisiones **Descartado** y **ProponerFichaNueva** solo
viven en esa tabla y no tienen contraparte en el trámite, así que desaparecen. La bandeja
cuenta como pendiente todo lo que tiene `Decision is null`: un trámite descartado a mano
regresa a la bandeja después del siguiente guardado, que es exactamente lo que el comentario
de la propia entidad dice que existe para evitar.

*Ruta de código leída completa; no ejecutada contra base. Primer paso de la fase es medir el
daño real en `TramitesEstado_Ensayo`.*

**Camino recomendado:** rekeyar `ConciliacionSiger` sobre `(ExpedienteId, TramiteIndex)` en
vez de `ExpedienteTramiteId`. Es la identidad estable que ya reconoce `plan.md`, y es mucho
menos invasivo que reescribir `ExpedienteMapper` para que preserve filas por `TramiteIndex`.

**Riesgo:** la migración de rekey necesita resolver filas huérfanas preexistentes.

---

### Fase 3 — Control de publicación en HA + pantalla de administración (~4 tareas)

**Entrega:** D-08 y D-09.

**Por qué va aquí:** no depende de nada de lo demás, y arregla un defecto vivo hoy —
hay 303 fichas en Aprobado/Completo pero solo 50 con `Publicado = 1`, porque la bandera solo
se recalcula al editar. D-05 empeora eso: si se prohíbe editar en SIGER, esas fichas **ya no
se editarán nunca ahí** y jamás recalcularán su bandera. La arquitectura nueva convierte un
bug latente en uno permanente si no se atiende ahora.

**Contenido:**

1. Bandera manual de publicación en HA, y qué papel le queda a `ReglaPublicacion` (ver P-01).
2. Migración y relleno **conservador**: las 50 fichas publicadas hoy siguen publicadas; las
   ~253 restantes quedan **sin publicar** y aparecen como *candidatas*. Bajo ningún concepto
   el relleno debe autopublicar 253 fichas sin revisar — eso vuelca contenido no revisado
   sobre el ciudadano de golpe.
3. Pantalla «Publicado en HondurasÁgil». **Ya existe el 70%**: `Siger/Index.cshtml.cs` tiene
   filtro `Publicado` Sí/No y contador `TotalPublicados`. Falta la acción de publicar/quitar
   —individual y en lote—, el aviso de ficha incompleta, y la lista de candidatas.
4. Permiso propio para publicar (`[Permission(...)]`): publicar al ciudadano no debería ser
   el mismo permiso que editar una ficha.

**Riesgo:** que el relleno se escriba «publicar todo lo Aprobado» por comodidad.

---

### Fase 4 — URL SOL compuesta (~3 tareas)

**Entrega:** D-04.

**Por qué va aquí:** es independiente de todo lo demás y es la pieza más pequeña con valor
propio. Puede adelantarse o atrasarse sin tocar el resto del plan.

**Contenido:**

1. URL base de SOL en `Institucion`. Ojo: la entidad tiene setters privados y factoría
   validadora; `RegistrarContacto` ya valida URL absoluta con la misma regla. Es un cambio
   de dominio, no solo una columna.
2. Tramo final en la ficha, y composición **en un solo lugar**.
3. La API pública debe seguir emitiendo la **URL absoluta**.

**Riesgo — el más filoso del plan:** `TramiteSiger.SolUrl` hoy guarda la URL completa, el
catálogo público ya la expone como absoluta (`t.SolUrl`) y `SoloFichasCompletas` la evalúa.
Si el campo pasa a guardar solo el tramo sin componer en la salida, **se rompe cada enlace
SOL de HA**. Además hay que decidir qué se hace con las URLs completas ya cargadas (P-05) y
qué se muestra cuando el trámite tiene tramo pero la institución no tiene base — eso sería
un enlace roto en producción. Normalizar barras en el punto de composición, no en cada uso.

---

### Fase 5 — El expediente aprende a guardar lo que SIGER guarda (~5 tareas)

Las tareas 5–9 de `plan.md`, sin cambios de contenido: categoría, modalidad de catálogo
cerrado, gratuidad, las dos tablas hijas (entregables y lugares), su siembra desde
`DocEntregado`/`Horario`/`Telefono`/`DirSede`, la conversión de las 240 modalidades y la UI
del expediente.

**Lo que cambia es su justificación:** antes era conveniente; ahora es **obligatoria**. D-05
baja trámites de SIGER al expediente, así que el expediente tiene que poder guardar todo lo
que guarda SIGER o la importación pierde datos en el camino.

**Riesgo ya identificado en `plan.md`:** la conversión de las 240 modalidades debe correr
**antes** de aplicar el CHECK, no después.

---

### Fase 6 — De PD a SIGER: promover, actualizar y versionar (~6 tareas)

**Entrega:** D-07, más las tareas 10–15 de `plan.md`.

**Por qué promover, actualizar y versionar van juntos:** son la misma operación —escribir de
PD hacia la ficha— una creando y otra actualizando. Si el versionado aterriza después de los
primeros «Pasar a SIGER», esos quedan como agujeros sin historial. El versionado tiene que
existir desde la primera escritura.

**Contenido:**

1. Historial de versiones (forma en P-06).
2. Promover: expediente → ficha nueva. Nace como versión 1.
3. «Pasar a SIGER»: diff contra lo publicado, confirmación, escritura y versión nueva (PR-07).
4. Ver el historial y una versión anterior.

**Riesgo:** si el historial se implementa como fila-por-versión en `TramitesSiger`, revienta
`ExpedienteTramite.TramiteSigerId`, `ConciliacionSiger.TramiteSigerId`, la unicidad de
`Codigo` y el índice filtrado de `IdSiger`, y obliga a filtrar por versión vigente en el
catálogo, Completitud, Tablero y Conciliación. Ver P-06.

---

### Fase 7 — De SIGER a PD: importar y partir el editor (~5 tareas)

**Entrega:** D-05 y D-06. Dirección completamente nueva; no existe en ningún documento previo.

**Depende de la Fase 5.** Sin los campos nuevos del expediente, importar pierde datos.

**Contenido:**

1. El bucket «Trámites Importados de SIGER» por institución: qué es ese expediente
   contenedor, cómo se crea, quién lo ve.
2. Selector de expediente destino (D-06: existente o bucket).
3. La importación en sí: mapeo campo a campo, y qué **no** se trae (P-02, P-03).
4. Guarda contra doble importación: dos personas importando la misma ficha producirían dos
   trámites de expediente apuntando a la misma ficha. El enlace queda conciliado al importar.
5. Partir el editor de SIGER: los campos de contenido pasan a solo lectura con enlace al
   expediente; los campos propios de SIGER siguen editables ahí (P-03).

**Riesgo:** que «partir el editor» se implemente como «bloquear el editor», dejando
`EstadoSiger`, `EsPopular`, tareas de digitalización, observaciones DIGER, vigencia y los
campos de SOL sin ningún lugar donde editarse.

---

### Fase 8 — Visibilidad y cierre (~3 tareas)

Las tareas 16–17 de `plan.md`: insignia en el expediente, aviso en el detalle, filtro en el
inventario. Más la actualización de `diseno.md` y `plan.md` a lo acordado aquí.

---

## 4. Preguntas abiertas

Cada una lleva recomendación, para que baste aprobar o corregir.

**P-01 — ¿La bandera de publicación es manual pura, o manual con compuerta de estado?**
D-08 dice «pueden seleccionar». Recomiendo **manual pura**, con `ReglaPublicacion` demotada a
*advertencia* visible en la pantalla («esta ficha está Registrada» / «le faltan campos»),
sin bloquear. Un bloqueo duro haría que la pantalla se niegue a publicar fichas que el
administrador sí quiere publicar; la advertencia cubre el riesgo sin quitarle el control.

**P-02 — Pasos de SIGER ↔ flujo del expediente: ¿se mapean?**
El mapeo es ambiguo y con pérdida en ambos sentidos. `FlujoNodo` tiene `Fase` (actual vs
propuesto — habría que elegir cuál se publica), `Tipo` y `RetornoA`, que no tienen
contraparte en SIGER; `PasoSiger` tiene `Descripcion` obligatoria y una `Modalidad` que
incluye «Interno», que no tiene contraparte en el flujo. Recomiendo que **los pasos sigan
siendo propiedad de SIGER** y no se mapeen, igual que las tareas de digitalización: el flujo
del expediente es un artefacto de análisis, no una lista de pasos para el ciudadano.

**P-03 — Al partir el editor, ¿qué campos siguen editándose en SIGER?**
Mi lista: `EstadoSiger`, la bandera de publicación en HA, `EsPopular`,
`TareasDigitalizacion`, `ObservacionesDiger`, `VigenciaDocumento`, `Temporalidad`,
`EstaEnSol` y el tramo SOL. Ninguno tiene contraparte en el expediente. ¿Falta o sobra alguno?

**P-04 — ¿Dónde se captura el tramo SOL: en la ficha SIGER o en el trámite del expediente?**
Recomiendo **en la ficha SIGER**, por coherencia con el reparto de propiedad ya acordado:
es dato de publicación, no de contenido del trámite.

**P-05 — ¿Qué se hace con las URLs SOL completas ya cargadas?**
Recomiendo **dejarlas quietas**: componer cuando hay tramo, y usar la URL completa tal cual
cuando no lo hay. Partir las existentes en base + tramo es adivinar, y se adivina mal.

**P-06 — Versionamiento: ¿tabla de fotos o fila por versión?**
Recomiendo una tabla de versiones que guarde una **foto** de la ficha y sus hijos, dejando la
fila viva como la última versión. Cumple igual lo pedido en D-07 —no se borra, queda
historial, se muestra la más nueva— y no toca ni una FK ni un índice. Fila-por-versión es lo
que dispara el riesgo descrito en la Fase 6.

**P-07 — «Quitar de HA» = despublicar, no borrar.** Lo asumo así salvo que me corrija.

---

## 5. Riesgos transversales

| Riesgo | Dónde |
|---|---|
| Enlaces SOL rotos en HA por el cambio de significado de `SolUrl` | Fase 4 |
| Autopublicar 253 fichas sin revisar en el relleno | Fase 3 |
| Duplicados por importar dos veces la misma ficha | Fase 7 |
| Enrutamiento de edición no determinista si no se arregla la conciliación | Fase 2 → 7 |
| Historial con agujeros si el versionado llega después del primer «Pasar a SIGER» | Fase 6 |
| Campos propios de SIGER sin lugar donde editarse | Fase 7 |
| Conversión de modalidades corriendo después del CHECK | Fase 5 |

Reglas ya vigentes en `plan.md` que siguen aplicando: `EnableRetryOnFailure` es incompatible
con `BeginTransaction` explícito; las migraciones necesitan `--output-dir Persistence\Migrations`;
`ExpedienteTramite.Id` no es estable; y el reparto de proveedor entre `Application.Tests`
(EF In-Memory, sin CHECK ni índices únicos) y `Web.Tests` (SQLite, que sí los aplica).
