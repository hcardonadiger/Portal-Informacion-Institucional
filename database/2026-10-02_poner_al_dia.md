# Poner al día una base desplegada — 2026-10-02

Para llevar la base de **producción** al código de hoy **sin sacarle copia y sin tocar los datos
que ya subieron otras personas**. Todo es aditivo: crea tres tablas que no existen y no modifica
ni borra una sola fila de las que ya están. Se puede correr las veces que haga falta — la segunda
corrida no hace nada.

## Los cinco archivos, en orden

| # | Archivo | Qué hace | ¿Modifica? |
|---|---|---|---|
| 1 | `00_diagnostico.sql` | Dice en qué estado está la base y **qué hay que correr** | No |
| 2 | `2026-09-04_poner_al_dia.sql` | Seis columnas en cinco tablas — **incluye `Usuarios.IsDeleted`, sin la cual nadie inicia sesión** | Sí |
| 3 | `2026-09-18_poner_al_dia.sql` | Catálogos de prioridades y categorías | Sí |
| 4 | `2026-09-24_poner_al_dia.sql` | Encuesta en el cierre + tres columnas que ninguna migración crea | Sí |
| 5 | `2026-10-02_poner_al_dia.sql` | **Este.** Ámbito de módulos, tokens MCP y adjuntos de reunión | Sí |

**Corra siempre el 1 primero.** Su última sección dice, con nombre y apellido, cuáles de los
otros hacen falta. Si dice «Nada que hacer», terminó.

El del 2026-09-04 es el más nuevo de la carpeta aunque su fecha sea la más vieja: se escribió el
2026-10-02, al comparar el respaldo real de producción y descubrir que **cinco migraciones no las
cubría ningún script**. `2026-09-04_poner_al_dia.md` cuenta esa comparación con números.

## Cómo correrlos

```
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 00_diagnostico.sql -o salida.txt
sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-02_poner_al_dia.sql
```

O péguelos en SSMS y ejecute. Las banderas no son decorativas:

- `-b` detiene en el primer error. **Sin ella** sqlcmd sigue de largo, llega al `COMMIT` y
  confirma lo que alcanzó a hacer. Así fue como una base quedó a medias antes.
- `-I` activa `QUOTED_IDENTIFIER`.
- `-f 65001` es UTF-8; sin ella los acentos de los mensajes salen rotos.

Adentro, `SET XACT_ABORT ON` hace lo mismo: cualquier error deshace **todo**. O entra completo o
no entra nada.

## Qué trae

Tres migraciones que el código tiene y **ningún script anterior cubrió**. Es exactamente lo que
`dotnet ef database update` habría aplicado, y las tres se registran en `__EFMigrationsHistory`
al final para que EF no las repita.

### 1. `ModuloAmbitos` — migración `20260908161947_AgregarModuloAmbito`

Recorta un módulo a una institución, área o unidad concretas. `AreaId` y `UnidadId` son
nulables a propósito: un recorte a nivel de institución deja las dos vacías.

El índice **único** `(Modulo, InstitucionId, AreaId, UnidadId)` es la regla de negocio — un
módulo no se recorta dos veces al mismo ámbito.

Esta tabla vivió un tiempo **sólo en un stash** y en la rama `rescate/modulo-ambito`; de ahí que
ninguna puesta al día anterior la mencione. **Es muy probable que la base desplegada ya la
tenga**, porque se agregó a mano en su momento; en ese caso el paso no hace nada y lo único que
cambia es que queda registrada en el historial.

### 2. `TokensAccesoMcp` — migración `20260928160056_AddTokenAccesoMcp`

Los tokens con los que un agente externo (Claude, típicamente) entra al portal por MCP.

Guarda el token **hasheado, nunca el token**: `HashSha256` —64 caracteres, que es un SHA-256 en
hexadecimal— es lo que se compara al autenticar, y `Prefijo` —único— es lo único legible, para
que el dueño reconozca cuál es cuál en la lista sin que el valor completo vuelva a aparecer. Si
esta tabla se filtrara, no hay nada ahí que sirva para entrar.

`ExpiraUtc`, `RevocadoUtc` y `UltimoUsoUtc` son el ciclo de vida. Un token revocado **no se
borra, se marca**, para que la auditoría conserve de quién era.

`AsignacionId` no lleva clave foránea (así lo declara el modelo): el token recuerda con qué
asignación —institución + área/unidad + rol— se emitió, para que el agente externo entre con el
alcance que su dueño tenía **en ese momento**, y no con el que tenga después.

La FK a `Usuarios` va **en cascada**: borrar un usuario se lleva sus tokens, que es lo que uno
quiere — un token huérfano sigue abriendo la puerta.

