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
| **D-04** | La institución gana una URL base de SOL. El trámite solo guarda el tramo final. *(Hecho — ver Fase 7.)* |
| **D-05** | Editar un trámite que ya está en PD se hace **siempre en el expediente**. |
| **D-06** | Al importar, el usuario elige el expediente destino, **o** el bucket «Trámites Importados de SIGER» de esa institución. |
| **D-07** | «Pasar a SIGER» crea una **versión nueva**. La anterior no se borra. Se muestra la más nueva. *(Hecho — ver Fase 9.)* |
| **D-08** | Quién controla PD **selecciona manualmente** qué trámites se publican en HA. |
| **D-09** | PD tiene una pantalla que lista todo lo publicado en HA, para administrarlo. |
| **D-10** | La publicación es **manual pura y no bloquea**. La regla de estado queda como *advertencia*. |
| **D-11** | Los pasos del proceso siguen siendo propiedad de SIGER. **No** se mapean con el flujo del expediente. |
| **D-12** | El contenido se edita en el expediente. `EstadoSiger` es lo único que se sigue editando **solo** en la ficha. *(Hecho — ver Fase 8.)* |
| **D-13** | El trámite captura solo el tramo final de la URL SOL, con `sol.pdihonduras.gob.hn/<URL de la institución>/` como prefijo fijo en pantalla. *(Hecho. El prefijo va pegado al campo, no como texto de ayuda.)* |
| **D-14** | Las URLs SOL completas ya cargadas **no se tocan**, y solo las usan los trámites que nunca pasaron por PD. *(Hecho. Medido: la única que hay apunta a google.com y está publicada — ver Fase 7.)* |
| **D-15** | El historial es una **tabla de fotos** de la ficha y sus hijos. La fila viva es la última versión. *(Hecho. La versión 0 quedó de la Fase 2; los pases escriben de la 1 en adelante.)* |
| **D-16** | «Quitar de HA» **despublica**, no borra. |
| **D-17** | **Bloqueo condicional.** Si la ficha ya está en PD, sus campos de contenido quedan bloqueados en la ficha y solo se editan en el expediente. Si no está en PD, se editan en la ficha. Nunca en los dos lugares a la vez. |
| **D-18** | Antes de tocar nada se guarda una **foto del inventario SIGER original**, completa y permanente. |
| **D-19** | Hay una fase de **llenado asistido** de los campos que falten, adelantada para aprovechar que casi todo el inventario está libre. |
| **D-20** | La URL base de la institución **sale por defecto de su llave primaria** (`CONSUCOOP`, `IHADFA`), y se puede corregir a mano cuando la ruta real de SOL difiera. *(Hecho. La columna es anulable y nula significa «vale la llave»: no se copia el Id.)* |
| **D-21** | Los buckets de importación se marcan con `OrigenExternoId` y se **excluyen** de los listados, conteos y tableros del módulo de expedientes. |
| **D-22** | Desenlazar una ficha la **desbloquea**, con advertencia explícita de que vuelve a editarse por su lado. |
| **D-23** | La captura en lote **se queda como está**. Solo debe excluir las fichas bloqueadas. |
| **D-24** | El llenado asistido deja todo en **cola de revisión**; no escribe directo. Cada valor propuesto queda con su procedencia registrada, para distinguirlo después de lo verificado por una persona. *(Al construirlo, la procedencia quedó en la fila de la propuesta y no en una columna `Autollenado` de la ficha — ver Fase 5.)* |
| **D-25** | La documentación del API se hace en la **Fase 6**, sin esperar al resto, y **consolidando**: la especificación generada es la verdad sobre la forma, y el documento a mano solo cubre lo que aquélla no puede expresar. *(Hecho. Redactarla encontró tres afirmaciones falsas que llevaban meses publicadas en Swagger — ver Fase 6.)* |

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
ejemplo acordado es `sol.pdihonduras.gob.hn/CONSUCOOP/…`, en mayúsculas como la llave.*

