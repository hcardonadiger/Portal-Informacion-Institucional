# Plan revisado — integración PortalDigital ↔ SIGER ↔ HondurasÁgil

Sustituye a `plan.md` en el orden y el alcance. Las tareas de `plan.md` **no se descartan**:
se reubican y se les suman frentes nuevos. `diseno.md` sigue vigente salvo por el reparto de
propiedad de campos, que D-17 rehace.

Las fases están numeradas en el orden en que se ejecutan. La Fase 1 ya está hecha.

Origen: acuerdos con jefatura de HondurasÁgil y PortalDigital.

---

## 1. La meta

Que la información de PD y la de SIGER terminen siendo la misma, **sin haber perdido lo que
SIGER tenía antes**, y pudiendo consultarlo cuando haga falta.

De ahí salen las tres reglas que gobiernan el plan: una sola fuente de verdad por ficha en cada
momento (D-17), una foto de lo original antes de tocar nada (D-18), y un historial de cada
cambio posterior (D-15).

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

Tres lecturas:

1. **El llenado masivo es el problema dominante.** 1 032 fichas × 4 campos.
2. **El expediente cubre hoy el 23 % del inventario y enlaza el 0,1 %.** Un diseño que exija
   pasar por el expediente antes de tocar una ficha bloquearía el 99,9 % del trabajo pendiente.
3. **1 056 de 1 057 fichas están hoy libres de PD**, así que el llenado puede operar sobre casi
   todo el inventario sin bloqueo alguno. Esa ventana se cierra a medida que se importa.

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
| **D-17** | **Bloqueo condicional.** Si la ficha ya está en PD, sus campos de contenido quedan bloqueados en la ficha y solo se editan en el expediente. Si no está en PD, se editan en la ficha. Nunca en los dos lugares a la vez. |
| **D-18** | Antes de tocar nada se guarda una **foto del inventario SIGER original**, completa y permanente. |
| **D-19** | Hay una fase de **llenado asistido** de los campos que falten, adelantada para aprovechar que casi todo el inventario está libre. |
| **D-20** | La URL base de la institución **sale por defecto de su llave primaria** (`CONSUCOOP`, `IHADFA`), y se puede corregir a mano cuando la ruta real de SOL difiera. |
| **D-21** | Los buckets de importación se marcan con `OrigenExternoId` y se **excluyen** de los listados, conteos y tableros del módulo de expedientes. |
| **D-22** | Desenlazar una ficha la **desbloquea**, con advertencia explícita de que vuelve a editarse por su lado. |
| **D-23** | La captura en lote **se queda como está**. Solo debe excluir las fichas bloqueadas. |
| **D-24** | El llenado asistido deja todo en **cola de revisión**; no escribe directo. Cada valor que proponga queda marcado en una columna **`Autollenado`**, para distinguirlo después de lo verificado por una persona. |
| **D-25** | La documentación del API se hace en la **Fase 6**, sin esperar al resto, y **consolidando**: la especificación generada es la verdad sobre la forma, y el documento a mano solo cubre lo que aquélla no puede expresar. |

### D-17 — el bloqueo condicional

**La regla:** una ficha está «en PD» cuando existe un trámite de expediente que la apunta
(`ExpedienteTramite.TramiteSigerId`). Ese solo predicado decide quién manda:

| Estado de la ficha | Dónde se editan sus campos de contenido |
|---|---|
| Sin trámite de expediente que la apunte | **En la ficha.** Captura en lote y llenado asistido operan aquí. |
| Con trámite de expediente que la apunte | **Solo en el expediente.** En la ficha quedan de solo lectura, con enlace al expediente. Los datos viajan al pasar a SIGER. |

**Por qué encaja:** es el mismo predicado que ya gobierna la lectura en D-03. «Se trae de PD si
existe, si no de SIGER» y «se edita en PD si existe, si no en SIGER» son la misma frase. Una
sola regla para leer y para escribir.

