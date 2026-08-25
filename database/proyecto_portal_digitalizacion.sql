/*
    Alta del proyecto «Portal de Digitalización de Trámites»: el desarrollo del propio portal,
    registrado como un proyecto más del portafolio.

    Los hitos cumplidos no son una reconstrucción de memoria: salen del historial del
    repositorio (174 commits entre el 2026-06-26 y el 2026-08-13) y de los artefactos que
    dejó cada entrega — migraciones EF, scripts de `database/` y documentos de diseño.

    Estado al momento del alta: publicado para validación interna, con el módulo de
    seguimiento de proyectos ya incorporado. Faltan la aprobación de la coordinación y la
    puesta en producción, que quedan como los dos últimos hitos.

    Idempotente: reconoce el proyecto por Nombre y los hitos por (Proyecto, Nombre).

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/proyecto_portal_digitalizacion.sql
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

DECLARE @nombre    nvarchar(300)    = N'Portal de Digitalización de Trámites';
DECLARE @henry     uniqueidentifier = 'FF56AE4F-AB09-41A6-BCE3-F954E5E7DAAF';
DECLARE @henryNom  nvarchar(200)    = N'Henry Ortez';
DECLARE @actor     nvarchar(200)    = N'Henry Alexis Ortez Banegas';
DECLARE @hoy       datetime2        = SYSUTCDATETIME();
DECLARE @avancePct int              = 80;

BEGIN TRANSACTION;

-- ── Proyecto ────────────────────────────────────────────────────────────────
DECLARE @base int = (
    SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(Codigo, 10, 10) AS int)), 0)
    FROM Proyectos WHERE Codigo LIKE N'PRY-2026-%'
);

IF NOT EXISTS (SELECT 1 FROM Proyectos WHERE Nombre = @nombre AND IsDeleted = 0)
BEGIN
    INSERT INTO Proyectos
        (IsDeleted, Codigo, Nombre, Objetivo, AreaId, ResponsableId, Responsable, Prioridad, Estado,
         FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, CreatedAt, CreatedBy)
    VALUES
        (0,
         N'PRY-2026-' + FORMAT(@base + 1, '00'),
         @nombre,
         N'Construir y poner en producción el portal institucional que sostiene el ciclo de vida de la digitalización de trámites: expedientes y su metodología, reuniones y compromisos, mesa de ayuda, inventario SIGER, tableros de seguimiento y seguimiento de proyectos, con seguridad administrable y autenticación por certificado digital.',
         NULL, @henry, @henryNom, N'Alta', N'EnEjecucion',
         '2026-06-26',      -- primer commit del repositorio
         NULL,              -- sin fecha de cierre comprometida
         '2026-06-26',
         NULL, @avancePct, @hoy, @actor);
END

DECLARE @proy int = (SELECT Id FROM Proyectos WHERE Nombre = @nombre AND IsDeleted = 0);

-- ── Hitos ───────────────────────────────────────────────────────────────────
/* Los trece primeros están cumplidos y llevan la fecha real de su entrega. Los tres
   últimos son el camino que queda: validación interna en curso, aprobación de la
   coordinación y paso a producción. */
DECLARE @h TABLE (Orden int, Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30), FechaReal date NULL);

INSERT INTO @h (Orden, Nombre, Descripcion, Estado, FechaReal) VALUES
( 1, N'Arranque del repositorio y estructura base',
     N'Solución .NET 9 con Clean Architecture (Domain, Application, Infrastructure, Web), Razor Pages y MediatR.',
     N'Completado', '2026-06-26'),
( 2, N'Importación de las 199 fichas de trámites',
     N'Carga de las fichas de trámites digitalizados: 31 expedientes, 199 trámites, 1 826 requisitos, 916 pasos y 345 fundamentos legales. Scripts en database/import-fichas.',
     N'Completado', '2026-07-07'),
( 3, N'Gestión de expedientes y trámites',
     N'Editor de expedientes con su metodología por etapas, requisitos, flujos, documentación y perfiles de infraestructura.',
     N'Completado', '2026-07-08'),
( 4, N'Reuniones, compromisos y registro de asistencia',
     N'Actas, acuerdos con responsable y plazo, autorregistro de asistentes por QR y exportación del registro de reunión.',
     N'Completado', '2026-07-08'),
( 5, N'Autenticación con certificado digital',
     N'Ingreso con certificado emitido por la PKI del Estado, sobre un puerto propio con mTLS.',
     N'Completado', '2026-07-13'),
( 6, N'Mesa de ayuda: notificaciones, tickets y chat',
     N'Tickets con temas y tiempos de atención, chat de soporte, y notificaciones internas con recordatorios.',
     N'Completado', '2026-07-21'),