*Al implementar (Fase 7): la ruta se emite tal como esté escrita —la llave en mayúsculas, o lo que
alguien haya corregido a mano—. No se fuerza a minúsculas ni al revés, porque forzarlo sin saber
qué espera SOL sería elegir al azar entre dos direcciones y una de las dos da 404. Lo que sí quedó
resuelto es que **cualquier llave sirve como ruta**: las 45 son solo letras, números, guion y
guion bajo, y la factoría de `Institucion` no admite otra cosa.*

*El pendiente mayor que apareció al implementar —**cuál es el host de SOL**— quedó **RESUELTO el
25 de agosto de 2026**: es `sol.pdihonduras.gob.hn`, confirmado por DIGER. Convivían dos, porque
este plan escribía `sol.gob.hn` y el editor de fichas llevaba desde el 14 de agosto un marcador de
posición con el otro. Ganó el del editor. Vive en `Sol:UrlBase`, así que si algún día cambia es
una línea de configuración y no un despliegue.*

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


### Fase 5 — Llenado asistido — HECHA

**Entrega:** D-19, D-24.

**El problema medido.** De 1 057 fichas, 1 032 no tienen categoría, ni modalidad, ni tiempo, ni
costo: 4 128 huecos. Llenarlos a mano son meses; llenarlos en automático y directo mete datos sin
verificar en el portal que ve el ciudadano. D-24 corta por en medio: la máquina propone en masa,
la persona decide en bloque.

**Lo que se descubrió al mirar los datos, y que cambió el diseño.** Antes de escribir una línea se
midió qué había de dónde derivar, y dos de los cuatro campos no tenían nada:

| Señal | Realidad |
|---|---|
| `CostoTexto` | lleno en **1** ficha de 1 057 — el costo no se deriva de ahí |
| `PasoSiger.Modalidad` | **ningún** paso la declara |
| `EstaEnSol` / `DisponibleEnLinea` | en **0** fichas |
| `PasoSiger.TiempoRegistrado` | **numérico en días** (1, 0.5, 30) en 706 fichas — esto sí se suma |
| Lugares de atención | en 1 008 fichas |

Sin esa medición, la regla del costo se habría escrito «si no menciona pago, es gratuito», que
es la peor de las respuestas posibles: un dato inventado con la misma apariencia que uno
verificado.

**Lo construido:**

1. **`PropuestasLlenado`** — la cola. Cada fila es un valor propuesto para un campo de una ficha,
   con su **certeza** y su **justificación** en lenguaje llano. Índice único **filtrado** sobre
   `(TramiteSigerId, Campo)` para pendientes: garantiza en la base que un hueco no acumule dos
   propuestas, y deja volver a proponer lo que alguien rechazó si la regla mejora.
2. **Cuatro reglas** (`ReglasLlenado`), funciones puras y probadas sin base de datos:
   - *Tiempo* — suma los días declarados en los pasos. Certeza **Alta** si todos declararon;
     Media si alguno no, porque entonces la suma se queda corta. Redondea **hacia arriba**.
   - *Costo* — solo si el texto de pasos o requisitos dice algo, y **cita la frase**. El silencio
     no se interpreta.
   - *Categoría* — palabras del nombre contra las ocho categorías; la descripción solo si el
     nombre calla, y con certeza más baja. **Un empate no se resuelve, se abandona.**
   - *Modalidad* — la más débil; ninguna respuesta pasa de certeza **Baja** y todas se declaran
     «supuesto, no dato».
3. **Pantalla SIGER → Llenado asistido** con filtros por institución, campo y certeza; aprobar y
   rechazar marcadas, y **aprobar todo lo que coincide con el filtro** diciendo cuántas son antes
   de hacerlo. Permiso propio `Siger.Llenado`.
