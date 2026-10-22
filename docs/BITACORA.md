# Bitácora — Gestión Digital (DIGER)

Registro de trabajo del proyecto. Es la fuente para los informes a jefaturas:
se escribe en lenguaje claro y **sin datos sensibles** (ni contraseñas, ni cadenas
de conexión, ni datos personales de ciudadanos).

---

## Registro por fecha

### 2026-10-22 (noche, 3) — Entregas 1 y 2 integradas en `ike_opt`
Pedido del Analista. Entrega 2 aceptada.
- `Jamil-GestionGD` se subió a origin. `ike_opt` traía 70 commits nuevos de origin (entre ellos la
  rama Jamil y la rama DashBoards); se puso al día por avance rápido.
- Merge `7a6ecb8` (sin rebase ni force), subido a origin. Hubo un solo conflicto, mecánico, en
  `Infrastructure/DependencyInjection.cs`: `ike_opt` registró `SolOptions` y nosotros
  `CorreoOptions` en el mismo lugar. Quedaron las dos. `src/Web/Mcp` no cambió con el merge.
- En `ike_opt`: compila con las 3 advertencias de siempre y pasan 1380 pruebas (1 omitida).
- `ike_opt` se trajo de vuelta a `Jamil-GestionGD`; las dos ramas quedan con el mismo contenido.
- Revisión en el navegador con la base `GestionGD_DashBoards` (la eligió Jamil; `GestionGD_new`
  ya no existe en la instancia). El portal corrió solo en la sesión, sin tocar appsettings y con
  el correo apagado. Resultado en Riesgos y bloqueos.

### 2026-10-22 (noche, 2) — Tanda 1, entrega 2: notificaciones y correo
Pedido del Analista. Rama `Jamil-GestionGD`. Cada defecto tiene una prueba que fallaba antes. Sin
migraciones, sin tocar `src/Web/Mcp` ni el servidor, el usuario o la contraseña del correo.
- **Hora de Honduras:** los recordatorios automáticos (reunión de mañana, compromisos vencidos y
  próximos), su deduplicación diaria y el cronograma de la ficha del proyecto usaban la fecha UTC.
  Desde las 6:00 p. m. daban por vencido lo que vence ese día. Ahora usan
  `Notificaciones:ZonaHoraria` (`America/Tegucigalpa`; UTC-6 fijo con advertencia si el servidor
  no la reconoce). Commit `044e5b7`. Las etapas de cronograma de Expedientes van aparte, en el
  commit `4209a94`.
- **Enlaces absolutos y nombre de la institución:** los enlaces de los recordatorios se arman con
  `Correo:UrlBase`. Los seis asuntos y la firma de la recuperación toman el nombre de la sección
  `Institucion`. Commit `7f0e155`.
- **Tiempo máximo de envío:** 15 s, configurable con `Smtp:TimeoutSegundos`. Commit `bed99dd`.
- **La pantalla dice si el correo no salió:** el correo y los recordatorios devuelven lo que pasó
  (commit `d1cd68c`). Las cuatro pantallas de recordatorio muestran una advertencia si la campana
  llegó y el correo no (commit `b56f26b`). Con el correo bien, cada una conserva su texto. Los
  textos los propuso y revisó el agente de interfaz `diger-ui`. Veredicto: aprobado con cambios
  menores, ya aplicados.
- **Advertencia al arrancar** si falta `Correo:UrlBase` o el servidor de correo. Commit `d43edac`.
- **Ayuda:** la pregunta frecuente de notificaciones explica los avisos automáticos con la hora de
  Honduras y qué pasa si el correo no sale.
- Notas de despliegue con los pasos de IIS (siempre activo, precarga y comprobar Application
  Initialization): `docs/despliegue/2026-10-22_tanda1_entrega2_notificaciones.md`.
