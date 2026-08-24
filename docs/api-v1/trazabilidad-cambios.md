# Trazabilidad de cambios — API v1 y despliegue

Bitácora de todo lo que se toca a partir del 17 de agosto de 2026, con su motivo y su
verificación. Un cambio sin motivo escrito es un cambio que nadie podrá revertir con
criterio dentro de seis meses.

## Línea base medida el 17 de agosto de 2026

| Qué | Estado antes de tocar nada |
|---|---|
| API v1 | 7 de 7 rutas construidas, verificadas contra datos reales con 27 casos |
| Fase 1 (esquema) | Aplicada y verificada en `TramitesEstado_Ensayo` |
| Fase 2 (deudas de datos) | 751 de 1.057 con `InstitucionId`, 49 publicados |
| Fichas que el ciudadano podía ver | **0** — ninguna tenía categoría, modalidad, tiempo ni costo |
| Pantalla `CapturaLote` | Construida, nunca abierta en un navegador |

## Verificación previa: la cadena de captura funciona

Antes de cambiar una línea de código se comprobó, en `TramitesEstado_Ensayo` y con navegador
real, que la cadena funciona de punta a punta:

1. Sesión iniciada en el portal; `/Siger/CapturaLote` filtrada por INPREMA e incompletas.
2. Los 24 trámites marcados, cuatro campos aplicados en lote, diálogo confirmado.
3. Resultado: **24 de 24** con los cuatro campos, **20 publicados solos**, `UpdatedAt`
   sellado en las 24.
4. La API pasó de `{"items":[],"total":0}` a `total: 20` con `fichaCompleta: true`.

**Conclusión: no falta código para que el portal tenga contenido. Falta captura humana.**

## Cierre de Fase 1 y Fase 2 sobre `TramitesEstado_Prod`

Ejecutado el 17 de agosto de 2026, con respaldo previo en
`TramitesEstado_Prod_antes_cierre_20260817.bak`.

| Bloque | Antes | Después |
|---|---|---|
| Migraciones de la Fase 1 | sin aplicar | `AgregarCamposVentanilla` + `CorregirColacionBusqueda` |
| Columnas nuevas en `TramitesSiger` | 0 | **9** |
| Categorías sembradas | 0 | **8** |
| Colación de `Nombre` | `CI_AS` | **`Modern_Spanish_CI_AI`** |
| G-1 · sin `NombreCorto` | 44 | **2** (43 de 45, la meta) |
| G-2 · sin `InstitucionId` | 1.057 | **0** |
| Instituciones | 45 | **86** (41 creadas inactivas) |
| G-3 · publicados | 0 | **49** |
| `UpdatedAt` tocado por los scripts | — | **0**, la regla de oro se respetó |

## Cambios en el código

### 17 de agosto de 2026 — correcciones halladas al ejercer la API

| Archivo | Qué cambió | Por qué |
|---|---|---|
| `src/Application/Siger/Publico/Queries/GetTramitePublico/GetTramitePublicoQuery.cs` | Cinco `Include` sustituidos por seis consultas explícitas | **Explosión cartesiana.** 18×3×1×18×2 = 1.944 filas para devolver 9 KB. Medido: **25 s → 0,011 s**, con respuesta idéntica byte a byte. La peor ficha publicada (`603-023`) generaba 2.520 filas. Sin esto, sincronizar 49 fichas tardaba ~20 minutos y expiraba |
| `src/Presentation/Middleware/GlobalExceptionHandler.cs` | El detalle interno deja de viajar al cliente; se devuelve `traceId` | Un error interno no debe describir las tripas del sistema a quien llama |
| `src/Presentation/Program.cs` | Swagger tras la bandera `PortalDigitalApi:PublicarSwagger` | En producción, publicar el contrato entero sin querer es regalar el mapa |
| `src/Presentation/Controllers/CambiosController.cs` | `desde` pasa a `DateTime?` con `BadRequest` explicativo | Antes, omitirlo daba un error opaco; ahora señala `/api/v1/codigos-publicados` |

> **Nota sobre `AsSplitQuery()`.** Fue mi primer intento y **no compila**: vive en el ensamblado
> Relational, que `Application` no referencia — y con razón. Además habría roto las pruebas en
> memoria. De ahí las consultas explícitas.

### 18 de agosto de 2026 — puesta en marcha

| Archivo | Qué cambió | Por qué |
|---|---|---|
| `src/Presentation/appsettings.Development.json` | `DigerTramitesEstado_Dev` → `TramitesEstado_Ensayo` | **La base apuntada no existía.** Es la causa de que la API fallara al arrancar: `Error 4060`, que no significa «contraseña mala» sino «esa base no existe». Las bases reales son `TramitesEstado_Ensayo` y `TramitesEstado_Prod` |
| *user-secrets* de `src/Presentation` | Se fijó `PortalDigitalApi:ApiKey` | Sin clave, las siete rutas responden 401. **No va en `appsettings.json`**, que se versiona |
| `docs/api/fase0-fase2/03-residuo-instituciones.sql` (en honduras-agil) | Decisión registrada + **PASO 3** para activarlas | DIGER decidió el 18-08 que las 41 son institución, ninguna dependencia |

**Cómo correr la API.** Es solo el proyecto `Presentation`, y basta esto:

```
dotnet run --project src/Presentation/Diger.TramitesEstado.Presentation.csproj --urls "http://localhost:5199"
```

Comprobación — esta ruta no exige clave, a propósito:

```
curl http://localhost:5199/api/v1/salud
→ {"estado":"ok","baseDeDatos":true,"horaServidor":"..."}
```

Si `baseDeDatos` viene en `false`, el problema es la cadena de conexión, no la API.


### 18 de agosto de 2026 — Swagger visible al entrar al puerto

Swagger ya existía en `/swagger`, pero **la raíz del puerto devolvía un 404 vacío**, que es
justo la impresión que da un servidor caído. De ahí que pareciera que no hubiera documentación.