4. **Aprobar nunca pisa trabajo humano.** Antes de escribir se comprueba que el campo siga vacío;
   si alguien lo llenó a mano entre la propuesta y la aprobación, la propuesta se descarta y se
   reporta aparte.

**Medido en Ensayo, sobre las 1 032 fichas reales:**

| | Propuestas | |
|---|---|---|
| modalidad | 989 | Presencial 630 · Híbrido 357 · Virtual 2 |
| categoría | 751 | repartidas en las ocho categorías |
| tiempo | 703 | 625 de ellas con certeza Alta |
| costo | 368 | 363 «tiene costo» · 5 «gratuito» |
| **total** | **2 811** | Alta 625 · Media 887 · Baja 1 299 |

Quedan **1 317 huecos sin propuesta** —664 de costo, 329 de tiempo, 281 de categoría, 43 de
modalidad—. No es un fallo: es la parte del inventario que ninguna regla puede derivar
honestamente y que necesita a una persona. La pantalla los cuenta a la vista para que no pasen
por resueltos.

Segunda corrida: **0 propuestas nuevas**, 2 811 reconocidas como ya encoladas. Idempotente.

**Desviación de D-24, deliberada: no hay columna `Autollenado` en la ficha.** La procedencia vive
en la fila de la propuesta, que sobrevive a la aprobación. El motivo es que una bandera guardada
en `TramitesSiger` se vuelve mentira en cuanto alguien corrige el campo a mano —seguiría diciendo
«esto lo puso una máquina» sobre un valor que puso una persona— y limpiarla obligaría a enganchar
todas las rutas de edición. Aquí la pregunta se responde comparando: el campo es de origen
automático si existe una propuesta aprobada para él **y** la ficha todavía tiene ese valor
(`ValorLlenado.SigueVigente`). Esa respuesta no se desactualiza sola. Si se prefiere la columna
literal, es media hora de trabajo.

**Despliegue:** `scripts/sql/12-cola-llenado-asistido.sql`, idempotente, probado dos veces contra
una base llevada al estado exacto previo. No toca ningún dato existente: crea una tabla vacía.

---

### Fase 6 — Documentación del API pública — HECHA

**Entrega:** D-25.

**El problema medido.** Había dos descripciones de la misma API: la que Swagger genera de los
comentarios XML del código, y un `docs/api-v1/openapi-v1.yaml` **escrito y mantenido a mano**.
Cuando dos documentos describen lo mismo, divergen. Y esta divergencia no la ve nadie de este
lado: la ve el integrador, y la descubre cuando su código ya falló.

**Redactar la documentación encontró tres contratos rotos.** Ninguno era un fallo del código —el
código siempre hizo lo correcto—. Lo que estaba mal era lo que se le decía a quien integra, y
llevaba meses publicado en Swagger:

| Lo que la documentación afirmaba | Lo que la API hace |
|---|---|
| Se puede ordenar por `institucion` y por `tiempo` | Solo reconoce `nombre`; lo demás cae en el orden por omisión **sin avisar** |
| Por omisión ordena por nombre | Por omisión pone primero los **populares** |
| La modalidad admite `Mixto` | Se llama `Hibrido`; `Mixto` devuelve cero resultados |
| Un `tamano` fuera de rango «se recorta al intervalo» | **Vuelve a 20.** Pedir 500 devuelve 20, no 100 |

La tercera es la que más cuesta: quien pagine en bucle contando con 100 hace cinco veces más
peticiones de las que presupuestó, y nada se lo dice.

Se comprobó además una afirmación que **sí era cierta** —que la búsqueda ignora tildes— y de paso
se documentó de dónde sale: no del código, sino de la colación `Modern_Spanish_CI_AI` de las
columnas (migración `CorregirColacionBusqueda`). Apuntar esta API a una base sin esa colación
cambia el comportamiento sin que nada avise.

Y se documentó la asimetría que más fácil sorprende: **`?modalidad=Virtual` devuelve también los
híbridos** —un trámite híbrido también se puede hacer en línea— pero `?modalidad=Hibrido` no
devuelve los virtuales. Estaba en el código desde el principio, en ninguna documentación.

