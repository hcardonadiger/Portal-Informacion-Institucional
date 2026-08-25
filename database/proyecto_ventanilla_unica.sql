/*
    Alta del proyecto «Ventanilla Única — Honduras Ágil»: el punto único de acceso del
    ciudadano a los trámites del Estado, alimentado con la información del Portal de
    Digitalización de Trámites a través de un API intermediario.

    Estado al momento del alta: en desarrollo. El análisis de requerimientos, la definición
    del flujo, la identificación del origen de datos y el establecimiento del proyecto base
    ya están cumplidos; el trabajo en curso es el API que hará de intermediario con el
    portal de digitalización.

    Los hitos son exactamente los cuatro tramos cerrados más el que está en curso. No se
    agregan entregables posteriores porque todavía no están definidos: cuando se acuerden,
    se suman con un script de actualización, igual que se hizo con SENPRENDE.

    Idempotente: reconoce el proyecto por Nombre, los hitos por (Proyecto, Nombre) y el
    avance por Descripción.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/proyecto_ventanilla_unica.sql
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

DECLARE @nombre    nvarchar(300) = N'Ventanilla Única — Honduras Ágil';
DECLARE @actor     nvarchar(200) = N'Henry Alexis Ortez Banegas';
DECLARE @hoy       datetime2     = SYSUTCDATETIME();
DECLARE @avancePct int           = 40;

/* El equipo técnico son dos personas y el campo es uno solo. `Responsable` es un snapshot
   de presentación (mismo criterio que MetaTramite.Responsable), así que lleva los dos
   nombres; `ResponsableId` queda NULL porque ningún Id representa a los dos. Si más
   adelante se define un único responsable formal, se le pone su Id aquí. */
DECLARE @responsable nvarchar(200) = N'Henry Cardona / Jamil García';

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
         N'Poner en operación Honduras Ágil, la ventanilla única que da al ciudadano un punto único de acceso a los trámites del Estado, tomando la información de trámites del Portal de Digitalización de Trámites por medio de un API intermediario.',
         NULL, NULL, @responsable, N'Alta', N'EnEjecucion',
         NULL,              -- sin fecha de inicio planificada registrada
         NULL,              -- sin fecha de cierre comprometida
         NULL,              -- sin fecha de inicio real registrada
         NULL, @avancePct, @hoy, @actor);
END

DECLARE @proy int = (SELECT Id FROM Proyectos WHERE Nombre = @nombre AND IsDeleted = 0);

-- ── Hitos ───────────────────────────────────────────────────────────────────
/* Los cuatro primeros están cumplidos; no llevan FechaReal porque no se registró la fecha
   de cada entrega. El quinto es el trabajo en curso. */
DECLARE @h TABLE (Orden int, Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30));

INSERT INTO @h (Orden, Nombre, Descripcion, Estado) VALUES
( 1, N'Análisis de requerimientos',
     N'Levantamiento y análisis de los requerimientos de la ventanilla única: qué debe resolver Honduras Ágil como punto único de acceso ciudadano a los trámites del Estado.',
     N'Completado'),

( 2, N'Definición del flujo',
     N'Definición del flujo de la ventanilla única: el recorrido del ciudadano desde que consulta un trámite hasta que lo gestiona.',
     N'Completado'),

( 3, N'Identificación del origen de datos',
     N'Se estableció que la información de trámites proviene del Portal de Digitalización de Trámites, que queda como fuente de datos de la ventanilla.',
     N'Completado'),

( 4, N'Establecimiento del proyecto base',
     N'Creación del proyecto base sobre el que se construye la ventanilla: estructura de la solución y andamiaje inicial.',
     N'Completado'),

( 5, N'API intermediario con el portal de digitalización',
     N'Desarrollo del API que hace de intermediario entre la ventanilla única y el Portal de Digitalización de Trámites, para exponer a Honduras Ágil la información de trámites del portal. Es el trabajo en curso.',
     N'EnProceso');

UPDATE hp
SET hp.Orden       = h.Orden,
    hp.Descripcion = h.Descripcion,
    hp.Estado      = h.Estado
FROM ProyectoHitos hp
JOIN @h h ON h.Nombre = hp.Nombre
WHERE hp.ProyectoId = @proy;

INSERT INTO ProyectoHitos (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT @proy, h.Orden, h.Nombre, h.Descripcion, NULL, NULL, h.Estado, NULL, NULL
FROM @h h
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoHitos x WHERE x.ProyectoId = @proy AND x.Nombre = h.Nombre
);

-- ── Bitácora: el estado al momento del alta ─────────────────────────────────
DECLARE @desc nvarchar(2000) = N'Alta del proyecto en el portafolio. La ventanilla única Honduras Ágil ya tiene cerrados el análisis de requerimientos, la definición del flujo y la identificación del origen de datos, que quedó establecido en el Portal de Digitalización de Trámites; el proyecto base también está establecido. El trabajo en curso es el desarrollo del API que servirá de intermediario entre la ventanilla y el portal de digitalización. La ejecución técnica está a cargo de Henry Cardona y Jamil García.';

INSERT INTO ProyectoAvances
    (ProyectoId, HitoId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT @proy,
       (SELECT TOP 1 Id FROM ProyectoHitos
        WHERE ProyectoId = @proy AND Nombre = N'API intermediario con el portal de digitalización'),
       @hoy, @actor, @desc, @avancePct, NULL, NULL, NULL, NULL
WHERE NOT EXISTS (SELECT 1 FROM ProyectoAvances WHERE ProyectoId = @proy AND Descripcion = @desc);

UPDATE p
SET p.AvancePct = u.Pct, p.UpdatedAt = @hoy, p.UpdatedBy = @actor
FROM Proyectos p
CROSS APPLY (SELECT TOP 1 a.PorcentajeReportado AS Pct FROM ProyectoAvances a
             WHERE a.ProyectoId = p.Id ORDER BY a.Fecha DESC, a.Id DESC) u
WHERE p.Id = @proy;

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT Codigo, Estado, Prioridad, AvancePct AS Pct, Responsable
FROM Proyectos WHERE Id = @proy;

SELECT h.Orden, LEFT(h.Nombre, 54) AS Hito, h.Estado
FROM ProyectoHitos h WHERE h.ProyectoId = @proy ORDER BY h.Orden;
