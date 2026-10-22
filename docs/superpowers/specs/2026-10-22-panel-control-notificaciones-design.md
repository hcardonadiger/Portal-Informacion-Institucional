# Panel de control — Notificaciones — diseño

**Fecha:** 2026-10-22 · **Rama:** `ike_opt` · **Estado:** aprobado en conversación, pendiente de revisión escrita

## Qué se quiere

Un módulo nuevo, **Panel de control**, desde el que se administra el portal sin editar
`appsettings.json` ni reiniciar. Su primer submódulo es **Notificaciones**, donde se decide:

1. Cada cuánto revisa el sistema plazos y genera los avisos automáticos.
2. Qué módulos generan avisos, por canal: campana y correo, cada uno con su interruptor.
3. Qué avisos parecidos se juntan en uno solo («Tiene 6 compromisos vencidos»).
4. Desde qué cuenta y con qué contraseña sale el correo de las notificaciones.
5. Bajo qué reglas de horario, fecha, periodicidad o acumulación sale el correo (**entrega 2**).

## Decisiones tomadas

| Pregunta | Decisión |
|---|---|
| Dónde se guarda la configuración | **En la base** (enfoque A). Descartados: que el panel escriba `appsettings.json` (contraseña en claro, IIS no deja escribir la carpeta) y dejar el correo fuera del panel (no cumple el pedido) |
| «Tiempo de reinicio» | **Cada cuánto revisa** el sistema. Horas + minutos (p. ej. 0 h 15 min). Mínimo **5 min**, máximo **24 h** |
| Qué significa apagar un módulo | **Dos interruptores por módulo: Campana y Correo**, independientes |
| Correo de avisos automáticos | **Sí**: con Correo encendido, los avisos automáticos del módulo también salen por correo (hoy no lo hacen). El **chat de soporte nunca** va por correo |
| Agrupación | Por **persona + tipo**: lo nuevo se suma al aviso **sin leer** del mismo tipo. Interruptor **Agrupar** por módulo, encendido por defecto |
| Correo agrupado | Pasa por una **bandeja de salida**; un despachador manda **un correo por persona por revisión** con lo acumulado |
| Correo de avisos que ocurren al momento (ticket asignado, cambio de estado) | **También por la bandeja**: sale en la siguiente revisión, no al instante. Sin eso, las reglas de horario de la entrega 2 no podrían detenerlos. Con revisión cada 15 min, la demora es como mucho 15 min |
| Reglas de correo | Pestaña propia, **entrega 2**: horario, fechas, periodicidad y acumulación |
| Recordatorios manuales | Respetan el interruptor de Correo del módulo, **saltan las reglas** y salen al instante |
| Estado inicial del correo | **Todo apagado** el día del despliegue, incluidos los módulos cuyos recordatorios manuales hoy sí mandan correo |
| Entregas | **Dos**: 1) panel + agrupación + bandeja funcionando sin reglas; 2) reglas |

## Contexto verificado

- **Nada de esto tiene pantalla hoy.** La cadencia sale de `Notificaciones:IntervaloHoras`
  (4) y `DelayInicialMinutos` (2) en `appsettings.json`, vía `NotificacionesOptions`
  (`src/Application/Notificaciones/`), y se lee **una vez al arrancar**
  (`RecordatorioBackgroundService.EjecutarBucleAsync`).
- La anticipación de «próximo» (**3 días**) y los topes por corrida (50 reuniones, 60
  compromisos, 60 etapas) están fijos en `RecordatorioBackgroundService.cs`. Esta entrega no
  los mueve al panel.
- Hay **13 tipos** en `TipoNotificacion` (`src/Domain/Enums/Enums.cs:153`). Los generan
  cuatro sitios: `RecordatorioBackgroundService`, `AsignarTicketCommand`,
  `ProyectoEstadoCambiadoEventHandler`, `SoporteHub.NotificarTecnicoAsync`. Los recordatorios
  manuales (`EnviarRecordatorioManualCommands`) son los **únicos que hoy mandan correo**.
  `TicketResuelto` está definido y nadie lo emite.
