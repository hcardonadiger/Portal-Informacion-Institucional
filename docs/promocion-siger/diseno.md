# Promover un trámite del expediente a SIGER

Diseño validado el 2026-08-21. Sustituye la captura manual de fichas para los
trámites que DIGER levanta en un expediente y que SIGER no tiene.

## El problema

Hoy el portal del ciudadano muestra **únicamente** trámites de SIGER:

```csharp
// GetCatalogoPublicoQuery.cs
// "Sin excepción: es la única condición que hace público al catálogo."
var query = ctx.TramitesSiger.AsNoTracking().Where(t => t.Publicado);
```

Un trámite levantado en un expediente vive en `ExpedienteTramites`, otra tabla.
Nada del código lo copia a `TramitesSiger`: el único lugar que crea fichas es el
editor SIGER (`Siger/Editor.cshtml.cs`), a mano.

El enlace `ExpedienteTramites.TramiteSigerId` ya existe, pero solo se usa en un
sentido — al importar desde SIGER, los datos bajan de SIGER al expediente. En el
otro sentido no hay nada.

Consecuencia medida en la base de ensayo: **240 trámites de expediente, 1
enlazado.** El trabajo de levantamiento no llega al ciudadano.

## La decisión

Un botón **«Promover a SIGER»** en el trámite del expediente, que crea la ficha y
deja el enlace hecho. El trámite del expediente no se elimina, no se mueve y no
se transforma: lo único que cambia es que su `TramiteSigerId`, hoy vacío, apunta
a la ficha nueva.

Y el reparto de responsabilidad que la acompaña: **el expediente es la fuente de
verdad del contenido; SIGER decide qué se publica.** Eso obliga a que el
expediente pueda entregar todo lo que una ficha necesita para publicarse — hoy no
puede, y cerrar esa brecha es parte de este trabajo.

## Preguntas cerradas

| # | Pregunta | Decisión |
|---|---|---|
| PR-01 | ¿Qué pasa después de promover? | **Copia única con botón «Actualizar ficha».** El expediente reescribe lo suyo cuando el analista lo pide, no automáticamente. |
| PR-02 | ¿La ficha nace publicada? | **No.** Nace `EstadoSiger = "Registrado"`, invisible al ciudadano. Promover y publicar son dos actos con responsables distintos. |
| PR-03 | ¿Cómo se identifica una ficha que no viene de SIGER? | **`IdSiger` vacío** y código propio con marca de portal (`400-P01`). |
| PR-04 | ¿Qué se copia? | Cabecera, requisitos, entregables, lugares de atención y —cuando existan— pasos. |
| PR-05 | ¿Se amplía el expediente para que alimente la ficha completa? | **Sí:** categoría, modalidad como catálogo cerrado, lista de entregables y lista de lugares de atención. |
| PR-06 | ¿Promoción en lote? | **No por ahora.** Uno por uno, con revisión. |
| PR-07 | ¿«Actualizar» avisa antes de cambiar? | **Sí.** Muestra qué va a cambiar y pide confirmación. Se cambia igual, pero nadie pierde una corrección sin enterarse. |

## Lo que se verificó antes de diseñar

Datos de `TramitesEstado_Ensayo`, no supuestos:

- **Ningún campo se trunca.** Todos los largos del expediente caben en su destino
  SIGER: nombre 400→600, objetivo 2000→4000, área 200→400, tiempo 100→120,
  requisito 500→2000.
- **Ninguna pantalla del sistema edita pasos, requisitos, entregables ni lugares
  de SIGER.** Entraron por la carga masiva y desde entonces son de solo lectura en
  la aplicación. Por eso reemplazarlos en bloque desde el expediente no destruye
  nada que alguien haya podido escribir aquí.
- **`FlujoNodos` está vacía** (0 filas). El constructor de flujos existe y calza
  casi 1:1 contra `PasosSiger`, pero nadie lo ha usado. El mapeo se deja escrito;
  hoy produce cero pasos.