### 3. `ReunionAdjuntos` — migración `20261002162236_AddReunionAdjuntos`

Los documentos y enlaces del cierre de una reunión, en las dos tarjetas que ahora los capturan:

| `Seccion` | Tarjeta del portal |
|---|---|
| `Documento` | «Documentos y recursos» |
| `Evidencia` | «Evidencia fotográfica y de video» |

Y `Clase` distingue si la fila es un archivo que el portal guardó en disco o un enlace a algo que
vive afuera:

| `Clase` | Qué hay en `Url` |
|---|---|
| `Archivo` | ruta relativa bajo `/uploads/reuniones` |
| `Enlace` | dirección `http`/`https` (OneDrive, típicamente) |

Esa distinción es la que permite que **un video de una capacitación no se coma el disco del
servidor**: el video se deja en OneDrive y acá queda su enlace.

`Seccion` y `Clase` van como texto y no como número a propósito: estas filas se leen a mano
cuando algo no cuadra en un despliegue, y un `'Evidencia'`/`'Enlace'` legible vale más que un
`2`. Es el mismo criterio que ya sigue `Reuniones.Visibilidad`.

`Url` es `nvarchar(1000)` y no `600` como `Foto1Url`: un enlace de OneDrive compartido trae el
token en el *query string* y pasa largo de los 600 que alcanzan para una ruta local.

## Qué NO trae, y conviene saberlo

**Nada se borra.** El cambio de las reuniones quitó del portal «Detalle de capacitación» —el
objetivo formativo y el contenido— porque repetía el objetivo y el desarrollo de la memoria, pero
las columnas `Reuniones.ObjetivoCap` y `Reuniones.Contenido` **se quedan donde están, con lo que
ya tengan**: nada las borra y nada las lee. No hay que hacer nada con ellas. Si mañana alguien
quiere recuperar ese texto, ahí está.

Tampoco cambia `Reuniones.Foto1Url`/`Foto2Url`. Los dos cupos de foto del acta siguen
exactamente igual; la tabla nueva los acompaña, no los reemplaza.

## Lo único que queda a mano

Las cinco claves de permiso del módulo MCP:

| Clave | Para qué |
|---|---|
| `TokensMcp.Ver` / `.Crear` / `.Eliminar` | que un usuario administre **sus** tokens |
| `TokensMcp.Admin.Ver` / `.Admin.Eliminar` | ver y revocar los tokens de **todo el portal** |

**No se insertan en este script**, y no hace falta: el portal las descubre por reflexión al
arrancar (`PermissionCatalogSyncService`) y sincroniza la tabla `Permisos` él mismo.

Lo que ningún arranque hace es **otorgarlas**. Salvo el Administrador —que aprueba todo por
código, vía `EsAdministrador`— nadie podrá emitir un token MCP hasta que alguien marque esas
casillas en `/Accesos/Permisos`. Eso es a propósito, y es la única cosa de esta puesta al día que
necesita una mano humana después de correr el script.

## Dos cosas del servidor, no de la base

1. **La carpeta de subidas.** Los adjuntos de clase `Archivo` se guardan bajo
   `wwwroot/uploads/reuniones`. La identidad del *app pool* necesita **permiso de escritura**
   ahí, igual que ya lo necesita en `uploads/tickets`. Sin eso el portal arranca y falla al
   guardar el primer archivo.

2. **El tamaño máximo es 5 MB, no 20.** Lo fija `Uploads:ReunionesMaxMb` en `appsettings.json`.
   Es deliberado y el portal lo dice bien cuando alguien se pasa: *«pesa más de 5 MB. Súbalo a
   OneDrive y pegue acá el enlace»* — que es justamente el flujo para el que existe `Clase =
   'Enlace'`. Si quieren más, es cambiar esa línea de configuración; no se toca código ni base.
   Y los videos no están entre las extensiones permitidas, también a propósito: van por enlace.

## Qué cambió en `00_diagnostico.sql`

El diagnóstico no conocía estas tres tablas, así que en una base al día reportaba «Nada que
hacer» cuando en realidad faltaban. Ahora:

- La lista del modelo pasó de **81 a 84 tablas** (secciones 1 y 2).
- Las tres migraciones nuevas entraron en las listas de las secciones 6, 7 y 8.
- Hay una **recomendación nueva** que nombra este script, y que además recuerda lo de las claves
  `TokensMcp.*`.
- **El «PARE» ya no se dispara por estas tres tablas.** Ese aviso es para atrasos que ningún
  script arregla («faltan módulos enteros, avise antes de seguir»), y las tres de acá sí tienen
  remedio. Se cuentan aparte, en `@tablasFaltan1002`, y el «PARE» sólo mira las que quedan
  fuera. Sin esa separación, una base que simplemente no había corrido este script habría
  recibido un «PARE» falso — que es exactamente la clase de aviso que hace que uno deje de
  creerle al diagnóstico.