- El revisor evita repetidos con «mismo destinatario + tipo + **URL** hoy». La agrupación
  rompe esa clave (un aviso agrupado tiene una sola URL para muchos elementos), así que el
  control de repetidos pasa a los elementos.
- La campana se pinta en el servidor en cada página (`_Layout.cshtml:568`), sin SignalR.
  Sumar a un aviso existente no necesita avisar al navegador.
- `SmtpEmailService` toma `IOptions<SmtpSettings>` (sección `Smtp`) **al construirse**. En
  `appsettings.json` el host es `smtp.gmail.com` y `Smtp:Password` tiene un valor real de 16
  caracteres, **versionado desde `0462b14`**. Ver «Fuera de alcance».
- Data Protection guarda sus claves en disco (`Program.cs:117`, `PersistKeysToFileSystem`), así
  que un secreto cifrado sigue siendo legible tras reiniciar, siempre que esa carpeta se
  conserve en el servidor.
- `PermisoAuditoria` es la bitácora de la **matriz de permisos** (rol, clave, otorgado o
  revocado) y no admite otros eventos. El panel lleva **bitácora propia** con el mismo patrón
  de solo agregar.
- Permisos: `[Permission(modulo, accion)]` con el vocabulario fijo `Ver/Crear/Editar/Eliminar`.
  Si hace falta más detalle se usa un módulo más específico, no un verbo nuevo (CLAUDE.md).

## Entrega 1

### Pantalla y permisos

- Menú: grupo nuevo **«Panel de control»** junto a Administración, visible solo con
  `PanelControl.Notificaciones.Ver`. Por ahora tiene una sola opción, **Notificaciones**.
- `/PanelControl`: portada con una tarjeta por submódulo. Deja sitio para los siguientes
  (tamaño de archivos, etc.).
- `/PanelControl/Notificaciones`, con pestañas: **General** (bloques 1–3) y **Reglas de
  correo** (entrega 2; en la entrega 1 la pestaña no se muestra).
- Permisos: `PanelControl.Notificaciones` → **Ver** (consultar) y **Editar** (guardar y
  enviar el correo de prueba). Los roles con `EsAdministrador` entran por código. Con Ver sin
  Editar, la pantalla se ve con los controles deshabilitados y el POST responde 403.

**Bloque 1 — Revisión automática.** Campos **Horas** (0–24) y **Minutos** (0–59). Se
rechaza menos de 5 min o más de 24 h, primero en el navegador (`prevalidacion.js`) y luego en
el validador. Muestra la última revisión y la próxima estimada.

**Bloque 2 — Módulos.** Una fila por módulo con tres interruptores, **Campana**, **Correo** y
**Agrupar**, y debajo los avisos que incluye:

| Módulo | Tipos |
|---|---|
| Tickets | `TicketAsignado`, `TicketResuelto`, `RecordatorioManualTicket` |
| Reuniones | `ReuniónMañana`, `CompromisoVencido`, `CompromisoProximo`, `RecordatorioManualReunion`, `RecordatorioManualCompromiso` |
| Expedientes | `EtapaCronogramaVencida`, `EtapaCronogramaProxima`, `RecordatorioManualExpediente` |
| Proyectos | `ProyectoCambioEstado` |
| Soporte | `ChatRecibido` (Correo deshabilitado y siempre apagado) |

**Bloque 3 — Correo saliente.** Servidor, puerto, SSL, usuario, **contraseña**, correo
remitente y nombre del remitente.
- La contraseña **nunca se devuelve** al navegador: el campo llega vacío con la leyenda «hay
  una contraseña guardada». Vacío al guardar = conservar la actual. Una casilla «Quitar la
  contraseña» la borra.
- **«Enviar correo de prueba»**: manda un correo a `Usuario.Correo` de quien lo pulsa, con lo
  ya guardado, y muestra el resultado concreto (enviado / autenticación rechazada / tiempo
  agotado / servidor no responde).