- Resultado: compila sin advertencias nuevas y pasan 1035 pruebas (1 omitida). Antes eran 989.
- Pruebas existentes modificadas, solo por la firma o el tipo de retorno nuevos:
  `EnviarRecordatorioTests` (la de reunión ahora mira los correos intentados, porque el asistente no
  tiene usuario), `CorreosEscapadosTests`, `PasswordRecoveryTests` y `RecuperacionUrlBaseTests`.

### 2026-10-22 (noche) — Tanda 1, entrega 1: seguridad (S1 a S5)
Pedido del Analista. Rama `Jamil-GestionGD`. Cada falla tiene una prueba que fallaba antes de la
corrección. Sin migraciones, sin cambios de interfaz, sin tocar `src/Web/Mcp` ni la sección `Smtp`.
- **S1, correo en el registro:** el registro ya no guarda el cuerpo, los enlaces, los tokens ni
  la dirección de destino de un correo. Commit `e24c828`.
- **S2, datos sin escapar en los correos:** se escapan los nombres, títulos y mensajes en los
  recordatorios de ticket, reunión y compromiso, y en la recuperación de contraseña (commit
  `8182303`). El recordatorio de Expediente va aparte (commit `ea2538d`).
- **S3, riesgos de otro proyecto:** editar, cambiar de estado y eliminar comprueban el proyecto
  y el alcance. La herramienta MCP queda protegida por el comando. Commit `fc9aa20`.
- **S4, interesados de otro proyecto:** editar y quitar comprueban el proyecto y el alcance.
  Commit `cffa71c`.
- **S5, enlace de recuperación:** sale solo de `Correo:UrlBase`. Si falta, no se envía. El QR de
  asistencia usa esa dirección si existe. Commit `65daa70`.
- Notas de despliegue: `docs/despliegue/2026-10-22_tanda1_entrega1_seguridad.md`. Antes de
  publicar hay que configurar `Correo__UrlBase` en IIS.
- Resultado: compila sin advertencias nuevas y pasan 989 pruebas (1 omitida). Antes eran 950, así
  que hay 39 nuevas.
- Pruebas existentes modificadas, solo por la firma nueva de los comandos: `EnviarRecordatorioTests`
  y `PasswordRecoveryTests`. `PortalFactory` acepta configuración y servicios de prueba por
  instancia.

### 2026-10-22 (tarde) — Traspaso desde GestionDigital e investigación de la tanda 1
- **El mensaje de traspaso llegó a Ingeniería por error.** Iba dirigido al Analista de GestionGD.
  El plan de la tanda 1 lo define el Analista; Ingeniería solo aportó la investigación. No se
  modificó código, datos ni configuración por ese mensaje.
- Jamil decidió que los cambios de su lista se hacen en GestionGD, en la rama `Jamil-GestionGD`
  sacada de `ike_opt` (`d3bbd38`), no de `dev`. La rama `Jamil` no se toca: solo se subió a
  `origin` como respaldo, porque era un avance rápido. GestionDigital queda congelado.
- Investigación de solo lectura. Confirmadas en GestionGD, y también en `dev`, que tiene los mismos
  archivos:
  - el registro guarda el cuerpo de los correos, con el token de recuperación;
  - los correos llevan datos de las personas sin escapar, incluido el recordatorio de Expediente;
  - se pueden editar, cambiar de estado o eliminar riesgos de otro proyecto;
  - se pueden editar y quitar interesados de otro proyecto;
  - el enlace de recuperación se arma con la dirección de la petición;
  - la contraseña del correo saliente está versionada en `appsettings.json`.
- Hallazgos entregados al Analista (ver Pendiente).
- Línea base de `Jamil-GestionGD`: compila con 3 advertencias que ya existían y pasan 950 pruebas
  (1 omitida).

### 2026-10-22
- Revisión inicial del repositorio para retomar el proyecto: rama `DashBoards`,
  documentación existente, agentes de apoyo y convenciones del código.
- Línea base verificada: la solución compila y la suite completa pasa
  (997 pruebas correctas, 1 omitida que ya estaba omitida, 0 con error).
- Se crea esta bitácora.

