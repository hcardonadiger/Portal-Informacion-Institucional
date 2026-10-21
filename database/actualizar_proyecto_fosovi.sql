/*
    Historia del proyecto «SOL — FOSOVI» (PRY-2026-07), reconstruida de dos hilos de correo:

      · «Ayuda Memoria y Compromisos - Reunión DIGER-FOSOVI»
      · «RE: Remisión de documentación recopilada y seguimiento de información pendiente»

    El hilo cubre del 17 de junio al 3 de julio de 2026 e involucra a DIGER (Henry Cardona,
    Henry Ortez, Brizzio Zelaya, Carlos Ordoñez), SEFIN (Rodrigo Suazo, Juan Carlos García) y
    la comisión FOSOVI.

    El proyecto ya existía con sus seis hitos y CERO avances registrados: esto le pone la
    bitácora. Los seis hitos que tiene calzan con la historia, así que no se crea ninguno nuevo;
    solo se mueve el estado del que la evidencia respalda.

    ── DOS COSAS QUE CONVIENE SABER ────────────────────────────────────────────

    1. FECHA DEL ÚLTIMO AVANCE. El correo que convoca a la reunión legal del 15 de julio no
       conserva su fecha de envío recuperable en el .msg; por el hilo se ubica entre el 3 y el
       15 de julio. Se registra el 2026-07-07 y el propio texto del avance lo aclara, para que
       nadie lo lea después como un dato firme.

    2. AMBIENTE. Este script no fija la base: se corre con -d contra la que sea. Ubica el
       proyecto por nombre, no por Id, porque los Id no coinciden entre ambientes.
       Ojo: al 25/08/2026 ninguna base local salvo DigerTramitesEstado tiene el módulo de
       proyectos, así que el ambiente de test tiene que estar en otro servidor.

    Idempotente: cada avance se reconoce por su descripción; correrlo dos veces no duplica.

        sqlcmd -S <servidor> -d <base> -i actualizar_proyecto_fosovi.sql
*/

-- QUOTED_IDENTIFIER encendido y en su propio lote: Proyectos.Codigo lleva un índice único
-- filtrado y SQL Server rechaza la escritura si viene apagada, que es como la deja sqlcmd.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor    nvarchar(200) = N'Henry Alejandro Cardona Hércules';
DECLARE @ahora    datetime2     = SYSUTCDATETIME();
DECLARE @pid      int;
DECLARE @avancePct int = 28;

BEGIN TRANSACTION;

-- Se busca por nombre y se exige que haya exactamente uno: un LIKE que calce con dos proyectos
-- escribiría la historia en el equivocado.
SELECT @pid = MIN(Id) FROM Proyectos WHERE Nombre LIKE N'%FOSOVI%' AND IsDeleted = 0;

IF @pid IS NULL OR (SELECT COUNT(*) FROM Proyectos WHERE Nombre LIKE N'%FOSOVI%' AND IsDeleted = 0) <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50000, N'No hay exactamente un proyecto que calce con «FOSOVI». Revise el portafolio de esta base.', 1;
END

-- ── Bitácora de ejecución ───────────────────────────────────────────────────
DECLARE @avances TABLE (
    orden       int,
    fecha       datetime2(0),
    hito        nvarchar(300),
    porcentaje  int,
    descripcion nvarchar(2000),
    bloqueo     nvarchar(1000)
);