**Resuelve las dos presiones a la vez:** no hay meses de trabajo insufrible, porque hoy 1 056 de
1 057 fichas están libres; y no hay edición en dos lugares, porque en ningún momento un campo es
editable desde dos pantallas.

**Es barato:** `ExpedienteTramite.TramiteSigerId` ya tiene índice. Y no depende de
`ConciliacionesSiger` —la tabla que la Fase 3 repara— porque `TramiteSigerId` viaja en el DTO
del expediente y sobrevive a los guardados.

**Los tres grupos de campos:**

| Grupo | Campos | Dónde se editan |
|---|---|---|
| **Contenido** | Nombre, descripción, objetivo, dirigido a, categoría, modalidad, tiempo, costo, requisitos, entregables, lugares, vigencia, temporalidad, observaciones DIGER, si está en SOL, tramo de la dirección | Según el bloqueo |
| **Propio de SIGER** | `EstadoSiger`, `Codigo`, pasos del proceso (D-11) | Siempre en la ficha. Nunca se bloquean. |
| **Curaduría y operación** | Publicación en HA, `EsPopular`, tareas de digitalización | Siempre en la pantalla de administración. Nunca se bloquean. |

### D-18 — la foto original, y por qué va primera

La meta dice «sin haber perdido la información de antes de SIGER». Eso **no** lo garantiza el
historial de D-15, porque ese historial arranca en el primer pase desde un expediente. Para
cuando eso ocurra, la captura en lote y el llenado asistido ya habrán modificado 1 032 fichas.

El único momento en que lo original está íntegro y garantizado es antes de empezar. Después,
D-15 cubre todo lo que venga, con esta foto como versión cero.

### Cómo D-13, D-14 y D-20 conviven

Una sola regla, en un solo lugar:

- Si el trámite tiene tramo → `URL base de la institución` + `tramo`. La base sale de la llave
  primaria salvo que alguien la haya corregido (D-20).
- Si no tiene tramo → la URL completa heredada, tal cual (D-14). Hoy: una ficha.

La API pública sigue emitiendo la **URL absoluta** en todos los casos, para que HA no cambie.
Con D-20 desaparece el caso «institución sin base», porque toda institución tiene llave.

*Pendiente menor de confirmar al implementar: si la ruta de SOL distingue mayúsculas. El
ejemplo acordado es `sol.gob.hn/CONSUCOOP/…`, en mayúsculas como la llave.*

---

## 4. Qué sobrevive de lo ya hecho

**La Fase 1 completa (4 commits en `Jamil`).** Cero reescritura:

- `ModalidadNormalizador`, `CodigoPromovido` — intactos.
- `IdSiger` nulable + índice único filtrado + migración `SigerIdOpcional` — **reforzado**.
- `ReglaPublicacion` — sobrevive, cambia de papel: de determinante a advertencia (D-10).

**Las tareas 5–17 de `plan.md`** se reubican en las fases 7, 8 y 10. Ninguna se descarta.

Tamaño: de 17 tareas a unas **37**. Cuatro hechas, ~33 por delante.

---

## 5. Las fases, en orden

| # | Fase | Tareas |
|---|---|---|
| 1 | **Hecha** — `IdSiger` opcional, índice filtrado, regla unificada | ✓ 4 |
| 2 | **Hecha** — La foto del SIGER original | ✓ 1 |
| 3 | **Hecha** — Detener la pérdida de conciliaciones | ✓ 3 |
| 4 | **Hecha** — Control de publicación en HA + pantalla de administración | ✓ 4 |
| 5 | Llenado asistido | ~3 |
| 6 | Documentación del API pública | ~3 |
| 7 | URL SOL compuesta | ~3 |
| 8 | El expediente aprende a guardar todo lo que SIGER guarda | ~8 |
| 9 | De PD a SIGER: promover, actualizar y versionar | ~6 |
| 10 | De SIGER a PD: importar y aplicar el bloqueo | ~6 |
| 11 | Visibilidad y cierre | ~3 |