- Si la contraseña guardada no se puede descifrar, el bloque lo dice en rojo («la contraseña
  guardada no se puede leer en este servidor; vuelva a escribirla»).

### Datos

**`AjustesNotificaciones`** (una sola fila, `Id = 1`)
- `IntervaloMinutos` (int)
- `SmtpConfigurado` (bit): false hasta el primer guardado del bloque 3. Mientras sea false se
  usa la sección `Smtp` de `appsettings`.
- `SmtpHost`, `SmtpPuerto`, `SmtpSsl`, `SmtpUsuario`, `SmtpRemitente`, `SmtpNombreRemitente`
- `SmtpPasswordCifrada` (nvarchar(max), null). Se cifra con `IDataProtector`, propósito
  `PanelControl.Notificaciones.Smtp`.
- `UltimaRevisionUtc` (datetime2, null)
- Auditable (`UpdatedAt`, `UpdatedBy`)

**`ModulosNotificacion`**: `Modulo` (PK, nvarchar(30)), `CampanaActiva`, `CorreoActivo`,
`Agrupar`.

**`NotificacionElementos`**: `Id`, `NotificacionId` (FK, cascada), `Clave` (nvarchar(400): la
URL del elemento, que es lo que hoy identifica a cada uno), `Titulo`, `FechaUtc`. Índice
`(NotificacionId)`.

**`Notificacion`** gana `Cantidad` (int, default 1) y `FechaActualizacion` (datetime2). La
campana ordena por `FechaActualizacion`.

**`CorreosPendientes`** (bandeja de salida): `Id`, `DestinatarioId`, `Modulo`, `Tipo`,
`Titulo`, `Url`, `CreadoUtc`, `Estado` (Pendiente / Enviado / Fallido), `Intentos`,
`UltimoError`, `EnviadoUtc`. Índice `(Estado, DestinatarioId)`.

**`PanelControlBitacora`** (solo agregar): `Id`, `Seccion`, `Detalle`, `Actor`, `Fecha`.
Ejemplo: «intervalo: 4 h → 15 min», «Reuniones: correo encendido», «contraseña cambiada». **El
valor de la contraseña nunca se escribe.**

**Valores iniciales** (migración y script):
- `IntervaloMinutos` = **240**, las 4 horas de `appsettings.json`. Ni el script ni la migración
  pueden leer la configuración del servidor: si un despliegue tenía otro
  `Notificaciones__IntervaloHoras`, se ajusta desde el panel, y así lo dicen las notas de
  despliegue. A partir de ahí `Notificaciones:IntervaloHoras` deja de usarse.
- Los cinco módulos con Campana **encendida**, Correo **apagado** y Agrupar **encendido**.
- `SmtpConfigurado = 0`.

**Despliegue:** migración EF en `Persistence\Migrations` más
`database/2026-10-22_panel_control_notificaciones.sql`, con la forma de
`2026-09-24_poner_al_dia.sql`: idempotente contra el catálogo, `THROW` si faltan las tablas
base, fila de historial solo para la migración real y un `SELECT` final de controles
«presente».

### Comportamiento

**Catálogo `ModulosNotificacion` (código, Application).** Asocia cada `TipoNotificacion` a su
módulo, declara si admite correo (Soporte no lo admite) y si es **diario**: lo son los cinco
que genera el revisor (`ReuniónMañana`, `CompromisoVencido`, `CompromisoProximo`,
`EtapaCronogramaVencida`, `EtapaCronogramaProxima`), que son los que hoy se avisan una vez
por día. Tiene un plural por tipo para el título agrupado. Una prueba recorre `Enum.GetValues<TipoNotificacion>()` y falla si un tipo
no tiene módulo.