INSERT INTO @avances (orden, fecha, hito, porcentaje, descripcion, bloqueo) VALUES
 (1, '2026-06-19 17:03', N'Levantamiento de servicios y diagramas de flujo', 5,
  N'Reunión de acercamiento y coordinación técnica para la digitalización de trámites en la Plataforma SOL, realizada el 17 de junio de 2026 con representantes de FOSOVI, SEFIN y DIGER. Se remite la ayuda memoria con objetivos, resultados, hallazgos técnicos, acuerdos y próximos pasos.',
  NULL),

 (2, '2026-06-23 07:36', N'Levantamiento de servicios y diagramas de flujo', 8,
  N'FOSOVI y SEFIN confirman la reunión del 26 de junio en las instalaciones de FOSOVI (colonia Kennedy). DIGER solicitó de antemano la ficha de información por trámite y el flujo real del procedimiento de cada uno de los trámites identificados.',
  NULL),

 (3, '2026-06-26 12:58', N'Levantamiento de servicios y diagramas de flujo', 15,
  N'Reunión de primer acercamiento y análisis institucional en FOSOVI, con participación de FOSOVI, SEFIN y DIGER. Se remite la ayuda memoria correspondiente con los acuerdos y los próximos pasos.',
  NULL),

 (4, '2026-07-01 15:17', N'Alojamiento temporal en servidores de la DIGER', 20,
  N'Se comparte con FOSOVI el documento de adhesión al alojamiento en la DIGER y los perfiles técnicos requeridos para la gestión de la Plataforma SOL, como paso previo al levantamiento y la digitalización de los trámites.',
  NULL),

 (5, '2026-07-03 10:36', N'Análisis legal y reglamentario que faculta a FOSOVI', 25,
  N'FOSOVI remite, a través de SEFIN, la documentación consolidada a la fecha: formatos para la presentación de trámites y fundamento legal del FOSOVI. DIGER acusa recibo el mismo día y queda en revisarla.',
  N'La documentación se remitió fuera del plazo acordado y llegó incompleta: siguen pendientes la documentación interna del proceso (dictámenes, resoluciones, certificados finales y autorizaciones) y el análisis de factibilidad con su proyección social y económica.'),

 (6, '2026-07-07 09:00', N'Análisis legal y reglamentario que faculta a FOSOVI', 28,
  N'DIGER convoca a una reunión virtual con el equipo legal de FOSOVI para revisar el estatus legal de la institución, con fecha tentativa del miércoles 15 de julio a las 2:00 pm, y reitera los compromisos pendientes. (La fecha de este registro es aproximada: el correo no conserva su fecha de envío; por el hilo se ubica entre el 3 y el 15 de julio.)',
  N'Tres entregas pendientes de FOSOVI: documentación interna del proceso, análisis de factibilidad con proyección social y económica, y documentación de seguimiento de los trámites actuales.');

-- Los avances se imputan al hito por nombre. Si algún hito no calza se inserta igual, como
-- avance general: perder la imputación es preferible a perder la entrada de bitácora.
INSERT INTO ProyectoAvances (ProyectoId, EntregableId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo)
SELECT @pid,
       (SELECT TOP 1 h.Id FROM ProyectoEntregables h WHERE h.ProyectoId = @pid AND h.Nombre = a.hito),
       a.fecha,
       @actor,
       a.descripcion,
       a.porcentaje,
       a.bloqueo
FROM @avances a
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoAvances x
    WHERE x.ProyectoId = @pid AND x.Descripcion = a.descripcion)
ORDER BY a.orden;

DECLARE @nuevos int = @@ROWCOUNT;

-- ── Estado de los hitos ─────────────────────────────────────────────────────
-- Solo el que la evidencia mueve: se compartió el documento de adhesión al alojamiento, así que
-- ese hito arrancó. El levantamiento y el análisis legal ya estaban en proceso y siguen ahí:
-- ninguno está cumplido mientras falte la documentación de FOSOVI.
UPDATE ProyectoEntregables
SET Estado = N'EnProceso'
WHERE ProyectoId = @pid
  AND Nombre = N'Alojamiento temporal en servidores de la DIGER'
  AND Estado = N'Pendiente';

-- ── Snapshot del proyecto ───────────────────────────────────────────────────
UPDATE Proyectos
SET AvancePct = @avancePct,
    UpdatedAt = @ahora,
    UpdatedBy = @actor
WHERE Id = @pid AND AvancePct < @avancePct;

