# Reuniones del portal a Outlook — diseño

**Fecha:** 2026-10-22 · **Rama:** `ike_opt` · **Estado:** aprobado en conversación, pendiente de revisión escrita

## Qué se quiere

Que una reunión agendada en el portal llegue al calendario de Outlook de quien la organiza y
de sus invitados, como una cita de verdad (con Aceptar/Rechazar y, si es virtual, enlace de
Teams), y que los cambios posteriores en el portal se reflejen en esa cita.

**El portal sigue siendo la fuente de la verdad.** La dirección es una sola: portal → Outlook.
Lo que se mueva a mano en Outlook no vuelve al portal.

## Decisiones tomadas

| Pregunta | Decisión |
|---|---|
| Dirección | Portal → Outlook |
| Desde qué cuenta sale la invitación | La del **organizador**, con Microsoft Graph y permiso **delegado** |
| A quién se invita | Asistentes **pre-registrados** con correo + `EpCorreo` + `FacCorreo` |
| Cuándo se envía | **Botón explícito** «Enviar a Outlook»; luego cada guardado actualiza y borrar cancela |
| Teams | **Siempre por defecto**, cualquiera sea la `Modalidad`: las presenciales también se graban para sacar la transcripción. Una casilla al enviar permite quitarlo en una reunión concreta |
| Cómo obtiene permiso el portal | **«Conectar mi Outlook»** desde el perfil; el login del portal no cambia |

Descartados: login de todo el portal con Entra ID (proyecto de autenticación completo, fuera de
alcance) y Graph sin guardar tokens (impediría actualizar/cancelar al guardar).

## Contexto verificado

- `diger.gob.hn` está en **Microsoft 365 / Exchange Online**: MX
  `diger-gob-hn.mail.protection.outlook.com`, SPF `spf.protection.outlook.com`, registro `MS=`.
  Tenant `a28bd482-1bfe-46d3-9a02-ccd7f27b83bf`.
- Desde la máquina de desarrollo responden `login.microsoftonline.com` y `graph.microsoft.com`
  (200, ~0,2 s). **Falta comprobarlo desde el servidor de producción.**
- `appsettings.json` envía correo por `smtp.gmail.com` como `no-reply@diger.gob.hn`, lo que no
  cuadra con un dominio en Microsoft 365. Queda anotado; este trabajo no lo toca.
- Datos reales en `GestionGD_new` (81 reuniones vivas):
  - `Hora` siempre `HH:mm` (el editor usa un campo de hora); 3 vacías.
  - `Duracion` vacía en 79; las otras dos: «1 hora» y «1».
  - `Modalidad`: Presencial 31, Virtual 46, **Híbrida** (con tilde) 2, vacía 2.
  - Correos disponibles para invitar: 4 pre-registrados (los 4 con correo), 6 reuniones con
    `EpCorreo`, 6 con `FacCorreo`.

## Arquitectura

```
Web (Perfil, Editor de reunión)
  └─ MediatR ─► Application
                 ├─ EnviarReunionAOutlookCommand
                 ├─ SincronizarReunionOutlookCommand
                 ├─ CancelarReunionOutlookCommand
                 ├─ EventoOutlookBuilder        (puro, sin E/S)
                 ├─ HorarioReunion              (parser puro de Fecha/Hora/Duracion)
                 └─ IOutlookCalendario, IConexionMicrosoft   (interfaces)
Infrastructure/Outlook
  ├─ GraphOutlookCalendario   : IOutlookCalendario   (MSAL + HttpClient contra Graph v1.0)
  ├─ ConexionMicrosoftService : IConexionMicrosoft   (caché MSAL cifrada en SQL)
  └─ AppDbContext             (tabla ConexionesMicrosoft, columnas nuevas en Reuniones)
```

Ninguna otra parte del portal conoce Graph. Los handlers solo ven `IOutlookCalendario`.