### 2026-10-19 (rama `DashBoards`, no incluida en `Jamil-GestionGD`)
- Tablero ejecutivo de proyectos para jefaturas (`/Tableros/Ejecutivo`): indicadores
  de cartera, semáforo de salud por proyecto con etiqueta de texto, asuntos que
  requieren decisión. El Inicio de `/Tableros` ordena y marca los demás tableros
  (rediseño pendiente, repetido, propuesto para retirar). Commit `382ae85`.
- Corrección: el avatar de la cabecera no se veía en tema oscuro. Commit `6285619`.
- Corrección: el panel del usuario se transparentaba en tema oscuro. Commit `39629b3`.

---

## Hecho
- Tanda 1, entrega 1 (seguridad): S1 a S5 (2026-10-22, rama `Jamil-GestionGD`).
- Tanda 1, entrega 2 (notificaciones y correo) (2026-10-22, rama `Jamil-GestionGD`).
- Tablero ejecutivo de proyectos (2026-10-19, rama `DashBoards`). Sin cambios de base de datos.

## En curso
- (nada abierto)

## Pendiente
0. **Tanda 1**, según el plan del Analista. Las entregas 1 (seguridad) y 2 (notificaciones y
   correo) están hechas; siguen las que defina el Analista. Antes de publicar hay que configurar
   `Correo__UrlBase` en IIS y seguir los pasos de IIS de la entrega 2.
1. Decidir el siguiente paso de tableros: rediseñar los tableros marcados
   (Proyectos operativo, Reuniones, Tickets, Digitalización, Trámites, Expedientes)
   o unificar los tres tableros de Proyectos por alcance. Decide Jamil.
2. Crear `docs/ux/GUIA-UX.md` a partir de la aplicación existente
   (`DESIGN.md` y `.claude/agents/references/lengua-de-casa-diger.md` como base).

## Descartado
- 2026-10-22 — Mostrar el mensaje de éxito en el editor de expedientes. Decidió Jamil: queda como
  está.

## Para después
- (nada registrado)

## Decisiones
- 2026-10-22 — Ingeniería (entrega 2):
  - El agente de interfaz de GestionGD es `diger-ui`; aquí no existe `ux-ui`.
  - Los textos dicen «el portal» y no «esta institución», porque el servidor de correo es uno
    solo para toda la instalación.
  - No se sugiere «intentarlo de nuevo», porque cada intento vuelve a dejar el aviso en la campana.
  - Sin `Correo:UrlBase`, los enlaces de los recordatorios quedan relativos, como antes; el arranque
    lo advierte.
  - `Smtp:TimeoutSegundos` y `Notificaciones:ZonaHoraria` tienen valor por defecto en el código.
    `ZonaHoraria` además quedó escrita en `appsettings.json`.
  - El tiempo máximo de envío, si se agota, cuenta como correo fallido.
  - En los pasos de IIS se recomienda además quitar el tiempo de inactividad del grupo: si no,
    IIS lo apaga a los 20 minutos aunque esté en `AlwaysRunning`.
- 2026-10-22 — Jamil: la tanda 1 se hace en la rama `Jamil-GestionGD`, sacada de `ike_opt`, en
  esta carpeta y sin worktrees nuevos. La rama `Jamil` no se toca.
- 2026-10-22 — Ingeniería (entrega 1):
  - `IEmailService` recibe el tipo de correo (`TipoCorreo`), que es lo que queda en el registro.
    Tampoco se registra la dirección de destino, que es un dato personal.
  - En riesgos e interesados, el proyecto entra como parámetro **opcional al final** de cada
    comando. Así la herramienta MCP compila sin cambios y queda protegida igual, porque el alcance
    se exige por el proyecto del propio registro.
  - Una `Correo:UrlBase` mal escrita (sin `http`/`https`) se trata igual que una que falta. Sin
    ella tampoco se genera el token de recuperación.
  - El QR, cuando usa `Correo:UrlBase`, le quita a la ruta la carpeta virtual del sitio, porque
    la dirección configurada ya la incluye.
  - `appsettings.json` trae la clave `Correo:UrlBase` vacía, y `appsettings.Development.json` la
    trae apuntando a `https://localhost:49175`.
  - La advertencia por falta de `Correo:UrlBase` sale cuando alguien pide la recuperación, no al
    arrancar. La advertencia al arrancar queda para la entrega de notificaciones, si se quiere.
