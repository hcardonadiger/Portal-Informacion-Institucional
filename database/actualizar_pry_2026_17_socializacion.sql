/*
================================================================================
  Pone al día PRY-2026-17 «Portal de Digitalización de Trámites» al entrar en
  la etapa de socialización para el lanzamiento interno.
================================================================================

  CÓMO SE USA
  -----------
  1. Ejecútelo tal cual. Arranca en MODO SIMULACIÓN: no cambia nada, solo informa
     cómo quedaría la ficha y qué avance daría.
  2. Revise el listado.
  3. Cambie @soloSimular a 0 y vuelva a ejecutarlo para aplicar.

  Requiere las tablas Proyectos, ProyectoEntregables, ProyectoActividades y
  BitacoraProyecto. Si falta alguna, se detiene y lo dice.

  QUÉ PROBLEMA RESUELVE
  ---------------------
  La ficha del proyecto se quedó en el 2026-08-22. Entre el 23 y el 27 de agosto
  se hizo trabajo sustancial que no está registrado —lo dice el historial de git—
  y el portafolio se va a mirar en la socialización con esa foto vieja:

    · API pública v1 separada y documentada desde el código   (24–25 ago)
    · Integración PD–SIGER–HondurasÁgil, en curso             (24–25 ago)
    · Módulo de proyectos: EDT, riesgos, interesados, GANTT   (25–27 ago)

  Además el proyecto no tenía fecha de cierre planificada —por eso encendía la
  señal «Sin línea base»— y ninguna actividad tenía fechas plan, así que el
  cronograma GANTT no tenía de dónde dibujar barras.

  QUÉ HACE
  --------
  1. Agrega las cuatro actividades faltantes (API, SIGER, estructura de
     proyectos y socialización) y reordena las tres finales para que la
     secuencia siga leyéndose en orden de ejecución.
  2. Carga fechas planificadas en las actividades abiertas (órdenes 15 a 20),
     aterrizando en el cierre del 2026-09-30.
  3. Fija FechaFinPlan del proyecto en 2026-09-30.
  4. Recalcula AvancePct con la misma regla del dominio —promedio simple de las
     actividades no canceladas, redondeo AwayFromZero— en vez de escribir un
     número a mano.
  5. Deja constancia en BitacoraProyecto, que es lo que habría escrito el
     dominio si el cambio hubiera entrado por el portal.

  SOBRE EL AVANCE
  ---------------
  Baja de 84 % a 81 %. No es un retroceso: el denominador creció al registrar
  trabajo que está en curso. Es el número honesto para presentar.

  ES IDEMPOTENTE
  --------------
  Las actividades se insertan por nombre solo si no existen, así que volver a
  correrlo no duplica nada.

================================================================================
*/

SET NOCOUNT ON;

-- sqlcmd arranca con QUOTED_IDENTIFIER en OFF y las tablas del portal tienen
-- índices filtrados; sin esto, el primer UPDATE revienta con el error 1934.
SET QUOTED_IDENTIFIER ON;

DECLARE @soloSimular bit = 1;          -- <<< cambie a 0 para aplicar

DECLARE @proyectoId   int  = 17;
DECLARE @cierrePlan   date = '2026-09-30';
DECLARE @actor        nvarchar(200) = N'Henry Alexis Ortez Banegas';
DECLARE @ahora        datetime2 = SYSUTCDATETIME();

-- ── Verificación de esquema ────────────────────────────────────────────────
IF OBJECT_ID('Proyectos')            IS NULL OR OBJECT_ID('ProyectoEntregables') IS NULL
OR OBJECT_ID('ProyectoActividades')  IS NULL OR OBJECT_ID('BitacoraProyecto')    IS NULL
BEGIN
    RAISERROR('Falta alguna de las tablas requeridas (Proyectos, ProyectoEntregables, ProyectoActividades, BitacoraProyecto). No se hizo nada.', 16, 1);
    RETURN;
END

DECLARE @entregableId int =
    (SELECT TOP 1 Id FROM ProyectoEntregables WHERE ProyectoId = @proyectoId ORDER BY Orden);

IF @entregableId IS NULL
BEGIN
    RAISERROR('El proyecto 17 no tiene entregables. No se hizo nada.', 16, 1);
    RETURN;
END

BEGIN TRAN;

-- ── 1. Reordenar las tres finales para hacerle sitio a lo nuevo ────────────
--    Validación interna 14→17, Aprobación 15→19, Puesta en producción 16→20.
--    El índice (EntregableId, Orden) no es único, así que el reordenamiento no
--    necesita pasos intermedios.
UPDATE ProyectoActividades SET Orden = 17, UpdatedAt = @ahora, UpdatedBy = @actor
WHERE EntregableId = @entregableId AND Nombre = N'Validación interna del portal';

UPDATE ProyectoActividades SET Orden = 19, UpdatedAt = @ahora, UpdatedBy = @actor
WHERE EntregableId = @entregableId AND Nombre = N'Aprobación de la coordinación';

UPDATE ProyectoActividades SET Orden = 20, UpdatedAt = @ahora, UpdatedBy = @actor
WHERE EntregableId = @entregableId AND Nombre = N'Puesta en producción';