**Sin SDK de Graph.** Se usa MSAL (`Microsoft.Identity.Client`) para los tokens y un
`HttpClient` simple contra Graph v1.0: son tres llamadas (`POST me/events`,
`PATCH me/events/{id}`, `POST me/events/{id}/cancel`) y no justifican el SDK entero.

### `IOutlookCalendario`

```csharp
Task<ResultadoOutlook> CrearAsync(Guid organizadorId, EventoOutlook evento, Guid transaccionId, CancellationToken ct);
Task<ResultadoOutlook> ActualizarAsync(Guid organizadorId, string eventId, EventoOutlook evento, CancellationToken ct);
Task CancelarAsync(Guid organizadorId, string eventId, string comentario, CancellationToken ct);
```

`ResultadoOutlook` = `{ EventId, EnlaceTeams }`. Los fallos se expresan con dos excepciones
propias: `OutlookConexionVencidaException` (MSAL pide interacción: token revocado o expirado) y
`OutlookNoDisponibleException` (red, 5xx, throttling). Nada de tipos de Graph cruza la interfaz.
Una respuesta de Graph ilegible o incompleta (JSON roto, sin `id`) también se trata como
«Outlook no disponible»: no se deja escapar una `JsonException` por el puerto.

### `EventoOutlook`

Registro inmutable: `Asunto`, `Inicio`, `Fin` (en `Central America Standard Time`, la zona de
Windows para America/Tegucigalpa), `Ubicacion` (`Lugar`), `Cuerpo` (`ObjetivoAgenda` + enlace a
la reunión en el portal), `Invitados` (lista de `{ Correo, Nombre }`), `EsTeams`.

El enlace a la ficha (`/Reuniones/Acta/{id}`) se arma con la dirección pública configurada
(`Correo:UrlBase`), **no con la de la petición**. El cuerpo entra en la huella: armado con el
host de la petición, el mismo guardado sin cambios hecho por otro host —el subdominio `cert.*`,
que comparte la sesión, http en vez de https, un nombre interno del servidor— cambiaba la huella,
provocaba un `PATCH` y un «reunión actualizada» a cada invitado, con un enlace a ese host. Solo
si `Correo:UrlBase` falta o no es válida se usa la petición, como antes (el arranque ya advierte
que falta). Editor y asistencia usan el mismo ayudante (`SincronizacionOutlook`).

`Huella()` = SHA-256 de la representación canónica del evento (invitados ordenados y en
minúsculas). Es lo que decide si un guardado justifica llamar a Graph.

### `EventoOutlookBuilder`

`Reunion` → `EventoOutlook`, o un motivo de rechazo legible:

- Sin `Fecha` o sin `Hora` válida → rechazo «La reunión necesita fecha y hora para ir a Outlook».
- `Hora` debe ser `HH:mm` o `H:mm` (0–23 / 0–59; «9:00» vale igual que «09:00»). Otra forma →
  rechazo con el valor encontrado.
- `Duracion`: vacía → **1 hora**. Acepta `"N"` (horas), `"N hora(s)"`, `"N h"`, `"N min"`,
  `"N minutos"`, `"H:mm"`. Cualquier otra cosa → rechazo con el valor encontrado (no se adivina).
- `EsTeams` = `Reunion.OutlookConTeams`, que nace en **true** sin mirar la `Modalidad`. Una
  reunión presencial lleva `Lugar` como ubicación **y** el enlace de Teams: en la sala se
  abre la reunión para grabarla y obtener la transcripción.
- Invitados: asistentes con `EsPreregistro = true` y correo válido, más `EpCorreo` y
  `FacCorreo`; deduplicados por correo en minúsculas; se excluye el correo del propio
  organizador.

## Datos

### Tabla nueva `ConexionesMicrosoft`