| Archivo | Qué cambió | Por qué |
|---|---|---|
| `src/Presentation/Diger.TramitesEstado.Presentation.csproj` | `GenerateDocumentationFile` activado; `NoWarn` 1591 y 1573 | **Los controladores ya llevaban su documentación escrita, pero el compilador no generaba el `.xml`**, así que Swagger no podía mostrarla. Se veían las rutas sin una sola explicación |
| `src/Presentation/Program.cs` | `GET /` redirige a `/swagger`; portada con autenticación y protocolo de sincronización; `IncludeXmlComments` | Entrar al puerto a secas ahora lleva a la documentación |
| `src/Presentation/Swagger/RequisitoApiKeyFilter.cs` | **Nuevo.** El candado se pone por operación, no global | Global, `/api/v1/salud` salía con candado **siendo anónima**. Una documentación que miente sobre el contrato es peor que no tenerla |
| Los cinco controladores | Resúmenes ampliados, 10 parámetros documentados, respuestas 400/404/503 explicadas | Los diez parámetros del catálogo salían sin una palabra de explicación |

Comprobado sobre la API en marcha: **7 operaciones, 0 sin documentar, 0 parámetros sin
descripción**, `/api/v1/salud` sin candado y las otras seis con él. Captura en
`docs/api-v1/img/swagger-raiz.png`.

**Nota para producción.** Swagger sigue apagado fuera de Development salvo que se encienda a
propósito con `PortalDigitalApi:PublicarSwagger`. La redirección de la raíz va dentro de esa
misma condición: con Swagger apagado, `/` no redirige a ninguna parte.

### 18 de agosto de 2026 — usuario de solo lectura y script reversible

**El problema.** La API pública es la única cara expuesta a la red y se conectaba con las
mismas credenciales que el portal interno: escritura sobre las 64 tablas. Medido: solo lee
nueve.

| Archivo | Qué es |
|---|---|
| `scripts/sql/10-usuario-solo-lectura-api.sql` | **Nuevo.** Crea `api_portaldigital_lectura` con `SELECT` sobre nueve tablas y `DENY` de escritura sobre el esquema. Con `DO` y `UNDO` |
| `scripts/Aplicar-CambiosProduccion.ps1` | **Nuevo.** Orquestador reversible de todos los cambios de este trabajo |
| `honduras-agil/scripts/sql/20-catalogo-portal-SUBIDA.sql` | **Nuevo.** Las nueve tablas `Portal*`, idempotente |
| `honduras-agil/scripts/sql/21-catalogo-portal-BAJADA.sql` | **Nuevo.** La vuelta atrás, endurecida a mano con guardas `IF EXISTS` |
| `scripts/Desplegar.ps1` | BOM añadido (ver abajo) |

**Comprobado, no supuesto.** Ciclo completo `DO → UNDO → DO` sobre `TramitesEstado_Ensayo` y
`VentanillaDigital_Net`:

- el usuario lee 1.057 trámites y **no** puede escribir ni leer `Expedientes`
- las nueve tablas se crean, se borran y se recrean
- tras el `UNDO`: **15 trámites sembrados y 23 votos intactos**
- tras el `DO` final: 49 trámites resincronizados con sus 380 pasos
- la API sirve sus siete rutas con el usuario restringido, sin un solo error de permisos

**Cuatro fallos reales encontrados al ejecutarlo** — ninguno habría aparecido leyendo el código:

1. `WITH COMPRESSION` no existe en Express Edition (`Msg 1844`) y tumbaba el respaldo entero.
2. `DROP LOGIN` falla con la API conectada (`Msg 15434`), pero el `DROP USER` previo sí
   funciona: quedaba un login huérfano mientras el script decía «revertido».
3. El mensaje de aborto afirmaba «las bases están respaldadas» **cuando el respaldo era
   justo lo que había fallado**.
4. En modo comprobación se imprimía la contraseña entera, porque el enmascarado miraba el
   nombre de la variable y no su contenido — y una cadena de conexión se llama
   `ConnectionStrings__DefaultConnection`.

**Codificación.** Los `.ps1` ahora llevan BOM. Sin él, PowerShell 5.1 lee en ANSI y el guión
largo `—` acaba en una comilla tipográfica que **cierra la cadena**: error de sintaxis en una
línea que se ve bien. Mismo patrón de fallo silencioso que `sqlcmd` sin `-f 65001`.

**Pendiente de una acción manual:** reponer el usuario en `TramitesEstado_Prod`. El `UNDO` de
ensayo borró el login, que es de servidor, y dejó allí un usuario huérfano.

### 18 de agosto de 2026 (tarde) — dos trampas del login, encontradas ejecutando

Al reponer el usuario en `TramitesEstado_Prod` aparecieron dos problemas que **no existen en
el código y solo se ven al usarlo**. Los dos están ya resueltos dentro del script.

**1. Un LOGIN pertenece al servidor, no a la base.**

Ensayo y producción compartían el nombre `api_portaldigital_lectura`, y por tanto **la misma
contraseña**. Al fijar la de producción, la API de ensayo empezó a devolver `Login failed`.
No fue una hipótesis: pasó, y se midió.

Ahora el nombre es un parámetro (`-v Usuario=` en el SQL, `-NombreUsuario` en PowerShell).
Con servidores separados da igual; con instancia compartida hay que usar uno por entorno.
Comprobado: `api_portaldigital_lectura` y `api_portaldigital_lectura_ensayo` conviven, cada
uno con su clave, cada uno leyendo 1.057 trámites sin pisar al otro.

**2. `CHECK_POLICY = ON` trae bloqueo de cuenta, y eso puede tumbar la API.**

Unos pocos intentos con la contraseña equivocada —una cadena de conexión vieja en cualquier
aplicación— **bloquean el login**, y la API deja de conectar sin que nadie haya tocado nada.

El síntoma engaña: `sqlcmd` dice `Login failed for user`, que se lee como «contraseña mala».
Para distinguirlo:

```sql
SELECT name, LOGINPROPERTY(name,'IsLocked'), LOGINPROPERTY(name,'BadPasswordCount')
FROM   sys.sql_logins WHERE name LIKE 'api_portaldigital%';
```

Se añadió `Accion="DESBLOQUEAR"` al script. Probado bloqueando el login a propósito con ocho
intentos fallidos y recuperándolo: `IsLocked` de 1 a 0, y vuelve a leer.

> Se mantiene `CHECK_POLICY = ON` a conciencia: rechaza contraseñas débiles al crear el
> usuario. El precio es este bloqueo, que ahora tiene salida documentada. Si en su entorno
> pesa más la disponibilidad que esa comprobación, la alternativa es `CHECK_POLICY = OFF`
> —y entonces la fortaleza de la clave queda enteramente en manos de quien la elige.

**Estado final medido:**

| | |
|---|---|
| `api_portaldigital_lectura` sobre `TramitesEstado_Prod` | lee 1.057 · no escribe · no bloqueado |
| `api_portaldigital_lectura_ensayo` sobre `TramitesEstado_Ensayo` | lee 1.057 · no escribe · no bloqueado |