- 2026-10-19 — Jamil: Expedientes, SIGER y Recursos siguen activos; la entrega de
  tableros se limita al tablero ejecutivo.

## Bugs
- **2026-10-22 — S1. Token de recuperación y cuerpo del correo en el registro.**
  - Síntoma: cuando un envío fallaba o no había servidor configurado, el registro (en IIS, el
    Visor de eventos) guardaba el correo completo, con el enlace y el token.
  - Causa raíz: `SmtpEmailService` escribía el cuerpo «para desarrollo».
  - Solución: solo se registran el tipo, un identificador del envío y el error.
  - Archivos: `IEmailService.cs`, `SmtpEmailService.cs` y quienes lo llaman
    (`EnviarRecordatorioManualCommands.cs`, `SolicitarRecuperacionPasswordCommand.cs`).
  - Pruebas: `CorreoSeguridadTests`.
- **2026-10-22 — S2. Datos de las personas sin escapar en los correos.**
  - Síntoma: un título con `<b>` o un mensaje con `<script>` entraba como marcado en el correo.
  - Causa raíz: el HTML se armaba concatenando los datos tal cual.
  - Solución: `PlantillasCorreo.E` escapa solo `& < > " '` (no toca las tildes), y se aplica solo
    a los datos de las personas. La plantilla no cambia.
  - Archivos: `PlantillasCorreo.cs`, `EnviarRecordatorioManualCommands.cs` (incluido el bloque de
    Expediente, en un commit aparte) y `SolicitarRecuperacionPasswordCommand.cs`.
  - Pruebas: `CorreosEscapadosTests`.
- **2026-10-22 — S3. Riesgos de otro proyecto.**
  - Síntoma: cambiando el número del riesgo en la petición se editaba, se cambiaba de estado o se
    eliminaba un riesgo de otro proyecto, incluso de otra institución. Afectaba también a la
    herramienta MCP.
  - Causa raíz: los comandos buscaban solo por Id, y la tabla `ProyectoRiesgos` no tiene filtro
    de alcance.
  - Solución: `RiesgoEnAlcance` comprueba el alcance por medio de `Proyectos`, que sí lo tiene, y
    el proyecto que manda la ficha.
  - Archivos: `RiesgoCommands.cs` y `Proyectos/Editor.cshtml.cs`.
  - Pruebas: `RiesgosAlcanceTests` y `RiesgosAlcancePaginaYMcpTests`.
- **2026-10-22 — S4. Interesados de otro proyecto.**
  - Síntoma y causa raíz: los mismos que en S3, con la tabla `ProyectoInteresados`. Quitar a
    alguien le quitaba el acceso al proyecto.
  - Solución: `InteresadoEnAlcance`, igual que en riesgos.
  - Archivos: `InteresadoCommands.cs` y `Proyectos/Editor.cshtml.cs`.
  - Pruebas: `InteresadosAlcanceTests` e `InteresadosAlcancePaginaTests`.
- **2026-10-22 — S5. Enlace de recuperación con la dirección de la petición.**
  - Síntoma: pidiendo la recuperación de otra persona con un host propio, su correo llevaba el
    token a ese host.
  - Causa raíz: `OlvidarPassword` armaba la dirección con `Request.Scheme` y `Request.Host`, que
    los manda el cliente.
  - Solución: `CorreoOptions.UrlBase`. Sin ella no se envía nada y queda una advertencia en el
    registro. El QR de asistencia la usa si existe.
  - Archivos: `CorreoOptions.cs`, `SolicitarRecuperacionPasswordCommand.cs`,
    `OlvidarPassword.cshtml.cs`, `Reuniones/Asistencia.cshtml.cs`, `DependencyInjection.cs` y
    `appsettings*.json`.
  - Pruebas: `RecuperacionUrlBaseTests` y `EnlacesConUrlBaseTests` (recuperación y QR).
  - Revisado sin cambios: las páginas de inicio de sesión con certificado y la cookie por host
    también leen `Request.Host`, pero solo para redirigir a quien hace la petición. No arman
    enlaces para otras personas.