**Lo construido:**

1. **La especificación se genera y ya no se escribe.** `docs/api-v1/openapi-v1.yaml` lo produce
   ahora el código, con una cabecera que dice cómo regenerarlo y prohíbe editarlo. El documento
   **no declara ningún servidor**: la dirección depende del ambiente, no del contrato, y clavarla
   haría que la especificación dijera que la API vive en la máquina de quien la generó.
2. **`docs/api-v1/README.md`** con lo que una especificación no sabe decir: las dos cadencias de
   sincronización y por qué hacen falta las dos, el punto ciego de la fecha de modificación, qué
   significa `fichaCompleta` y por qué se publica lo incompleto, los tres estados del costo, los
   tres significados de un `solUrl` vacío, la clave y los límites.
3. **`tests/Presentation.Tests`** — proyecto nuevo, 15 pruebas. Levanta la API real en memoria.
   - La comprobación de desfase: descarga la especificación que el código genera hoy y la compara
     con la comprometida. Se verificó que **falla de verdad** adulterando el archivo a propósito,
     no solo que pasa cuando todo coincide.
   - La superficie exacta: **estas siete rutas y ninguna más**. Hacia abajo protege a
     HondurasÁgil, que ya depende de las siete; hacia arriba obliga a que una ruta nueva pase por
     una decisión en vez de colarse sola en la v1.
   - Doce pruebas de contrato que atan **exactamente las frases** que ahora están escritas: el
     orden, la asimetría de la modalidad, el tamaño que vuelve a 20, el 404 compartido entre lo
     no publicado y lo inexistente, y que `/salud` es la única ruta sin clave.

**Por qué las pruebas de contrato y no solo la comprobación de desfase.** La comprobación atrapa
que el archivo se quede atrás, pero no que la prosa mienta: los tres errores encontrados vivían en
comentarios que Swagger copiaba fielmente. Una documentación generada de una frase falsa sigue
siendo falsa. Ahora, si alguien cambia el comportamiento, falla una prueba que nombra la frase que
hay que corregir.

**Medido en Ensayo el 25 de agosto de 2026**, y anotado en la guía porque el integrador lo
necesita: 1 057 fichas en el inventario, **50 publicadas**, y de ésas **21 completas**. Con
`?soloFichasCompletas=true` —el filtro que debe usar un portal de cara al ciudadano— el catálogo
se reduce a menos de la mitad. No es un defecto de la API: es el estado real de la captura.

**Despliegue: ninguno.** Esta fase no toca esquema ni datos. No hay script que llevar al
Producción real.

**Lo que NO se documentó, a propósito:** el versionado y el flujo de promoción. Son internos y
todavía no existen del lado del contrato público; documentar lo que no está construido es la forma
más segura de que la documentación empiece a mentir el primer día.

---

### Fase 7 — URL SOL compuesta — HECHA

**Entrega:** D-04, D-13, D-14, D-20.

**Lo que se midió antes de diseñar, y lo que cambió.**

| Señal | Realidad |
|---|---|
| Fichas con `SolUrl` | **1 de 1 057**, y en las dos bases (Ensayo y la copia de Producción) |
| Qué dice esa dirección | **`https://google.com`** en ambas — un valor de prueba, no una dirección de SOL |
| Estado de esas fichas | **`Publicado = 1` y `EstaEnSol = 1`** |
| Llaves de institución con caracteres que no van en una URL | **0** de 45 |
| Host de SOL según el plan | `sol.gob.hn` — **descartado**, ver abajo |
| Host de SOL según el editor de fichas | `sol.pdihonduras.gob.hn` |

Dos consecuencias.