### 18 de agosto de 2026 — puertos y pantalla de comprobación

**El choque que impedía arrancar los dos a la vez.** La API estaba configurada en
`https://localhost:49176`, que es **exactamente** el puerto de autenticación por certificado
del portal interno (`Ports:DevCert`, usado por cinco páginas de `Cuenta/`). Con el portal
arriba, la API no podía enlazar su puerto — y si no arranca, no hay navegador que abrir.

Reparto nuevo, sin solapes:

| Sistema | HTTPS | HTTP |
|---|---|---|
| Portal interno | 49175 (49176 = certificado) | 49177 |
| **API pública** | **7199** | **5199** |
| HondurasÁgil | 7180 | 5180 |

Los puertos del portal **no se tocaron**: son configurables por `Ports:DevMain/DevCert/DevHttp`
y hay cinco archivos que dependen de ellos.

| Archivo | Qué cambió |
|---|---|
| `src/Presentation/Properties/launchSettings.json` | Perfiles `API (https)` y `API (http)`, con `launchUrl: swagger` |
| `src/Presentation/…csproj.user` | Perfil activo actualizado (el anterior dejó de existir) |
| `scripts/Iniciar-Todo.ps1` | **Nuevo.** Levanta los tres, espera a que respondan y abre el navegador |

**Sobre el navegador.** Que no se abra desde la consola no es un fallo de configuración:
`launchBrowser` de `launchSettings.json` solo lo obedecen Visual Studio y `dotnet watch`.
`dotnet run` nunca abre nada. De ahí el script.

**Pantalla de comprobación en HondurasÁgil** (`/diagnostico/catalogo-portal`): muestra lo
replicado tal cual llegó, con los huecos marcados. No es el catálogo del ciudadano y lo dice
en la propia página.

**Lo que revela, medido sobre las 49 fichas replicadas:**

| | |
|---|---|
| con modalidad, plazo y categoría | **20** — y son las que llevan valores de prueba (`PRUEBA - 5 dias habiles`) |
| **con costo** | **0** — ni una sola |
| sin nada | 29 |

Por eso la Fase 6 no es encender un interruptor: `TramiteListItemDto` exige `Modalidad`,
`TiempoTexto` y `CostoTexto` **no anulables**, y en el catálogo replicado están vacíos.
Servirlos hoy obligaría a inventar plazos y costos del Estado.

### 18 de agosto de 2026 — por qué los dos se iban al puerto 5000

**Reproducido, no supuesto.** Ejecutando `dotnet run --no-launch-profile` —que es lo que
hace Visual Studio cuando no aplica el perfil— se obtuvo exactamente el síntoma reportado:

```
--- HondurasÁgil ---   Now listening on: http://localhost:5000
--- API ---            (no arrancó)      Hosting environment: Production
                       Cannot open database "DigerTramitesEstado". Login failed.
```

**La causa es una sola y explica los tres síntomas.** Sin perfil de arranque:

1. no se define `ASPNETCORE_URLS` → ASP.NET usa el **5000** por omisión, y los dos chocan;
2. no se define `launchBrowser` → **no se abre el navegador**, ni con uno solo;
3. no se define `ASPNETCORE_ENVIRONMENT` → la aplicación se cree en **Production** y **no lee
   `appsettings.Development.json`**. Por eso la API buscaba `DigerTramitesEstado`, que no
   existe — **el mismo fallo del primer día**.

**Red de seguridad añadida** en `src/Presentation/Program.cs` y en el `Program.cs` de
HondurasÁgil: si nadie dijo en qué puertos escuchar, los fija el propio programa
(`Ports:DevHttps` / `Ports:DevHttp`, con 7199/5199 y 7180/5180 por omisión).

> **Un error mío que merece constar:** la primera versión de esa guarda estaba condicionada a
> `IsDevelopment()`, y **no servía para nada** — precisamente cuando falta el perfil, el
> entorno es Production. Se descubrió al probarlo: HondurasÁgil siguió yéndose al 5000.
> Ahora la guarda solo respeta dos cosas: que alguien haya fijado URLs explícitas, y que no
> se esté ejecutando bajo IIS (donde manda el módulo, no Kestrel).

**Comprobado tras el arreglo:** los dos a la vez, sin perfil → HondurasÁgil en 5180/7180 y
**nadie en el 5000**.

**Lo que la red de seguridad NO arregla, y hay que resolver en Visual Studio:** el entorno.
Sin perfil se sigue leyendo `appsettings.json` en vez de `appsettings.Development.json`. El
remedio es que Visual Studio vuelva a aplicar el perfil: cerrarlo y borrar la carpeta `.vs`
de cada repositorio, que es solo caché y se regenera sola (ya está en `.gitignore`).

### 18 de agosto de 2026 (cierre) — Visual Studio había revertido el archivo

Al borrar las carpetas `.vs` apareció lo que faltaba para cerrar el caso. Las marcas de
tiempo lo dicen todo:

| Archivo | Modificado |
|---|---|
| `…Presentation.csproj.user` | 13:01 — por mí |
| `Properties/launchSettings.json` | **13:41 — por Visual Studio** |

Visual Studio **reescribió `launchSettings.json`** devolviéndolo a la versión del repositorio
(puerto 49176, el del certificado), mientras el archivo de preferencias seguía apuntando al
perfil renombrado `API (https)`.

**Un perfil activo que no existe hace que Visual Studio arranque sin perfil**, y ya está
documentado arriba lo que eso provoca: puerto 5000, sin navegador y en Production.

**Corrección, y lección.** Se rehízo el archivo **conservando el nombre original del perfil**
(`Diger.TramitesEstado.Presentation`) en vez de renombrarlo. Renombrar un perfil obliga a que
`.csproj.user`, el script de arranque y la caché de Visual Studio cambien a la vez, y basta
que uno se quede atrás para reproducir el fallo. Cambiar solo los puertos no tiene ese riesgo.
`Iniciar-Todo.ps1` se ajustó al nombre real.

**Estado final, con los dos corriendo a la vez:**

| | Puertos | Entorno | Comprobación |
|---|---|---|---|
| API | 7199 / 5199 | Development | `salud` = ok · Swagger en la raíz = 200 |
| HondurasÁgil | 7180 / 5180 | Development | portada = 200 · diagnóstico = 200 |
| | | | **nadie en 5000 ni en 49176** |

---