| Columna | Tipo | Nota |
|---|---|---|
| `Id` | uniqueidentifier, PK, FK `Usuarios` | **es el id del usuario**: una conexión por usuario |
| `Upn` | nvarchar(256) | cuenta Microsoft conectada, para mostrarla |
| `HomeAccountId` | nvarchar(200) | lo necesita MSAL para encontrar la cuenta dentro de la caché |
| `CacheTokens` | varbinary(max) | caché MSAL **cifrada con Data Protection** (propósito propio) |
| `ConectadaEl` | datetime2 | |
| `RequiereReconexion` | bit | se enciende con `OutlookConexionVencidaException` |
| `UltimoError` | nvarchar(500) NULL | |

Lleva además las columnas de auditoría de `BaseAuditableEntity` (`CreatedAt`/`CreatedBy`,
`UpdatedAt`/`UpdatedBy`).

No lleva filtro de alcance institucional (igual que `Usuario`). Si se pierden las llaves de
Data Protection, la caché deja de poder leerse: se trata como conexión vencida y el usuario
reconecta.

### Columnas nuevas en `Reuniones`

| Columna | Tipo |
|---|---|
| `OutlookEventId` | nvarchar(400) NULL |
| `OutlookTransaccionId` | uniqueidentifier NULL |
| `OutlookOrganizadorId` | uniqueidentifier NULL |
| `OutlookConTeams` | bit NOT NULL DEFAULT 1 |
| `OutlookEnlaceTeams` | nvarchar(1000) NULL |
| `OutlookHuella` | nchar(64) NULL |
| `OutlookSincronizadaEl` | datetime2 NULL |
| `OutlookPendiente` | bit NOT NULL DEFAULT 0 |
| `OutlookError` | nvarchar(500) NULL |

Entrega: migración EF `20261022221928_ReunionesAOutlook` en `Persistence\Migrations` **y**
`database/2026-10-23_poner_al_dia.sql`, idempotente contra el catálogo, con su comprobación
`presente`/`FALTA` (convención del repo).

## Flujos

### Conectar / desconectar (perfil)

- `Cuenta/Perfil` gana una tarjeta «Microsoft Outlook»: sin conexión → «Conectar mi Outlook»;
  con conexión → cuenta conectada, fecha y «Desconectar»; vencida → aviso y «Reconectar».
- «Conectar» redirige a Microsoft (authorization code + PKCE) con los alcances
  `Calendars.ReadWrite User.Read offline_access`, y `state` atado a la sesión. El `state` y el
  verificador PKCE viajan en **TempData** (la cookie de TempData, cifrada con Data Protection),
  no en una sesión del servidor ni en la base: el portal no usa `ISession`.
- Los handlers «Conectar» y «Desconectar» del perfil, como `OnGetAsync`, empiezan exigiendo un
  usuario: `AllowAnonymousToFolder("/Cuenta")` gana sobre el `[Authorize]` de la página.
- `/Cuenta/OutlookCallback` canjea el código, guarda la caché cifrada y vuelve al perfil.
  Lleva `[PermisoNoRequerido]` (autoservicio) y `[Authorize]`, como las demás de `/Cuenta`
  que exigen sesión.
- El retorno valida el `state` (comparación de tiempo constante) **antes que nada**, también
  en las respuestas de error de Microsoft, que lo traen. Un retorno que no corresponde o
  falsificado **no consume** el estado pendiente: lo lee sin gastarlo, así un enlace preparado
  que llegue antes no arruina la conexión en curso. El texto de `error` de Microsoft nunca se
  refleja en la página: el aviso es un mensaje fijo.
- Ni el retorno ni «Conectar» terminan en la página de error: un imprevisto al canjear (la base,
  MSAL) o un rechazo de MSAL al armar la URL vuelven al perfil con un aviso fijo («Microsoft no
  completó la conexión…» o el código de error de MSAL, nunca su mensaje).
- MSAL refresca la caché al pedir un token; ese refresco se serializa **por usuario** con un
  candado en memoria. Basta con un solo proceso de IIS; una granja de servidores necesitaría
  un `rowversion` en la fila.
- «Desconectar» borra la fila. Las citas ya enviadas quedan en Outlook; sus actualizaciones
  futuras quedarán pendientes con el aviso de conexión vencida.