---

### Fase 1 — HECHA

`IdSiger` opcional, índice filtrado, regla de publicación unificada, normalizador y generador
de código. 217 pruebas en verde.

---

### Fase 2 — La foto del SIGER original — HECHA

**Entrega:** D-18. Copia completa y permanente de las 1 057 fichas y sus seis colecciones hijas.

**Va primera y no se puede posponer.** Cualquier escritura anterior a esta foto es información
original perdida para siempre. Es la fase más barata del plan y sostiene la mitad de la meta.

**Lo construido:** la tabla `FotosTramiteSiger` guarda cada ficha como documento JSON congelado,
sin llave foránea al inventario para que el archivo sobreviva a que borren la ficha, y con índice
único sobre `(TramiteSigerId, Version)` para que el original sea irrepetible. La captura es
idempotente y va por lotes de cien, así que una corrida interrumpida no pierde lo ya retratado.
Se dispara desde **Siger → Archivo del original**, que además responde de forma permanente
«¿ya se tomó, y de cuántas?». Cada ficha enlaza a **Siger → Original**, que enseña cómo llegó.

Debe quedar consultable, no solo respaldada: la meta dice «poderla visitar».

---

### Fase 3 — Detener la pérdida de conciliaciones — HECHA

**El defecto.** `ExpedienteMapper.Aplicar` llama a `LimpiarHijos()`, que hace `_tramites.Clear()`,
y vuelve a agregar los trámites desde cero: cada guardado borra y reinserta los
`ExpedienteTramite` con Id nuevo. `ConciliacionesSiger` colgaba de ese Id con
`OnDelete(Cascade)`, así que cada guardado se llevaba las decisiones por delante. El enlace
sobrevivía porque `TramiteSigerId` viaja en la fila del formulario; las decisiones **Descartado**
y **ProponerFichaNueva** solo vivían en esa tabla y desaparecían, y el trámite reaparecía en la
bandeja como si nadie lo hubiera revisado.

**Lo que el plan recomendaba estaba mal, y se descartó.** Rekeyar sobre
`(ExpedienteId, TramiteIndex)` no sirve: `TramiteIndex` se asigna por la **posición en el arreglo
del formulario** (`OriginalShapeMapper.ToInput`, `for (var t = 0; ...)`), y el editor permite
quitar un trámite del medio (`quitarTramiteApertura` → `splice`) y reordenar por arrastre. Es
decir, el índice renumera. Rekeyar sobre él habría cambiado «la decisión se pierde» por «la
decisión queda pegada al trámite equivocado» — dato callado y falso en lugar de callado y
ausente, que es peor.

**Lo construido.** `ExpedienteTramite.ClaveEstable`, un Guid que **viaja dentro de la fila del
formulario**, exactamente por el mismo camino que `tramite_siger_id`: se guarda en el objeto al
hacer `snapshotTramites()` y se reconstruye desde ahí en `restoreTramites()`, así que se mueve
con su trámite en vez de quedarse fija en una posición. `ConciliacionSiger` se identifica ahora
por esa clave, y su cascada cuelga del **expediente**, que sí es estable.

La migración no renombra la columna vieja —EF lo propuso, y habría dejado ids de trámite
haciéndose pasar por ids de expediente— sino que crea las nuevas, las rellena cruzando contra
`ExpedienteTramites` mientras la vieja todavía existe, y recién después la borra. El valor por
defecto es `NEWID()` para que cada fila existente reciba una clave distinta; con el `Guid.Empty`
que ponía EF, las 240 filas habrían quedado idénticas y el índice único habría fallado.

Aplicada a Ensayo: 240 claves distintas para 240 trámites, cero vacías, y la única conciliación
que había quedó apuntando al mismo trámite de antes.

