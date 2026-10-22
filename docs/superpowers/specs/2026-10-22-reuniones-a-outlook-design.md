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
                 └─ IOutlookCalendario, IConexionMicrosoftStore   (interfaces)
Infrastructure
  ├─ GraphOutlookCalendario   : IOutlookCalendario   (Microsoft.Graph + MSAL)
  ├─ ConexionMicrosoftStore   : IConexionMicrosoftStore (caché MSAL cifrada en SQL)
  └─ AppDbContext             (tabla ConexionesMicrosoft, columnas nuevas en Reuniones)
```

Ninguna otra parte del portal conoce Graph. Los handlers solo ven `IOutlookCalendario`.

### `IOutlookCalendario`

```csharp
Task<ResultadoOutlook> CrearAsync(Guid organizadorId, EventoOutlook evento, Guid transactionId, CancellationToken ct);
Task<ResultadoOutlook> ActualizarAsync(Guid organizadorId, string eventId, EventoOutlook evento, CancellationToken ct);
Task CancelarAsync(Guid organizadorId, string eventId, string comentario, CancellationToken ct);
```

`ResultadoOutlook` = `{ EventId, EnlaceTeams }`. Los fallos se expresan con dos excepciones
propias: `OutlookConexionVencidaException` (MSAL pide interacción: token revocado o expirado) y
`OutlookNoDisponibleException` (red, 5xx, throttling). Nada de tipos de Graph cruza la interfaz.

### `EventoOutlook`

Registro inmutable: `Asunto`, `Inicio`, `Fin` (en `Central America Standard Time`, la zona de
Windows para America/Tegucigalpa), `Ubicacion` (`Lugar`), `Cuerpo` (`ObjetivoAgenda` + enlace a
la reunión en el portal), `Invitados` (lista de `{ Correo, Nombre }`), `EsTeams`.

`Huella()` = SHA-256 de la representación canónica del evento (invitados ordenados y en
minúsculas). Es lo que decide si un guardado justifica llamar a Graph.

### `EventoOutlookBuilder`

`Reunion` → `EventoOutlook`, o un motivo de rechazo legible:

- Sin `Fecha` o sin `Hora` válida → rechazo «La reunión necesita fecha y hora para ir a Outlook».
- `Hora` debe ser `HH:mm` (0–23 / 0–59). Otra forma → rechazo con el valor encontrado.
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
| `UsuarioId` | uniqueidentifier, PK, FK `Usuarios` | una conexión por usuario |
| `Upn` | nvarchar(256) | cuenta Microsoft conectada, para mostrarla |
| `CacheTokens` | varbinary(max) | caché MSAL **cifrada con Data Protection** (propósito propio) |
| `ConectadaEl` | datetime2 | |
| `RequiereReconexion` | bit | se enciende con `OutlookConexionVencidaException` |
| `UltimoError` | nvarchar(500) NULL | |

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
| `OutlookHuella` | char(64) NULL |
| `OutlookSincronizadaEl` | datetime2 NULL |
| `OutlookPendiente` | bit NOT NULL DEFAULT 0 |
| `OutlookError` | nvarchar(500) NULL |

Entrega: migración EF en `Persistence\Migrations` **y** `database/<fecha>_poner_al_dia.sql`
idempotente contra el catálogo, con su comprobación `presente`/`FALTA` (convención del repo).

## Flujos

### Conectar / desconectar (perfil)

- `Cuenta/Perfil` gana una tarjeta «Microsoft Outlook»: sin conexión → «Conectar mi Outlook»;
  con conexión → cuenta conectada, fecha y «Desconectar»; vencida → aviso y «Reconectar».
- «Conectar» redirige a Microsoft (authorization code + PKCE) con los alcances
  `Calendars.ReadWrite User.Read offline_access`, y `state` atado a la sesión.
- `/Cuenta/Microsoft/Callback` canjea el código, guarda la caché cifrada y vuelve al perfil.
  Lleva `[PermisoNoRequerido]` (autoservicio) y `[Authorize]`, como las demás de `/Cuenta`
  que exigen sesión.
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
el usuario ve el motivo y puede reintentar.

### Actualizar al guardar

`ActualizarReunionCommand` **no cambia**: guardar nunca falla por Outlook. Tras un guardado
exitoso, si la reunión tiene `OutlookEventId`, la página llama a
`SincronizarReunionOutlookCommand(ReunionId)`:

1. Reconstruye el evento. Si el builder lo rechaza (p. ej. borraron la hora), marca pendiente
   con ese motivo.
2. Si `Huella()` == `OutlookHuella` → no hace nada (evita mandar «reunión actualizada» a todos
   los invitados en cada guardado).
3. Si difiere → `ActualizarAsync` con la conexión de **`OutlookOrganizadorId`** (no la de quien
   edita). Guarda huella, enlace y fecha; limpia pendiente.
4. Fallo → `OutlookPendiente = 1`, `OutlookError` con el motivo. El editor y la ficha muestran
   «No se pudo actualizar en Outlook · Reintentar»; con conexión vencida el texto nombra al
   organizador: «La conexión de Outlook de *Nombre* venció; debe reconectarse desde su perfil».

«Reintentar» es el mismo comando.

### Cancelar al borrar

Antes de `EliminarReunionCommand`, si hay `OutlookEventId`, la página llama a
`CancelarReunionOutlookCommand`, que invoca `CancelarAsync` con la conexión del organizador
(Graph avisa a los invitados). **Si falla, la reunión se borra igual** y el usuario ve:
«La reunión se eliminó, pero no se pudo cancelar en Outlook: cancélela a mano.» El portal no
bloquea un borrado por Microsoft.

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
3. URIs de redirección: `https://localhost:49175/Cuenta/Microsoft/Callback` (desarrollo) y la
   de producción.
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