- **La modalidad del expediente es texto libre y sucio:** diez variantes en 240
  filas, contra un catálogo cerrado de tres valores en SIGER.
- **El enlace inverso ya existe:** `Siger/Detalle` ya lista los expedientes
  vinculados a una ficha.

## Cambios en el modelo de datos

### 1. `TramitesSiger.IdSiger` pasa a admitir vacío

Un trámite nacido en el portal no tiene identificador de SIGER, y el vacío dice
esa verdad sin necesidad de una columna extra: **`IdSiger` vacío significa «esta
ficha no existe en SIGER, la creamos nosotros»**. De ahí salen las insignias, el
aviso en la ficha y el filtro del inventario.

Detalle que hay que hacer bien: SQL Server solo admite **un** nulo en un índice
único. El índice pasa a ser filtrado:

```csharp
b.HasIndex(x => x.IdSiger).IsUnique().HasFilter("[IdSiger] IS NOT NULL");
```

Sin ese filtro, la segunda promoción falla.

### 2. `ExpedienteTramites` gana los campos que la ficha necesita

| Campo nuevo | Tipo | Por qué |
|---|---|---|
| `CategoriaId` | `int?` → FK a `CategoriasTramite`, `SetNull` | Obligatoria para publicar. El expediente no tenía el concepto. |
| `Modalidad` | catálogo cerrado `Virtual` / `Presencial` / `Hibrido` | Hoy es texto libre; SIGER tiene CHECK. Se arregla en el origen, no traduciendo cada vez. |
| `ModalidadDetalle` | `nvarchar(60)` | Guarda el texto libre actual. «En línea (total)» y «En línea Tipo de solicitud» llevan matiz que no se debe perder al normalizar. |
| `EsGratuito` | `bool?` | Ver la nota de abajo. |

**Nota sobre `EsGratuito`.** No estaba en las preguntas cerradas; lo agrego
porque la decisión PR-05 lo exige y sin él no se sostiene. El costo es uno de los
cinco campos que `FichaPublicaCompletitud` exige para publicar, y no se puede
deducir honestamente de los campos de pago del expediente — el propio código ya
dejó dicho que nunca se infiera de un texto vacío. Son tres estados: sin
capturar, tiene costo, es gratuito. Si prefiere que el costo lo siga poniendo
SIGER, se quita este campo y sale de la columna del expediente en el reparto.

### 3. Dos tablas hijas nuevas

Siguen la forma que ya usan `TramiteRequisitos` y `FlujoNodos`
(`ExpedienteId` + `TramiteIndex` + `Orden`), y sus largos son los de su destino
en SIGER para que nunca haya que truncar:

```
ExpedienteTramiteEntregables
  ExpedienteId, TramiteIndex, Orden
  Entregable    nvarchar(1000)  obligatorio
  Formato       nvarchar(2000)
  Presentacion  nvarchar(600)

ExpedienteTramiteLugares
  ExpedienteId, TramiteIndex, Orden
  Lugar         nvarchar(1000)  obligatorio
  Ciudad        nvarchar(2000)
  Direccion     nvarchar(2000)
  Telefonos     nvarchar(1000)
```

### 4. Migración de los datos que ya existen

La conversión de modalidad es **determinista y cubre las 240 filas**, sin casos
sueltos:

| Texto actual | Filas | Queda como |
|---|---|---|
| «En línea», «En linea», «En línea (total)», «Trámite en línea», «En línea Tipo de solicitud» | 183 | `Virtual` |
| «En línea / Presencial», «En línea, Presencial» | 16 | `Hibrido` |
| «Presencial» | 3 | `Presencial` |
| vacío o nulo | 38 | sin capturar |

El texto original se conserva íntegro en `ModalidadDetalle`.

Las dos tablas nuevas se siembran con lo que hoy vive suelto en el trámite:

- `DocEntregado` (lleno en 202 de 240) → un entregable.
- `Horario`, `Telefono`, `EmailTramite` y la dirección de sede del expediente →
  un lugar de atención.