**Un defecto emparentado que queda fuera de alcance.** `PlanTrabajo` enlaza sus metas por
`ExpedienteTramiteIndex` y el tablero de trámites arma su llave como
`$"{ExpedienteId}-{TramiteIndex}"`. Ambos sufren la misma renumeración: una meta puede quedar
apuntando a otro trámite si alguien reacomoda el expediente. Ahora existe una clave estable a la
que migrarlos.


### Fase 4 — Control de publicación en HA + pantalla de administración — HECHA

**Entrega:** D-08, D-09, D-10, D-16.

**El defecto que arregla.** `Publicado` no lo elegía nadie: lo recalculaban tres pantallas desde
`EstadoSiger` en cada guardado. Eso tenía dos consecuencias. Corregir una tilde podía sacar un
trámite del portal del ciudadano —o meterlo— sin que nadie lo pidiera. Y como la bandera solo se
recalculaba al editar, había 303 fichas en Aprobado o Completo y solo 50 publicadas: las otras
253 no iban a corregirse nunca, porque nadie las iba a volver a editar.

**Lo construido:**

1. Se quitaron las tres asignaciones automáticas (dos en el editor, una en la captura por lotes).
   `ReglaPublicacion` sobrevive pero cambia de papel: de decidir a aconsejar, y se renombró a
   `EstadoListoParaPublicar` para que el nombre no mienta.
2. Pantalla **Siger → Publicado en HA**: tres pestañas (en HondurasÁgil, candidatas, todo el
   inventario), búsqueda, filtro por institución, selección múltiple y las dos acciones. Cada
   fila lleva su aviso —estado distinto de Aprobado, o campos sin llenar— y **el aviso no
   bloquea** (D-10).
3. Permiso propio `Siger.Publicacion`, separado de `Siger`: se puede corregir contenido todo el
   día sin poder decidir qué sale al público. `Ver` para mirar, `Editar` para publicar y quitar.
4. Quitar de HA **despublica**, no borra (D-16).

**No hizo falta ninguna migración.** El relleno conservador que pedía el plan ocurre por no hacer
nada: la columna ya existe y ya trae las 50 publicadas de hoy. Al dejar de recalcularla, esas 50
se quedan y las 253 candidatas esperan decisión, que es exactamente lo que se buscaba.

**Medido en Ensayo:** 50 publicadas de 1 057, 253 candidatas, ninguna publicada con estado
dudoso, y 29 publicadas a las que les falta algún campo —esas salen con aviso.

Las dos pruebas que más importan no son las de la pantalla sino las que impiden que la bandera
se vuelva a mover sola: editar una ficha publicada sin aprobar no la despublica, y editar una
aprobada sin publicar no la publica. Ambas comprueban además que el guardado ocurrió, para que
no pasen en verde por no haber hecho nada.


### Fase 5 — Llenado asistido (~3 tareas)

**Entrega:** D-19. Completar los campos que faltan en 1 032 fichas, derivando lo que se pueda
derivar y dejando en cola de revisión lo que necesite criterio humano.

**Por qué aquí y no al final:** hoy 1 056 de 1 057 fichas están desbloqueadas, que es la
condición más barata posible para llenarlas. Cada ficha que se importe después queda bloqueada
por D-17, y llenarla habría que hacerlo por el expediente, una por una. La ventana es ahora.

**Condición previa innegociable:** la Fase 2. El llenado toca 1 032 fichas.

**Cómo escribe** (D-24): nada va directo a la ficha; todo pasa por cola de revisión, y cada valor
propuesto queda marcado en la columna `Autollenado`.

---

### Fase 6 — Documentación del API pública (~3 tareas)

**Va aquí y no al final** por tres razones. El consumidor ya existe: HondurasÁgil está integrado
contra esta API hoy, así que documentación que llegue después del consumidor llega tarde por
definición. El contrato ya está estable: de las fases que faltan, **solo la 7 roza la superficie
del API**, y ni siquiera cambia la forma —`solUrl` sigue siendo una URL absoluta, lo que cambia
es cómo se arma—. Y escribir la documentación es una revisión de diseño disfrazada: encontrar un
problema de contrato redactándolo cuesta una tarde; encontrarlo cuando HA ya depende de él
cuesta una migración coordinada entre dos sistemas.

