# API pública de trámites (v1) — guía de integración

Esta guía es **lo que una especificación no sabe decir**. La forma de la API —rutas, campos,
tipos, códigos de respuesta— vive en [`openapi-v1.yaml`](./openapi-v1.yaml), que se **genera del
código** y no puede desfasarse. Acá va lo demás: cómo se usa, qué significan los datos y qué
trampas tiene.

> **Regla de la casa:** si algo se puede leer en la especificación, no se repite acá. Dos
> descripciones de lo mismo divergen, y quien paga la divergencia es quien integra.

---

## 1. Qué es esta API

Publica el inventario oficial de trámites del Estado para que otros sistemas lo consuman. Hoy su
único consumidor es **HondurasÁgil**.

**Es de solo lectura.** No hay POST, PUT ni DELETE, y no los habrá en la v1. Los trámites se
editan en PortalDigital, no por acá.

**Solo sirve lo publicado.** Un trámite sin publicar no existe para esta API: pedirlo devuelve
404, el mismo 404 que uno que nunca existió. Es a propósito —distinguirlos permitiría averiguar
qué códigos hay sin poder verlos—.

**La identidad pública es el código** (`603-019`), no el Id interno. El Id interno no viaja
nunca: es un detalle de esta base y podría cambiar.

---

## 2. Dónde vive

| Ambiente | Dirección | Swagger |
|---|---|---|
| Desarrollo local | `https://localhost:7199` | `/swagger` |
| Ensayo | la que fije IIS al desplegar | solo si se enciende `PortalDigitalApi:PublicarSwagger` |
| Producción | **pendiente de definir** — se fija al desplegar | apagado salvo que se encienda a propósito |

La especificación generada **no declara ningún servidor**, y es deliberado: la dirección depende
del ambiente y no del contrato. Si el documento clavara un host, diría que la API vive en la
máquina de quien lo generó.

Swagger sale publicado siempre en Development. Fuera de Development hay que encenderlo a
propósito con `PortalDigitalApi:PublicarSwagger`, para que un entorno de integración pueda
consultarlo sin que el contrato entero quede expuesto en producción por olvido.

---

## 3. La clave

Toda ruta pide la cabecera `X-Api-Key`. Sin ella, o con una que no coincida, la respuesta es
**401**.

```
GET /api/v1/tramites?institucion=INPREMA
X-Api-Key: <la clave>
```

**`/api/v1/salud` es la única excepción**, y también es a propósito: un monitor externo no
debería tener que custodiar un secreto para comprobar que el servicio está en pie.

La clave **nunca va en `appsettings.json`**, que se versiona. En desarrollo vive en user-secrets:

```
dotnet user-secrets set "PortalDigitalApi:ApiKey" "<clave>" --project src/Api
```

Dónde vive en producción sigue **sin decidirse** (P-03 en [`decisiones-fase0.md`](./decisiones-fase0.md)).

---

## 4. Los límites

**300 peticiones por minuto por clave**, en ventana fija, sin cola: la que sobra recibe **429** de
inmediato, no espera turno. El límite es generoso a propósito, pensando en un cliente que
reintente con ganas durante una sincronización.

Las respuestas van comprimidas (Brotli o gzip) si el cliente lo acepta. El catálogo completo es
mucho texto repetido; conviene mandar `Accept-Encoding`.

---

## 5. Cómo sincronizar: hacen falta dos rutas, no una

Ésta es la parte que más fácil se implementa mal, porque `/cambios` **parece** suficiente y no lo
es.

### La cadencia corta — `/cambios?desde=`

El día a día. Devuelve los **códigos** que cambiaron desde una fecha, no las fichas: el consumidor
pide después el detalle de cada uno.

Junto a los códigos viene **`generadoEl`, la hora del servidor**. Guárdela y úsela como el
siguiente `desde`. Si usa su propio reloj, cada ciclo pierde la franja en que los dos relojes no
coinciden, y esos trámites no vuelven a aparecer nunca.

No distingue alta de modificación, y no hace falta: el consumidor hace *upsert*, que es
idempotente.

### La cadencia larga — `/codigos-publicados`

Hace lo que `/cambios` **no puede hacer: detectar bajas.** Un trámite retirado no deja ninguna
fila que devolver, así que no hay forma de reportarlo como cambio. Lo que usted tenga guardado y
no aparezca en esta lista, ya no existe y hay que retirarlo.