## Si la base está muy atrasada

El script **se detiene solo** si no encuentra `Reuniones` o `Usuarios`, con un mensaje que lo
dice, y no deja nada aplicado. Usa `THROW` y no `RAISERROR` por la razón que ya nos costó una
vez: `RAISERROR` deja el mensaje pero **no corta el lote**, así que el script seguiría y moriría
más abajo con un error de clave foránea que no le dice nada a quien lo corre.

## Antes de correrlo en el servidor

Los cambios son **aditivos y sobre tablas que no existen**: tres `CREATE TABLE`, cinco
`CREATE INDEX`, dos claves foráneas. No hay `ALTER TABLE` sobre tablas con datos, así que no hay
bloqueo de escritura sobre `Reuniones` ni `Usuarios` más allá del instante en que se declara la
FK. Aun así conviene correrlo en un momento sin gente usando el portal.

**Para deshacerlo** (sólo si hiciera falta):

```sql
DROP TABLE IF EXISTS [ReunionAdjuntos];
DROP TABLE IF EXISTS [TokensAccesoMcp];
DROP TABLE IF EXISTS [ModuloAmbitos];
DELETE FROM __EFMigrationsHistory
WHERE MigrationId IN (N'20260908161947_AgregarModuloAmbito',
                      N'20260928160056_AddTokenAccesoMcp',
                      N'20261002162236_AddReunionAdjuntos');
```

Las tres tablas son nuevas, así que el inverso sí es limpio: no hay columnas que devolver a su
estado anterior. Ojo igual con `ModuloAmbitos` — **si la base ya la tenía antes de este script,
borrarla pierde los recortes de módulo que hubiera configurados**. Mire qué hay dentro antes.

## Cómo se probó

Contra SQL Server 2025 real (`localhost\SQL2025`), no a ojo.

**1. Rama de creación, desde cero.** Base nueva con sólo `Reuniones`, `Usuarios` y
`__EFMigrationsHistory`. El script creó las tres tablas, los cinco índices y las dos claves
foráneas: **35 controles, todos «presente», salida 0**.

**2. Idempotencia.** Segunda corrida sobre eso mismo: los nueve pasos dijeron «ya estaba», ningún
control dijo FALTA, y el historial siguió en **3 filas sin duplicar**.

**3. Fidelidad al esquema de EF — la prueba que de verdad importa.** `GestionGD_new` tiene las
tres tablas creadas por `dotnet ef`. Se comparó contra lo que produce este script, columna por
columna (tipo, longitud, nulabilidad, *identity*), índice por índice (único o no, columnas y su
orden) y clave foránea por clave foránea (con su acción de borrado): **42 líneas, cero
diferencias**. El SQL escrito a mano deja la base igual que la habría dejado EF.

**4. Producción simulada, de punta a punta.** Se clonó `GestionGD_new` con
`DBCC CLONEDATABASE` —esquema completo, 84 tablas—, se le quitaron las tres tablas y sus tres
filas del historial, y se le cargó el historial real de las otras **73 de 76** migraciones. Sobre
esa base:

| Paso | Resultado |
|---|---|
| `00_diagnostico.sql` antes | Recomendó este script por su nombre. **No dio «PARE».** |
| El script | Creó las tres tablas; ningún control en FALTA; salida 0 |
| Esquema resultante | De nuevo **42 líneas idénticas** a las de EF |
| `00_diagnostico.sql` después | `TablasFaltantes 0 · ColumnasFaltantes 0 · MigracionesSinRegistrar 0` → «Nada que hacer» |
| El script, otra vez | No hizo nada; historial en **76 filas**, sin duplicar |

Ese cuarto escenario era el mejor que había **antes** de tener el respaldo de producción.

**5. Contra el respaldo real de producción.** Ese mismo 2026-10-02 se restauró `GestionGD 2.bak`
—la base desplegada— y se corrió la cadena completa de cuatro scripts sobre ella. Resultado: el
esquema quedó en **1335 objetos idénticos** a los que espera el código, **ninguna tabla de datos
perdió una fila** (38 956 → 38 979, y las 23 de diferencia son el historial y los tres catálogos
nuevos), y el diagnóstico pasó a «Nada que hacer». Los números están en
`2026-09-04_poner_al_dia.md`, que es donde vive la comparación completa.

Ahí se vio también que producción le faltaban **13 migraciones, no 3** — de ahí que exista el
script del 2026-09-04.
