/*
    Actualización con cuatro registros de reunión del portal:

      2026-07-16  Tercera capacitación IHADFA          → PRY-2026-05  SOL — IHADFA
      2026-08-06  Incidente de correos IHADFA          → PRY-2026-05  SOL — IHADFA
      2026-07-22  Levantamiento de estatus BOMBEROS    → PRY-2026-10  SOL — HBCBH
      2026-08-07  Mejoras detectadas en CNI            → PRY-2026-24  SOL — CNI  (proyecto nuevo)

    El registro de SRECI del 2026-07-08 que venía en el mismo lote ya se cargó con
    `actualizar_proyectos_ayudas_memoria.sql`; este script no lo repite.

    Dos cosas que cambian el estado de fondo, no solo el porcentaje:

      - **HBCBH no era un proyecto por arrancar.** Estaba como «Planificado» porque las dos
        reuniones que lo originaron parecían un primer acercamiento. El acta dice otra cosa:
        la reunión fue para conocer el estado de los trámites que la institución **ya tiene
        implementados** en SOL, y de paso levantar uno nuevo. Pasa a ejecución.

      - **El CNI no estaba en el portafolio.** Tiene trámites activos en producción —dictamen
        técnico, constancias de inversionista y de renovación— y una lista de ajustes en curso.
        En la carga inicial sus acuerdos habían quedado bajo IHT/CANATURH. Se crea su proyecto.

    El hito del CNI lleva plazo 2026-08-21, que ya pasó: va a aparecer como hito vencido en el
    tablero. Es correcto, no un error de captura — el acuerdo daba dos semanas desde el 7 de agosto.

    Idempotente: proyecto por Nombre, hitos por (Proyecto, Nombre), avances por Descripción.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/actualizar_ihadfa_hbcbh_cni.sql
*/
-- QUOTED_IDENTIFIER tiene que ir encendido: Proyectos.Codigo lleva un indice unico filtrado
-- (WHERE IsDeleted = 0) y SQL Server rechaza el INSERT si la opcion viene apagada, que es
-- justo como la deja sqlcmd por omision. SSMS la enciende sola, y por eso el problema solo
-- aparece al correr por linea de comandos. Va en su propio lote a proposito.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor    nvarchar(200)    = N'Henry Alexis Ortez Banegas';
DECLARE @hoy      datetime2        = SYSUTCDATETIME();
DECLARE @brizzio  uniqueidentifier = '9BBB2591-9790-427E-B1E0-792B8E562385';
DECLARE @brizNom  nvarchar(200)    = N'Brizzio Zelaya';

DECLARE @ihadfa int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-05' AND IsDeleted = 0);
DECLARE @hbcbh  int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-10' AND IsDeleted = 0);
IF @ihadfa IS NULL OR @hbcbh IS NULL BEGIN RAISERROR(N'Falta PRY-2026-05 o PRY-2026-10.',16,1); RETURN; END

BEGIN TRANSACTION;

-- ════════ 1. Proyecto nuevo: SOL — CNI ══════════════════════════════════════
DECLARE @nombreCNI nvarchar(300) = N'SOL — CNI (Consejo Nacional de Inversiones)';
DECLARE @base int = (SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(Codigo,10,10) AS int)),0) FROM Proyectos WHERE Codigo LIKE N'PRY-2026-%');

IF NOT EXISTS (SELECT 1 FROM Proyectos WHERE Nombre = @nombreCNI AND IsDeleted = 0)
    INSERT INTO Proyectos
        (IsDeleted, Codigo, Nombre, Objetivo, AreaId, ResponsableId, Responsable, Prioridad, Estado,
         FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, CreatedAt, CreatedBy)
    VALUES
        (0, N'PRY-2026-' + FORMAT(@base + 1, '00'), @nombreCNI,
         N'Sostener y mejorar los trámites del Consejo Nacional de Inversiones en la Plataforma SOL: generación automática de dictámenes técnicos y de constancias de inversionista y de renovación, con el equipo institucional validando sus cambios en el ambiente de testing antes de llevarlos a producción.',
         NULL, @brizzio, @brizNom, N'Media', N'EnEjecucion',
         '2026-07-01', NULL, '2026-07-01', NULL, 50, @hoy, @actor);