-- ── 2. Las actividades que faltaban ────────────────────────────────────────
--    Se insertan por nombre solo si no existen: correrlo dos veces no duplica.
DECLARE @nuevas TABLE (
    Orden int, Nombre nvarchar(400), Descripcion nvarchar(1000),
    Estado nvarchar(40), AvancePct int,
    IniPlan date, FinPlan date, IniReal date, FinReal date);

INSERT INTO @nuevas VALUES
 (14, N'API pública v1 documentada',
      N'API pública separada del código de PortalDigital, con la documentación generada desde el propio código.',
      N'Completada', 100, NULL, NULL, '2026-08-24', '2026-08-25'),

 (15, N'Integración con SIGER y HondurasÁgil',
      N'Identidad estable del trámite, archivado del SIGER original, escritura del expediente hacia la ficha con historial, bloqueo condicional y publicación manual en HondurasÁgil. Plan por fases, decisiones D-10 a D-23.',
      N'EnProceso', 60, '2026-08-24', '2026-09-12', '2026-08-24', NULL),

 (16, N'Estructura de proyectos: EDT, riesgos, interesados y GANTT',
      N'Descomposición en entregables y actividades, dependencias, registro de riesgos e interesados, repositorio documental, auditoría y cronograma GANTT.',
      N'Completada', 100, NULL, NULL, '2026-08-25', '2026-08-27'),

 (18, N'Socialización para lanzamiento interno',
      N'Presentación del portal a las áreas, recolección de observaciones y ajustes previos a la aprobación.',
      N'EnProceso', 10, '2026-08-28', '2026-09-12', '2026-08-28', NULL);

INSERT INTO ProyectoActividades
    (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan,
     FechaInicioReal, FechaFinReal, AvancePct, Estado, CreatedAt, CreatedBy)
SELECT @entregableId, n.Orden, n.Nombre, n.Descripcion, n.IniPlan, n.FinPlan,
       n.IniReal, n.FinReal, n.AvancePct, n.Estado, @ahora, @actor
FROM @nuevas n
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoActividades a
    WHERE a.EntregableId = @entregableId AND a.Nombre = n.Nombre);

-- ── 3. Fechas plan de las actividades abiertas que ya existían ─────────────
--    Sin esto el GANTT del proyecto sale vacío en la presentación.
UPDATE ProyectoActividades
   SET FechaInicioPlan = '2026-08-23', FechaFinPlan = '2026-09-05',
       UpdatedAt = @ahora, UpdatedBy = @actor
 WHERE EntregableId = @entregableId AND Nombre = N'Validación interna del portal';

UPDATE ProyectoActividades
   SET FechaInicioPlan = '2026-09-15', FechaFinPlan = '2026-09-22',
       UpdatedAt = @ahora, UpdatedBy = @actor
 WHERE EntregableId = @entregableId AND Nombre = N'Aprobación de la coordinación';

UPDATE ProyectoActividades
   SET FechaInicioPlan = '2026-09-23', FechaFinPlan = @cierrePlan,
       UpdatedAt = @ahora, UpdatedBy = @actor
 WHERE EntregableId = @entregableId AND Nombre = N'Puesta en producción';

-- ── 4. Avance recalculado, no escrito a mano ───────────────────────────────
--    Misma regla que Proyecto.RecalcularAvance / EntregableProyecto.AvanceCalculado:
--    promedio simple de las actividades no canceladas, redondeo AwayFromZero.
DECLARE @avance int = (
    SELECT CAST(ROUND(AVG(CAST(AvancePct AS float)), 0) AS int)
      FROM ProyectoActividades
     WHERE EntregableId = @entregableId AND Estado <> N'Cancelada');

UPDATE Proyectos
   SET AvancePct    = @avance,
       FechaFinPlan = @cierrePlan,
       UpdatedAt    = @ahora,
       UpdatedBy    = @actor
 WHERE Id = @proyectoId;

-- ── 5. Constancia en bitácora ──────────────────────────────────────────────
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
VALUES
 (@proyectoId, N'ModificacionEstructura',
  N'Puesta al día para la socialización: se registran API pública v1, integración SIGER–HondurasÁgil (en curso) y estructura de proyectos con GANTT; se agrega la actividad de socialización para lanzamiento interno.',
  @actor, @ahora),
 (@proyectoId, N'ModificacionFicha',
  N'Se fija el cierre planificado en ' + CONVERT(nvarchar(10), @cierrePlan, 23)
    + N' y se cargan fechas plan en las actividades abiertas. Avance recalculado a '
    + CAST(@avance AS nvarchar(10)) + N' %.',
  @actor, @ahora);

-- ── Informe ────────────────────────────────────────────────────────────────
SELECT Orden, Nombre, Estado, AvancePct AS Pct,
       FechaInicioPlan AS IniPlan, FechaFinPlan AS FinPlan, FechaFinReal AS FinReal
  FROM ProyectoActividades
 WHERE EntregableId = @entregableId
 ORDER BY Orden;

SELECT Codigo, Estado, AvancePct AS AvanceNuevo, FechaInicioPlan, FechaFinPlan
  FROM Proyectos WHERE Id = @proyectoId;

IF @soloSimular = 1
BEGIN
    ROLLBACK TRAN;
    PRINT '';
    PRINT '*** MODO SIMULACIÓN: nada se guardó. Ponga @soloSimular = 0 para aplicar. ***';
END
ELSE
BEGIN
    COMMIT TRAN;
    PRINT '';
    PRINT '*** Cambios aplicados. ***';
END