## 18 de agosto de 2026 — Fichas incompletas: avisar en vez de esconder

**Decisión del usuario.** Llenar a mano las 49 fichas del piloto se pospone. En su lugar:

- **HondurasÁgil** muestra el trámite igual, y pinta `-` donde la institución no ha capturado
  el dato. Nunca esconde un trámite por un campo vacío.
- **PortalDigital** avisa, ficha por ficha, qué le falta, para que el técnico lo complete.

### Por qué así

Esconder el trámite castiga al ciudadano por un error administrativo: quien busca "constancia
de estudios" prefiere encontrarla y ver un plazo pendiente antes que no encontrarla. El guion
es **solo de presentación**: en la base el campo sigue vacío, para que el día que alguien
capture el dato nadie tenga que distinguir un guion escrito por una persona de uno inventado
por el portal, y para que los conteos de completitud no cuenten como lleno lo que está vacío.

### PortalDigital — qué se tocó

| Archivo | Cambio |
|---|---|
| `src/Application/Siger/Publico/PublicoDtos.cs` | `FichaPublicaCompletitud` gana `CamposFaltantes(...)` y `Frase(...)`. `Evaluar(...)` pasa a definirse **sobre** `CamposFaltantes` |
| `src/Web/Pages/Siger/Index.cshtml(.cs)` | Aviso con el total de incompletas, filtro *Ficha pública*, y marca por fila con el detalle en el `title` |
| `src/Web/Pages/Siger/Detalle.cshtml(.cs)` | Alerta que enumera los campos faltantes y lleva al editor |
| `src/Web/Pages/Siger/Editor.cshtml(.cs)` | El editor **por fin pinta** lo que ya calculaba |
| `src/Web/Pages/_ViewImports.cshtml` | `@using …Application.Siger.Publico` |

Dos detalles que costaron y conviene no repetir:

1. **`Evaluar` se redefinió sobre `CamposFaltantes`**, no al revés. Si se mantienen como dos
   reglas gemelas, el día que se agregue un campo obligatorio la alerta que ve el técnico y el
   filtro que ve el ciudadano empiezan a decir cosas distintas.
2. **El filtro del inventario es un árbol de expresión, no un método.** Un método privado no lo
   traduce EF y revienta dentro de un `Where`. La versión "completa" se deriva con
   `Expression.Not(...)` de la "incompleta", para que no puedan discrepar.

### El editor no mostraba nada

`EditorModel.FichaCompleta` existía desde antes, se calculaba bien… y **ninguna vista lo
pintaba**. El técnico que abría el editor no tenía forma de saber por qué su trámite aprobado
no se publicaba. Por eso las pruebas nuevas se escribieron **sobre el HTML que sale del
servidor** y no sobre el modelo de página: una prueba del PageModel habría pasado en verde con
ese defecto adentro.

### HondurasÁgil — qué se tocó

| Archivo | Cambio |
|---|---|
| `src/Web/CampoVisible.cs` | **Nuevo.** `Mostrar`, `MostrarCosto` y `HayDato` |
| `src/Web/Pages/Index.cshtml` · `Tramites/Index.cshtml` · `Tramites/Detalle.cshtml` | Plazo y costo pasan por el ayudante (10 sitios) |
| `src/Web/Pages/Diagnostico/CatalogoPortal.cshtml` | Usaba una raya larga propia; ahora una sola convención |

El guion **no** se usa para todo campo opcional: un teléfono que la institución no tiene se
omite entero, como ya se hacía. El guion es para plazo y costo, que el ciudadano espera
encontrar siempre y donde una casilla en blanco se lee como error de la página.

### Comprobado

| Qué | Cómo |
|---|---|
| Alerta en inventario, detalle y editor | 8 pruebas de integración sobre el HTML renderizado |
| La regla y su frase | 15 pruebas unitarias, incluida una que impide que `Evaluar` y `CamposFaltantes` se contradigan |
| Guion en el catálogo replicado | 136 celdas con `-` en `/diagnostico/catalogo-portal` |
| Que no aparece donde sí hay dato | 0 guiones en `/tramites`: se siguen viendo "1 a 3 días", "Gratuito", "L. 50.00 por documento" |
| Baterías completas | PortalDigital **181** · HondurasÁgil **213** · 0 fallos, 0 advertencias nuevas |

**No hay cambios de esquema**, así que `Aplicar-CambiosProduccion.ps1` no necesita pasos nuevos.

---

## 19-08-2026 · Fase 6a — el ciudadano ve el catálogo real

**Solo HondurasÁgil.** PortalDigital no se toca: sigue publicando lo mismo por la API v1.

### Lo que se encontró antes de escribir código

Tres medidas que cambian el sentido de la fase, tomadas sobre `VentanillaDigital_Net` y
sobre las dos bases de PortalDigital:

| Medida | Valor |
|---|---|
| Fichas replicadas | 49 |
| Con modalidad, plazo, costo y categoría | **20**, todas de INPREMA, todas «Virtual», todas en la misma categoría |
| Sin ninguno de los cuatro | **29** (IHTT 18, CONSUCOOP 11) |
| Texto del plazo en las 20 «completas» | `PRUEBA - 5 dias habiles` |
| Fichas con plazo en `TramitesEstado_Prod` | **0** |

Es decir: **el contenido del piloto todavía no existe**. Lo que hay en `TramitesEstado_Ensayo`
—de donde tira la sincronización de desarrollo— son datos que alguien escribió para probar el
editor, y en producción los 49 publicados no tienen ficha pública ninguna.

Eso no impide hacer la sustitución, que es trabajo de código y se puede comprobar entera. Sí
impide dar por cumplido el criterio de salida de la fase, que habla de mostrarle al ciudadano
el catálogo del piloto. **Por eso la fase se parte en dos**: la F6a es la sustitución, y la F6b
—soltar las tablas heredadas— espera a que un corte haya funcionado con contenido de verdad.

### Qué hace ahora el portal

El catálogo del ciudadano sale de las tablas `Portal*` y no de trámites sembrados. Con eso,
casi todo campo pasa a ser anulable, y las decisiones de la sustitución son las de qué hacer
con cada hueco:

| Hueco | Qué se hace | Por qué no lo contrario |
|---|---|---|
| Plazo, costo | Se pinta `-` | Rellenar con un valor por defecto convertiría «nadie lo ha escrito» en una afirmación |
| Modalidad | Insignia gris, borde discontinuo, «Modalidad sin definir» | No pintar nada se lee como «presencial por defecto» |
| Categoría | Entrada «Sin clasificar» al final de la navegación | Sin ella, 29 de 49 fichas no tendrían ningún camino desde la navegación por categorías |
| Estadística de portada | Tercera casilla, «sin modalidad» | Con dos casillas, los 29 caerían en «presenciales» y el portal afirmaría que no se pueden hacer en línea |
| Instituciones sin trámites | No aparecen | 42 de las 45 replicadas darían tarjetas que llevan a una lista vacía |

### Lo que se pierde, dicho en voz alta

Dos secciones de la ficha desaparecen porque la API v1 no publica su dato:

- **Plantillas descargables.** En su sitio van los **entregables** —lo que el ciudadano recibe
  al terminar—, que sí se replican (65 filas). No son lo mismo y no se deben confundir: la
  plantilla se descarga para llenarla, el entregable se recibe al final.
- **Trámites relacionados.** No existe quien los llene. En su sitio van los **enlaces
  oficiales** del trámite (68 filas). Inventar relaciones por categoría daría sugerencias que
  el ciudadano leería como si alguien las hubiera revisado.

También se retira la **redirección 301 de `/Tramites/{id entero}`**: los quince trámites de
demostración ya no se sirven, así que redirigía a una ficha que devuelve 404, y un 301 hacia
un 404 traslada el posicionamiento a una dirección muerta. Se comprobó que ninguno de los 49
códigos replicados es puramente numérico.

### Una columna nueva y por qué no la trae la API

`PortalTramites.TiempoMinutos` (migración `20260819151715_TiempoMinutosEnCatalogoReplicado`).
La API publica el plazo como texto y no en minutos, así que sin esta columna el orden «menos
tiempo» del catálogo moría con la sustitución. Lo deriva `TramitePortal.Refrescar` con la
misma regla que ya usaba el catálogo propio, ahora expuesta como `TiempoEstimado.MinutosDe`.

**La migración no rellena la columna a propósito.** Reescribir el analizador en T-SQL dejaría
dos definiciones de la misma regla. Las filas ya replicadas quedan en `null` —y se ordenan al
final, que es lo correcto para un plazo desconocido— hasta el **primer ciclo completo**
posterior. Medido: tras forzarlo, `PRUEBA - 5 dias habiles` → **7.200** minutos.

### Los sembradores: retirados, no borrados

Los cinco (`Categorias`, `Instituciones`, `Tramites`, `TramitesRelacionados` y
`CorreccionesDeTexto`) dejan de registrarse en `DependencyInjection` y llevan una cabecera que
lo explica. **Siguen en el repositorio**, porque el plan lo pide así hasta que un corte haya
funcionado en producción. Con ellos se va también el interruptor `PortalDigital:Origen`: sin
sembradores, la posición `Semillas` solo podía servir un catálogo vacío, y un interruptor con
una sola posición útil no documenta una elección, la finge.

`ExigirFichaCompleta` se queda en `false` y ahora es una decisión permanente: en `true`
dejaría fuera 29 de las 49 fichas del corte, que es exactamente esconder el trámite.

### `GlosarioTests`, rehecho

La prueba que comprobaba los 24 términos contra los quince trámites sembrados **no se
sostiene** desde la sustitución: el texto lo escribe ahora PortalDigital y cambia sin que aquí
se toque una línea, de modo que rompería la compilación cada vez que una institución
reescribiera un requisito. Eso no es una prueba, es una alarma en el sitio equivocado.

La comprobación se mueve a donde el dato es real —`GetCoberturaGlosarioQuery`, visible en
`/diagnostico/catalogo-portal`— y lo que se prueba en cada compilación es la **regla**, que sí
es nuestra y sí es determinista (4 pruebas).

### Comprobado

| Qué | Cómo |
|---|---|
| Baterías | **232** en verde: 103 de dominio, 129 de aplicación. 0 errores, 0 advertencias |
| El portal sirve los 49 | «49 trámites encontrados» en `/Tramites`; portada 49 / 20 digitales / 0 presenciales / 3 instituciones |
| Filtros | `?CategoriaId=sin` → 29 · `?CategoriaId=1` → 20 · `?Modalidad=Virtual` → 20 · `?InstitucionId=IHTT` → 18 |
| Navegación por categorías | 8 categorías más «Sin clasificar» (29) al final |
| Rutas | `/Tramites/603-001` → 200 · `/Tramites/1` → 404 |
| Derivación del plazo | Ciclo completo forzado: 49 actualizados, `PRUEBA - 5 dias habiles` → 7.200 min |
| **Desbordes** | **150 combinaciones** (10 rutas × 5 anchos de 320 a 1440 px × 3 tamaños de letra): **0 desbordes reales** |
| **Contraste** | **6.072 textos** en 12 rutas × 4 temas: **0 fallos AA**, peor caso **5,71:1** |

Las mediciones se hicieron en navegador, con la misma metodología de la Fase 3 de la auditoría
UX, y encontraron **tres defectos que se corrigieron**:

1. `.tramite-requisito-meta` —la línea de metadatos del requisito, nueva— empujaba la página a
   **487 px** sobre una pantalla de 320. Le faltaba `min-width: 0` dentro de un contenedor flex.
2. La insignia «Modalidad sin definir» heredaba el `white-space: nowrap` de las demás y medía
   **200 px** al 130 % de texto, junto al título de un paso. Ahora se parte.
3. El lugar de un paso llega de IHTT como `Gerencia de Operaciones/Subgerencia Técnica`: el
   navegador no parte por la barra y la línea se salía. `overflow-wrap: anywhere`.

Dos avisos sobre la propia medición, por si alguien la repite:

- Un iframe **fuera de la ventana** lo estrangula el compositor y las transiciones CSS no
  avanzan nunca, de modo que el modo oscuro se mide con los colores de partida. Daba **696
  fallos AA falsos**. Hay que medir dentro del viewport y desactivar las transiciones.
- La única lectura de `scrollWidth` que quedó alta (6 px, ficha 123-011 a 320 px y 130 %) se
  comprobó pidiéndole a la página que se desplazara: `scrollX` se queda en 0. **No se desplaza
  a los lados** — es el mismo artefacto que ya documentó la Fase 3.

### Lo que queda fuera de la F6a

- **`/diagnostico/catalogo-portal` no respeta los temas**: su hoja está escrita con colores
  fijos y en modo oscuro falla contraste. Es una pantalla interna de comprobación, existía así
  antes de esta fase, y no está entre las rutas del ciudadano.