DECLARE @cni int = (SELECT Id FROM Proyectos WHERE Nombre = @nombreCNI AND IsDeleted = 0);

-- ════════ 2. Estado de HBCBH ════════════════════════════════════════════════
UPDATE Proyectos
SET Estado = N'EnEjecucion', FechaInicioReal = ISNULL(FechaInicioReal, FechaInicioPlan),
    UpdatedAt = @hoy, UpdatedBy = @actor
WHERE Id = @hbcbh AND Estado = N'Planificado';

-- ════════ 3. Hitos ══════════════════════════════════════════════════════════
DECLARE @h TABLE (Proy int, Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30), FechaPlan date NULL, FechaReal date NULL, Nuevo bit);

INSERT INTO @h VALUES
-- ── IHADFA: se actualiza el de capacitación y se agregan cuatro ──────────────
(@ihadfa, N'Capacitación del equipo legal y operativo',
 N'Tercera y última capacitación sobre los seis trámites reestructurados: recorrido del flujo completo con un caso real, desde la revisión previa hasta la resolución y emisión del certificado.',
 N'Completado', NULL, '2026-07-16', 0),
(@ihadfa, N'Acta de entrega de los seis trámites digitalizados',
 N'Los seis trámites cuentan con acta de entrega y la institución queda habilitada para operarlos.',
 N'Completado', NULL, '2026-07-16', 1),
(@ihadfa, N'Designación del administrador institucional de la plataforma',
 N'Nombrar al administrador interno y reasignar los responsables de expedientes, que quedan provisionalmente en la Secretaría General.',
 N'EnProceso', NULL, NULL, 1),
(@ihadfa, N'Notificaciones desde una cuenta institucional Microsoft 365',
 N'Los correos que salían por el proveedor tercerizado dejaron de llegar a Gmail. Se sustituye por una cuenta institucional propia, se valida en testing contra Gmail y Outlook y recién entonces se aplica en producción.',
 N'EnProceso', NULL, NULL, 1),
(@ihadfa, N'Salida a producción para la ciudadanía',
 N'Apertura de los seis trámites al público, acompañada de la socialización en redes y cápsulas informativas.',
 N'Pendiente', NULL, NULL, 1),

-- ── HBCBH: se precisa el trámite y se agregan dos ────────────────────────────
(@hbcbh, N'Mesa técnica de levantamiento',
 N'Primer levantamiento del proceso de revisión de proyectos y emisión de la constancia de factibilidad requerida para el permiso de construcción, junto al estado de los trámites ya implementados.',
 N'EnProceso', NULL, NULL, 0),
(@hbcbh, N'Flujo, requisitos y formularios del trámite',
 N'Hoja de requisitos por tipo de proyecto, formularios, hoja de remisión con medidas de cumplimiento, orden de pago y demás documentos internos y externos del proceso.',
 N'EnProceso', '2026-07-29', NULL, 0),
(@hbcbh, N'Soporte de archivos AutoCAD en la plataforma',
 N'Los planos vienen en AutoCAD. Hay que revisar si la plataforma los admite o si se incorporan por enlace de almacenamiento o evidencia de envío por correo.',
 N'EnProceso', NULL, NULL, 0),
(@hbcbh, N'Mesa técnica presencial sobre el nuevo trámite',
 N'Revisión conjunta del flujo completo, los requisitos, los documentos y los usuarios involucrados.',
 N'Pendiente', '2026-07-29', NULL, 1),

-- ── CNI ──────────────────────────────────────────────────────────────────────
(@cni, N'Trámites del CNI en producción',
 N'Dictamen técnico, constancia de inversionista y constancia de renovación operando en la Plataforma SOL.',
 N'Completado', NULL, '2026-07-01', 1),
(@cni, N'Orientación sobre el uso del ambiente de testing',
 N'El equipo institucional valida sus modificaciones en testing antes de trasladarlas a producción.',
 N'Completado', NULL, '2026-08-07', 1),
(@cni, N'Actualización de moldes y campos de dictámenes y constancias',
 N'Revisión en testing de moldes, campos y configuraciones: número de expediente, denominación societaria, finalidad de la empresa, monto de inversión, representante o apoderado legal, RTN, identidad, pasaporte, matrícula mercantil, ubicación y fecha de solicitud.',
 N'EnProceso', '2026-08-21', NULL, 1),