- 2026-10-19 — Panel del usuario transparente en tema oscuro. Causa: el panel no
  tenía fondo propio para el tema oscuro. Solución: fondo con token de panel.
- 2026-10-19 — Avatar invisible en tema oscuro. Causa: una regla más específica
  (`.user-menu-btn .side-avatar`) dejaba azul sobre azul. Solución: fondo celeste
  con texto oscuro en tema oscuro.

## Riesgos y bloqueos
- **La base local no sirve para revisar en el navegador** (2026-10-22). `GestionGD_new`, a la que
  apunta `appsettings.Development.json`, ya no existe en `SQL2025`; de ahí el error 4060.
  `GestionGD_DashBoards` tiene la última migración, pero a su tabla `Reuniones` le falta
  `Duracion` y a las fichas SIGER del expediente les faltan columnas nuevas (`ClaveEstable`,
  `EstaEnSol` y otras). Por eso el acta, el compromiso y el editor de expedientes dan error 500.
  Sí se vio: la advertencia en tema oscuro se lee bien (ámbar sobre fondo oscuro, unos 8,8:1) y
  cabe en 375 px. No se vio: la impresión del acta y el editor de expedientes en móvil. Necesita:
  que Jamil decida si se corren los guiones de puesta al día sobre esa base o si se usa otra.
- **Desborde horizontal en móvil en Tableros** (unos 23 px a 375 px): el menú de usuario de la
  cabecera y los KPI. Ya estaba antes de la integración; no viene de la tanda 1.
- **Correos ya escritos en el Visor de eventos del servidor** (de antes de S1): pueden tener datos
  de personas y enlaces de recuperación ya vencidos. Necesita: decisión de Jamil sobre vaciar ese
  registro o restringir quién lo lee.
- **Publicar `Jamil-GestionGD` lleva todo `ike_opt` a producción**, no solo la tanda 1. Su
  despliegue va primero (`database/DESPLIEGUE.md`).
- **Contraseña del correo saliente versionada** desde el commit `b709609` (2026-08-07). Está en
  `dev` y en varias ramas remotas, y el registro de eventos del servidor puede guardar tokens de
  recuperación. Necesita: que la jefatura la cambie, y la decisión de Jamil sobre limpiar el
  historial.
- **Documentos de Expedientes sin descarga.** La subida guarda una dirección `/uploads/...` que
  ya no se sirve desde el 2026-08-26, así que el enlace probablemente no abre. No está reproducido
  todavía.
- **Respaldo de base de datos en el historial del repositorio** (`TramitesEstado.bak`,
  13 MB, versionado desde el commit `2d8fa08`). Puede contener datos reales. `ike_opt` ya lo
  quitó del árbol (llegó con la rama Jamil), pero sigue en el historial. Borrarlo de ahí
  implicaría reescribirlo, cosa que no se hace sin aprobación.
- **`appsettings.json` del Web apunta a una base que ya no existe en local**
  (`GestionGD_TEST`). Cualquier arranque fuera de `Development` falla en la primera
  consulta. El valor real de producción se configura en el servidor.
- **Dos migraciones inertes** (`20260719000000_AddPreregistroAsistente`,
  `20260719100000_AddInstitucionBranding`): una base creada desde cero con las
  migraciones quedaría sin tres columnas que el modelo usa. Las bases en uso sí
  las tienen.
- **El host `Presentation`** sigue apuntando a bases `DigerTramitesEstado*` que no
  corresponden a las del Web.
