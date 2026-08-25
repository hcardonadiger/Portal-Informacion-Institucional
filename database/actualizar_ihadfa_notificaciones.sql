/*
    Actualiza PRY-2026-05 «SOL — IHADFA» con el hilo «Remisión de ayuda memoria — Revisión
    técnica de correos de notificación IHADFA», del 10 al 17 de agosto de 2026, derivado de
    la reunión técnica del viernes 7 de agosto por Teams.

    El periodo no trae avance de producto: se consumió entero en atender el incidente de
    notificaciones y en deslindar qué es falla de la Plataforma SOL y qué es configuración
    institucional. Por eso el porcentaje se mantiene en 65 y lo que se registra es el estado
    del bloqueo, que es el dato útil para gerencia.

    Qué quedó establecido en el hilo:
      · El error al verificar el correo NO viene de la Plataforma SOL sino del sitio POWWEB
        que se usaba antes; la evidencia se remitió al Ing. Mario Cruz.
      · Que no se generen la copia de la resolución ni la certificación tampoco es falla de
        la plataforma: es configuración y carga de los moldes, que corresponde a IHADFA.
      · El envío de notificaciones sigue detenido a la espera de que se entreguen las
        licencias a Infotecnología y se complete la migración del dominio institucional.

    Se agregan dos hitos que el hilo descubre: la visualización del establecimiento y el
    lugar en las solicitudes —escalable a Naciones Unidas— y el segundo grupo de seis
    trámites de procesos de importación.

    Idempotente: hitos por (Proyecto, Nombre), con UPDATE además de INSERT, y avance por
    Descripción.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/actualizar_ihadfa_notificaciones.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor nvarchar(200) = N'Henry Alexis Ortez Banegas';
DECLARE @hoy   datetime2     = SYSUTCDATETIME();
DECLARE @pct   int           = 65;   -- sin cambio: el periodo fue de incidente, no de avance
DECLARE @p     int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-05' AND IsDeleted = 0);
IF @p IS NULL BEGIN RAISERROR(N'No se encontró PRY-2026-05.', 16, 1); RETURN; END

BEGIN TRANSACTION;

-- ── Hitos ───────────────────────────────────────────────────────────────────
DECLARE @h TABLE (Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30));

INSERT INTO @h VALUES
(N'Adecuación de moldes, requisitos y elementos gráficos',
 N'Al emitir la certificación no se generan la copia de la resolución ni la certificación, lo que impide validar el flujo completo. Se aclaró que no es una falla de la plataforma: los documentos se generan a partir del llamado de campos del formulario hacia cada molde, así que corresponde a IHADFA ajustar resoluciones, certificaciones, certificados y dictámenes, cargarlos de nuevo en el ambiente y validarlos con pruebas. Ya se sostuvo una sesión sobre esto con el digitador delegado Brayan Mazariegos y se remitió la grabación como material de consulta.',
 N'EnProceso'),

(N'Designación del administrador institucional de la plataforma',
 N'Brayan Mazariegos quedó como digitador delegado y administrador de la plataforma, y recibió la capacitación sobre el llamado de campos hacia los moldes, la carga de documentos y las pruebas en el ambiente de testing. Falta todavía que el equipo institucional acceda al administrador de la cuenta de correo desde la que salen las notificaciones.',
 N'EnProceso'),

(N'Notificaciones desde una cuenta institucional Microsoft 365',
 N'Reunión técnica del 7 de agosto por Teams sobre los parámetros de Microsoft 365, Exchange y SMTP; no se completaron las pruebas por el horario del personal participante y quedó grabación como respaldo. Las validaciones confirmaron que el error al verificar el correo no proviene de la Plataforma SOL sino del sitio POWWEB usado anteriormente, y que el sistema de envío de notificaciones tiene una configuración incorrecta o la perdió en algún momento, arrastrada desde el periodo de la Ing. Daniela Zavala. Para cerrarlo hace falta que el responsable entregue las licencias a Infotecnología, se complete la migración del dominio institucional y el equipo acceda al administrador de la cuenta de correo.',
 N'EnProceso'),

(N'Usuarios y correos institucionales del nuevo personal',
 N'Depende de la entrega de licencias y de la migración del dominio institucional: hasta que eso se resuelva no se pueden dar de alta las nuevas incorporaciones en la plataforma.',
 N'Pendiente'),

(N'Salida a producción para la ciudadanía',
 N'IHADFA no considera viable iniciar el uso operativo mientras las notificaciones no lleguen y el flujo de certificación no se valide de extremo a extremo. La activación de los seis trámites reestructurados queda condicionada a esos dos puntos.',
 N'Pendiente'),

(N'Visualización del establecimiento y el lugar en las solicitudes',
 N'IHADFA pidió que la solicitud muestre el nombre del establecimiento y el lugar desde donde se presenta, para hacerla más legible al gestionarla. No es una configuración interna inmediata: debe revisarse y, de ser viable, escalarse al equipo de Naciones Unidas para su análisis, desarrollo e implementación. Mientras tanto se ofrecieron alternativas con los filtros y búsquedas ya disponibles.',
 N'Pendiente'),

(N'Segundo grupo de seis trámites — procesos de importación',
 N'La institución tiene contemplados otros seis trámites, relacionados con procesos de importación, cuya revisión arranca una vez resuelto el incidente de notificaciones y activados los seis trámites reestructurados.',
 N'Pendiente');

UPDATE hp
SET hp.Descripcion = h.Descripcion,
    hp.Estado      = h.Estado
FROM ProyectoHitos hp
JOIN @h h ON h.Nombre = hp.Nombre
WHERE hp.ProyectoId = @p;

INSERT INTO ProyectoHitos (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT @p,
       (SELECT ISNULL(MAX(Orden), 0) FROM ProyectoHitos WHERE ProyectoId = @p)
         + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       h.Nombre, h.Descripcion, NULL, NULL, h.Estado, NULL, NULL
FROM @h h
WHERE NOT EXISTS (SELECT 1 FROM ProyectoHitos x WHERE x.ProyectoId = @p AND x.Nombre = h.Nombre);

-- ── Bitácora ────────────────────────────────────────────────────────────────
DECLARE @desc nvarchar(2000) = N'Seguimiento del incidente de notificaciones entre el 10 y el 17 de agosto, a partir de la ayuda memoria de la reunión técnica del 7 de agosto sobre Microsoft 365, Exchange y SMTP. La semana se fue en deslindar responsabilidades técnicas y no dejó avance de producto. Quedó establecido que el error al verificar el correo no proviene de la Plataforma SOL sino del sitio POWWEB anterior —evidencia remitida al Ing. Mario Cruz— y que el sistema de envío de notificaciones arrastra una configuración incorrecta o perdida desde el periodo de la Ing. Daniela Zavala. Sobre el reclamo de que no se generan la copia de la resolución ni la certificación, se aclaró que tampoco es falla de la plataforma sino configuración y carga de los moldes, que corresponde a IHADFA ajustar, cargar y probar; ya hubo sesión con el digitador delegado Brayan Mazariegos y se remitió la grabación. Una reunión con el Ing. Mario Cruz se canceló a última hora el 11 de agosto. IHADFA sumó la petición de mostrar el nombre del establecimiento y el lugar en las solicitudes, que debe escalarse a Naciones Unidas. Se convocó reunión presencial para el miércoles 19 de agosto a las 2:00 p. m., pendiente de confirmación, para validar el incidente, revisar los moldes y el flujo, y acordar fechas.';

INSERT INTO ProyectoAvances
    (ProyectoId, HitoId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT @p,
       (SELECT TOP 1 Id FROM ProyectoHitos WHERE ProyectoId = @p AND Nombre = N'Notificaciones desde una cuenta institucional Microsoft 365'),
       '2026-08-17T15:05:00', @actor, @desc, @pct,
       N'El envío de notificaciones sigue detenido a la espera de que el responsable de las licencias las entregue a Infotecnología del IHADFA y se complete la migración del dominio institucional; sin eso no se puede cerrar la configuración ni activar los seis trámites reestructurados. La visualización del establecimiento y el lugar en las solicitudes depende del equipo de Naciones Unidas y no tiene fecha.',
       N'IHADFA_historia.pdf', N'/uploads/proyectos/f21f9165e98f19517f69f37607f7eda7.pdf', 5727465
WHERE NOT EXISTS (SELECT 1 FROM ProyectoAvances WHERE ProyectoId = @p AND Descripcion = @desc);

UPDATE p
SET p.AvancePct = u.Pct, p.UpdatedAt = @hoy, p.UpdatedBy = @actor
FROM Proyectos p
CROSS APPLY (SELECT TOP 1 a.PorcentajeReportado AS Pct FROM ProyectoAvances a
             WHERE a.ProyectoId = p.Id ORDER BY a.Fecha DESC, a.Id DESC) u
WHERE p.Id = @p;

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT Codigo, Estado, AvancePct AS Pct, Responsable FROM Proyectos WHERE Id = @p;

SELECT h.Orden, LEFT(h.Nombre, 52) AS Hito, h.Estado,
       CASE WHEN h.Descripcion IS NULL THEN 'sin desc' ELSE 'ok' END AS Desc_
FROM ProyectoHitos h WHERE h.ProyectoId = @p ORDER BY h.Orden;

SELECT CONVERT(varchar(10), a.Fecha, 103) AS Fecha, a.PorcentajeReportado AS Pct,
       ISNULL(a.ArchivoNombre, '—') AS Evidencia
FROM ProyectoAvances a WHERE a.ProyectoId = @p ORDER BY a.Fecha;