### Enviar

`EnviarReunionAOutlookCommand(ReunionId)` — botón en el editor, permiso `Reuniones.Editar`,
visible solo si el usuario tiene conexión y la reunión aún no está en Outlook.

1. Exige conexión activa del usuario actual.
2. Junto al botón va la casilla **«Crear reunión de Teams»**, marcada por defecto; su valor se
   guarda en `OutlookConTeams`. Queda fija después del envío: Graph permite volver en línea
   una cita, pero no quitarle el Teams a una que ya lo tiene, así que el portal no ofrece
   cambiarlo y no lo promete.
3. `EventoOutlookBuilder` arma el evento o devuelve el motivo de rechazo (se muestra tal cual).
4. Genera y **guarda primero** `OutlookTransaccionId` si no existe, y luego llama a `CrearAsync`
   con él: Graph descarta la creación repetida con el mismo `transactionId`, así que
   reintentar después de un fallo al guardar no duplica la cita a los invitados.
5. Guarda `OutlookEventId`, `OutlookOrganizadorId` (= usuario actual), `OutlookEnlaceTeams`,
   `OutlookHuella`, `OutlookSincronizadaEl`; `OutlookPendiente = 0`.

Si Graph falla, solo queda guardado el `OutlookTransaccionId`; la reunión sigue «sin enviar» y
el usuario ve el motivo y puede reintentar. Un fallo imprevisto (no uno de los dos tipados)
tampoco es una página de error: la tarjeta dice «No se pudo enviar a Outlook. Vuelva a
intentarlo; si se repite, avise a TI.»

### Actualizar al guardar

`ActualizarReunionCommand` **no cambia**: guardar nunca falla por Outlook. Tras un guardado
exitoso, si la reunión tiene `OutlookEventId`, la página llama a
`SincronizarReunionOutlookCommand(ReunionId)`:

1. Reconstruye el evento. Si el builder lo rechaza (p. ej. borraron la hora), marca pendiente
   con ese motivo.
2. **Si la cita ya terminó** (fin del evento anterior a la hora actual de Honduras), no llama a
   Graph y no marca pendiente; si la reunión tenía un aviso pendiente, lo descarta (ya no hay
   nada que mandar) y guarda solo eso. Llenar la memoria después de la reunión cambia
   `ObjetivoAgenda`, que va en el cuerpo de la cita, y avisaba «reunión actualizada» a todos los
   invitados de una reunión que ya pasó. Borrar la reunión sigue cancelando la cita. El primer
   envío («Enviar a Outlook») de una reunión ya terminada sigue permitido: es una acción
   explícita de la persona. Agregado en la fase 2 parte A (2026-10-23).
3. Si `Huella()` == `OutlookHuella` → no hace nada (evita mandar «reunión actualizada» a todos
   los invitados en cada guardado).
4. Si difiere → `ActualizarAsync` con la conexión de **`OutlookOrganizadorId`** (no la de quien
   edita). Guarda huella, enlace y fecha; limpia pendiente.
5. Fallo → `OutlookPendiente = 1`, `OutlookError` con el motivo. El editor y la ficha muestran
   «No se pudo actualizar en Outlook · Reintentar»; con conexión vencida el texto nombra al
   organizador: «La conexión de Outlook de *Nombre* venció; debe reconectarse desde su perfil».

«Reintentar» es el mismo comando.

**También desde la asistencia.** Agregar o quitar pre-registrados desde `Reuniones/Asistencia`
(pre-registrar, agregar del directorio, eliminar un registro) cambia a quién se invita, así que
tras el cambio la página llama al mismo comando, con la misma ficha. Un fallo no deshace ni
convierte en error el cambio de la lista: se agrega al aviso de éxito de esa página («Pero no se
pudo actualizar la cita en Outlook…»).