**La primera es un defecto vivo, no una hipótesis.** La única ficha con enlace a SOL de todo el
inventario está publicada y marcada como disponible en línea, así que la API pública viene
emitiendo `solUrl: "https://google.com"` y HondurasÁgil pintaría un botón de «hacer el trámite en
línea» que lleva a Google. No se corrigió el dato —no es una decisión técnica— pero **sí se hizo
corregible**: el editor gana una casilla para quitar el enlace heredado, que antes no existía.

**La segunda desmonta el riesgo que el plan le atribuía a esta fase.** Decía que pasar a guardar
el tramo rompería los enlaces SOL de HondurasÁgil. Enlaces reales que romper hay **cero**: el
único es un marcador de posición. Y el riesgo quedó además cerrado por construcción, porque la
API sigue emitiendo la URL absoluta.

**El host quedó en configuración, no en código, y ya está confirmado.** Al implementar convivían
dos: este plan escribía `sol.gob.hn` y el editor de fichas llevaba desde el 14 de agosto un
marcador de posición con `sol.pdihonduras.gob.hn`. Se dejó en `Sol:UrlBase` justamente porque
componer con el equivocado no rompe un enlace sino **mil**, todos con apariencia correcta.
**DIGER confirmó el 25 de agosto que es `sol.pdihonduras.gob.hn`**, y ese es el valor vigente.

**Lo construido:**

1. **`Institucion.RutaSol`**, anulable, con setter privado y factoría validadora. **Nula significa
   «nadie la ha corregido»** y entonces vale la llave (`RutaSolEfectiva`). No se copia el Id a la
   columna al crear la institución, por la misma razón que en la Fase 5 no se guardó la bandera de
   autollenado: un valor copiado se vuelve mentira en cuanto cambia el original, y borra la
   diferencia entre «nadie lo tocó» y «alguien lo puso igual a la llave». Vaciar el campo la
   devuelve al valor por defecto en vez de dejar la institución sin ruta.
2. **`TramiteSiger.SolTramo`** y **`DireccionSol`** (en Domain), el único lugar donde una dirección
   se arma. Normaliza barras ahí y no en cada uso: repartido entre la API, el editor, el detalle y
   la captura en lote, bastaría con que uno de los cuatro olvidara recortar para producir `//`.
   Rechaza espacios y tildes en vez de escaparlos —un enlace escapado existe, se ve bien y lleva a
   un 404 que nadie reporta—.
3. **Las pantallas.** El editor de fichas enseña el prefijo **pegado al campo**, no como texto de
   ayuda: quien captura tiene que ver la dirección que va a producir, no imaginarla. El editor de
   instituciones permite corregir la ruta, diciendo cuál es el valor por defecto.
4. **El `CHECK` de la base**, que era lo primero que rompía. `CK_TramitesSiger_Sol` exigía una
   `SolUrl` absoluta; con él intacto, guardar una ficha capturada como manda D-13 habría fallado
   contra la restricción. Ahora vale el tramo **o** la heredada.

**La trampa que se cerró a propósito.** El enlace heredado dejó de ser editable —se enseña pero no
se escribe—, y ese es justo el momento en que un formulario lo borra en silencio: el POST no trae
el valor y el código lo cree vacío. Se lee de la base y no del formulario, y hay una prueba que
falla si alguien lo cambia. Nadie se habría enterado hasta que un enlace del portal ciudadano
dejara de funcionar, y para entonces el dato ya no estaría.

**El contrato público no se movió, y está comprobado.** `solUrl` sigue siendo una URL absoluta.
La comprobación de desfase que dejó la Fase 6 pasó sin regenerar nada: la especificación generada
hoy es idéntica byte a byte a la comprometida. Es la primera vez que ese guardián sirve para algo.

**Pruebas:** 41 nuevas, 337 en total. Las de `DireccionSol` cubren las barras de sobra en todas
sus combinaciones, la precedencia del tramo sobre la heredada, y que la llave de una institución
siempre sirva como ruta —si algún día se aflojara la validación de la llave, esa prueba lo diría—.