**`IAjustesNotificacionesProvider` (Application; impl. Infrastructure).** Devuelve los
ajustes vigentes: intervalo, módulos y SMTP (con la contraseña ya descifrada, solo hacia el
servicio de correo). Usa `IMemoryCache` por 1 minuto y se invalida al guardar. Sin fila en la
base, construye los ajustes desde `appsettings`.

**`INotificacionService`: la única puerta.** `Encolar`/`CrearYGuardarAsync` mantienen su
firma y suman la clave del elemento (por defecto, la URL). Por cada aviso:
1. Toma el módulo del tipo. Campana apagada → no crea aviso.
2. **Solo para los tipos diarios:** si el elemento ya se avisó hoy a esa persona (existe en
   `NotificacionElementos` con `FechaUtc` ≥ inicio del día local), no hace nada. Reemplaza al
   `YaEnviada` del revisor. Los demás tipos (ticket asignado, chat, cambio de estado,
   recordatorios) no se descartan: si el mismo elemento ya está en el grupo sin leer, solo se
   actualiza `FechaActualizacion` y el aviso sube, sin sumar a `Cantidad`. Así el segundo
   mensaje del mismo chat no se pierde ni se cuenta dos veces.
3. Si Agrupar está encendido y hay un aviso **sin leer** del mismo tipo para esa persona, le
   suma el elemento: `Cantidad++`, título plural, URL `/Notificaciones/{id}` y
   `FechaActualizacion` = ahora. Si no, crea un aviso con un elemento, título y URL de hoy.
4. Si Correo está encendido (y el tipo admite correo), anota una fila en
   `CorreosPendientes`.

**`RecordatorioBackgroundService`**
- Lee `IntervaloMinutos` del proveedor **en cada vuelta**.
- Espera con un `SenalRevision` (singleton) que el comando de guardado dispara. La espera
  termina por tiempo o por señal, así que un cambio de intervalo aplica al momento.
- Al terminar la vuelta escribe `UltimaRevisionUtc` y llama al despachador.
- Se quita su `YaEnviada` (lo hace el paso 2 de arriba).

**Despachador de correo** (`DespachadorCorreos`, al final de cada vuelta)
- Agrupa los `Pendiente` por destinatario y manda **un correo** con todo, ordenado por tipo,
  usando `PlantillasCorreo` con los datos escapados en el HTML, como ya hacen los correos desde
  `8182303`.
- Si sale: `Enviado`. Si falla: `Intentos++` y `UltimoError`; a los 3 intentos, `Fallido`. El
  panel muestra cuántos fallidos hay.
- Destinatario sin correo o inactivo → `Fallido` con motivo, sin reintentar.
- En la entrega 2, aquí se evalúan las reglas antes de enviar.

**Recordatorios manuales.** Siguen enviando al instante, **sin pasar por la bandeja**, pero
solo si Correo está encendido en su módulo. Si está apagado, el resultado que ya devuelve
`EnviarRecordatorioManualCommands` dice «el aviso llegó a la campana; el correo de este
módulo está apagado en el Panel de control».

**`SmtpEmailService`.** Pide los ajustes SMTP al proveedor **en cada envío** en vez de
`IOptions<SmtpSettings>`. Si la contraseña no se puede descifrar, se comporta como «no
configurado» (como hoy con `EstaConfigurado = false`) y registra un aviso en el log.

**`/Notificaciones/{id}`.** Página de un aviso con la lista de sus elementos, cada uno con su
enlace. Abrirla marca el aviso como leído. Solo la ve el destinatario: si el id es de otra
persona, 404.

### Pruebas

**Application** (EF In-Memory):
- Campana apagada → no se crea aviso. Correo apagado → no se anota en la bandeja.
- Agrupar: dos elementos del mismo tipo → un aviso, `Cantidad = 2`, título plural, URL
  `/Notificaciones/{id}`. Leído el aviso → el siguiente abre uno nuevo. Agrupar apagado → dos
  avisos.
- Un elemento de tipo diario ya avisado hoy no se repite; mañana sí. Dos `ChatRecibido` del
  mismo chat el mismo día → un aviso con `Cantidad = 1` y `FechaActualizacion` movida.