**Cualquier** fallo del lado de Outlook —no solo los dos tipados: también una respuesta
ilegible o un fallo del token— deja la reunión pendiente. Y si lo que falla es el propio
comando de sincronización (p. ej. el guardado sacó la reunión del alcance de quien edita), la
reunión ya quedó guardada y la página no lo convierte en error: muestra «La reunión se guardó,
pero no se pudo actualizar en Outlook. Use «Reintentar» en la tarjeta de Outlook.»

### Cancelar al borrar

Antes de `EliminarReunionCommand`, si hay `OutlookEventId`, la página llama a
`CancelarReunionOutlookCommand`, que invoca `CancelarAsync` con la conexión del organizador
(Graph avisa a los invitados). **Si falla —por cualquier motivo—, la reunión se borra igual**
y el usuario ve: «Reunión eliminada. No se pudo cancelar la cita en Outlook: cancélela a mano
desde el calendario del organizador.» El portal no bloquea un borrado por Microsoft.

## Seguridad

- Tokens jamás en logs, en la página ni en el JSON de ninguna respuesta.
- La caché se cifra con un `IDataProtector` de propósito exclusivo
  (`"ConexionesMicrosoft.CacheTokens"`).
- `ClientId`, `TenantId` y `ClientSecret` van en configuración; el secreto en User Secrets en
  desarrollo y en variables de entorno / configuración del servidor en producción, nunca en un
  appsettings versionado.
- Todo `fetch` que escriba (si «Reintentar» se hace por AJAX) lleva el token antiforgery.

## Requisitos fuera del código (TI)

1. Registrar una aplicación en Entra ID, de un solo tenant (`a28bd482-…`).
2. Permisos **delegados**: `Calendars.ReadWrite`, `User.Read`, `offline_access`; para la
   fase 2, además `OnlineMeetingTranscript.Read.All`, que exige consentimiento de
   administrador.
3. URIs de redirección, **las tres**: `https://localhost:49175/Cuenta/OutlookCallback` y
   `https://localhost:49176/Cuenta/OutlookCallback` (desarrollo: el portal escucha en los dos
   puertos https y la dirección de retorno sale del puerto por el que se entró) y la de
   producción. Una que falte hace que Microsoft rechace el retorno.
4. Crear un secreto de cliente y entregarlo por canal seguro.
5. Si el tenant restringe el consentimiento de usuarios, otorgar consentimiento de
   administrador una vez.
6. Confirmar que el servidor de producción alcanza `login.microsoftonline.com` y
   `graph.microsoft.com` por HTTPS.

Sin 1–5 se puede construir y probar todo con el falso `IOutlookCalendario`; sin ellos no se
puede hacer la prueba real de punta a punta.

## Pruebas

- **Unitarias (Application.Tests / Domain.Tests):**
  - Parser de `Hora`/`Duracion` con los valores reales listados arriba, más rechazos.
  - `EventoOutlookBuilder`: deduplicación de invitados, exclusión del organizador, Teams en
    toda modalidad cuando `OutlookConTeams` (incluida la presencial, con `Lugar` como
    ubicación), sin Teams cuando se desmarcó, zona horaria, 1 h por defecto.
  - `Huella()`: estable con los mismos datos, cambia con cualquier campo visible, no cambia con
    el orden de los invitados.
- **Handlers con `IOutlookCalendario` falso (NSubstitute):** enviar guarda id y huella; guardar
  sin cambios visibles no llama a Graph; fallo de Graph deja pendiente; conexión vencida nombra
  al organizador; borrar cancela y borra aunque cancelar falle; el `transactionId` se reusa en
  el reintento.
- **Script SQL:** dos corridas contra una copia de `GestionGD_new`, todo `presente`, la segunda
  sin cambios.
- **Manual de punta a punta** (cuando TI entregue el registro): conectar una cuenta, enviar una
  reunión de prueba invitando a un correo propio, editar la hora, quitar un invitado, borrar; en
  cada paso comprobar Outlook.

Ninguna prueba automática llama a Graph.

**Pendiente:** la prueba manual de punta a punta con una cuenta real espera el registro de TI
(`ClientId` y secreto). En ella hay que mirar en especial:

- Que Microsoft vuelva por **GET** (`response_mode=query`). Un retorno por `form_post` llegaría
  como POST entre sitios, sin la cookie de sesión (`SameSite=Lax`), y el retorno no
  encontraría ni la sesión ni el estado.
- Si un `PATCH` sobre una cita que ya tiene Teams conserva el mismo enlace para unirse.

### Límites conocidos de la fase 1

- **Un 401 de Graph no marca la conexión.** Deja la reunión pendiente con el aviso de conexión
  vencida, pero no enciende `RequiereReconexion` en el perfil del organizador: la tarjeta del
  perfil sigue diciendo «conectada» hasta que MSAL mismo pida interacción al renovar.
- **«Cambiar de cuenta» deja las citas en la cuenta vieja.** Si quien ya organizó citas reconecta
  con otra cuenta Microsoft, esas citas siguen en el calendario de la cuenta anterior; la próxima
  actualización va a la cuenta nueva, recibe 404 y la reunión queda pendiente con «la cita ya no
  existe en Outlook».
- **Un fallo de Graph se registra** con el estado HTTP, el `error.code` del cuerpo y la ruta (que
  lleva el id de la cita, que no es secreto); nunca las cabeceras ni el resto del cuerpo.

### Nota de despliegue

- El candado por usuario alrededor de la caché de tokens vive en memoria: supone **un solo
  proceso**. En IIS, el grupo de aplicaciones sin *web garden* (un solo proceso de trabajo). En un
  reciclado con solapamiento conviven dos procesos durante unos segundos; una renovación que caiga
  justo ahí puede perder la caché y pedir reconexión, nada peor.
- **Recomendación pendiente para el operador, antes de la prueba real:** proteger el anillo de
  llaves de Data Protection con `ProtectKeysWithDpapi()` (o el equivalente del servidor). Esas
  llaves ahora cifran *refresh tokens* de larga vida, no solo cookies de sesión. No se cambia
  `Program.cs` en esta fase: queda como decisión del despliegue.

## Fuera de alcance

- Outlook → portal (leer o importar citas de Outlook).
- Login del portal con Microsoft.
- Enviar desde un buzón institucional compartido.
- Sincronizar respuestas de los invitados (aceptó/rechazó) de vuelta a la asistencia.
- Reuniones recurrentes.
- **Arrancar la grabación y la transcripción solas.** Las inicia quien conduce, desde Teams.
  Automatizarlo exige `OnlineMeetings.ReadWrite`; queda para después.
- **Que el portal resuma por su cuenta** al terminar la reunión, sin que nadie lo pida. Haría
  falta una llave de IA propia, mandaría transcripciones fuera del tenant sin decisión de nadie
  y saltaría la revisión humana. Ver fase 2: la IA es la del usuario, a pedido suyo.

## Fase 2 — De la transcripción de Teams a la ayuda memoria, por el MCP

Va después de la fase 1 y la reutiliza entera: la misma conexión de cada usuario, y la
reunión ya guarda su `OutlookEventId` y su enlace de Teams.

Se parte en dos. **La parte A está hecha** (2026-10-23, plan
`docs/superpowers/plans/2026-10-23-memoria-por-mcp-fase2a.md`): las reglas en el servidor y el
MCP que llena la memoria con un JSON que la IA redacta desde la transcripción que la persona le
pega. No depende de TI. **La parte B** —que el portal descargue la transcripción de Teams— está
hecha en código (2026-10-28) y **apagada** hasta que TI dé el consentimiento de administrador.

### Parte A — hecha

El editor (paso 3, Memoria) ya tenía «Llenar automáticamente desde una transcripción»: un
prompt que **cambia según el tipo** (reunión normal: objetivo, desarrollo, acuerdos;
capacitación: además convocados, asistentes, % y compromisos de seguimiento; ambos: cierre).
Sus reglas vivían en el JavaScript de `Reuniones/Editor.cshtml`. Ahora:

- **Una sola copia de las reglas:** `Application/Reuniones/Memoria/MemoriaDesdeTranscripcion`
  (`Instrucciones(esCap, conEncuesta)` da el prompt, el formato y los campos de las cuatro
  variantes; `Interpretar(json, …)` da la propuesta y el mensaje). El editor y el MCP la llaman;
  ninguno las reescribe.
- **El editor sigue llenando el formulario, no guarda** (`?handler=InterpretarMemoria` devuelve la
  propuesta). Desvía de lo que decía el punto 3 de abajo («el botón Aplicar pasa a llamar al
  comando»): eso habría escrito en la base y perdido lo que la persona tecleó sin guardar.
- **`LlenarMemoriaReunionCommand`** escribe en la reunión, para el MCP. Sin `sobreescribir`,
  un campo con un valor distinto detiene todo, como en el editor. **Nunca** reemplaza acuerdos
  que ya tienen seguimiento (estado distinto de Pendiente, fecha de cumplimiento, nota o
  comentarios), ni con `sobreescribir`. El `responsable` de un acuerdo se guarda como texto.
  Un valor vacío de la IA (`""`, `[]`) no borra el campo.
- **Las cifras de capacitación** aceptan el mismo entero escrito como `80.0`, `"24.0"` o `"80%"`;
  una fracción (`24.5`) o un negativo no se aplican.
- **Herramientas MCP:** `instrucciones_memoria_reunion(id)` (`Reuniones.Ver`) y
  `llenar_memoria_reunion(id, json, sobreescribir=false)` (`Reuniones.Editar`). No sincroniza
  Outlook: con la regla de reunión terminada (Actualizar al guardar, paso 2) no haría nada.
- **Outlook no actualiza una reunión que ya terminó** (fase 1, Actualizar al guardar, paso 2).

### Parte B — hecha en código (2026-10-28), apagada hasta el consentimiento de TI

Cómo quedó, frente al diseño original de abajo:

- **Llave `MicrosoftGraph:Transcripciones`** (por defecto `false`). Solo encendida se piden al
  conectar `OnlineMeetings.Read` (para hallar la reunión en línea por su enlace; el diseño no lo
  nombraba y Graph lo exige) y `OnlineMeetingTranscript.Read.All`. Encenderla sin el
  consentimiento de administrador haría fallar «Conectar mi Outlook» a todos, calendario incluido.
- **Token aparte:** `IConexionMicrosoft.ObtenerTokenTranscripcionesAsync`. El de calendario sigue
  pidiendo solo sus dos alcances, así que una conexión de antes del permiso **no se cae** para el
  calendario: la transcripción responde «tiene que reconectarse una vez» y la fila no se marca vencida.
- **`ITranscripcionesTeams`** (Application/Reuniones/Memoria) → `GraphTranscripcionesTeams`
  (Infrastructure/Outlook): `me/onlineMeetings?$filter=JoinWebUrl eq '…'`, `…/transcripts`, la de
  `createdDateTime` más reciente, `…/content?$format=text/vtt`. `TranscripcionVtt.ATexto` deja
  «Hablante: texto» y junta frases seguidas de la misma persona.
- **`GetTranscripcionReunionQuery`**: pasa por el filtro de alcance, usa la conexión del
  organizador; sin `OutlookEnlaceTeams` u organizador no llama a Microsoft.
- **MCP `obtener_transcripcion_reunion(id)`** (`Reuniones.Ver`): transcripción + las mismas
  instrucciones de `instrucciones_memoria_reunion`. Errores como `McpException` con el mensaje.
- **403** de Graph → mensaje que apunta al consentimiento de TI; **404** o lista vacía → «no
  tiene transcripción… Teams la publica minutos después».
- Pruebas: `TranscripcionesTeamsTests` (VTT, Graph con HttpMessageHandler, la consulta) y dos
  casos en `McpMemoriaReunionTests`.
