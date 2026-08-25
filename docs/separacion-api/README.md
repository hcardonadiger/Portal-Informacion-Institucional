# Separar la API pública del código de PortalDigital

**Hecho el 25 de agosto de 2026.**

## Qué se pedía

Que el código de PortalDigital y el de la API pública fueran independientes: *«si hay una
modificación en PD que no sea necesario tocar API y viceversa»*.

## Cómo estaba

`src/Presentation` —el proyecto de la API— referenciaba `Application`, `Domain` e
`Infrastructure`. Con esas tres referencias entraba en el proceso de la API, sin usarse: SMTP, el
cliente del agente de IA, ocho repositorios, el cliente de Supabase, un servicio de recordatorios
en segundo plano, el catálogo de roles y la caché de permisos.

Dos de esas cosas había que registrarlas **a mano** en `Program.cs`, con un comentario que
explicaba que sin ellas el host ni siquiera arrancaba:

```csharp
// El catálogo de roles lo necesita CurrentUserService (Infrastructure) para construirse,
// aunque esta API no use cookies/roles — sin esto el host no arranca.
builder.Services.AddSingleton<IRolCatalogo, RolCatalogo>();
builder.Services.AddHostedService<RolCatalogoLoader>();
```

Una API pública de solo lectura que necesita un catálogo de roles para arrancar es la definición
del problema.

## Qué se hizo

`src/Api`, un proyecto que **no referencia ningún proyecto de PortalDigital**. Lee las ocho tablas
que necesita a través de su propio modelo, en `src/Api/Lectura/ModeloDeLectura.cs`.

PortalDigital sigue siendo el dueño del esquema y de las migraciones. La API solo lee, y en
producción con el usuario de solo lectura que ya existía
(`scripts/sql/10-usuario-solo-lectura-api.sql`).

### Lo único que compartían de verdad

Una regla: qué hace que una ficha esté «completa». La API la evaluaba por su cuenta, con una copia
en C# y otra en SQL dentro de su consulta. Eso significaba que **agregar un campo obligatorio en
PortalDigital obligaba a tocar y desplegar la API**.

Ahora la decide PortalDigital y la publica en una columna calculada por la base
(`TramitesSiger.FichaCompleta`, migración `ColumnaFichaCompleta`). La API la lee y la sirve.

La calcula la base y no la aplicación por una razón concreta: una columna que alguien tiene que
acordarse de recalcular al guardar es una columna que tarde o temprano miente. Basta un camino de
escritura que la olvide —una carga masiva, un UPDATE directo, una importación— para que quede
desfasada, y nadie lo nota hasta que el ciudadano ve una ficha a medias. Calculada por SQL Server
no hay forma de escribirla mal, porque no hay forma de escribirla.

## Cómo se comprobó que no se rompió nada

**La especificación OpenAPI generada salió idéntica byte a byte a la comprometida**, salvo tres
líneas del encabezado que nombran las rutas del proyecto y tenían que cambiar. Ni una ruta, ni un
esquema, ni un campo, ni una descripción se movió: para HondurasÁgil no cambió nada.

Esa comprobación existía desde la Fase 6 del plan de promoción SIGER, escrita para otro propósito;
resultó ser exactamente el guardián que hacía falta acá.

Además:

| Comprobación | Dónde |
|---|---|
| La regla en C# y la columna dicen lo mismo, sobre una tabla de verdad de once casos | `scripts/sql/16-verificar-ficha-completa.sql` y `FichaPublicaCompletitudTests` |
| La columna coincide con la regla vieja en las 1 057 fichas reales de Ensayo | mismo script, última consulta |
| Cada columna que la API declara existe en la base real | `ModeloContraLaBaseRealTests` |
| Las siete rutas y su contrato | `ContratoDelCatalogoTests`, `EspecificacionPublicadaTests` |

## El hueco que abre, y su tapa

Mientras la API compartía las entidades de PortalDigital, renombrar una columna la rompía **al
compilar**. Ahora no: la API describe las ocho tablas por su cuenta.

Ninguna prueba en memoria puede cubrir eso, porque siembra y lee por el mismo modelo — si el
modelo se equivoca, se equivoca de forma consistente y todo pasa en verde. Por eso existe
`ModeloContraLaBaseRealTests`, que le pregunta al catálogo de SQL Server. Se salta sin base a
mano; se corre así:

```bash
PD_CONEXION_PRUEBAS="<cadena>" dotnet test tests/Api.Tests
```

Con la variable puesta, no poder conectar **es** el fallo: callarlo convertiría el guardián en un
adorno el día que más hace falta.

## Qué cambia a partir de ahora

| En PortalDigital… | ¿Hay que tocar la API? |
|---|---|
| Expedientes, reuniones, tickets, permisos, chat, tableros | **No.** |
| Una tabla o una columna nuevas | **No.** |
| Una regla de negocio, incluida la de ficha completa | **No.** |
| Renombrar o borrar una columna que la API publica | **Sí** — y debe. Esa columna ya no es un detalle interno: viaja en el contrato público. |

## Lo que queda pendiente

- **Aplicar `15-ficha-completa-columna.sql` a Producción.** Va después del 13 y el 14, que también
  siguen pendientes allí; el script se detiene solo si falta `SolTramo`.
- **Volver a publicar la API.** El ensamblado pasó de `Diger.TramitesEstado.Presentation` a
  `Diger.TramitesEstado.Api`. `scripts/Desplegar.ps1` ya apunta al proyecto nuevo, pero el
  despliegue anterior deja archivos viejos en la carpeta de destino; conviene vaciarla.
- **Mover `src/Api` a su propio repositorio**, si el jefe lo quiere del todo separado. La carpeta
  se puede levantar tal cual: no referencia nada de este repositorio. Lo único que tendría que
  viajar con ella son `tests/Api.Tests` y `docs/api-v1`.