> **Si esta ruta no responde, no borre nada.** Confundir «no pude preguntar» con «ya no existe
> nada» vacía un portal entero por un fallo de red de dos segundos.

### El punto ciego de las dos

`/cambios` se apoya en la fecha de modificación de la ficha. **Un UPDATE hecho directo contra la
base no la toca**, y ese cambio sería invisible para siempre. Por eso conviene forzar un ciclo
completo cada tanto —recorrer `/tramites` paginado y refrescarlo todo— aunque `/cambios` no
reporte nada.

### Resumen

| Ruta | Cada cuánto | Para qué |
|---|---|---|
| `/cambios?desde=` | minutos u horas | traer lo nuevo y lo modificado |
| `/codigos-publicados` | a diario | retirar lo dado de baja |
| `/tramites` completo | semanal o mensual | tapar el punto ciego de la fecha de modificación |

---

## 6. `fichaCompleta`, y por qué se publica lo incompleto

Una ficha está **completa** cuando tiene los cuatro campos que al ciudadano le importan —
**categoría, modalidad, tiempo y costo** — y además, si está marcada como disponible en SOL,
tiene el enlace a SOL.

Cada ficha del catálogo trae `fichaCompleta` como dato informativo. El filtrado real lo hace el
servidor con **`?soloFichasCompletas=true`**.

**Quién lo decide.** Esa regla es de PortalDigital, no de esta API. La API no la evalúa: lee una
columna que PortalDigital mantiene calculada en la base y la sirve tal cual. Para un consumidor no
cambia nada —el campo y el filtro son los mismos de siempre—, pero significa que **el día que
DIGER agregue un campo obligatorio, esta API no cambia ni se redespliega**: el catálogo empieza a
reportar menos fichas completas y ya está.

**Un portal de cara al ciudadano debería mandar siempre ese filtro.** Sin él pueden salir fichas
sin plazo y sin costo, que al ciudadano le sirven de poco: le dicen que el trámite existe pero no
lo que fue a averiguar.

Entonces, ¿por qué se publica lo incompleto? Porque **publicar y estar completo son dos decisiones
distintas y las toman personas distintas.** Publicar dice «esta ficha es oficial y puede salir».
Completarla es trabajo de captura que va a su propio ritmo, institución por institución. Si una
cosa esperara a la otra, o se retendría información oficial que ya es válida, o se apuraría la
captura para poder publicar. La API expone las dos y deja que cada consumidor elija.

**Cuánto cuesta hoy ese filtro** (medido en Ensayo el 25 de agosto de 2026):

| | |
|---|---|
| Fichas en el inventario | 1 057 |
| Publicadas | 50 |
| Publicadas **y** completas | **21** |

Es decir, con el filtro puesto el catálogo se reduce a menos de la mitad. No es un defecto de la
API: es el estado real de la captura, y el número sube conforme las instituciones completan sus
fichas.

---

## 7. Cómo leer los datos sin equivocarse

### El costo tiene tres estados, no dos

| `costoEsGratuito` | Significa |
|---|---|
| `true` | Es gratuito. |
| `false` | Tiene costo. `costoTexto` puede traer el monto, o no. |
| `null` | **No se ha capturado.** No se sabe. |

`null` **nunca** se debe mostrar como «gratuito». Que nadie haya escrito un monto no prueba que no
lo haya; prueba que la ficha está a medio llenar. Un portal que lo confunda le está diciendo al
ciudadano que no pague algo que sí se paga.

### `solUrl` vacío no siempre significa lo mismo

`estaEnSol` dice si el trámite se puede hacer en línea; `solUrl` es a dónde ir.

| `estaEnSol` | `solUrl` | Significa |
|---|---|---|
| `false` | `null` | Normal: el trámite no está en línea. No ofrezca enlace. |
| `true` | una URL | El caso bueno: enlace el botón ahí. |
| `true` | `null` | **Ficha incompleta.** Dice que está en línea pero no dice dónde. Por eso este caso hace que `fichaCompleta` sea `false`. No invente la dirección. |

Hoy hay **una sola ficha** con `estaEnSol` en `true` en todo el inventario. La composición de esa
dirección cambia en la Fase 7 del plan, pero **la forma no**: `solUrl` seguirá siendo una URL
absoluta.

### `modalidad`: pedir Virtual también trae los híbridos

Los valores son `Presencial`, `Virtual` e `Hibrido` —sin tilde, con esa grafía exacta—.