- **F6b**: soltar `Tramites` y sus hijas, `Categorias` e `Instituciones`, con sus entidades,
  configuraciones y sembradores. Espera a que un corte funcione con contenido real.
- **El contenido del piloto.** Mientras el origen no tenga fichas de verdad, el portal enseña
  49 trámites con nombre, requisitos, pasos y lugares —que sí son reales— y `-` en plazo y
  costo. Apuntar la sincronización a `TramitesEstado_Prod` daría 49 fichas con todos los
  campos en `-`; dejarla en `Ensayo` enseña «PRUEBA» en 20 de ellas. Ninguna de las dos es
  publicable: **eso es la F3, y es captura humana, no código.**

---

## 19-08-2026 · Fase 6b — retirar el esquema heredado

**Solo HondurasÁgil.** Se sueltan las ocho tablas del catálogo propio: `Tramites` y sus cinco
colecciones hijas, `Categorias` e `Instituciones`, con sus entidades, configuraciones y
sembradores. Migración `20260819194558_RetirarCatalogoSembrado`.

> **Se hizo antes de tiempo, y conviene que conste.** La F6a dejó esta fase bloqueada «hasta
> que un corte funcione en producción con contenido real», y ese corte no ha pasado. Se
> ejecuta por indicación del usuario. Lo que eso significa en la práctica está abajo, en
> «qué deja de ser reversible».

### Antes de borrar: lo que se rescató

`InstitucionesSeeder` guardaba **ocho instituciones con teléfono, sitio, dirección y horario
leídos uno a uno en la página oficial de cada entidad** el 12-08-2026. Se comprobó que no eran
redundantes:

| Medida | Valor |
|---|---|
| Instituciones replicadas desde PortalDigital | 45 |
| De ellas, con teléfono | **0** |
| De ellas, con sitio web | **0** |
| De las 8 siglas sembradas, cuántas existen en PortalDigital | **2** (INM, SERNA) |

Eran la única copia. Se volcaron a
`docs/api/contactos/2026-08-12-contactos-institucionales-verificados.md`, con sus fuentes, sus
salvedades y un script acotado a las dos que no admiten duda. **Ese script no se ejecutó**:
escribe sobre producción y es una decisión humana.

De paso quedó anotada una inconsistencia del origen: SERNA aparece **dos veces** en
`TramitesEstado_Prod` —como `MIAMBIENTE`, con el nombre completo, y como `SERNA`, cuyo nombre
es literalmente «SERNA»—. Hay que decidir cuál es la buena antes de escribir en ninguna.

### Un defecto que la fase destapó, y no era el esquema

Al retirar los quince trámites de demostración, sus métricas quedaban apuntando a códigos
inexistentes. Medido antes de tocar nada, en la ventana de 30 días:

| Código | Consultas | ¿Sigue en el catálogo? |
|---|---|---|
| `DEMO-t1` | 100 | no |
| `DEMO-t6` | 60 | no |
| `123-011` | 42 | sí |
| `603-001` | 42 | sí |

`GetMasConsultadosQuery` recortaba al top-4 **antes** de saber cuáles resuelven. La portada
pedía cuatro fichas, resolvía dos, y pintaba **dos tarjetas donde van cuatro** sin explicar
por qué. No es un artefacto de los datos de demostración: pasa igual cada vez que PortalDigital
retira un trámite, porque la sincronización lo borra de la réplica y las consultas acumuladas
se quedan —a propósito, son un dato medido—.

Se arregló en los dos sitios: la consulta descarta los códigos que ya no están en el catálogo
**antes** del recorte, y la migración purga las filas con prefijo `DEMO-`. El prefijo se
eligió en la F1b justamente para poder encontrarlas hoy.

### Qué se borró

| | |
|---|---|
| Tablas | `Tramites`, `TramitePasos`, `TramiteRequisitos`, `TramiteLugares`, `TramitePlantillas`, `TramitesRelacionados`, `Categorias`, `Instituciones` |
| Entidades | las ocho correspondientes, más el objeto de valor `Costo` |
| Configuraciones | las ocho de EF |
| Sembradores | los cinco, más `TramitesCatalogo` (47 KB de datos del mockup) |
| Pruebas | 12 archivos de lo que dejó de existir |

`TiempoEstimado` se redujo a lo único que seguía vivo. Era un objeto de valor con sentido
cuando el catálogo era nuestro —guardaba el texto para mostrar y el valor estructurado para
ordenar—, pero desde la F6a el texto vive en su propia columna de la réplica y del objeto solo
se usaba el analizador. Ahora es `Domain/Plazo.cs`, una clase estática con un método:
`Plazo.EnMinutos(texto)`.

### Qué deja de ser reversible

- El `Down` de la migración **devuelve el esquema, no los datos**. Las ocho tablas vuelven
  vacías, y los sembradores que las llenaban ya no existen: revertir deja un portal que
  arranca pero sin catálogo propio. Comprobado ejecutándolo.
- Los quince trámites de demostración, las ocho categorías y las ocho instituciones **solo
  están en git**, en el commit `599db75`, bajo `src/Infrastructure/Persistence/Seeding/`.
- Los **23 votos** de demostración se borraron. No había ninguno real: los tres códigos
  votados eran `DEMO-t1`, `DEMO-t2` y `DEMO-t3`.

Esto es lo que la F6a quería evitar hasta que un corte real funcionara. Se hace ahora por
decisión del usuario; queda dicho para que nadie lo descubra el día que haga falta.

### Un hueco de cobertura que la fase abrió y se cerró

Al borrar las pruebas de las entidades retiradas, la batería bajó de 232 a 151. La caída es
correcta —eran pruebas de código que ya no existe— pero dejaba a la vista otra cosa: las
entidades del catálogo replicado, que desde hoy son **las únicas**, nunca tuvieron pruebas de
dominio. Nacieron como réplica y convivían con un catálogo propio que sí las tenía.

Se escribieron diez (`TramitePortalTests`), que dejan la batería en 161, e incluyen las dos que
importan de verdad:

- que `Refrescar` **borre** `TiempoMinutos` cuando el origen retira el texto del plazo — un
  minutaje que sobrevive a su texto es indistinguible de uno bueno y ordenaría el catálogo por
  un dato que ya nadie publica;
- que un campo vacío se guarde como `null` y no como cadena vacía — el portal decide con
  `is null` si pinta el dato o el guion, y una cadena vacía dejaría la casilla en blanco, que
  se lee como error de la página.