**Despliegue:** `scripts/sql/13-url-sol-compuesta.sql`, idempotente, probado contra una base
llevada al estado **exacto** de la copia de Producción (que va una migración por detrás de
Ensayo). Dos corridas seguidas, ambas limpias. Se comprobó además, con inserciones dentro de una
transacción revertida, que el `CHECK` nuevo acepta una ficha en SOL con solo tramo y sigue
rechazando una que diga estar en SOL sin ningún enlace. **No toca datos existentes:** las dos
columnas nacen en NULL. Es independiente del script 12, así que puede aplicarse antes o después.

---

### Fase 8 — El expediente aprende a guardar todo lo que SIGER guarda — HECHA

**Entrega:** D-12, y lo que D-17 necesita para poder aplicarse.

**Por qué esta fase existe.** D-17 invierte quién manda: en cuanto una ficha SIGER queda enlazada
a un expediente, sus campos de contenido se vuelven de solo lectura en la ficha y solo se editan
desde el expediente. **Un campo que el expediente no sepa guardar es un campo que, a partir de ese
momento, nadie puede editar en ninguna parte** — y nada avisaría: el formulario lo aceptaría y lo
descartaría en silencio.

**Lo que se midió antes de tocar nada** (Ensayo, 25-08-2026, 240 trámites de expediente):

| Señal | Realidad |
|---|---|
| Modalidad escrita a mano | 202 de 240, en **ocho** variantes distintas |
| La variante dominante | «En línea» (166), y una sin tilde: «En linea» (1) |
| `DocEntregado` | 202 trámites |
| `Telefono` | 197 trámites · `DirSede` en 6 de 36 expedientes |
| Trámites ya enlazados a una ficha | **1** — el bloqueo de D-17 hoy no afecta a nadie |

Las ocho variantes caen en el catálogo sin ambigüedad, así que la conversión es revisable de un
vistazo en vez de ser un salto de fe.

**Lo construido:**

1. **Ocho campos nuevos en el trámite del expediente:** categoría, detalle de la modalidad,
   gratuidad, vigencia del documento, temporalidad, observaciones DIGER, si está en SOL y el tramo
   del enlace. Más **dos tablas hijas**: entregables y lugares de atención, con la misma regla de
   reemplazo en bloque que las diez colecciones que ya existían.
2. **La modalidad pasa a catálogo cerrado**, protegida por un CHECK, y **el texto original se
   conserva** en un campo aparte. «En línea (total)» y «En línea» acaban las dos en `Virtual`, y
   ese «(total)» lo escribió alguien queriendo decir algo; después de convertir no hay forma de
   recuperarlo.
3. **La conversión de los 202 y el CHECK van en la misma migración, en ese orden.** El CHECK no se
   crea junto con las columnas a propósito: puesto antes, la migración fallaría contra cualquier
   base con datos.
4. **Siembra**: `DocEntregado` pasa a ser el primer entregable, y el teléfono del trámite más la
   dirección de sede del expediente pasan a ser su primer lugar de atención. Nadie tiene que
   volver a teclear 202 documentos y 197 teléfonos.
5. **La pantalla del expediente** gana la tarjeta «Ficha pública» y las dos tablas repetidoras.

**Resultado de la conversión, medido:** 183 Virtual · 16 Hibrido · 3 Presencial · 38 sin modalidad.
**Cero fuera del catálogo**, y las 202 conservan su texto original. Siembra: 202 entregables y
236 lugares, ninguno vacío.

**Un desplegable que perdía datos en silencio.** El editor ofrecía «Mixto» y «En línea (parcial)».
«Mixto» no contiene ni «linea» ni «presencial», así que al normalizar se convertía en nada: quien
eligiera esa opción se quedaba sin modalidad y nada se lo decía. El desplegable pasa a ofrecer el
catálogo real, y el matiz se escribe aparte.