La asimetría que sorprende: **`?modalidad=Virtual` devuelve también los híbridos.** Un trámite
híbrido también se puede hacer en línea, y filtrar solo por `Virtual` subestimaría cuántos hay
disponibles. `?modalidad=Hibrido`, en cambio, devuelve solo híbridos.

Un valor que no exista —`Mixto`, por ejemplo— no da error: devuelve cero resultados.

### `ultimaRevision` no es un campo del formulario

Sale de `UpdatedAt ?? UltimaModificacion ?? CreatedAt`. Es cuándo se tocó el registro, no una
fecha que alguien escriba a mano. No se puede usar para «esta ficha fue revisada por una persona
en tal fecha», porque no es eso.

### Los conteos se calculan en cada petición

`conteoTramitesPublicados` de instituciones y categorías se calcula en vivo, nunca está
almacenado. Cuentan **solo lo publicado**, así que una categoría puede salir en `0` aunque existan
trámites suyos sin publicar.

---

## 8. Detalles de la paginación que muerden

- `pagina` empieza en 1. Un número menor se trata como 1.
- `tamano` va de 1 a 100, por omisión 20. **Un valor fuera de ese intervalo no da error y tampoco
  se recorta: vuelve a 20.** Pedir `tamano=500` devuelve 20, no 100. Si pagina en bucle contando
  con 100, va a hacer cinco veces más peticiones de las que presupuestó.
- `total` es el total del filtro, no de la página.
- Los filtros se combinan con **Y**, no con O: pedir institución y modalidad a la vez devuelve los
  que cumplen las dos cosas.
- **`orden` solo reconoce `nombre`** (A–Z). Cualquier otro valor, y no mandar ninguno, pone
  primero los trámites marcados como populares y dentro de cada grupo ordena por nombre. No hay
  orden por institución ni por tiempo; pedirlos se ignora en silencio.
- **`busqueda` no busca por código ni por institución.** Busca en nombre, descripción y objetivo,
  y no distingue tildes ni mayúsculas —«migracion» encuentra «Migración»—. Esa insensibilidad la
  garantiza la colación de esas columnas en la base, no el código: si esta API se apunta a una
  base cuyas columnas no la tengan, la búsqueda se vuelve sensible a tildes sin que nada avise.
- **`institucion` es la sigla** (`INPREMA`), tal como sale en `/api/v1/instituciones`. No es el
  nombre largo. Y solo salen las instituciones **activas**: una dada de alta pero aún sin aprobar
  no aparece, aunque sus trámites sí puedan estar publicados. Hoy hay 45 activas.

---

## 9. Qué **no** está documentado todavía, y por qué

El **versionado de fichas** y el **flujo de promoción de expedientes a SIGER** son internos y
todavía no existen del lado del contrato público. Documentar lo que no está construido es la
forma más segura de que la documentación empiece a mentir el primer día.

Cuando existan y toquen la superficie pública, entrarán acá y en la especificación a la vez.

---

## 10. Cómo cambiar esta API sin romper a nadie

**La especificación se genera; no se edita.** Después de tocar rutas, tipos de respuesta o los
comentarios XML de los controladores:

```
ACTUALIZAR_SPEC=1 dotnet test tests/Api.Tests
```

y revise el diff de `openapi-v1.yaml` como parte del cambio.

`tests/Api.Tests` falla si el archivo comprometido y el código dejan de coincidir. Falla
también si aparece o desaparece una ruta: las siete están enumeradas a mano en
`EspecificacionPublicadaTests`, para que una ruta nueva tenga que pasar por una decisión y no se
cuele sola en la v1.

**Un cambio de forma es una v2, no una edición de esta v1.** HondurasÁgil ya depende de estas
siete rutas; retirar una o cambiar el tipo de un campo no es un ajuste.

Lo que esta guía afirma sobre el comportamiento —el orden, la asimetría de la modalidad, el tamaño
de página que vuelve a 20, el 404 compartido, la clave— está atado por pruebas en
`ContratoDelCatalogoTests`. Si cambia el comportamiento, esas pruebas fallan y avisan de que hay
un párrafo de acá que corregir.

---

## Documentos vecinos

| Archivo | Qué es |
|---|---|
| [`openapi-v1.yaml`](./openapi-v1.yaml) | La especificación. **Generada** — no editar. |
| [`decisiones-fase0.md`](./decisiones-fase0.md) | Las decisiones con que nació la API, y las que siguen abiertas. |
| [`trazabilidad-cambios.md`](./trazabilidad-cambios.md) | Bitácora de lo que se ha tocado, con motivo y verificación. |
