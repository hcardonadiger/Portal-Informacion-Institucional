-- ============================================================================
-- Proyecto: Socialización y coordinación institucional — Mesa Técnica de Simplificación
--
-- Origen  : artifact «Hoja de Ruta de Socialización» (12 semanas, 3 fases, 4 frentes)
--           https://claude.ai/code/artifact/8804fecc-49a2-4ad1-abfd-a843780093e5
-- Contenido: 1 proyecto, 3 entregables, 25 actividades, 4 riesgos.
--
-- Idempotente: el proyecto se reconoce por Nombre, los entregables por proyecto + nombre,
-- las actividades por entregable + nombre y los riesgos por proyecto + descripción.
-- Correrlo dos veces actualiza; no duplica.
--
-- SIN FECHAS, a propósito. La hoja de ruta dice de forma explícita que el horizonte de 12
-- semanas es un punto de partida y que las fechas se ajustan «a la confirmación de la jornada
-- con autoridades». Poner fechas acá sería inventarlas. La ventana de cada bloque (S1–S4, S5,
-- S6–S12) queda escrita en la descripción del entregable; el cronograma se llena en el portal
-- cuando se fije la fecha de la jornada.
--
-- SIN INTERESADOS. Registrar a alguien como interesado le abre el proyecto completo, y el
-- circuito exige que sea usuario del portal. Las autoridades, la Representante del BID y los
-- enlaces institucionales no tienen cuenta: se agregan desde el portal cuando corresponda.
--
-- OJO: escribe Estado directo, sin pasar por Proyecto.CambiarEstado, así que no valida las
-- transiciones ni dispara el evento que notifica al responsable. Para un alta es lo que se
-- quiere; para mover el estado de un proyecto vivo, el portal.
--
--   sqlcmd -S localhost -d DigerTramitesEstado -i proyecto_socializacion_mesa_simplificacion.sql
-- ============================================================================

-- QUOTED_IDENTIFIER tiene que ir encendido: Proyectos.Codigo lleva un índice único filtrado
-- (WHERE IsDeleted = 0) y SQL Server rechaza el INSERT si la opción viene apagada, que es
-- justo como la deja sqlcmd por omisión.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET XACT_ABORT ON;
SET NOCOUNT ON;
GO

BEGIN TRANSACTION;

DECLARE @actor    nvarchar(300) = N'Carga desde hoja de ruta de socialización';
DECLARE @ahora    datetime2     = SYSUTCDATETIME();
DECLARE @hoy      date          = CAST(SYSDATETIME() AS date);
DECLARE @prefijo  nvarchar(20)  = N'PRY-2026-';
DECLARE @pid int, @eid int, @nuevo bit, @n int;

DECLARE @nombre nvarchar(300) = N'Socialización y coordinación institucional — Mesa Técnica de Simplificación';

-- ── Proyecto ────────────────────────────────────────────────────────────────
SET @pid   = (SELECT TOP 1 Id FROM Proyectos WHERE Nombre = @nombre AND IsDeleted = 0);
SET @nuevo = CASE WHEN @pid IS NULL THEN 1 ELSE 0 END;

IF @nuevo = 1
BEGIN
    -- Correlativo por año, igual que el portal y que el importador de plantillas.
    SET @n = (SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(Codigo, LEN(@prefijo) + 1, 10) AS int)), 0) + 1
              FROM Proyectos WHERE Codigo LIKE @prefijo + N'%');

    INSERT INTO Proyectos (IsDeleted, Codigo, Nombre, Objetivo, InstitucionId, AreaId, UnidadId,
                           ResponsableId, Responsable, Prioridad, Estado,
                           FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal,
                           AvancePct, CreatedAt, CreatedBy)
    VALUES (0, @prefijo + FORMAT(@n, '00'), @nombre,
            N'Socializar los resultados de la Mesa Técnica de Simplificación Administrativa, preparar a las instituciones y conformar mesas técnicas multi-área, en paralelo al trabajo del BID sobre el inventario de procesos. Cubre el antes, el durante y el después de la entrega de resultados a las autoridades. Horizonte de 12 semanas, comprimible a 8 solapando preparación y cascada sectorial.',
            N'DIGER', NULL, NULL,
            NULL, NULL, N'Alta', N'Planificado',
            NULL, NULL, NULL, NULL,
            0, @ahora, @actor);

    SET @pid = SCOPE_IDENTITY();