Nadie pierde datos y nadie tiene que volver a teclear.

## Promover

Precondición: el trámite tiene que estar **guardado**. El editor de expedientes
es un formulario de JavaScript con estado sin guardar; si el botón copiara de la
pantalla, promovería datos que todavía no existen en la base. El botón guarda
primero y promueve después, en un solo clic.

Dentro de una transacción:

1. Se genera el código: prefijo numérico que esa institución ya usa en SIGER
   (400 = Aduanas, 24 = Propiedad…) más una `P` de portal y un correlativo
   **por institución** → `400-P01`, `400-P02`, `24-P01`. Si la institución no
   tiene ninguna ficha SIGER de donde tomar el prefijo, se usa `DGR`.
2. Se crea la ficha con `IdSiger` vacío y `EstadoSiger = "Registrado"`.
3. Se copian las colecciones.
4. Se guarda `ExpedienteTramites.TramiteSigerId`.

El código es único, así que la generación va con reintento: dos clics
simultáneos no pueden producir el mismo correlativo.

### Mapeo campo por campo

| Expediente | Ficha SIGER |
|---|---|
| `NombreTramite` | `Nombre` |
| `Expediente.Institucion` / `InstitucionId` | `Institucion` / `InstitucionId` |
| `AreaResponsable` | `Dependencia` |
| `Descripcion` | `Descripcion` |
| `Objetivo` | `Objetivo` |
| `Dirigido` | `DirigidoA` |
| `SitioWeb` | `EnlacePrincipal` |
| `TiempoReal` ?? `PlazoLegal` | `TiempoTexto` |
| `CategoriaId` | `CategoriaId` |
| `Modalidad` | `Modalidad` |
| `EsGratuito` | `CostoEsGratuito` |
| `TgrMonto`, `MetodoPago` | `CostoTexto` (ver abajo) |
| `TramiteRequisitos` | `RequisitosSiger` |
| `ExpedienteTramiteEntregables` | `EntregablesSiger` |
| `ExpedienteTramiteLugares` | `LugaresAtencionSiger` |
| `FlujoNodos` | `PasosSiger` (hoy vacío) |
| — | `IdSiger` = vacío |
| — | `Codigo` = generado |
| — | `EstadoSiger` = «Registrado», `Publicado` = falso |

**Cómo se arma `CostoTexto`.** Si `EsGratuito` es verdadero, queda vacío: «es
gratuito» ya es una respuesta completa y no hay monto que escribir. Si hay costo,
se arma con `TgrMonto` y, cuando exista, `MetodoPago` («L. 250.00 — Depósito
bancario»). Si `EsGratuito` está sin capturar, `CostoTexto` no se toca y la ficha
queda incompleta por costo, que es lo correcto: no se infiere de un texto vacío.

### El diálogo

Muestra lo que se va a crear y, abajo, la frase que ya existe en el sistema:
*«Falta capturar: categoría, costo.»* Reutiliza `FichaPublicaCompletitud.Frase`,
para que la promoción, el detalle y el editor no acaben con tres redacciones del
mismo aviso.

**No exige** que la ficha esté completa. Se puede promover con campos vacíos,
igual que hoy se puede guardar una ficha incompleta — es la misma decisión que ya
se tomó en su momento (P-09, opción 1): una ficha incompleta se guarda, no se
bloquea.

## Actualizar

Segundo botón, visible solo en un trámite ya promovido. Muestra qué va a cambiar
antes de aplicar:

```
Modalidad:   Virtual → Híbrido
Tiempo:      «3 días hábiles» → «5 días hábiles»
Requisitos:  7 → 9
```

Se confirma y se aplica. Si no hay diferencias, lo dice y no hace nada.

### El reparto de propiedad

Esto es lo que hace segura la actualización:

| Reescribe el expediente | No se toca nunca |
|---|---|
| Nombre, Dependencia, Descripción, Objetivo, Dirigido a, Enlace principal | Código |
| Categoría, Modalidad, Tiempo, Costo | `EstadoSiger`, `Publicado` |
| Requisitos, Entregables, Lugares, Pasos | `EstaEnSol`, `SolUrl`, `SolVerificadoEl` |
| | `EsPopular` |
| | `VigenciaDocumento`, `Temporalidad`, `ObservacionesDiger` |
| | `TareasDigitalizacion` |

La columna derecha es lo que SIGER sabe y el expediente no: si esto está en SOL y
dónde, si es un trámite destacado, y si está aprobado para el público. Nada de
eso se puede deducir de un levantamiento.

Las colecciones se reemplazan en bloque. Es seguro precisamente porque ninguna
pantalla del sistema las edita: no hay trabajo que perder.

## Tareas de digitalización

**No se copian, ni ahora ni después.** Son el plan interno de DIGER, no las ve el
ciudadano — la consulta pública no las expone. Y en un trámite promovido no
tienen sentido, porque el expediente entero *es* esa tarea. El enlace inverso que
ya existe en `Siger/Detalle` cuenta esa historia mejor que una lista copiada.

En una ficha promovida, esa sección se muestra vacía con una nota que explica por
qué y enlaza al expediente.

## Visibilidad

Lo que hoy hay es una insignia azul «SIGER» en el editor de expedientes, muda: no
distingue nada y no lleva a ninguna parte.

**En el expediente**, la insignia pasa a distinguir dos casos y a ser un enlace a
la ficha:

- 🔵 **SIGER** — este trámite se trajo del inventario
- 🟢 **EN SIGER** — este trámite lo publicamos nosotros desde aquí

La diferencia sale de `IdSiger`: si está vacío, nació aquí. El mismo dato que
resuelve la identidad resuelve la insignia.

**En la ficha SIGER**, un aviso «Creada desde el expediente EXP-xxx» que la separa
de las 1.057 importadas, más la lista de expedientes vinculados que ya existe. Y
en el inventario, la misma marca, filtrable.

## Permisos

`Siger / Crear` para promover, `Siger / Editar` para actualizar. Quien no puede
crear fichas SIGER no debe poder crearlas por la puerta de atrás. Se usa el
atributo `[Permission]` que ya gobierna esas pantallas.

## Lo que no hace

- No borra ni modifica el trámite del expediente más allá del enlace.
- No publica.
- No promueve en lote.
- No toca las tareas de digitalización.
- No inventa costo ni categoría: si no están, la ficha queda incompleta y se dice.

## Riesgos y detalles a cuidar

1. **El índice único filtrado sobre `IdSiger`.** Sin el filtro, la segunda
   promoción falla con una violación de índice. Es el error más fácil de cometer
   aquí.
2. **La regla de publicación está duplicada a la espera.** `CalcularPublicado`
   vive hoy como método privado dentro del editor. La promoción necesita la misma
   regla; se extrae junto a `FichaPublicaCompletitud` para que no puedan
   discrepar.
3. **Carrera en el correlativo del código.** Generación dentro de la transacción
   y reintento ante violación de unicidad.
4. **Datos sin guardar en el editor.** El botón guarda antes de promover.
5. **Reimportación de SIGER.** Si algún día DIGER vuelve a cargar el inventario,
   el cargador tiene que respetar las filas con `IdSiger` vacío. Hoy no existe tal
   cargador en el código, pero queda advertido.

## Fuera de alcance, anotado

- **`Publicado` está desincronizado en la base:** 303 fichas tienen `EstadoSiger`
  Aprobado o Completo, pero solo 50 tienen `Publicado = 1`. El flag solo se
  recalcula al editar la ficha. Es un defecto anterior a este trabajo y merece su
  propia corrección.
- **Promoción en lote** — 240 trámites esperando, uno por uno va a doler.
- **Los flujos vacíos.** El mapeo a pasos queda escrito, pero mientras nadie use
  el constructor de flujos, las fichas promovidas no tendrán pasos del proceso, y
  esa es la sección más útil para el ciudadano.