### Comprobado

| Qué | Cómo |
|---|---|
| Baterías | **161** en verde: 51 de dominio, 110 de aplicación. 0 errores, 0 advertencias |
| Migración | **Ida, vuelta y vuelta a la ida** contra la base de desarrollo. Tras el `Up`: 12 tablas, las 8 fuera. Tras el `Down`: las 8 de vuelta, vacías. Tras el segundo `Up`: 12 otra vez |
| Las métricas reales sobreviven | 12 filas de consultas → **4**, exactamente las de códigos vigentes (`123-011`, `603-001`, `123-001`, `603-019`). Votos 23 → 0, y los 23 eran `DEMO-` |
| El catálogo sigue en pie | «49 trámites encontrados»; portada 49 / 20 / 0 / 3 |
| Las ocho rutas del ciudadano | 200 todas, con la base ya sin las tablas |
| El voto | Emitido con navegador real sobre `603-001`: se guarda contra la réplica. Retirado después |
| Códigos retirados | `/Tramites/DEMO-t1` → **404** |
| Desbordes | **150 combinaciones**: **0 reales** (el mismo artefacto de 6 px que ya documentó la Fase 3) |
| Contraste | **5.268 textos** en 10 rutas × 4 temas: **0 fallos AA**, peor caso **5,71:1** |

---

## 19-08-2026 · Dos decisiones del usuario, y un hallazgo que salió al comprobarlas

No hubo cambio de código. Se cerraron dos preguntas abiertas y, al medirlas contra la base,
apareció un tercer asunto que no estaba en el radar de nadie.

### D-05 · `SERNA` y `MIAMBIENTE` se quedan separadas

La fase anterior dejó esto como ambiguo y sin resolver. La decisión es **no fundirlas**, y la
razón es mejor que la que yo había planteado: pudieron ser entidades distintas en su momento.
Fundirlas hoy borra esa historia sin vuelta atrás; unirlas mañana, si se confirma que son la
misma, siempre se puede.

Medido contra `TramitesEstado_Prod` (la sigla es la columna `Id`, no existe `Sigla`):

| Fila | Nombre | `Activo` | Trámites |
|---|---|---|---|
| `MIAMBIENTE` | Secretaría de Recursos Naturales y Ambiente | **0** | **5** |
| `SERNA` | SERNA | 1 | 0 |

De ahí sale a cuál va el contacto rescatado, y no es una moneda al aire: la tarjeta levantada
a mano dice «Secretaría de Recursos Naturales y Ambiente», **palabra por palabra** el nombre
de `MIAMBIENTE`, que además es la que carga los 5 trámites. La fila `SERNA` no se toca.

Dos avisos que quedan escritos en el documento de contactos, porque no se ven desde la
pantalla: `MIAMBIENTE` está **inactiva** y `GET /api/v1/instituciones` filtra por `Activo`, así
que cargarle el teléfono no la hará aparecer; y sus 5 trámites sí se publican, porque el
catálogo no mira si la institución está activa. Hoy no hay ningún trámite publicado colgando
de una institución inactiva —medido: **0**—, pero el desajuste está esperando.

El script pasa de dos filas a tres (INM, IHSS, MIAMBIENTE). **Sigue sin ejecutarse.**

### La F3 estaba mal contada en el plan, y era culpa del plan

La captura la hacen los **técnicos y administradores de las instituciones**, desde
PortalDigital, en `/Siger/CapturaLote`. No era una pregunta abierta; el plan lo dejaba
implícito y se prestaba a leerlo como si lo fuera. Queda dicho de forma explícita.

También se corrige el motivo por el que las tres herramientas siguen sin abrirse en navegador:
no es LocalDB —eso caducó—, es que exigen sesión con permiso `Siger/Editar`.

### El hallazgo: la captura despublica lo que toca

Al medir el estado real de las dos bases salió esto:

| | `Prod` | `Ensayo` |
|---|---|---|
| Trámites publicados | 49 | 49 |
| Con `PRUEBA - 5 dias habiles` | 0 | **20** |
| Sin tiempo | **49** | 29 |
| Sin costo | **49** | 29 |
| Que cumplen la regla de ficha mínima | **0** | — |

Los 49 de producción están en `Publicado` y en `Aprobado`, y **ninguno** cumple la regla de
ficha mínima. Pero las dos pantallas que escriben —`CapturaLote.cshtml.cs:52` y
`Editor.cshtml.cs:132`— recalculan `Publicado` como «aprobada **Y** completa».

La consecuencia es directa: **el primer guardado que no complete la ficha del todo en ese mismo
paso la despublica.** Un técnico que llene por tandas —categoría de treinta, luego modalidad—
vería el catálogo vaciarse mientras trabaja, y si el corte ya ocurrió, se vaciaría de cara al
ciudadano. Es justo lo contrario de lo que espera quien está capturando.

No es un descuido: el comentario de `CalcularPublicado` dice que es deliberado. Es una
**decisión de política** que hoy se contradice con el estado de la base y con todo lo que se
construyó en F5 y F6a —el «-» de `CampoVisible`, «Modalidad sin definir», el costo de tres
estados—, que existe precisamente para enseñar fichas incompletas sin mentir.

Queda anotado como **P-09**, con dueño y fase. No se toca el código: cambiar cuándo se publica
algo cambia lo que ve el ciudadano, y eso no lo decide quien encuentra el problema.

---

## 20-08-2026 · Las dos aplicaciones miraban bases distintas

El usuario editó y completó la ficha `400-002` («Rectificación posterior al levante de las
mercancías», ADUANAS) desde el Web y no apareció en HondurasÁgil. La edición fue correcta:
en `TramitesEstado_Prod` quedó `EstadoSiger = Completo`, `Publicado = 1`, con categoría,
modalidad, tiempo y costo, sellada a las 15:21. ADUANAS pasó de 0 a 1 trámite publicado —
es decir, la regla de `CalcularPublicado` funcionó en su dirección buena.

Lo que fallaba estaba después, y eran dos paredes independientes.

### Pared 1 — el desdoblamiento de bases en desarrollo

| Aplicación | Puerto | `appsettings.Development.json` |
|---|---|---|
| `src/Web` (donde capturan los técnicos) | 49175 | `TramitesEstado_Prod` |
| `src/Presentation` (la API que consume HondurasÁgil) | 5199 | `TramitesEstado_Ensayo` |