END
ELSE
BEGIN
    -- Solo el objetivo, como en el resto de los scripts: Estado, Prioridad, fechas y AvancePct
    -- son estado vivo que mueven la app y los avances, y pisarlos revierte lo que el
    -- responsable haya registrado.
    UPDATE Proyectos SET
        Objetivo  = N'Socializar los resultados de la Mesa Técnica de Simplificación Administrativa, preparar a las instituciones y conformar mesas técnicas multi-área, en paralelo al trabajo del BID sobre el inventario de procesos. Cubre el antes, el durante y el después de la entrega de resultados a las autoridades. Horizonte de 12 semanas, comprimible a 8 solapando preparación y cascada sectorial.',
        UpdatedAt = @ahora,
        UpdatedBy = @actor
    WHERE Id = @pid;
END

-- ── Entregable 1 · Antes (S1–S4) ────────────────────────────────────────────
DECLARE @e1 nvarchar(300) = N'Enlaces designados + kit de socialización validado con BID';

IF NOT EXISTS (SELECT 1 FROM ProyectoEntregables WHERE ProyectoId = @pid AND Nombre = @e1)
    INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
    VALUES (@pid, 1, @e1,
            N'Fase «Antes» — semanas 1 a 4. Objetivo: instituciones informadas y sin sorpresas antes de la jornada con autoridades. Responsable: secretaría técnica de la Mesa Técnica + enlaces institucionales.',
            NULL, NULL, N'Pendiente', NULL, NULL, @ahora, @actor);
ELSE
    UPDATE ProyectoEntregables SET
        Orden = 1,
        Descripcion = N'Fase «Antes» — semanas 1 a 4. Objetivo: instituciones informadas y sin sorpresas antes de la jornada con autoridades. Responsable: secretaría técnica de la Mesa Técnica + enlaces institucionales.',
        UpdatedAt = @ahora, UpdatedBy = @actor
    WHERE ProyectoId = @pid AND Nombre = @e1;

SET @eid = (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = @pid AND Nombre = @e1);

-- Las actividades conservan el frente en el nombre: la hoja de ruta es una matriz de 4 frentes
-- por 3 fases, y aplanarla a una lista perdería la mitad de la información.
;WITH a(orden, nombre, descripcion) AS (
    SELECT 1,  N'Comunicación · Plan de socialización: audiencias, mensajes clave y canales', NULL
    UNION ALL SELECT 2,  N'Comunicación · Kit de materiales — resumen ejecutivo, FAQ, versión ejecutiva del observatorio', NULL
    UNION ALL SELECT 3,  N'Comunicación · Pre-briefings bilaterales con instituciones clave', NULL
    UNION ALL SELECT 4,  N'Preparación institucional · Mapeo de actores y designación de enlaces técnicos por institución', N'Designar enlace titular y suplente por institución, como mitigación del riesgo de rotación.'
    UNION ALL SELECT 5,  N'Preparación institucional · Taller de inducción metodológica a los enlaces', NULL
    UNION ALL SELECT 6,  N'Preparación institucional · Validación de hallazgos propios con cada institución', NULL
    UNION ALL SELECT 7,  N'Mesas técnicas · Diseño de la gobernanza — estructura, roles y periodicidad', N'Ejes de referencia: rectora/coordinación (quincenal), legal y normativa (quincenal), tecnología e interoperabilidad (quincenal), atención al ciudadano (mensual).'
    UNION ALL SELECT 8,  N'Mesas técnicas · Definición de ejes temáticos según hallazgos del inventario', NULL
    UNION ALL SELECT 9,  N'BID · Validación conjunta de mensajes, alcance y agenda del evento', NULL
    UNION ALL SELECT 10, N'BID · Ensayo de la presentación de resultados con el equipo técnico', NULL
)
MERGE ProyectoActividades AS d
USING (SELECT @eid AS EntregableId, orden, nombre, descripcion FROM a) AS o
    ON d.EntregableId = o.EntregableId AND d.Nombre = o.nombre
