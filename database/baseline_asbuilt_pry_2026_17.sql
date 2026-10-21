/*
================================================================================
  Línea base «as-built» de PRY-2026-17: las fechas tal como se dieron.
================================================================================

  CÓMO SE USA
  -----------
  1. Ejecútelo tal cual. Arranca en MODO SIMULACIÓN: no cambia nada, solo informa
     cómo quedaría el cronograma.
  2. Revise el listado.
  3. Cambie @soloSimular a 0 y vuelva a ejecutarlo para aplicar.

  Corre después de:
    · actualizar_pry_2026_17_socializacion.sql
    · completar_pry_2026_17_modulos_faltantes.sql

  POR QUÉ EXISTE
  --------------
  El proyecto se ejecutó sin planificación previa: no hubo fechas comprometidas
  contra las cuales medir. Esta es una carga inicial, así que la línea base se
  establece ahora desde lo efectivamente ejecutado —un baseline as-built— en vez
  de dejar el cronograma vacío esperando un plan que nunca existió.

  Consecuencia a tener presente: al quedar plan = real, las diecinueve
  actividades cerradas se ven «a tiempo» por construcción. No es un logro
  medido, es la definición de la línea base. Cualquier lectura de desempeño
  contra plan solo tiene sentido de aquí en adelante, sobre las actividades
  abiertas (18, 21 a 24), que sí llevan fechas comprometidas de verdad.

  QUÉ HACE
  --------
  Sobre las actividades COMPLETADAS, y solo sobre ellas:
    · FechaInicioReal = cierre de la actividad completada anterior
    · FechaInicioPlan = FechaInicioReal
    · FechaFinPlan    = FechaFinReal (que ya estaba)

  Las actividades abiertas no se tocan: sus fechas plan son compromisos reales
  hacia adelante y no deben mezclarse con la reconstrucción del pasado.

  EL ÚNICO DATO INFERIDO
  ----------------------
  El inicio. No está registrado en ninguna parte y no se puede recuperar del
  repositorio: éste se sembró con dos volcados masivos —187 archivos el 26-06 y
  427 el 03-07—, así que el historial devuelve esas dos fechas para todo lo
  anterior y no distingue cuándo empezó cada módulo.

  Se reconstruye encadenando: cada actividad arranca el día en que cerró la
  anterior. Para un desarrollo secuencial el lapso resultante es el tiempo real
  que tomó cada frente; lo inferido es el día exacto del relevo, no la duración.
  La primera arranca en el inicio del proyecto.

  Donde dos actividades cerraron el mismo día (07-08, 08-13 y 08-27) la segunda
  queda con una barra de un día. Es correcto: cerraron juntas.

  ES IDEMPOTENTE
  --------------
  Recalcula desde FechaFinReal en cada corrida, así que volver a ejecutarlo da
  el mismo resultado. No inserta ni borra nada.

================================================================================
*/

SET NOCOUNT ON;

-- sqlcmd arranca con QUOTED_IDENTIFIER en OFF y las tablas del portal tienen
-- índices filtrados; sin esto, el UPDATE revienta con el error 1934.
SET QUOTED_IDENTIFIER ON;

DECLARE @soloSimular bit = 1;          -- <<< cambie a 0 para aplicar

DECLARE @proyectoId int = 17;
DECLARE @actor nvarchar(200) = N'Henry Alexis Ortez Banegas';
DECLARE @ahora datetime2 = SYSUTCDATETIME();

IF OBJECT_ID('ProyectoActividades') IS NULL OR OBJECT_ID('BitacoraProyecto') IS NULL
BEGIN
    RAISERROR('Faltan tablas requeridas. No se hizo nada.', 16, 1);
    RETURN;
END

DECLARE @entregableId int =
    (SELECT TOP 1 Id FROM ProyectoEntregables WHERE ProyectoId = @proyectoId ORDER BY Orden);

DECLARE @inicioProyecto date =
    (SELECT FechaInicioPlan FROM Proyectos WHERE Id = @proyectoId);

IF @entregableId IS NULL OR @inicioProyecto IS NULL
BEGIN
    RAISERROR('El proyecto 17 no tiene entregable o no tiene fecha de inicio. No se hizo nada.', 16, 1);
    RETURN;
END

-- Una actividad completada sin cierre real no tiene de dónde reconstruirse.
-- Mejor detenerse que inventarle una fecha.
DECLARE @sinCierre int = (
    SELECT COUNT(*) FROM ProyectoActividades
     WHERE EntregableId = @entregableId AND Estado = N'Completada' AND FechaFinReal IS NULL);

IF @sinCierre > 0
BEGIN
    RAISERROR('Hay %d actividad(es) completadas sin FechaFinReal. No se cambió nada.', 16, 1, @sinCierre);
    RETURN;
END

BEGIN TRAN;

-- ── La cadena ──────────────────────────────────────────────────────────────
--    LAG sobre las completadas en orden de secuencia: el inicio de cada una es
--    el cierre de la anterior. La primera arranca en el inicio del proyecto.
;WITH completadas AS (
    SELECT Id, Orden, FechaFinReal,
           LAG(FechaFinReal) OVER (ORDER BY Orden) AS CierreAnterior
      FROM ProyectoActividades
     WHERE EntregableId = @entregableId AND Estado = N'Completada'
)
UPDATE a
   SET a.FechaInicioReal = ISNULL(c.CierreAnterior, @inicioProyecto),
       a.FechaInicioPlan = ISNULL(c.CierreAnterior, @inicioProyecto),
       a.FechaFinPlan    = c.FechaFinReal,
       a.UpdatedAt       = @ahora,
       a.UpdatedBy       = @actor
  FROM ProyectoActividades a
  JOIN completadas c ON c.Id = a.Id;

-- ── Constancia ─────────────────────────────────────────────────────────────
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
VALUES (@proyectoId, N'ModificacionFicha',
  N'Línea base as-built: el proyecto se ejecutó sin planificación previa, así que las fechas plan de las actividades ya cerradas se fijan con las de ejecución real. El inicio de cada una se reconstruye como el cierre de la anterior; no está registrado en ningún lado y el repositorio no puede darlo. Las actividades abiertas conservan sus compromisos hacia adelante.',
  @actor, @ahora);

-- ── Informe ────────────────────────────────────────────────────────────────
SELECT Orden, Nombre, Estado, AvancePct AS Pct,
       FechaInicioPlan AS IniPlan, FechaFinPlan AS FinPlan,
       DATEDIFF(day, FechaInicioPlan, FechaFinPlan) AS Dias
  FROM ProyectoActividades
 WHERE EntregableId = @entregableId
 ORDER BY Orden;

SELECT 'Actividades con barra dibujable: ' +
       CAST(SUM(CASE WHEN FechaInicioPlan IS NOT NULL AND FechaFinPlan IS NOT NULL THEN 1 ELSE 0 END) AS varchar)
       + ' de ' + CAST(COUNT(*) AS varchar)
  FROM ProyectoActividades WHERE EntregableId = @entregableId;

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