Va **después** de la Fase 5 para que los ejemplos se escriban sobre un catálogo con datos de
verdad y no sobre las 25 fichas completas que hay hoy.

**El problema real no es que falte documentación, es que hay tres.** Existen a la vez los
comentarios XML que alimentan Swagger —generados del código, no pueden desfasarse—, un
`docs/api-v1/openapi-v1.yaml` **escrito a mano** con siete rutas, y `trazabilidad-cambios.md`.
El YAML a mano y el que genera Swagger describen la misma API: es la misma duplicación que ya
mordió con la regla de publicación (tres copias) y con la identidad del trámite (dos), solo que
esta discrepancia no la ve el ciudadano —la ve el integrador, y la descubre cuando su código ya
falló.

**Contenido:**

1. La especificación **generada** pasa a ser la verdad sobre la forma: rutas, campos, tipos,
   códigos. No puede mentir. El YAML a mano se retira o se genera, pero no se mantiene en paralelo.
2. `docs/api-v1/` se queda solo con lo que una especificación generada no sabe decir: cómo
   integrarse, el contrato de frescura con `/cambios` y sus dos cadencias, qué significa
   `fichaCompleta` y por qué una ficha incompleta se publica igual, qué quiere decir un `solUrl`
   vacío, la clave y los límites.
3. Una comprobación que falle si las dos vuelven a divergir.

**Lo que NO se documenta todavía:** el versionado y el flujo de promoción. Son internos y aún no
existen; documentar lo que no está construido es la forma más segura de que la documentación
empiece a mentir el primer día.

---

### Fase 7 — URL SOL compuesta (~3 tareas)

**Entrega:** D-04, D-13, D-14, D-20.

**Ya no está bloqueada.** Antes esperaba a que alguien reuniera 45 direcciones; con D-20 la base
sale de la llave primaria y toda institución tiene una.

1. URL base de SOL en `Institucion`, con la llave como valor por defecto y posibilidad de
   corregirla. Ojo: setters privados y factoría validadora; `RegistrarContacto` ya valida URL
   absoluta con la misma regla. Es cambio de dominio, no solo una columna.
2. Tramo final en la ficha, y composición en un solo lugar. Normalizar barras ahí, no en cada
   uso.
3. La pantalla muestra el prefijo fijo `sol.gob.hn/<URL institución>/` junto al textbox (D-13).

**Riesgo:** `SolUrl` hoy se expone en el catálogo público como URL absoluta y
`SoloFichasCompletas` la evalúa. Si pasa a guardar solo el tramo sin componer en la salida, se
rompen los enlaces SOL de HA.

---

### Fase 8 — El expediente aprende a guardar todo lo que SIGER guarda (~8 tareas)

Si el expediente no puede guardar un campo, ese campo no se puede editar una vez la ficha queda
bloqueada por D-17.

Tareas 5–9 de `plan.md`: categoría, modalidad de catálogo cerrado, gratuidad, las dos tablas
hijas (entregables y lugares), su siembra desde `DocEntregado`/`Horario`/`Telefono`/`DirSede`,
la conversión de las 240 modalidades y la UI. **Más lo que D-12 agrega:** vigencia,
temporalidad, observaciones DIGER, si está en SOL y el tramo de la dirección.

**Riesgo:** la conversión de las 240 modalidades debe correr **antes** de aplicar el CHECK.

---

### Fase 9 — De PD a SIGER: promover, actualizar y versionar (~6 tareas)

**Entrega:** D-07, D-15, más las tareas 10–15 de `plan.md`.