WHEN MATCHED THEN UPDATE SET d.Orden = o.orden, d.Descripcion = o.descripcion, d.UpdatedAt = @ahora, d.UpdatedBy = @actor
WHEN NOT MATCHED THEN
    INSERT (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
    VALUES (o.EntregableId, o.orden, o.nombre, o.descripcion, NULL, NULL, NULL, NULL, 0, N'Pendiente', NULL, NULL, @ahora, @actor);

-- ── Entregable 2 · Durante (S5) ─────────────────────────────────────────────
DECLARE @e2 nvarchar(300) = N'Acta de acuerdos y compromisos de la jornada';

IF NOT EXISTS (SELECT 1 FROM ProyectoEntregables WHERE ProyectoId = @pid AND Nombre = @e2)
    INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
    VALUES (@pid, 2, @e2,
            N'Fase «Durante» — semana 5, hito central. Objetivo: presentar resultados y sentar la ruta de trabajo con autoridades y BID. Responsable: Mesa Técnica de Simplificación + BID + autoridades. Agenda acordada: 10:00 apertura · 10:10 resultados y hallazgos · 10:35 oportunidades de simplificación e interoperabilidad · 10:55 intervención de la Sra. Julia Johannsen (Representante del BID para Honduras) · 11:20 siguientes pasos · 11:50 acuerdos y cierre.',
            NULL, NULL, N'Pendiente', NULL, NULL, @ahora, @actor);
ELSE
    UPDATE ProyectoEntregables SET
        Orden = 2,
        Descripcion = N'Fase «Durante» — semana 5, hito central. Objetivo: presentar resultados y sentar la ruta de trabajo con autoridades y BID. Responsable: Mesa Técnica de Simplificación + BID + autoridades. Agenda acordada: 10:00 apertura · 10:10 resultados y hallazgos · 10:35 oportunidades de simplificación e interoperabilidad · 10:55 intervención de la Sra. Julia Johannsen (Representante del BID para Honduras) · 11:20 siguientes pasos · 11:50 acuerdos y cierre.',
        UpdatedAt = @ahora, UpdatedBy = @actor
    WHERE ProyectoId = @pid AND Nombre = @e2;

SET @eid = (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = @pid AND Nombre = @e2);

;WITH a(orden, nombre, descripcion) AS (
    SELECT 1, N'Comunicación · Jornada de presentación de resultados a autoridades', N'Hito central de la hoja de ruta (semana 5).'
    UNION ALL SELECT 2, N'Comunicación · Comunicado y resumen ejecutivo a instituciones no asistentes', NULL
    UNION ALL SELECT 3, N'Preparación institucional · Registro de compromisos institucionales asumidos en la jornada', N'Los compromisos se registran como acuerdos de la reunión, para poder darles seguimiento desde el módulo de compromisos.'
    UNION ALL SELECT 4, N'Mesas técnicas · Presentación pública de la hoja de ruta de conformación de mesas', NULL
    UNION ALL SELECT 5, N'BID · Intervención de la Representante del BID para Honduras', NULL
    UNION ALL SELECT 6, N'BID · Coordinación de siguientes pasos con instituciones y BID', NULL
)
MERGE ProyectoActividades AS d
USING (SELECT @eid AS EntregableId, orden, nombre, descripcion FROM a) AS o
    ON d.EntregableId = o.EntregableId AND d.Nombre = o.nombre
WHEN MATCHED THEN UPDATE SET d.Orden = o.orden, d.Descripcion = o.descripcion, d.UpdatedAt = @ahora, d.UpdatedBy = @actor
WHEN NOT MATCHED THEN
    INSERT (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
    VALUES (o.EntregableId, o.orden, o.nombre, o.descripcion, NULL, NULL, NULL, NULL, 0, N'Pendiente', NULL, NULL, @ahora, @actor);

-- ── Entregable 3 · Después (S6–S12) ─────────────────────────────────────────
DECLARE @e3 nvarchar(300) = N'Planes de acción institucionales + mecanismo de monitoreo';

IF NOT EXISTS (SELECT 1 FROM ProyectoEntregables WHERE ProyectoId = @pid AND Nombre = @e3)
    INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
    VALUES (@pid, 3, @e3,
            N'Fase «Después» — semanas 6 a 12. Objetivo: mesas técnicas multi-área activas y ruta de acompañamiento BID definida. Responsable: mesas técnicas por institución + Mesa Técnica + BID.',
            NULL, NULL, N'Pendiente', NULL, NULL, @ahora, @actor);
ELSE
    UPDATE ProyectoEntregables SET
        Orden = 3,
        Descripcion = N'Fase «Después» — semanas 6 a 12. Objetivo: mesas técnicas multi-área activas y ruta de acompañamiento BID definida. Responsable: mesas técnicas por institución + Mesa Técnica + BID.',
        UpdatedAt = @ahora, UpdatedBy = @actor
    WHERE ProyectoId = @pid AND Nombre = @e3;

SET @eid = (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = @pid AND Nombre = @e3);

;WITH a(orden, nombre, descripcion) AS (
    SELECT 1, N'Comunicación · Cascada de socialización sectorial — resultados propios por institución', NULL
    UNION ALL SELECT 2, N'Comunicación · Boletín periódico de avances y «quick wins»', NULL
    UNION ALL SELECT 3, N'Preparación institucional · Entrega del paquete de resultados institucional', NULL
    UNION ALL SELECT 4, N'Preparación institucional · Capacitación en el uso del observatorio como herramienta de seguimiento', NULL
    UNION ALL SELECT 5, N'Mesas técnicas · Conformación formal de mesas por institución o clúster de trámites', N'Formalizar cada mesa mediante instrucción de la autoridad institucional, como mitigación del riesgo de mesas sin mandato.'
    UNION ALL SELECT 6, N'Mesas técnicas · Planes de acción institucionales, priorizados por alcance', NULL
    UNION ALL SELECT 7, N'Mesas técnicas · Instalación de la mesa ampliada de coordinación interinstitucional', NULL
    UNION ALL SELECT 8, N'BID · Definición conjunta de la ruta de acompañamiento — fase siguiente', NULL
    UNION ALL SELECT 9, N'BID · Diseño del mecanismo de monitoreo y reporte de avances', NULL
)
MERGE ProyectoActividades AS d
USING (SELECT @eid AS EntregableId, orden, nombre, descripcion FROM a) AS o
    ON d.EntregableId = o.EntregableId AND d.Nombre = o.nombre
WHEN MATCHED THEN UPDATE SET d.Orden = o.orden, d.Descripcion = o.descripcion, d.UpdatedAt = @ahora, d.UpdatedBy = @actor
WHEN NOT MATCHED THEN
    INSERT (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
    VALUES (o.EntregableId, o.orden, o.nombre, o.descripcion, NULL, NULL, NULL, NULL, 0, N'Pendiente', NULL, NULL, @ahora, @actor);

-- ── Riesgos ─────────────────────────────────────────────────────────────────
-- Los cuatro vienen con su mitigación desde la hoja de ruta. La probabilidad y el impacto no
-- están en la fuente: son una valoración inicial para que el registro sirva, y se ajustan en
-- el portal. FechaDeteccion es hoy porque es cuando se registran, no cuando se anticiparon.
;WITH r(descripcion, categoria, probabilidad, impacto, mitigacion) AS (
    SELECT N'Rotación del enlace institucional interrumpe la continuidad del proceso.',
           N'Institucional', N'Media', N'Alta',
           N'Designar enlace titular y suplente por institución desde el arranque.'
    UNION ALL SELECT N'Los hallazgos se perciben como señalamiento en vez de oportunidad de mejora.',
           N'Operativo', N'Media', N'Alta',
           N'Enmarcar la comunicación en acompañamiento y oportunidades, no en señalamiento.'
    UNION ALL SELECT N'Las mesas técnicas se instalan sin mandato ni respaldo de alta dirección.',
           N'Institucional', N'Media', N'Alta',
           N'Formalizar la creación de cada mesa mediante instrucción de la autoridad institucional.'
    UNION ALL SELECT N'La agenda institucional ya cargada compite con la cadencia de las mesas.',
           N'Externo', N'Alta', N'Media',
           N'Alinear la cadencia de mesas con la agenda ya definida por la Mesa Técnica y el BID.'
)
MERGE ProyectoRiesgos AS d
USING (SELECT @pid AS ProyectoId, descripcion, categoria, probabilidad, impacto, mitigacion FROM r) AS o
    ON d.ProyectoId = o.ProyectoId AND d.Descripcion = o.descripcion
WHEN MATCHED THEN UPDATE SET
    d.Categoria = o.categoria, d.Probabilidad = o.probabilidad, d.Impacto = o.impacto,
    d.Estrategia = N'Mitigar', d.Mitigacion = o.mitigacion
WHEN NOT MATCHED THEN
    INSERT (ProyectoId, Descripcion, Categoria, Probabilidad, Impacto, Estrategia, Estado,
            Mitigacion, ResponsableId, Responsable, FechaDeteccion, FechaRevision, RegistradoPor, RegistradoEn)
    VALUES (o.ProyectoId, o.descripcion, o.categoria, o.probabilidad, o.impacto, N'Mitigar', N'Abierto',
            o.mitigacion, NULL, NULL, @hoy, NULL, @actor, @ahora);

-- ── Bitácora ────────────────────────────────────────────────────────────────
-- Sin esta entrada, nadie puede reconstruir después de dónde salió el proyecto.
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
VALUES (@pid, N'ModificacionEstructura',
        N'Alta desde la hoja de ruta de socialización de la Mesa Técnica de Simplificación Administrativa: 3 entregables (antes / durante / después), 25 actividades en 4 frentes y 4 riesgos. Sin fechas: el horizonte de 12 semanas se ancla cuando se confirme la jornada con autoridades.',
        @actor, @ahora);

-- ── Verificación antes de confirmar ─────────────────────────────────────────
DECLARE @nEnt int = (SELECT COUNT(*) FROM ProyectoEntregables WHERE ProyectoId = @pid);
DECLARE @nAct int = (SELECT COUNT(*) FROM ProyectoActividades a
                     JOIN ProyectoEntregables e ON e.Id = a.EntregableId WHERE e.ProyectoId = @pid);
DECLARE @nRie int = (SELECT COUNT(*) FROM ProyectoRiesgos WHERE ProyectoId = @pid);

IF @nEnt <> 3 OR @nAct <> 25 OR @nRie <> 4
BEGIN
    RAISERROR('Conteo inesperado: %d entregables (3), %d actividades (25), %d riesgos (4). Se revierte.', 16, 1, @nEnt, @nAct, @nRie);
    ROLLBACK TRANSACTION;
    RETURN;
END

COMMIT TRANSACTION;

SELECT Codigo, Nombre, Estado, Prioridad FROM Proyectos WHERE Id = @pid;
SELECT e.Orden, e.Nombre AS Entregable, COUNT(a.Id) AS Actividades
FROM ProyectoEntregables e
LEFT JOIN ProyectoActividades a ON a.EntregableId = e.Id
WHERE e.ProyectoId = @pid
GROUP BY e.Orden, e.Nombre
ORDER BY e.Orden;
GO