- Despachador: tres pendientes de una persona → un envío, tres `Enviado`. Fallo → `Intentos`
  sube y al tercero queda `Fallido`.
- Recordatorio manual con Correo apagado → no hay envío y el resultado lo dice.
- Catálogo: los 13 tipos tienen módulo. `ChatRecibido` no admite correo.
- Guardar: la contraseña queda cifrada (distinta del texto, legible con el protector). Vacío
  conserva la anterior. «Quitar» la borra. Intervalo < 5 min o > 24 h → rechazo. La bitácora
  registra el cambio sin el valor de la contraseña.
- Sin fila en la base → el proveedor devuelve los valores de `appsettings`.

**Web** (`PortalFactory`, SQLite):
- Sin `PanelControl.Notificaciones.Ver` → 403, y el menú no muestra el grupo.
- Con Ver y sin Editar → GET 200 con controles deshabilitados; POST 403.
- El HTML del panel **nunca** contiene la contraseña guardada.
- `/Notificaciones/{id}` de otra persona → 404.

**Base:** el script corre dos veces contra `GestionGD_new` y la segunda no cambia nada.

## Entrega 2 — Reglas de correo

Pestaña **Reglas de correo**. Cada regla tiene tipo, módulo (o «todos»), parámetros y un
interruptor Activa. El despachador manda lo acumulado de una persona en un módulo **cuando se
cumplen todas las reglas activas que aplican a ese módulo**. Si no se cumplen, sigue en la
bandeja.

| Tipo | Parámetros | Se cumple cuando |
|---|---|---|
| Horario | días de la semana, hora desde y hasta | la hora local está dentro de la franja |
| Fechas | rango de fechas bloqueado | la fecha local está fuera del rango |
| Periodicidad | «cada N horas/minutos» o «a las HH:mm» | pasó N desde el último envío a esa persona y módulo, o es la hora fijada |
| Acumulación | mínimo de elementos, **tiempo máximo de espera** | hay al menos N pendientes, o el más viejo pasó el tiempo máximo |

El tiempo máximo de espera es obligatorio: evita que un correo no salga nunca por no llegar al
mínimo. La pestaña muestra arriba cuántos correos esperan y qué regla los detiene. Los
recordatorios manuales no pasan por reglas. Se especifica en detalle (datos, pantalla y
pruebas) al terminar la entrega 1.

## Fuera de alcance

- **La contraseña de Gmail versionada** (`Smtp:Password` en `appsettings.json` desde
  `0462b14`). Hay que **cambiarla en la cuenta** y pasar la nueva al panel o a
  `Smtp__Password`. Quitarla del archivo no la borra del historial de git.
- `Uploads:ReunionesMaxMb = 5` en `appsettings.json` contradice el 20 del código y de su
  comentario en `Program.cs`. Es un submódulo futuro del panel («Archivos»); el arreglo
  puntual va aparte.
- Mover al panel la anticipación de 3 días y los topes por corrida.
- Preferencias por usuario: el panel decide para todo el portal.
- `TicketResuelto`: se le asigna módulo, pero que alguien lo emita es otro trabajo.

## Riesgos

- **Correo apagado por defecto** quita, desde el despliegue, el correo que hoy mandan los
  recordatorios manuales de Tickets, Reuniones y Expedientes. Va en las notas de despliegue
  con el paso para encenderlo.
- **Claves de Data Protection**: si el servidor pierde su carpeta de claves (reinstalación,
  despliegue que la borra), la contraseña guardada deja de leerse. El panel lo dice y basta con
  volver a escribirla. Las notas de despliegue tienen que pedir que esa carpeta se conserve.
- **Cambio de comportamiento del correo**: con Correo encendido, quien hoy no recibe correos
  automáticos empieza a recibirlos. Con agrupación y bandeja es uno por revisión como máximo,
  pero conviene encenderlo módulo por módulo.