-- ── Riesgo: el bloqueo ya ocurrió ───────────────────────────────────────────
-- Se registra como Materializado, no como Abierto: la demora en la entrega no es algo que
-- podría pasar, es lo que está pasando y ya movió el cronograma.
IF NOT EXISTS (SELECT 1 FROM ProyectoRiesgos r
               WHERE r.ProyectoId = @pid AND r.Descripcion LIKE N'FOSOVI no completa la documentación%')
    INSERT INTO ProyectoRiesgos
        (ProyectoId, Descripcion, Categoria, Probabilidad, Impacto, Estrategia, Estado,
         Mitigacion, FechaDeteccion, FechaRevision, RegistradoPor, RegistradoEn)
    VALUES
        (@pid,
         N'FOSOVI no completa la documentación requerida (proceso interno, factibilidad y seguimiento de trámites) y el levantamiento no puede cerrarse.',
         N'Institucional', N'Alta', N'Alta', N'Mitigar', N'Materializado',
         N'Reunión con el equipo legal de FOSOVI para destrabar el estatus legal, y seguimiento semanal de las tres entregas pendientes a través de SEFIN.',
         '2026-07-03', '2026-07-15', @actor, @ahora);

-- ── Auditoría ───────────────────────────────────────────────────────────────
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
VALUES (@pid, N'ModificacionFicha',
        N'Carga de la historia del proyecto desde los hilos de correo DIGER–FOSOVI (17/06 al 07/07/2026): '
          + CAST(@nuevos AS nvarchar(10)) + N' reporte(s) de avance y el riesgo institucional por la documentación pendiente.',
        @actor, @ahora);

-- ── Verificación ────────────────────────────────────────────────────────────
SELECT p.Codigo, p.Nombre, p.Estado, p.AvancePct,
       (SELECT COUNT(*) FROM ProyectoAvances a WHERE a.ProyectoId = p.Id) AS avances,
       (SELECT COUNT(*) FROM ProyectoAvances a WHERE a.ProyectoId = p.Id AND a.Bloqueo IS NOT NULL) AS con_bloqueo,
       (SELECT COUNT(*) FROM ProyectoRiesgos r WHERE r.ProyectoId = p.Id) AS riesgos
FROM Proyectos p WHERE p.Id = @pid;

SELECT CONVERT(varchar(16), a.Fecha, 120) AS fecha,
       ISNULL(h.Nombre, N'— avance general —')  AS hito,
       a.PorcentajeReportado                    AS pct,
       CASE WHEN a.Bloqueo IS NULL THEN '' ELSE 'con bloqueo' END AS bloqueo,
       LEFT(a.Descripcion, 90)                  AS detalle
FROM ProyectoAvances a
LEFT JOIN ProyectoEntregables h ON h.Id = a.EntregableId
WHERE a.ProyectoId = @pid
ORDER BY a.Fecha;

SELECT h.Orden, h.Nombre, h.Estado FROM ProyectoEntregables h WHERE h.ProyectoId = @pid ORDER BY h.Orden;

COMMIT TRANSACTION;
PRINT 'Historia de FOSOVI cargada.';


/*  OPCIONAL — asignar responsable
    ─────────────────────────────────────────────────────────────────────────
    El proyecto no tiene responsable, y eso tiene una consecuencia concreta: sin él nadie puede
    reordenar sus hitos ni corregir su bitácora, ni siquiera un administrador. Quien lleva la
    relación en todo el hilo es Henry Cardona. Queda comentado porque asignar responsable es una
    decisión de gestión, no un dato que se derive de los correos.

    UPDATE Proyectos
    SET ResponsableId = (SELECT TOP 1 Id FROM Usuarios WHERE Correo = N'hcardona@diger.gob.hn'),
        Responsable   = N'Henry Cardona'
    WHERE Nombre LIKE N'%FOSOVI%' AND IsDeleted = 0 AND ResponsableId IS NULL;
*/
