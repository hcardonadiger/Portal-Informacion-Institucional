/*
    Alta del proyecto «Actualización del Portal de Encuestas — Madurez Institucional».

    Es el otro sistema de la DIGER (DynamicSurveys, Blazor Server), no el portal de trámites:
    el que levanta la Evaluación de Madurez de Gobierno Digital Institucional (Honduras 2026)
    en las instituciones del Estado.

    Los hitos cumplidos salen de artefactos con fecha del propio repositorio de encuestas
    —los scripts de `database/import-*`, el servicio de firma y el tablero de madurez— no de
    una reconstrucción de memoria.

    Estado al momento del alta: en validación DIGER-CONSULTIA. Faltan la aprobación del
    instrumento y el lanzamiento a las instituciones, que quedan como los dos últimos hitos.

    Idempotente: reconoce el proyecto por Nombre y los hitos por (Proyecto, Nombre).

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/proyecto_portal_encuestas.sql
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

DECLARE @nombre    nvarchar(300)    = N'Actualización del Portal de Encuestas — Madurez Institucional';
DECLARE @henry     uniqueidentifier = 'FF56AE4F-AB09-41A6-BCE3-F954E5E7DAAF';
DECLARE @henryNom  nvarchar(200)    = N'Henry Ortez';
DECLARE @actor     nvarchar(200)    = N'Henry Alexis Ortez Banegas';
DECLARE @hoy       datetime2        = SYSUTCDATETIME();
DECLARE @avancePct int              = 75;

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
         N'Actualizar el portal de encuestas para levantar la Evaluación de Madurez de Gobierno Digital Institucional (Honduras 2026) en las instituciones del Estado: carga del instrumento, motor de ponderación por dimensión y subdimensión, tablero de madurez con escala 1–5, constancia de respuesta firmada y exportación de resultados; hasta su validación con CONSULTIA, la aprobación del instrumento y el lanzamiento.',
         NULL, @henry, @henryNom, N'Alta', N'EnEjecucion',
         '2026-07-10',      -- primer artefacto fechado de este ciclo: la firma de constancias
         NULL,              -- sin fecha de lanzamiento comprometida
         '2026-07-10',
         NULL, @avancePct, @hoy, @actor);
END

DECLARE @proy int = (SELECT Id FROM Proyectos WHERE Nombre = @nombre AND IsDeleted = 0);

-- ── Hitos ───────────────────────────────────────────────────────────────────
DECLARE @h TABLE (Orden int, Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30), FechaReal date NULL);

INSERT INTO @h (Orden, Nombre, Descripcion, Estado, FechaReal) VALUES
( 1, N'Constancia de respuesta con firma digital',
     N'Al enviar la encuesta, el participante recibe una constancia en PDF sellada con el certificado institucional.',
     N'Completado', '2026-07-10'),
( 2, N'Carga del instrumento de madurez institucional',
     N'10 dimensiones, 41 subdimensiones, 96 preguntas y 446 opciones cargadas desde el Excel del modelo, con generador reejecutable en database/import-madurez.',
     N'Completado', '2026-07-27'),
( 3, N'Motor de ponderación por dimensión y subdimensión',
     N'Cálculo en tres niveles con pesos editables y normalización. Retrocompatible por diseño: si a un grupo le falta un peso, cae al cálculo heredado y las encuestas anteriores no cambian.',
     N'Completado', '2026-07-27'),
( 4, N'Usabilidad del formulario de respuesta',
     N'Paginación por sección, expandir y contraer dimensiones, numeración automática de preguntas e instrucciones del instrumento.',
     N'Completado', '2026-07-27'),
( 5, N'Resultados y exportación de la evaluación',
     N'Detalle por subsección, por pregunta y por persona, con respuesta más común y filtro por varias instituciones, exportable a Excel.',
     N'Completado', '2026-07-28'),
( 6, N'Instrumento unificado con capacidad de cómputo',
     N'50 campos numéricos de capacidad (valor actual y proyección a tres años) en siete bloques, integrados al mismo instrumento sin alterar el cálculo de madurez. Requerimiento del BID 4942/BL-HO.',
     N'Completado', '2026-08-04'),
( 7, N'Instrumento de capacidad del Data Center',
     N'Levantamiento cuantitativo para el Data Center Gubernamental Unificado, cargado como instrumento propio.',
     N'Completado', '2026-08-05'),
( 8, N'Tablero de madurez con escala 1–5',
     N'Tablero por dimensión y subdimensión que traduce el porcentaje a nivel de madurez (Inicial a Optimizado), con su banda de color y descripción.',
     N'Completado', '2026-08-17'),
( 9, N'Datos de prueba para la validación',
     N'Juego de llenados sintéticos y su script de limpieza, para revisar resultados y tablero sin ensuciar la base.',
     N'Completado', '2026-08-17'),
(10, N'Validación DIGER — CONSULTIA',
     N'Revisión conjunta del instrumento y de la plataforma: contenido de las preguntas, ponderaciones, resultados y tablero, con los ajustes que surjan.',
     N'EnProceso',  NULL),
(11, N'Aprobación del instrumento',
     N'Visto bueno formal del instrumento y de la metodología de evaluación, previo al lanzamiento.',
     N'Pendiente',  NULL),
(12, N'Lanzamiento a las instituciones',
     N'Habilitación de accesos por institución, convocatoria y acompañamiento para que completen la evaluación.',
     N'Pendiente',  NULL);

UPDATE hp
SET hp.Orden       = h.Orden,
    hp.Descripcion = h.Descripcion,
    hp.Estado      = h.Estado,
    hp.FechaReal   = h.FechaReal
FROM ProyectoEntregables hp
JOIN @h h ON h.Nombre = hp.Nombre
WHERE hp.ProyectoId = @proy;

INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT @proy, h.Orden, h.Nombre, h.Descripcion, NULL, h.FechaReal, h.Estado, NULL, NULL
FROM @h h
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoEntregables x WHERE x.ProyectoId = @proy AND x.Nombre = h.Nombre
);

-- ── Bitácora: el estado al momento del alta ─────────────────────────────────
DECLARE @desc nvarchar(2000) = N'El instrumento de Evaluación de Madurez de Gobierno Digital Institucional está cargado y operando en el portal de encuestas: 10 dimensiones, 41 subdimensiones y 96 preguntas, con el motor de ponderación en tres niveles, el tablero que traduce el resultado a la escala de madurez 1–5, la constancia de respuesta firmada digitalmente y la exportación de resultados por subsección, pregunta y persona. Se sumaron los campos de capacidad de cómputo que pide el BID 4942/BL-HO y se generó un juego de datos de prueba para revisar resultados sin ensuciar la base. La plataforma está ahora en validación conjunta DIGER-CONSULTIA; siguen la aprobación del instrumento y el lanzamiento a las instituciones.';

INSERT INTO ProyectoAvances
    (ProyectoId, EntregableId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT @proy,
       (SELECT TOP 1 Id FROM ProyectoEntregables WHERE ProyectoId = @proy AND Nombre = N'Validación DIGER — CONSULTIA'),
       @hoy, @actor, @desc, @avancePct, NULL, NULL, NULL, NULL
WHERE NOT EXISTS (SELECT 1 FROM ProyectoAvances WHERE ProyectoId = @proy AND Descripcion = @desc);

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT Codigo, Estado, Prioridad, AvancePct, Responsable,
       CONVERT(varchar(10), FechaInicioReal, 103) AS Inicio
FROM Proyectos WHERE Id = @proy;

SELECT h.Orden, LEFT(h.Nombre, 52) AS Hito, h.Estado,
       CONVERT(varchar(10), h.FechaReal, 103) AS Cumplido
FROM ProyectoEntregables h WHERE h.ProyectoId = @proy ORDER BY h.Orden;