**La pantalla y la siembra tenían que ir juntas, y esa es la parte que más fácil se rompe.**
Guardar un expediente borra y reinserta todos sus hijos desde el formulario. Si lo sembrado no se
pintara, el primer guardado de cada expediente lo borraría — 202 entregables y 236 lugares— sin
error, sin aviso y sin forma de notarlo hasta ver la ficha publicada vacía.

**El script de despliegue encontró un fallo que las migraciones no pueden encontrar.** En el
`.sql` de producción todas las migraciones viajan en un solo lote, y SQL Server analiza el lote
entero antes de ejecutar nada: el `UPDATE` que copia el texto de la modalidad se refería a una
columna que la misma migración crea tres instrucciones más arriba, y fallaba con *Msg 207, Invalid
column name*. `dotnet ef database update` no lo ve, porque manda cada migración por separado. Se
resolvió metiendo esa instrucción en `EXEC`, que se compila al ejecutarse.

**Lo que no se sembró, a propósito:** el `Horario` del trámite. Un lugar de atención de SIGER no
tiene dónde guardarlo —sus campos son lugar, ciudad, dirección y teléfonos— y meterlo en la
dirección la corrompería. Se queda en el trámite del expediente. **Es un pendiente para la Fase 9**
decidir si el horario viaja a alguna parte o se queda como dato interno.

**Pruebas:** 20 nuevas, 357 en total. Cubren el viaje completo por los **dos** mapeadores —el de la
forma JSON del editor y el de la aplicación—, porque un campo que se pierda en cualquiera de los
dos no da error: el guardado responde bien y el dato simplemente no está.

**Despliegue:** `scripts/sql/14-ficha-completa-en-expediente.sql`. **Es el primero de la serie que
toca datos**, y por eso se probó con datos sucios de verdad: una base de rasguño con las siete
formas de modalidad que hay en el inventario, incluida la que no se reconoce. Dos corridas
seguidas, la segunda sin efecto. Se comprobó también que el CHECK rechaza un valor fuera del
catálogo y acepta los tres válidos. Ninguna fila se borra, y el texto original de la modalidad
sobrevive —el `Down` de la migración lo devuelve—.

---

### Fase 9 — De PD a SIGER: promover, actualizar y versionar — HECHA

**Entrega:** D-07, D-15.

**Promover y actualizar resultaron ser la misma operación**, y por eso van en un solo comando:
escribir del expediente hacia la ficha, una vez creando y las siguientes sobrescribiendo.
Separarlas habría dejado dos caminos que escriben lo mismo y que acabarían discrepando.

**Lo que ya estaba, y no hubo que rehacer.** La Fase 2 dejó el archivo de fotos con las **1 057
fichas ya retratadas** en la versión 0, la entidad con su número de versión, y hasta el valor
`PaseDesdeExpediente` reservado esperando esta fase. El historial no se construyó: se llenó.

**Lo construido:**

1. **`PromocionMapeo`** — el reparto de propiedad, que es el corazón de la fase:

| Manda | Campos |
|---|---|
| **El expediente** | nombre, descripción, objetivo, dirigido a, dependencia, enlace principal, categoría, modalidad, tiempo, costo, vigencia, temporalidad, observaciones DIGER, si está en SOL, tramo del enlace, y las tres colecciones de contenido |
| **SIGER** | `Codigo`, `IdSiger`, `EstadoSiger` y los **pasos del proceso** (D-11) |
| **La curaduría** | `Publicado` y `EsPopular` |

2. **El pase**, con su código propio: una ficha promovida hereda el prefijo de su institución y
   lleva la marca `-P` —`400-P01`— que delata a simple vista que no vino del inventario. Nace
   **sin publicar**: promover y publicar son actos distintos (D-10).
3. **La vista previa**, que dice campo por campo de qué a qué cambia antes de confirmar.
4. **El historial**: la pantalla del archivo pasó de enseñar solo la versión 0 a enseñar
   cualquiera, con la lista de versiones y su fecha.

