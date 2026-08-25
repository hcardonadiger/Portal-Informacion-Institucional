/*
    Actualización del proyecto SOL — SRECI (PRY-2026-06) con las ayudas memoria del
    2026-08-12 y del 2026-08-19.

    Origen: los dos "Registro de Reunión" generados por el portal, cuyos PDF quedaron
    adjuntos como evidencia de cada entrada de bitácora. Esas dos reuniones NO están en
    esta base (la última registrada acá es del 2026-07-29): la base de desarrollo es una
    foto anterior, así que el proyecto se actualiza desde las actas, no desde Reuniones.

    Nota sobre un plazo: el acuerdo 4 del acta del 19/08 trae plazo 2026-06-26, anterior a
    la propia reunión. Por la convocatoria acordada para el miércoles 26 de agosto se
    interpreta como 2026-08-26, y así se registra en el hito correspondiente.

    Idempotente: reconoce los hitos por (Proyecto, Nombre) y los avances por su descripción.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/actualizar_proyecto_sreci.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @proy int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-06' AND IsDeleted = 0);
IF @proy IS NULL
BEGIN
    RAISERROR(N'No se encontró el proyecto PRY-2026-06.', 16, 1);
    RETURN;
END

DECLARE @henry    uniqueidentifier = 'FF56AE4F-AB09-41A6-BCE3-F954E5E7DAAF';
DECLARE @henryNom nvarchar(200)    = N'Henry Ortez';
DECLARE @actor    nvarchar(200)    = N'Henry Alexis Ortez Banegas';
DECLARE @hoy      datetime2        = SYSUTCDATETIME();

BEGIN TRANSACTION;

-- ── 1. Responsable ──────────────────────────────────────────────────────────
/* Figura como Gerente de Digitalización de Trámites en ambas actas y tiene a su cargo
   3 de los 5 acuerdos del 12/08. Hasta ahora el proyecto estaba sin asignar. */
UPDATE Proyectos
SET ResponsableId = @henry,
    Responsable   = @henryNom,
    UpdatedAt     = @hoy,
    UpdatedBy     = @actor
WHERE Id = @proy;

-- ── 2. Hitos ────────────────────────────────────────────────────────────────
/* Se reordena la lista para que lea como la secuencia real del frente: primero el
   convenio y la conformación de mesas, después el levantamiento y el diagnóstico, y al
   final la arquitectura y el piloto. Se actualizan por nombre en vez de borrar y
   reinsertar, para no perder la imputación de futuros avances. */

DECLARE @nuevos TABLE (Orden int, Nombre nvarchar(300), Estado nvarchar(30), FechaPlan date NULL, FechaReal date NULL);

INSERT INTO @nuevos (Orden, Nombre, Estado, FechaPlan, FechaReal) VALUES
(1, N'Convenio de cooperación interinstitucional SRECI — DIGER',        N'EnProceso',  NULL,         NULL),
(2, N'Designación de contrapartes y mesas de trabajo',                  N'Completado', NULL,         '2026-08-19'),
(3, N'Mesas técnicas simultáneas por línea de trabajo',                 N'EnProceso',  '2026-08-26', NULL),
(4, N'Levantamiento de procesos y flujos de Auténticas y Apostillas',   N'EnProceso',  '2026-08-26', NULL),
(5, N'Análisis y diagnóstico del modelado actual',                      N'EnProceso',  NULL,         NULL),
(6, N'Viabilidad técnica de conexiones e infraestructura',              N'Pendiente',  NULL,         NULL),
(7, N'Propuesta de arquitectura de interoperabilidad (APIs)',           N'Pendiente',  NULL,         NULL),
(8, N'Piloto de emisión con firma electrónica',                         N'Pendiente',  NULL,         NULL);

UPDATE h
SET h.Orden     = n.Orden,
    h.Estado    = n.Estado,
    h.FechaPlan = n.FechaPlan,
    h.FechaReal = n.FechaReal
FROM ProyectoHitos h
JOIN @nuevos n ON n.Nombre = h.Nombre
WHERE h.ProyectoId = @proy;

INSERT INTO ProyectoHitos (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT @proy, n.Orden, n.Nombre, NULL, n.FechaPlan, n.FechaReal, n.Estado, NULL, NULL
FROM @nuevos n
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoHitos h WHERE h.ProyectoId = @proy AND h.Nombre = n.Nombre
);