(@cni, N'Acompañamiento técnico en automatizaciones',
 N'Revisión por parte de DIGER de los requerimientos que involucran procedimientos almacenados y configuraciones de base de datos, conforme el CNI remita los casos.',
 N'EnProceso', NULL, NULL, 1);

/* Actualiza los existentes por nombre (Nuevo = 0) y agrega los que no están (Nuevo = 1). */
UPDATE hp
SET hp.Descripcion = h.Descripcion, hp.Estado = h.Estado,
    hp.FechaPlan = h.FechaPlan, hp.FechaReal = h.FechaReal
FROM ProyectoHitos hp JOIN @h h ON h.Proy = hp.ProyectoId AND h.Nombre = hp.Nombre;

INSERT INTO ProyectoHitos (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT h.Proy,
       (SELECT ISNULL(MAX(x.Orden),0) FROM ProyectoHitos x WHERE x.ProyectoId = h.Proy)
         + ROW_NUMBER() OVER (PARTITION BY h.Proy ORDER BY (SELECT NULL)),
       h.Nombre, h.Descripcion, h.FechaPlan, h.FechaReal, h.Estado, NULL, NULL
FROM @h h
WHERE NOT EXISTS (SELECT 1 FROM ProyectoHitos x WHERE x.ProyectoId = h.Proy AND x.Nombre = h.Nombre);

-- ════════ 4. Bitácora ═══════════════════════════════════════════════════════
DECLARE @a TABLE (Proy int, Fecha datetime2, Pct int, HitoNombre nvarchar(300),
    Descripcion nvarchar(2000), Bloqueo nvarchar(1000) NULL,
    ArchivoNombre nvarchar(300), ArchivoUrl nvarchar(500), ArchivoTamano bigint);

INSERT INTO @a VALUES
(@ihadfa, '2026-07-16T16:00:00', 60, N'Acta de entrega de los seis trámites digitalizados',
 N'Tercera y última capacitación presencial sobre los seis trámites reestructurados con el equipo legal de la institución. Se recorrió el flujo completo con un caso real: revisión previa en Secretaría General con asignación del código único de expediente, validación del pago por la gerencia administrativa, programación y registro digital de la inspección de campo —conservando el acta física firmada como respaldo—, dictamen legal de los oficiales jurídicos y, al cierre, resolución y emisión del certificado con generación automática de documentos y verificación por código QR, con firma electrónica avanzada prevista a futuro. Se mostró la trazabilidad por histórico de cambios y el gestor documental por ciudadano, que evita volver a pedir documentos ya cargados. Los seis trámites cuentan con acta de entrega y la institución queda habilitada para operarlos. Quedaron identificados los ajustes previos a la salida: la regla de un pago por establecimiento con su nota al ciudadano, hacer visible el campo obligatorio de lista de productos, separar la fase de dictamen de la de resolución y certificado, corregir la nomenclatura de los documentos y actualizar la lista de inspectores y del equipo legal.',
 NULL, N'Registro_IHADFA_2026-07-16.pdf', N'/uploads/proyectos/19d0d3d76ae3629798458c0e5d49ed12.pdf', 335371),

(@ihadfa, '2026-08-06T17:15:00', 65, N'Notificaciones desde una cuenta institucional Microsoft 365',
 N'Reunión técnica por el incidente de notificaciones. Las pruebas confirmaron que la plataforma ejecutaba el envío y marcaba las solicitudes como completadas, pero los mensajes no llegaban a cuentas de Gmail, mientras que a Outlook sí. La causa no está en SOL sino en el servicio de correo tercerizado que usa la institución: ajustar el protocolo SSL y el puerto recuperó parcialmente Outlook, pero no Gmail, y se estima que Google está restringiendo los correos de ese proveedor. Afecta de forma significativa a la ciudadanía, porque buena parte se registra con direcciones de Gmail. Como medida definitiva se acordó dejar de usar el intermediario y configurar una cuenta institucional propia administrada con Microsoft 365, probarla en testing contra Gmail y Outlook, y aplicarla en producción solo cuando se confirme la entrega y que no afecta los demás servicios.',
 N'Las notificaciones a cuentas de Gmail siguen sin llegar. Depende de que IHADFA cree la cuenta institucional en Microsoft 365 y remita los parámetros de configuración.',
 N'Registro_IHADFA_2026-08-06.pdf', N'/uploads/proyectos/124b8466c0b9a65c9bf57545200efdfd.pdf', 153096),

(@hbcbh, '2026-07-22T20:00:00', 15, N'Mesa técnica de levantamiento',
 N'Reunión para conocer el estado de los trámites que el Cuerpo de Bomberos ya tiene implementados en la Plataforma SOL y hacer un primer levantamiento del proceso de revisión de proyectos y emisión de la constancia de factibilidad requerida para el permiso de construcción. Se acordó iniciar las mesas técnicas por correo formal, que la institución prepare el flujo actual, la hoja de requisitos por tipo de proyecto, los formularios, la hoja de remisión con medidas de cumplimiento y los documentos internos y externos del proceso, y participar en una mesa presencial para revisar flujo, requisitos, documentos y usuarios. Quedó por resolver un punto técnico: los planos vienen en AutoCAD y hay que ver si la plataforma los admite o si se incorporan por enlace de almacenamiento o evidencia de envío por correo.',
 NULL, N'Registro_HBCBH_2026-07-22.pdf', N'/uploads/proyectos/e6164c488a2c45d537b04338f18061ce.pdf', 162944),

(@cni, '2026-08-07T17:00:00', 50, N'Actualización de moldes y campos de dictámenes y constancias',
 N'Reunión técnica sobre las incidencias y ajustes que el Consejo Nacional de Inversiones detectó en sus trámites activos de la Plataforma SOL, sobre todo en la generación automática de dictámenes técnicos y de constancias de inversionista y de renovación. Se orientó al equipo institucional sobre el uso del ambiente de testing para validar las modificaciones antes de trasladarlas a producción. El CNI revisará y actualizará en testing los moldes, campos y configuraciones —expediente, denominación societaria, finalidad de la empresa, monto de inversión, representante legal, RTN, identidad, pasaporte, matrícula mercantil, ubicación y fecha— con dos semanas de plazo, manteniendo comunicación con la DIGER para los casos que requieran procedimientos almacenados o configuraciones de base de datos.',
 NULL, N'Registro_CNI_2026-08-07.pdf', N'/uploads/proyectos/24d7ae42f74320a5066710329359106e.pdf', 145006);

INSERT INTO ProyectoAvances
    (ProyectoId, HitoId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT a.Proy,
       (SELECT TOP 1 h.Id FROM ProyectoHitos h WHERE h.ProyectoId = a.Proy AND h.Nombre = a.HitoNombre),
       a.Fecha, @actor, a.Descripcion, a.Pct, a.Bloqueo,
       a.ArchivoNombre, a.ArchivoUrl, a.ArchivoTamano
FROM @a a
WHERE NOT EXISTS (SELECT 1 FROM ProyectoAvances x WHERE x.ProyectoId = a.Proy AND x.Descripcion = a.Descripcion);

UPDATE p
SET p.AvancePct = u.Pct, p.UpdatedAt = @hoy, p.UpdatedBy = @actor
FROM Proyectos p
CROSS APPLY (SELECT TOP 1 a.PorcentajeReportado AS Pct FROM ProyectoAvances a
             WHERE a.ProyectoId = p.Id ORDER BY a.Fecha DESC, a.Id DESC) u
WHERE p.Id IN (@ihadfa, @hbcbh, @cni);

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT p.Codigo, LEFT(p.Nombre, 42) AS Proyecto, p.Estado, p.AvancePct AS Pct,
       COUNT(h.Id) AS Hitos, SUM(CASE WHEN h.Estado = N'Completado' THEN 1 ELSE 0 END) AS Cumplidos,
       (SELECT COUNT(*) FROM ProyectoAvances a WHERE a.ProyectoId = p.Id) AS Reportes
FROM Proyectos p LEFT JOIN ProyectoHitos h ON h.ProyectoId = p.Id
WHERE p.Id IN (@ihadfa, @hbcbh, @cni)
GROUP BY p.Id, p.Codigo, p.Nombre, p.Estado, p.AvancePct
ORDER BY p.Codigo;
