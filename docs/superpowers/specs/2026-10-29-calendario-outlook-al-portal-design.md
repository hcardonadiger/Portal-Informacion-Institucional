# Calendario de Outlook al portal — diseño

**Fecha:** 2026-10-29 · **Rama:** `ike_opt` · **Estado:** aprobado en conversación, pendiente de revisión escrita

## Qué y por qué

Hoy las reuniones viajan en un solo sentido: el portal crea la cita en el Outlook del organizador
(spec `2026-10-22-reuniones-a-outlook-design.md`). Las reuniones que nacen en Outlook —la
mayoría— no existen en el portal, así que no se les puede llenar ayuda memoria, acuerdos ni
asistencia, ni bajar su transcripción de Teams.

Esto agrega el sentido contrario: **el calendario de Outlook de cada usuario conectado entra al
portal y se mantiene al día**. Outlook manda sobre los Generales; todo lo demás es del portal.

La doctrina queda así: **Portal → Outlook** para las reuniones creadas en el portal (sin cambios);
**Outlook → Portal, solo Generales**, para el resto del calendario.

## Decisiones (tomadas con el usuario)

| Tema | Decisión |
| --- | --- |
| Modo | Sincronización continua, sin intervención |
| Qué citas | **Todas** las del calendario principal, incluidas personales y de todo el día |
| Visibilidad | Entran **Privadas** (solo quien las importó). El usuario puede volverlas Públicas |
| Quién manda | **Outlook** sobre Generales. El portal no devuelve cambios a Outlook para estas |
| Borrado o cancelación en Outlook | Se **marca "Cancelada en Outlook"**; no se borra nada |
| A quién | **Todo usuario con Outlook conectado**, sin casilla aparte |
| Rango | **De hoy a +90 días** (Graph exige un rango cerrado) |
| Mecanismo | **Revisión periódica cada 15 minutos** con `calendarView` |

Descartados: consulta delta (el rango queda fijo en el token y hay que reiniciarlo; más piezas
para el mismo resultado a esta escala) y webhooks (exigen HTTPS público alcanzable por Microsoft;
el portal vive en la red institucional). Si el retraso de 15 minutos llegara a molestar, delta
reemplaza al lector sin tocar las reglas.

## Qué entra y cómo se traduce

Por cada usuario conectado, cada ciclo: `GET me/calendarView?startDateTime=<hoy 00:00>&endDateTime=<hoy+90 23:59>`,
con `Prefer: outlook.timezone="Central America Standard Time"`, paginando `@odata.nextLink`.
Graph expande las recurrencias: cada ocurrencia es una cita y una reunión.

| Outlook | Portal |
| --- | --- |
| `subject` (vacío → "(Sin asunto)") | `Titulo` |
| `start` / `end` | `Fecha`, `Hora` (`HH:mm`), `Duracion` |
| `isAllDay` | `Fecha` sin `Hora` |
| `location.displayName` | `Lugar` |
| `onlineMeeting.joinUrl` presente | `Modalidad` = "Virtual", `OutlookEnlaceTeams` |
| sin enlace de Teams | `Modalidad` = "Presencial" |
| `isOrganizer` = true | `OutlookOrganizadorId` = el usuario (habilita la transcripción) |
| `id` | `OrigenExternoId` = `outlook:<id>` (idempotencia, índice único filtrado ya existente) |
| `iCalUId` | `OutlookICalUId` |
| `lastModifiedDateTime` | `OutlookModificadaEl` |
| — | `Visibilidad` = Privada, `CreadoPorId` = `OutlookImportadaPorId` = el usuario |
| — | `InstitucionDuenaId`, `AreaId`, `UnidadId` = primera asignación activa del usuario |

El cuerpo de la cita **no** se copia: la memoria se llena en el portal.

**Se salta** toda cita cuyo `iCalUId` ya pertenece a una reunión creada en el portal (las que el
portal envió a Outlook, y las copias de esas en el calendario de otros invitados). Para eso
`GraphOutlookCalendario` empieza a devolver y guardar el `iCalUId` al crear la cita. Las
reuniones enviadas antes de este cambio no tienen `iCalUId`: se reconocen además por
`OutlookEventId` igual al `id` de la cita en el calendario de su organizador.

Cada usuario importa su propia copia: si dos usuarios del portal asisten a la misma reunión de
Outlook, cada uno tiene la suya, privada. No se fusionan.

## Cambios, cancelaciones y lo que nunca se toca

