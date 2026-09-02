/*
    Actualiza PRY-2026-03 «SOL — CONSUCOOP» con el registro de reunión del 2026-07-16:
    «Primera Capacitación de CONSUCOOP», presencial en Boulevard Kuwait, 2 h 20 min,
    6 convocados y 6 asistentes (100 % de asistencia). Responsable DIGER: Brizzio Zelaya;
    representante institucional: Christhian Quintanilla.

    Los seis hitos que ya tenía el proyecto calzan con lo tratado, así que sobre todo se les
    pone descripción y se ajusta su estado. Se agregan dos que el registro descubre y que no
    estaban contemplados: la apertura de usuarios a nombre de la cooperativa (personas
    jurídicas) y la socialización del servicio al universo de cooperativas registradas.
    >>> Si esos dos frentes no se quieren en el portafolio, basta con borrar sus dos tuplas
        de @h; el resto del script funciona igual.

    Idempotente: hitos por (Proyecto, Nombre) —con UPDATE, no solo INSERT— y avance por
    Descripción.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/actualizar_proyecto_consucoop.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor nvarchar(200) = N'Henry Alexis Ortez Banegas';
DECLARE @hoy   datetime2     = SYSUTCDATETIME();
DECLARE @pct   int           = 45;
DECLARE @p     int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-03' AND IsDeleted = 0);
IF @p IS NULL BEGIN RAISERROR(N'No se encontró PRY-2026-03.', 16, 1); RETURN; END

BEGIN TRANSACTION;

-- ── Hitos ───────────────────────────────────────────────────────────────────
DECLARE @h TABLE (Orden int, Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30));

INSERT INTO @h VALUES
(1, N'Migración de hosting y configuración de ambientes',
    N'Ambiente institucional operando en la Plataforma SOL, con la administración a cargo del equipo de DIGER y la figura de coordinador y administrador habilitada para delegar y reasignar trámites cuando un funcionario se ausenta.',
    N'Completado'),

(2, N'Fichas y configuración de los siete trámites',
    N'Trámites configurados con sus formularios, tablas de relación para información masiva y plantillas de notificación. Quedan pendientes de habilitar el gestor documental único por ciudadano y la validación de personas jurídicas, ambos en desarrollo del lado de Naciones Unidas.',
    N'EnProceso'),

(3, N'Pruebas funcionales y depuración de trámites de prueba',
    N'Los trámites de prueba quedaron registrados en producción y contaminan las estadísticas. CONSUCOOP entrega los códigos de los trámites reales para que desarrollo los filtre.',
    N'EnProceso'),

(4, N'Ajustes de firma, código QR y notificaciones por correo',
    N'Firma electrónica avanzada y certificado verificable por código QR en uso. Pendiente revisar el tamaño de la firma y el sello en los documentos generados —insertándolos en una tabla para fijar su tamaño si se alcanzó el máximo permitido— y agregar al formulario de constancia de solvencia los campos de fecha de inicio y de vigencia a tres meses, para habilitar la renovación. Se dejó establecido que el canal oficial es la plataforma y que el correo funciona solo como notificación.',
    N'EnProceso'),

(5, N'Capacitación del personal técnico y legal',
    N'Primera capacitación impartida el 16 de julio de 2026, con 100 % de asistencia. Recorrido integral de la plataforma desde el perfil de funcionario: coordinadores y administradores, gestión de estados en cierres y vacaciones, conteo automático de plazos según horario hábil, bandejas de trámites, búsqueda por múltiples criterios y módulo de estadísticas. La sesión quedó grabada como respaldo ante rotación o ausencias.',
    N'EnProceso'),

(6, N'Instructivos y materiales de apoyo al ciudadano',
    N'Videos tutoriales de la plataforma y la grabación de la capacitación como material de respaldo. DIGER comparte además el compendio de normativa nacional sobre gobierno electrónico y validez legal de los documentos digitales.',
    N'EnProceso'),

(7, N'Usuarios de personas jurídicas y representación legal',
    N'CONSUCOOP trabaja con cooperativas, no con personas naturales: conviene que los usuarios se abran a nombre de la cooperativa y no de un funcionario. Ante solicitudes de quien no ejerce la representación legal —como el caso del oficial de cumplimiento— el criterio legal acordado es rechazar la solicitud fundamentando el rechazo en la normativa interna, ya que no puede restringirse el derecho constitucional de petición.',
    N'EnProceso'),

(8, N'Socialización del servicio a las cooperativas usuarias',
    N'Llegar al universo de cooperativas registradas. Se consulta con la jefatura de DIGER la posibilidad de realizar jornadas de capacitación dirigidas directamente a las cooperativas que usan la plataforma.',
    N'Pendiente');

UPDATE hp
SET hp.Orden       = h.Orden,
    hp.Descripcion = h.Descripcion,
    hp.Estado      = h.Estado
FROM ProyectoEntregables hp
JOIN @h h ON h.Nombre = hp.Nombre
WHERE hp.ProyectoId = @p;

INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT @p, h.Orden, h.Nombre, h.Descripcion, NULL, NULL, h.Estado, NULL, NULL
FROM @h h
WHERE NOT EXISTS (SELECT 1 FROM ProyectoEntregables x WHERE x.ProyectoId = @p AND x.Nombre = h.Nombre);

-- ── Bitácora ────────────────────────────────────────────────────────────────
DECLARE @desc nvarchar(2000) = N'Primera capacitación presencial a CONSUCOOP sobre la Plataforma SOL, con 6 convocados y 6 asistentes. Se recorrió la plataforma completa desde el perfil de funcionario: coordinador y administrador para delegar y reasignar trámites, gestión de estados en cierres y vacaciones con ventanas de mantenimiento y aviso a la ciudadanía, conteo automático de plazos según el horario hábil, bandejas de trámites, búsqueda por múltiples criterios y estadísticas por ubicación, estado y plazo. Se estableció que el canal oficial es la plataforma y que el correo solo notifica, y se detalló el uso de la firma electrónica avanzada y del certificado verificable por código QR. CONSUCOOP planteó su particularidad de operar con personas jurídicas y el asesor legal fijó el criterio para las solicitudes de quien no ejerce la representación legal. Quedaron seis compromisos con plazo al 20 de julio: revisar el tamaño de firma y sello, compartir el compendio de normativa, agregar fecha de inicio y vigencia a la constancia de solvencia, depurar los trámites de prueba en producción, consultar jornadas de capacitación para las cooperativas, y confirmar los ajustes para programar la próxima capacitación y la reunión de seguimiento. La sesión quedó grabada como respaldo ante rotación de personal.';

INSERT INTO ProyectoAvances
    (ProyectoId, EntregableId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT @p,
       (SELECT TOP 1 Id FROM ProyectoEntregables WHERE ProyectoId = @p AND Nombre = N'Capacitación del personal técnico y legal'),
       '2026-07-16T13:30:00', @actor, @desc, @pct,
       N'El gestor documental único por ciudadano y la validación de personas jurídicas están en desarrollo del lado de Naciones Unidas, propietario del desarrollo, así que las mejoras solicitadas dependen de su priorización. La conexión o API con las federaciones de cooperativas para la constancia de solvencia queda como opción a evaluar: son entidades privadas y eso la complica.',
       N'Registro_CONSUCOOP_2026-07-16.pdf', N'/uploads/proyectos/d1c2e43dec283a320bdbd22c036905ad.pdf', 449715
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

SELECT h.Orden, LEFT(h.Nombre, 50) AS Hito, h.Estado,
       CASE WHEN h.Descripcion IS NULL THEN 'sin desc' ELSE 'ok' END AS Desc_
FROM ProyectoEntregables h WHERE h.ProyectoId = @p ORDER BY h.Orden;

SELECT CONVERT(varchar(10), a.Fecha, 103) AS Fecha, a.PorcentajeReportado AS Pct, a.ArchivoNombre
FROM ProyectoAvances a WHERE a.ProyectoId = @p ORDER BY a.Fecha;