-- ── 3. Bitácora de ejecución ────────────────────────────────────────────────
DECLARE @a TABLE (
    Fecha datetime2, Pct int, HitoNombre nvarchar(300),
    Descripcion nvarchar(2000), Bloqueo nvarchar(1000) NULL,
    ArchivoNombre nvarchar(300), ArchivoUrl nvarchar(500), ArchivoTamano bigint
);

/* Las horas van en UTC: ProyectoAvances.Fecha se llena con DateTime.UtcNow desde la
   aplicación y la vista hace ToLocalTime(). Honduras es UTC-6 todo el año, así que las
   09:00 y 10:00 de las actas se guardan como 15:00 y 16:00. */
INSERT INTO @a VALUES
('2026-08-12T15:00:00', 15,
 N'Convenio de cooperación interinstitucional SRECI — DIGER',
 N'Reunión técnica virtual para revisar el alcance del borrador de convenio de cooperación interinstitucional entre la SRECI y DIGER, sobre modernización institucional, fortalecimiento tecnológico, transformación digital y mejora de la Plataforma SOL. Se acordó iniciar las mesas técnicas en paralelo, sin condicionar su arranque a la formalización del convenio, y realizar la primera mesa presencial el 19 de agosto priorizando gobierno digital y SOL. Participaron por DIGER Henry Ortez, Brizzio Zelaya, Jamil García y Carlos Ordóñez; por la SRECI, Gaspar Meza y Leticia Argueta.',
 N'El convenio sigue en socialización interna en DIGER. Se mitigó desacoplando el inicio de las mesas técnicas de su firma.',
 N'Registro_SRECI_2026-08-12.pdf', N'/uploads/proyectos/a6fb6dede6cb7ee8f96ffbdbfcc75bad.pdf', 149849),

('2026-08-19T16:00:00', 25,
 N'Designación de contrapartes y mesas de trabajo',
 N'Primera mesa presencial en las instalaciones de la SRECI, con doce participantes: nueve áreas de la Cancillería (Informática, Data Center, Auténticas y Apostillas, UPEG, Presupuesto y Despacho Ministerial) y tres de DIGER. Se definieron las líneas de trabajo y se identificaron los equipos de las siguientes mesas técnicas. Se acordó convocar la próxima reunión para el miércoles 26 de agosto a las 9:00, compartir la ficha técnica para el levantamiento del status de los trámites, y que el equipo de Apostillas y Auténticas levante sus procesos y flujos actuales. Se revisaron capacidades de la Plataforma SOL: formularios, moldes, límites de tamaño de archivo, trazabilidad y notificaciones por correo.',
 NULL,
 N'Registro_SRECI_2026-08-19.pdf', N'/uploads/proyectos/dfe8c1c6b8e10eed1cdc4306dcd038d3.pdf', 155871);

INSERT INTO ProyectoAvances
    (ProyectoId, HitoId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT @proy,
       (SELECT TOP 1 h.Id FROM ProyectoHitos h WHERE h.ProyectoId = @proy AND h.Nombre = a.HitoNombre),
       a.Fecha, @actor, a.Descripcion, a.Pct, a.Bloqueo,
       a.ArchivoNombre, a.ArchivoUrl, a.ArchivoTamano
FROM @a a
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoAvances x WHERE x.ProyectoId = @proy AND x.Descripcion = a.Descripcion
);

-- ── 4. Snapshot del avance ──────────────────────────────────────────────────
/* El porcentaje del proyecto es el del último reporte, igual que hace RegistrarAvanceCommand. */
UPDATE Proyectos
SET AvancePct = (SELECT TOP 1 PorcentajeReportado FROM ProyectoAvances
                 WHERE ProyectoId = @proy ORDER BY Fecha DESC, Id DESC),
    UpdatedAt = @hoy,
    UpdatedBy = @actor
WHERE Id = @proy;

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT p.Codigo, p.Estado, p.AvancePct, p.Responsable FROM Proyectos p WHERE p.Id = @proy;

SELECT h.Orden, LEFT(h.Nombre, 52) AS Hito, h.Estado,
       CONVERT(varchar(10), h.FechaPlan, 103) AS Planificada,
       CONVERT(varchar(10), h.FechaReal, 103) AS Cumplida
FROM ProyectoHitos h WHERE h.ProyectoId = @proy ORDER BY h.Orden;

SELECT CONVERT(varchar(16), a.Fecha, 120) AS Fecha, a.PorcentajeReportado AS Pct,
       a.ArchivoNombre AS Evidencia,
       CASE WHEN a.Bloqueo IS NULL THEN N'—' ELSE N'sí' END AS Bloqueo
FROM ProyectoAvances a WHERE a.ProyectoId = @proy ORDER BY a.Fecha;