( 7, N'Plan de trabajo, informes y cronograma',
     N'Plan anual de racionalización por institución, con metas atadas a expedientes, informes y cronograma de etapas.',
     N'Completado', '2026-07-22'),
( 8, N'Calendario y tableros de seguimiento',
     N'Calendario interactivo y los tableros de digitalización, expedientes, reuniones, tickets y Mi Tablero.',
     N'Completado', '2026-07-31'),
( 9, N'Trazabilidad y bitácora de expedientes',
     N'Despacho de eventos de dominio, máquina de estados del expediente, validación vinculada a usuario y bitácora append-only.',
     N'Completado', '2026-08-05'),
(10, N'Identidad de producto, modo oscuro y accesibilidad',
     N'Isotipo y favicon propios, sistema de tokens documentado en DESIGN.md, tema claro/oscuro con su selector y corrección de contraste.',
     N'Completado', '2026-08-07'),
(11, N'Inventario SIGER y conciliación con expedientes',
     N'Inventario de trámites del SIGER, su observatorio y la bandeja de conciliación contra los expedientes del portal.',
     N'Completado', '2026-08-10'),
(12, N'Modelo de seguridad: roles administrables y permisos por acción',
     N'Los roles pasaron de un enum a una tabla administrable con nivel de alcance, y el acceso a una matriz de permisos por acción exigida en cada handler.',
     N'Completado', '2026-08-13'),
(13, N'Módulo de seguimiento de proyectos',
     N'Proyectos con hitos y bitácora de ejecución con evidencia adjunta, más su tablero gerencial. Incluye la carga inicial del portafolio derivada de las reuniones.',
     N'Completado', '2026-08-22'),
(14, N'Validación interna del portal',
     N'El portal está publicado y en revisión por el equipo interno: recorrido funcional de los módulos, hallazgos y correcciones antes de someterlo a aprobación.',
     N'EnProceso',  NULL),
(15, N'Aprobación de la coordinación',
     N'Presentación del portal a la coordinación para su revisión formal y visto bueno de paso a producción.',
     N'Pendiente',  NULL),
(16, N'Puesta en producción',
     N'Despliegue en el ambiente productivo, migración de datos, configuración de certificados en IIS y habilitación de usuarios.',
     N'Pendiente',  NULL);

UPDATE hp
SET hp.Orden       = h.Orden,
    hp.Descripcion = h.Descripcion,
    hp.Estado      = h.Estado,
    hp.FechaReal   = h.FechaReal
FROM ProyectoHitos hp
JOIN @h h ON h.Nombre = hp.Nombre
WHERE hp.ProyectoId = @proy;

INSERT INTO ProyectoHitos (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT @proy, h.Orden, h.Nombre, h.Descripcion, NULL, h.FechaReal, h.Estado, NULL, NULL
FROM @h h
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoHitos x WHERE x.ProyectoId = @proy AND x.Nombre = h.Nombre
);

-- ── Bitácora: el estado al momento del alta ─────────────────────────────────
DECLARE @desc nvarchar(2000) = N'El portal está publicado para validación interna, con trece entregas cumplidas entre el 26 de junio y el 22 de agosto: expedientes y su metodología, reuniones y compromisos, mesa de ayuda, autenticación con certificado digital, inventario SIGER y su conciliación, trazabilidad con bitácora, identidad de producto con modo oscuro, y el modelo de seguridad de roles administrables con permisos por acción. En esta última entrega se incorporó el módulo de seguimiento de proyectos, con su tablero gerencial, y se cargó el portafolio derivado de las reuniones. Quedan dos pasos: la aprobación de la coordinación y la puesta en producción.';

INSERT INTO ProyectoAvances
    (ProyectoId, HitoId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT @proy,
       (SELECT TOP 1 Id FROM ProyectoHitos WHERE ProyectoId = @proy AND Nombre = N'Validación interna del portal'),
       @hoy, @actor, @desc, @avancePct, NULL, NULL, NULL, NULL
WHERE NOT EXISTS (SELECT 1 FROM ProyectoAvances WHERE ProyectoId = @proy AND Descripcion = @desc);

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT Codigo, Estado, Prioridad, AvancePct, Responsable,
       CONVERT(varchar(10), FechaInicioReal, 103) AS Inicio
FROM Proyectos WHERE Id = @proy;

SELECT h.Orden, LEFT(h.Nombre, 54) AS Hito, h.Estado,
       CONVERT(varchar(10), h.FechaReal, 103) AS Cumplido
FROM ProyectoHitos h WHERE h.ProyectoId = @proy ORDER BY h.Orden;