- **Cita cambiada** (`lastModifiedDateTime` distinto): se recalculan los Generales de la tabla y
  se guardan **solo si alguno cambió**, para no llenar la auditoría cada 15 minutos.
- **Nunca se toca**: memoria, objetivo, desarrollo, acuerdos, asistencia, adjuntos, encuesta,
  hilo, visibilidad, tipo, institución contraparte.
- **Editor**: en una reunión importada, título, fecha, hora, duración y lugar son de solo
  lectura con la nota "Viene de Outlook: cámbielo allá". El handler del editor también los
  ignora para estas reuniones (no basta con deshabilitar el campo).
- **Cancelada**: `isCancelled`, o la cita desaparece de la ventana sin que su fecha haya pasado
  → se consulta `me/events/{id}`. 404 → `CanceladaEnOutlookEl = ahora`; aviso en lista y editor.
  Si existe con fecha más allá de la ventana → se actualiza su fecha.
- **Reaparece** una cita cancelada → se limpia `CanceladaEnOutlookEl`.
- **Fecha pasada**: la cita sale de la ventana y la reunión se **congela**; no es cancelación.
- **Outlook desconectado**: se detiene la sincronización de ese usuario; lo importado se queda.
  Al reconectar se retoma sin duplicar.
- El usuario puede borrar una reunión importada como cualquier otra. Si la cita sigue en Outlook,
  **no** se vuelve a crear: el borrado lógico mantiene su `OrigenExternoId` y la búsqueda de
  existentes usa `IgnoreQueryFilters()`.

## Componentes

- **Domain** — `Reunion`: columnas `OutlookICalUId`, `OutlookImportadaPorId`,
  `OutlookModificadaEl`, `CanceladaEnOutlookEl`; propiedad `EsImportadaDeOutlook`; métodos
  `ImportarDeOutlook`, `ActualizarDeOutlook` (devuelve si cambió algo),
  `MarcarCanceladaEnOutlook`, `QuitarCancelacionOutlook`.
- **Application** — `ICalendarioOutlookLectura` (`ListarAsync(usuarioId, desde, hasta)`,
  `ExisteAsync(usuarioId, eventId)` → cita o null); `CitaOutlook` (record) y
  `CitaOutlookTraductor` (puro); `SincronizarCalendarioOutlookCommand(usuarioId)` → resumen
  (creadas, actualizadas, canceladas, saltadas). `ResultadoOutlook` agrega `ICalUId`.
- **Infrastructure** — `GraphCalendarioOutlookLectura` (usa `IConexionMicrosoft.ObtenerTokenAsync`,
  el token de calendario; `Calendars.ReadWrite` ya concedido cubre la lectura: **nada que pedir a TI**).
- **Web** — `SincronizacionCalendarioOutlookService` (`BackgroundService`, cada 15 min; un scope
  por usuario; no hace nada si la integración no está configurada); editor y listas.
- **Base** — migración EF `CalendarioOutlookAlPortal` + `database/2026-10-29_poner_al_dia.sql`
  idempotente contra el catálogo, con la fila de historial al final.

## Fallos

- Un usuario que falla no detiene a los demás.
- Conexión vencida → se salta (el aviso de reconectar ya existe en el perfil).
- 429, 5xx o tiempo agotado → se registra y se reintenta en el próximo ciclo.
- Logs: id de usuario y conteos. Nunca tokens ni contenido de citas (asunto, lugar, invitados).

## Pruebas

- `CitaOutlookTraductor`: zona horaria, todo el día, Teams sí/no, asunto vacío, organizador sí/no.
- `SincronizarCalendarioOutlookCommand` (puerto simulado + EF In-Memory): crea; no duplica; actualiza
  solo si cambió; nunca toca memoria ni visibilidad; salta las del portal por `iCalUId` y por
  `OutlookEventId`; cancela con 404; mueve fuera de ventana sin cancelar; congela pasadas;
  reaparecida quita cancelación; borrada por el usuario no se recrea.
- `GraphCalendarioOutlookLectura` (`HttpMessageHandler`): rango y cabecera de zona horaria,
  paginación, 404 en `ExisteAsync`.
- Dominio: los cuatro métodos.
- Web: el editor no deja cambiar Generales de una reunión importada.

## Fuera de alcance

Calendarios compartidos o secundarios; copiar el cuerpo o los invitados; devolver a Outlook los
cambios hechos en el portal a una reunión importada; sincronizar más allá de +90 días.
