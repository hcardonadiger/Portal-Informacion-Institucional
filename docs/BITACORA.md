# Bitácora — Gestión Digital (DIGER)

Registro de trabajo del proyecto. Es la fuente para los informes a jefaturas:
se escribe en lenguaje claro y **sin datos sensibles** (ni contraseñas, ni cadenas
de conexión, ni datos personales de ciudadanos).

---

## Registro por fecha

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
- Tablero ejecutivo de proyectos (2026-10-19, rama `DashBoards`). Sin cambios de base de datos.

## En curso
- (nada abierto)

## Pendiente
0. **Tanda 1**, según el plan del Analista. La entrega 1 (seguridad) está hecha; siguen las que
   defina el Analista. Antes de publicar la entrega 1 hay que configurar `Correo__UrlBase` en IIS.
1. Decidir el siguiente paso de tableros: rediseñar los tableros marcados
   (Proyectos operativo, Reuniones, Tickets, Digitalización, Trámites, Expedientes)
   o unificar los tres tableros de Proyectos por alcance. Decide Jamil.
2. Crear `docs/ux/GUIA-UX.md` a partir de la aplicación existente
   (`DESIGN.md` y `.claude/agents/references/lengua-de-casa-diger.md` como base).

## Descartado
- (nada registrado)

## Para después
- (nada registrado)

## Decisiones
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
- **Respaldo de base de datos dentro del repositorio** (`TramitesEstado.bak`,
  13 MB, versionado desde el commit `2d8fa08`). Puede contener datos reales. Sacarlo
  del repositorio requiere decisión de Jamil; borrarlo del historial implicaría
  reescribirlo, cosa que no se hace sin aprobación.
- **`appsettings.json` del Web apunta a una base que ya no existe en local**
  (`GestionGD_TEST`). Cualquier arranque fuera de `Development` falla en la primera
  consulta. El valor real de producción se configura en el servidor.
- **Dos migraciones inertes** (`20260719000000_AddPreregistroAsistente`,
  `20260719100000_AddInstitucionBranding`): una base creada desde cero con las
  migraciones quedaría sin tres columnas que el modelo usa. Las bases en uso sí
  las tienen.
- **El host `Presentation`** sigue apuntando a bases `DigerTramitesEstado*` que no
  corresponden a las del Web.