**Por qué van juntos:** promover y actualizar son la misma operación —escribir del expediente
hacia la ficha— una creando y otra actualizando. Si el versionado aterriza después de los
primeros pases, esos quedan como agujeros sin historial.

1. Historial como tabla de fotos, con la foto de la Fase 2 como versión cero.
2. Promover: expediente → ficha nueva.
3. «Pasar a SIGER»: diff contra lo publicado, confirmación, escritura y versión nueva.
4. Ver el historial y una versión anterior.

---

### Fase 10 — De SIGER a PD: importar y aplicar el bloqueo (~6 tareas)

**Entrega:** D-05, D-06, D-21, D-22 y la mitad de D-17. **Depende de la Fase 8.**

1. El bucket «Trámites Importados de SIGER» por institución, marcado con `OrigenExternoId` y
   excluido de listados, conteos y tableros (D-21). Sin eso, los buckets quedarían atrapados en
   `EnExploracion` —`CambiarEstado` es una máquina lineal estricta— y saldrían en todas partes
   como si fueran levantamientos reales.
2. Selector de expediente destino (D-06).
3. La importación: mapeo campo a campo. No se traen los pasos (D-11).
4. Guarda contra doble importación. `OrigenExternoId` ya se usa como clave de idempotencia en la
   importación de reuniones — sirve de precedente.
5. La ficha queda enlazada en el mismo acto, y con eso **bloqueada**.
6. El bloqueo en las pantallas: campos de contenido en solo lectura con enlace al expediente, y
   la captura en lote excluyendo las bloqueadas (D-23). Desenlazar desbloquea, con advertencia
   (D-22).

---

### Fase 11 — Visibilidad y cierre (~3 tareas)

Tareas 16–17 de `plan.md`: insignia en el expediente, aviso en el detalle, filtro en el
inventario. Más actualizar `diseno.md` y `plan.md` a lo acordado aquí.

---

## 6. Notas de implementación

**La cola de revisión de la Fase 5 necesita aprobación por tandas.** D-24 manda que nada se
escriba directo, y son 1 032 fichas. Revisar una por una reproduce el problema que la fase viene
a resolver, así que la cola debe permitir aprobar en bloque —filtrando por institución, por campo
o por nivel de certeza— y no solo de una en una.

**`Autollenado` debe sobrevivir a la aprobación.** Marca de dónde vino el dato, no si está
pendiente. Una vez alguien lo aprueba deja de estar en cola, pero sigue siendo un valor que
propuso una máquina, y eso es lo que hace auditable el llenado más adelante.

**Asumido salvo corrección:** el `Codigo` de la ficha se sigue generando del lado de SIGER; es
identidad de la ficha, no contenido del trámite.

## 7. Riesgos transversales

| Riesgo | Dónde |
|---|---|
| Escribir sobre el inventario antes de tomar la foto original | Fase 2 |
| Llenar fichas después de importarlas, ya bloqueadas | Fase 5 |
| Que la avenida de fichas completas llegue al ciudadano sin compuerta | Fase 4 → 5 |
| No poder distinguir después el llenado automático del verificado | Fase 5 / D-24 |
| Buckets contaminando listados y tableros de expedientes | Fase 10 / D-21 |
| Desenlazar sin advertencia y devolver el mando sin querer | Fase 10 / D-22 |
| Enlaces SOL rotos en HA por el cambio de significado de `SolUrl` | Fase 7 |
| Duplicados por importar dos veces la misma ficha | Fase 10 |
| Conversión de modalidades corriendo después del CHECK | Fase 8 |

Reglas vigentes de `plan.md` que siguen aplicando: `EnableRetryOnFailure` es incompatible con
`BeginTransaction` explícito; las migraciones necesitan `--output-dir Persistence\Migrations`;
`ExpedienteTramite.Id` no es estable; y el reparto de proveedor entre `Application.Tests`
(EF In-Memory, sin CHECK ni índices únicos) y `Web.Tests` (SQLite, que sí los aplica).