### Qué existe hoy

El editor (paso 3, Memoria) ya tiene «Llenar automáticamente desde una transcripción»: arma un
prompt que **cambia según el tipo** (reunión normal: objetivo, desarrollo, acuerdos;
capacitación: además convocados, asistentes, % y compromisos de seguimiento; ambos: cierre),
el usuario lo pasa a su IA con la transcripción y pega el JSON que vuelve. Las reglas de
«Aplicar» —no tocar Generales, el bloque de capacitación solo si el tipo lo es, la segunda
firma solo con encuesta— viven hoy en el JavaScript de `Reuniones/Editor.cshtml`.

### Qué se agrega

1. **Permiso nuevo en la misma conexión:** `OnlineMeetingTranscript.Read.All` (delegado; pide
   consentimiento de administrador). «Conectar mi Outlook» lo pide junto con los de la fase 1;
   quien ya se conectó en fase 1 reconecta una vez.
2. **`ITranscripcionesTeams`** (Application) / implementación Graph (Infrastructure):
   localiza la reunión en línea por su enlace (`/me/onlineMeetings?$filter=JoinWebUrl eq …`),
   lista sus transcripciones y descarga la más reciente en texto. Solo funciona con la conexión
   del **organizador** (`OutlookOrganizadorId`).
3. **Las reglas de «Aplicar» pasan al servidor:** un `AplicarMemoriaReunionCommand` que recibe
   el JSON y lo aplica con las mismas reglas. El botón «Aplicar» del editor pasa a llamarlo, y
   el MCP también. Una sola copia de las reglas; dejarlas en dos sitios es esperar a que diverjan.
4. **Dos herramientas MCP nuevas** en `src/Web/Mcp`:
   - `obtener_transcripcion_reunion(id)` — devuelve el tipo de la reunión, el prompt que le
     corresponde y la transcripción. Permiso `Reuniones.Ver`.
   - `llenar_memoria_reunion(id, json)` — aplica el JSON con `AplicarMemoriaReunionCommand`.
     Permiso `Reuniones.Editar`. Devuelve qué campos llenó y cuáles descartó.

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

- `AplicarMemoriaReunionCommand` con los casos que hoy cubre el JavaScript: Generales intactos,
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
