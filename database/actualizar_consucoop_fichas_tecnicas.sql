/*
    Actualiza PRY-2026-03 «SOL — CONSUCOOP» con el hilo «Fichas Técnicas Trámites CONSUCOOP»,
    del 6 de julio al 17 de agosto de 2026, entre Brizzio Zelaya y el Ing. Christhian Quintanilla.

    Lo que aporta el hilo:
      · 06/07 — CONSUCOOP remite las fichas técnicas de los trámites a implementar.
      · 09/07 — Reunión sobre la fase final de los CUATRO trámites ya configurados; se proponen
                cinco fechas de capacitación (14, 16, 21, 24 y 27 de julio), con los equipos
                divididos en tandas de dos a tres sesiones según su rol.
      · 14/08 — DIGER propone abrir la mesa técnica para modelar los NUEVOS trámites y pide que
                la institución designe un equipo interno que reciba la transferencia de
                capacidades para modelado y administración de la plataforma.
      · 17/08 — CONSUCOOP informa que ya se ejecutaron **todas las pruebas de los cuatro
                trámites, en ambiente de pruebas y en productivo**, y se acuerda la reunión de
                seguimiento para el jueves 20 de agosto a las 2:00 p. m.

    >>> DISCREPANCIA QUE NO SE RESUELVE ACÁ: el objetivo del proyecto habla de «sus siete
        trámites» y todo este hilo habla consistentemente de CUATRO trámites configurados más
        un conjunto de trámites nuevos por modelar. Puede que siete sea 4 + 3 nuevos, o que la
        cifra original esté desactualizada. El objetivo se deja intacto a propósito: corregirlo
        exige saber cuál de las dos lecturas es la correcta.

    Idempotente: hitos por (Proyecto, Nombre) —con UPDATE, no solo INSERT— y avance por Descripción.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/actualizar_consucoop_fichas_tecnicas.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor nvarchar(200) = N'Henry Alexis Ortez Banegas';
DECLARE @hoy   datetime2     = SYSUTCDATETIME();
DECLARE @pct   int           = 55;
DECLARE @p     int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-03' AND IsDeleted = 0);
IF @p IS NULL BEGIN RAISERROR(N'No se encontró PRY-2026-03.', 16, 1); RETURN; END

BEGIN TRANSACTION;

-- ── Hitos ───────────────────────────────────────────────────────────────────
DECLARE @h TABLE (Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30));

INSERT INTO @h VALUES
(N'Pruebas funcionales y depuración de trámites de prueba',
 N'CONSUCOOP confirmó el 17 de agosto que ya se ejecutaron todas las pruebas de los cuatro trámites configurados, tanto en el ambiente de pruebas como en el productivo. Los resultados y las observaciones se revisan en la reunión de seguimiento del 20 de agosto. Sigue abierta la depuración de los trámites de prueba que quedaron registrados en producción y que contaminan las estadísticas.',
 N'EnProceso'),

(N'Capacitación del personal técnico y legal',
 N'Primera capacitación impartida el 16 de julio de 2026, con 100 % de asistencia: recorrido integral de la plataforma desde el perfil de funcionario. Antes, el 9 de julio, se propusieron cinco jornadas —14, 16, 21, 24 y 27 de julio— con los equipos divididos en tandas de dos a tres sesiones según el rol de cada grupo en la atención, gestión o administración de los trámites. Queda pendiente la continuidad dirigida a los equipos regionales que usarán la plataforma.',
 N'EnProceso'),

(N'Modelado de los nuevos trámites y mesa técnica',
 N'CONSUCOOP remitió el 6 de julio las fichas técnicas de los trámites a implementar. La mesa técnica revisa y valida el proceso actual de cada uno —requisitos, flujo de atención, actores involucrados, documentos, fases y oportunidades de mejora— antes de configurarlos en la plataforma. Arranca en la reunión del 20 de agosto en las instalaciones de la institución.',
 N'EnProceso'),

(N'Equipo interno de CONSUCOOP para modelado y administración',
 N'DIGER pidió que la institución designe un equipo propio que acompañe el proceso y reciba la transferencia de capacidades para modelar trámites y administrar la Plataforma SOL. Es lo que permite que la digitalización avance a ritmo y que los servicios sean sostenibles sin depender del acompañamiento externo.',
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
DECLARE @desc nvarchar(2000) = N'Cierre de la fase de pruebas de los cuatro trámites configurados y apertura del frente de los nuevos. CONSUCOOP confirmó el 17 de agosto que ya ejecutó todas las pruebas de los cuatro trámites, tanto en el ambiente de pruebas como en el productivo; los resultados y las observaciones se revisan en la reunión de seguimiento acordada para el jueves 20 de agosto a las 2:00 p. m. en las instalaciones de la institución. En esa misma sesión se abre la mesa técnica para modelar los nuevos trámites a partir de las fichas técnicas que la institución remitió el 6 de julio, revisando requisitos, flujo de atención, actores, documentos, fases y oportunidades de mejora antes de configurarlos. DIGER planteó además que CONSUCOOP designe un equipo interno que reciba la transferencia de capacidades para el modelado y la administración de la plataforma, y que se acuerde la continuidad de la capacitación para los equipos regionales. Sobre las jornadas de capacitación, el 9 de julio se propusieron cinco fechas —14, 16, 21, 24 y 27 de julio— con los equipos divididos en tandas de dos a tres sesiones según su rol.';

INSERT INTO ProyectoAvances
    (ProyectoId, HitoId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT @p,
       (SELECT TOP 1 Id FROM ProyectoHitos
        WHERE ProyectoId = @p AND Nombre = N'Modelado de los nuevos trámites y mesa técnica'),
       '2026-08-17T08:35:00', @actor, @desc, @pct,
       N'El avance del frente nuevo depende de que CONSUCOOP designe su equipo interno para el modelado y la administración: sin ese equipo el proceso sigue atado al acompañamiento de DIGER. La habilitación de los cuatro trámites al público queda sujeta a lo que arroje la revisión de resultados del 20 de agosto.',
       N'RE_ Fichas Técnicas Tramites CONSUCOOP.msg',
       N'/uploads/proyectos/fa6ad9cfae0fb380d26512ab08ec4204.msg', 362496
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