**La decisión de diseño que más se va a agradecer: el diff se calcula con el mismo mapeo que
escribe.** No hay una lista de campos para comparar y otra para copiar — se crea una ficha de
mentira, se le aplica el mapeo real y se compara. Dos listas paralelas discreparían el día que
alguien agregue un campo a una y olvide la otra, y entonces el diálogo diría «no cambia nada»
mientras el pase sobrescribe algo. Hay una prueba que corre los dos y contrasta.

**Qué archiva y cuándo.** La foto guarda **el estado que se reemplaza**, no el nuevo: es lo que
permite responder «qué decía esta ficha antes del pase del martes». La versión 0 sigue reservada
para el inventario original, así que las de los pases empiezan en 1. **No archiva al crear**, y no
es un olvido: no había ficha que retratar.

**Desviación del plan original.** Allí `EstaEnSol` y el enlace a SOL figuraban del lado de SIGER.
Las fases 7 y 8 los movieron al expediente —D-17 los pone en el grupo de contenido y la Fase 8 le
dio al expediente dónde guardarlos—, así que ahora los manda el expediente. La `SolUrl` heredada
sí se queda en SIGER: es de antes de que las direcciones se compusieran y no tiene equivalente
del otro lado (D-14).

**Un permiso que el plan no pedía.** Pasar un trámite crea o sobrescribe una ficha del catálogo
que ve el ciudadano, así que los dos manejadores exigen además permiso de edición sobre SIGER.
Poder modelar un expediente no es lo mismo que poder escribir en el portal público; sin esa
separación, cualquiera con permiso de expedientes podría reescribirlo.

**Dos defectos encontrados de paso:**

- **El visor del archivo reventaba con un error 500** ante un documento ilegible —justo el caso
  que su tarjeta de «no se pudo interpretar» existía para cubrir—. Un JSON que *parsea* no es un
  JSON *utilizable*: `{}` producía una foto con las seis colecciones en nulo y la pantalla las
  recorría. Ahora se exige que el documento traiga código, que es lo mínimo que tiene toda foto
  de verdad.
- **La importación desde SIGER escribía «En línea (total)»** en el desplegable de modalidad, valor
  que la Fase 8 retiró del catálogo: escribirlo dejaba la modalidad en blanco sin avisar. Ahora
  escribe `Virtual` y deja constancia en el detalle.

**El diálogo dice que trabaja sobre lo guardado**, no sobre lo que hay en pantalla. El editor vive
en el navegador y no manda nada hasta que alguien guarda; leer de la base es lo único honesto, y
decirlo con todas sus letras evita que alguien pase datos viejos creyendo que pasa los nuevos.

**Pruebas:** 30 nuevas, **387 en total**. Las que sostienen la fase: que volver a pasar **no
despublique** una ficha, que antes de sobrescribir quede la foto, que las colecciones se
reemplacen y no se acumulen, y que los pasos del proceso sobrevivan al pase.

**Despliegue: ninguno.** Esta fase no toca el esquema —el archivo de fotos y el enlace al
expediente ya existían desde las fases 2 y 3—. No hay script que llevar al Producción real.


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

**La cola de revisión de la Fase 5 necesita aprobación por tandas — RESUELTO.** D-24 manda que
nada se escriba directo, y son 1 032 fichas: revisar una por una reproduce el problema que la
fase viene a resolver. La pantalla filtra por institución, campo y certeza, y tiene un botón que
aprueba **todo lo que coincide con el filtro**, diciendo cuántas son antes de hacerlo.

**La procedencia debe sobrevivir a la aprobación — RESUELTO, aunque no como decía D-24.** Marca
de dónde vino el dato, no si está pendiente. Se guarda en la fila de la propuesta —que no se
borra al aprobarse— y no en una columna `Autollenado` de la ficha: esa columna se vuelve mentira
en cuanto alguien corrige el campo a mano. Ver la desviación explicada en la Fase 5.

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