Se escribía en una base y se preguntaba en la otra. Comprobado contra la API en vivo:
`GET /api/v1/salud` → 200, `GET /api/v1/tramites/400-002` → **404**, y el código tampoco
aparecía en `/api/v1/codigos-publicados`. En `Ensayo` esa misma ficha seguía `Registrado`,
`Publicado = 0`.

Peor que el síntoma es lo que implicaba: **el entorno de desarrollo escribía en producción.**
Cualquier prueba de captura dejaba huella en `TramitesEstado_Prod`.

**Corregido el 20-08-2026** apuntando `src/Web/appsettings.Development.json` a
`TramitesEstado_Ensayo`, por decisión del usuario: probar tranquilo sin tocar producción.
Verificado antes de aplicarlo — las dos bases están en la misma migración
(`20260814162437_CorregirColacionBusqueda`, 48 aplicadas) y tienen idénticos usuarios,
roles y permisos (17 / 6 / 144 / 30), así que la sesión con `Siger/Editar` funciona igual.
El `appsettings.json` versionado no se tocó: producción sigue en `DigerTramitesEstado`.

### Pared 2 — el corte del piloto

Aunque las bases coincidieran, `400-002` tampoco habría llegado. `InstitucionesDelCorte`
vale `INPREMA, IHTT, CONSUCOOP` (D-04) y `SincronizarCatalogoPortalCommand` descarta lo
demás de forma explícita. ADUANAS no está en el corte, y no se agregó: cambiar el alcance
del piloto es una decisión, no un ajuste para que salga una prueba.

Para probar el ciclo completo hay que usar una ficha del corte. Candidata identificada:
**`603-002` · «Gestión de atención al docente» (INPREMA)**, hoy `Registrado`, `Publicado = 0`
y ausente de la réplica —que tiene 49 filas—. Al completarla y aprobarla debe aparecer como
la número 50 en HondurasÁgil.

### El sincronizador nunca estuvo roto

`PortalEstadoSincronizacion` del 20-08: último intento y último éxito a las **15:08:50**,
sin error, 0 actualizados y 0 retirados. Corría bien; simplemente no había nada que traer
de la base que él mira. Conviene recordarlo: **un sincronizador en verde no prueba que los
datos lleguen**, solo que preguntó sin fallar.

### Coda del mismo día — la ficha del corte tampoco «aparecía», y tampoco estaba rota

Con las bases ya unificadas, el usuario capturó `506-010` («Pago del Aporte Obligatorio»,
CONSUCOOP, sí del corte) y seguía sin verse. Los relojes lo explican todo, en UTC:

| Momento | Hora |
|---|---|
| Último ciclo de sincronización (al reiniciar HondurasÁgil) | **15:50:19** |
| `UpdatedAt` de la ficha capturada | **16:00:03** |
| Siguiente ciclo automático (`IntervaloMinutos: 60`) | 16:50 |

Se reinició *antes* de capturar, así que el ciclo del arranque corrió cuando la ficha aún no
existía. La API ya la servía sin problema —`GET /api/v1/tramites/506-010` → 200, presente en
`/codigos-publicados`, y `/cambios?desde=15:50:20` devolvía exactamente `["506-010"]`—; solo
faltaba que alguien preguntara.

Dicho de otro modo: **la segunda «avería» fue la latencia de sincronización disfrazada de
fallo.** Una hora de espera es indistinguible de un enlace roto para quien está probando, y
esa confusión ya costó dos diagnósticos en un mismo día.

**Mitigado** bajando `PortalDigital:IntervaloMinutos` a **2** en el
`appsettings.Development.json` de HondurasÁgil —solo desarrollo; producción se queda en 60—.
No existe disparador manual de sincronización en la capa Web: el `SincronizacionPortalHostedService`
es la única vía. Si la captura del piloto se hace por tandas, conviene que exista uno.

---

## 20-08-2026 · P-09 resuelto: publicar deja de exigir ficha completa

**Decisión del usuario: opción 1.** Una ficha incompleta se queda publicada y HondurasÁgil
enseña un guion donde falta el dato. La alternativa —despublicarla hasta completarla— vaciaba
el catálogo mientras se captura.

Hasta hoy, las dos pantallas que guardan fichas calculaban:

```csharp
Publicado = EstadoSiger is "Aprobado" or "Completo"
            && FichaPublicaCompletitud.Evaluar(categoría, modalidad, tiempo, costo, SOL…);
```

En producción había **49 fichas publicadas y ninguna cumplía la segunda mitad**. La primera
edición de cualquiera de ellas la habría borrado del portal del ciudadano, y un técnico
llenando por tandas habría visto el catálogo vaciarse mientras trabajaba. Justo lo contrario
de lo que espera quien captura.

Ahora es `Publicado = EstadoSiger is "Aprobado" or "Completo"`, y nada más.

**Alcance real: dos líneas.** Se comprobó antes de tocar nada que la API pública filtra
**solo** por `Publicado` —`GetCatalogoPublicoQuery`, `GetTramitePublicoQuery`,
`GetCodigosPublicadosQuery`, `GetCambiosPublicosQuery`, `GetInstitucionesPublicasQuery` y
`GetCategoriasPublicasQuery`, todas—, y que usa `FichaPublicaCompletitud` únicamente para
*informar* el campo `FichaCompleta` del contrato. Es decir, la completitud **no desaparece,
deja de censurar**:

| Sigue funcionando igual | Cambió |
|---|---|
| `CamposFaltantes` y `Frase` avisan al técnico de lo que falta | Guardar a medias ya no despublica |
| El editor y el listado marcan las fichas incompletas | |
| La API sigue publicando `FichaCompleta` en cada ficha | |
| HondurasÁgil puede seguir exigiendo ficha completa con `ExigirFichaCompleta` | |

Archivos: `src/Web/Pages/Siger/Editor.cshtml.cs` (`CalcularPublicado`) y
`src/Web/Pages/Siger/CapturaLote.cshtml.cs`. El porqué quedó escrito en el `<remarks>` del
primero, que es donde lo va a buscar quien se pregunte por qué la regla es así.

**Verificado:** compilación con 0 avisos y 0 errores; **181 pruebas verdes** (24 de Domain,
134 de Application, 23 de Web).

**Efecto colateral que conviene tener presente:** con la ficha mínima ya no gobernando la
publicación, el que decide qué ve el ciudadano es `EstadoSiger`. Aprobar una ficha vacía ahora
la publica vacía. Es exactamente lo que se pidió, pero traslada el peso a quien aprueba.