- Sin tocar: el editor web sigue con el pegado manual.

**Para encenderla:** TI concede consentimiento de administrador a los dos permisos delegados en
el registro `ecbeb8e6-…`; luego `MicrosoftGraph__Transcripciones=true` (o en appsettings) y cada
organizador reconecta Outlook una vez desde su perfil.

### Parte B — el diseño original

1. **Permiso nuevo en la misma conexión:** `OnlineMeetingTranscript.Read.All` (delegado; pide
   consentimiento de administrador). «Conectar mi Outlook» lo pide junto con los de la fase 1;
   quien ya se conectó en fase 1 reconecta una vez.
2. **`ITranscripcionesTeams`** (Application) / implementación Graph (Infrastructure):
   localiza la reunión en línea por su enlace (`/me/onlineMeetings?$filter=JoinWebUrl eq …`),
   lista sus transcripciones y descarga la más reciente en texto. Solo funciona con la conexión
   del **organizador** (`OutlookOrganizadorId`).
3. ~~Las reglas de «Aplicar» pasan al servidor~~ — **hecho en la parte A**, con otro nombre
   (`MemoriaDesdeTranscripcion` + `LlenarMemoriaReunionCommand`) y con el editor llenando el
   formulario en lugar de guardar.
4. **Herramientas MCP** en `src/Web/Mcp`:
   - `obtener_transcripcion_reunion(id)` — **pendiente**: devuelve el tipo de la reunión, el
     prompt que le corresponde y la transcripción. Permiso `Reuniones.Ver`. Hasta entonces,
     `instrucciones_memoria_reunion(id)` da el prompt y la persona pega la transcripción.
   - `llenar_memoria_reunion(id, json, sobreescribir)` — **hecho en la parte A**. Permiso
     `Reuniones.Editar`. Devuelve qué campos llenó y cuáles descartó.

### El flujo

En Claude, conectado al MCP del portal: «llena la ayuda memoria de la reunión 123». Claude pide
la transcripción y el prompt, redacta el JSON según el tipo y lo guarda. La persona revisa en
el editor como hoy. El portal no llama a ninguna IA ni necesita llave propia.

### Fallos que se nombran

- Sin transcripción: «La reunión no tiene transcripción. Debe iniciarse en Teams durante la
  reunión.»
- Aún no disponible: Teams la publica minutos después de terminar; el mensaje lo dice.
- La pide alguien que no es el organizador: se usa igual la conexión del organizador, y si
  está vencida se le nombra, como en la fase 1.

### Límites conocidos

- Presencial con un solo micrófono: Teams atribuye casi todo a la sala; el «responsable» de los
  acuerdos saldrá débil.
- Los resúmenes inteligentes de Teams (Teams Premium / Copilot) no son esto: aquí se usa la
  transcripción, que va con la licencia normal.
- La transcripción sale del tenant hacia la IA del usuario, igual que hoy al pegarla a mano.

### Pruebas

- (Hecho en la parte A, en `MemoriaDesdeTranscripcionTests` y `LlenarMemoriaReunionCommandTests`.) Las reglas con los casos que cubría el JavaScript: Generales intactos,
  capacitación solo si el tipo lo es, validador de institución solo con encuesta, claves
  desconocidas descartadas y reportadas.
- `ITranscripcionesTeams` falso para las herramientas MCP: sin transcripción, no disponible,
  conexión vencida.
- Manual: una reunión real grabada con transcripción, llenada desde Claude.

## Punto abierto

Con la regla de invitados elegida, hoy **la mayoría de las reuniones no tienen a quién
invitar**: solo 4 pre-registrados y 6 reuniones con enlace institucional llevan correo. Al
principio, la mayoría de los envíos quedarán solo en el calendario del organizador. No cambia
el diseño, pero si se quiere que los invitados reciban la cita, alguien tiene que capturar
pre-registros con correo. Si más adelante hace falta una lista de invitados propia, es un
cambio aditivo sobre `EventoOutlookBuilder`.
