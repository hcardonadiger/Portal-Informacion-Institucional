/*
================================================================================
  Pone al día la ficha del proyecto «Portal de Digitalización de Trámites»
  para la socialización del lanzamiento interno.
================================================================================

  Unifica en un solo archivo lo que antes eran tres scripts. Reemplaza a:
      actualizar_pry_2026_17_socializacion.sql
      completar_pry_2026_17_modulos_faltantes.sql
      baseline_asbuilt_pry_2026_17.sql

  CÓMO SE USA
  -----------
  1. Ejecútelo tal cual. Arranca en MODO SIMULACIÓN: no cambia nada, solo informa
     cómo quedaría la ficha.
  2. Revise el listado y el resumen del final.
  3. Cambie @soloSimular a 0 y vuelva a ejecutarlo para aplicar.

  El proyecto se busca por CÓDIGO, no por Id: el Id cambia entre ambientes.

  REQUISITO PREVIO
  ----------------
  El proyecto debe tener UN SOLO entregable. Si en su ambiente todavía está la
  estructura «espejo» —un entregable por cada actividad, con el mismo nombre—
  corra antes aplanar_entregables_espejo.sql. El script se detiene y se lo dice
  en vez de operar sobre una estructura que no entiende.

  QUÉ HACE, EN CUATRO PASOS
  -------------------------
  1. Registra los frentes de trabajo del 23 al 27 de agosto que no estaban en la
     ficha: API pública v1, integración SIGER–HondurasÁgil (en curso) y la
     estructura de proyectos con GANTT. Agrega la socialización como actividad.

  2. Registra cuatro módulos entregados que nadie había cargado, detectados al
     comparar el árbol del repositorio contra la ficha: contactos, biblioteca de
     recursos, migración del portal legado y manual de usuario.
     Se deja fuera Application/AI: es solo una interfaz sin implementación.

  3. Reordena la secuencia completa en orden cronológico y fija el cierre
     planificado del proyecto.

  4. Establece la línea base «as-built»: el proyecto se ejecutó sin planificación
     previa, así que las fechas plan de lo ya cerrado se fijan con las de
     ejecución real.

  EL ÚNICO DATO INFERIDO
  ----------------------
  El día en que arrancó cada actividad histórica. No está registrado en ninguna
  parte y el repositorio no puede darlo: se sembró con dos volcados masivos
  —187 archivos el 26-06 y 427 el 03-07—, así que el historial devuelve esas dos
  fechas para todo lo anterior y no distingue cuándo empezó cada módulo.

  Se reconstruye encadenando: cada actividad arranca el día en que cerró la
  anterior. Para un desarrollo secuencial el lapso resultante es el tiempo real
  que tomó cada frente; lo inferido es el día del relevo, no la duración.

  QUÉ ESPERAR DEL RESULTADO
  -------------------------
  · 24 actividades, 19 cerradas, avance 84 %.
  · Las 24 con barra dibujable en el cronograma.
  · Las 19 cerradas se ven «a tiempo» POR CONSTRUCCIÓN: es la definición de la
    línea base, no un resultado medido. El desempeño contra plan solo significa
    algo de aquí en adelante, sobre las cinco actividades abiertas.

  ES IDEMPOTENTE
  --------------
  Las actividades se insertan solo si no existen, el orden se aplica por nombre
  y la línea base se recalcula desde FechaFinReal en cada corrida. Volver a
  ejecutarlo da el mismo resultado; no duplica ni descoloca nada.

================================================================================
*/

SET NOCOUNT ON;

-- sqlcmd arranca con QUOTED_IDENTIFIER en OFF y las tablas del portal tienen
-- índices filtrados; sin esto, el primer UPDATE revienta con el error 1934.
SET QUOTED_IDENTIFIER ON;

-- ── Parámetros ─────────────────────────────────────────────────────────────
DECLARE @soloSimular bit           = 1;                 -- <<< cambie a 0 para aplicar
DECLARE @codigo      nvarchar(40)  = N'PRY-2026-17';
DECLARE @cierrePlan  date          = '2026-09-30';
DECLARE @actor       nvarchar(200) = N'Henry Alexis Ortez Banegas';

DECLARE @ahora datetime2 = SYSUTCDATETIME();

-- ── Guardas previas ────────────────────────────────────────────────────────
IF OBJECT_ID('Proyectos')           IS NULL OR OBJECT_ID('ProyectoEntregables') IS NULL
OR OBJECT_ID('ProyectoActividades') IS NULL OR OBJECT_ID('BitacoraProyecto')    IS NULL
BEGIN
    RAISERROR('Falta alguna de las tablas requeridas (Proyectos, ProyectoEntregables, ProyectoActividades, BitacoraProyecto). No se hizo nada.', 16, 1);
    RETURN;
END

DECLARE @proyectoId int = (SELECT Id FROM Proyectos WHERE Codigo = @codigo AND IsDeleted = 0);

IF @proyectoId IS NULL
BEGIN
    RAISERROR('No se encontró un proyecto activo con código %s. Ajuste @codigo. No se hizo nada.', 16, 1, @codigo);
    RETURN;
END

DECLARE @nEntregables int = (SELECT COUNT(*) FROM ProyectoEntregables WHERE ProyectoId = @proyectoId);

IF @nEntregables <> 1
BEGIN
    RAISERROR('El proyecto tiene %d entregables y este script espera exactamente 1. Si todavía está la estructura espejo, corra antes aplanar_entregables_espejo.sql. No se hizo nada.', 16, 1, @nEntregables);
    RETURN;
END

DECLARE @entregableId   int  = (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = @proyectoId);
DECLARE @inicioProyecto date = (SELECT FechaInicioPlan FROM Proyectos WHERE Id = @proyectoId);

IF @inicioProyecto IS NULL
BEGIN
    RAISERROR('El proyecto no tiene FechaInicioPlan y la línea base la necesita como arranque de la cadena. No se hizo nada.', 16, 1);
    RETURN;
END

BEGIN TRAN;

-- ══ PASO 1 · Los frentes del 23 al 27 de agosto y la socialización ════════
DECLARE @nuevas TABLE (
    Nombre nvarchar(400), Descripcion nvarchar(1000),
    Estado nvarchar(40), AvancePct int,
    IniPlan date, FinPlan date, IniReal date, FinReal date);

INSERT INTO @nuevas VALUES
 (N'API pública v1 documentada',
  N'API pública separada del código de PortalDigital, con la documentación generada desde el propio código.',
  N'Completada', 100, NULL, NULL, '2026-08-24', '2026-08-25'),

 (N'Integración con SIGER y HondurasÁgil',
  N'Identidad estable del trámite, archivado del SIGER original, escritura del expediente hacia la ficha con historial, bloqueo condicional y publicación manual en HondurasÁgil. Plan por fases, decisiones D-10 a D-23.',
  N'EnProceso', 60, '2026-08-24', '2026-09-12', '2026-08-24', NULL),

 (N'Estructura de proyectos: EDT, riesgos, interesados y GANTT',
  N'Descomposición en entregables y actividades, dependencias, registro de riesgos e interesados, repositorio documental, auditoría y cronograma GANTT.',
  N'Completada', 100, NULL, NULL, '2026-08-25', '2026-08-27'),

 (N'Socialización para lanzamiento interno',
  N'Presentación del portal a las áreas, recolección de observaciones y ajustes previos a la aprobación.',
  N'EnProceso', 10, '2026-08-28', '2026-09-12', '2026-08-28', NULL),

-- ══ PASO 2 · Módulos entregados que no estaban en la ficha ════════════════
 (N'Directorio de contactos institucionales',
  N'Alta, edición, baja y reactivación de contactos por institución, con su página de consulta.',
  N'Completada', 100, NULL, NULL, NULL, '2026-07-23'),

 (N'Biblioteca de recursos',
  N'Publicación de recursos descargables con registro de descargas.',
  N'Completada', 100, NULL, NULL, NULL, '2026-07-24'),

 (N'Migración de datos del portal legado',
  N'Importadores idempotentes desde Supabase: expedientes, reuniones y catálogos, con pantalla de migración unificada.',
  N'Completada', 100, NULL, NULL, NULL, '2026-08-13'),

 (N'Manual de usuario y ayuda en línea',
  N'Página de ayuda del portal enlazada desde el menú para todos los roles, con sus pruebas.',
  N'Completada', 100, NULL, NULL, NULL, '2026-08-27');

INSERT INTO ProyectoActividades
    (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan,
     FechaInicioReal, FechaFinReal, AvancePct, Estado, CreatedAt, CreatedBy)
SELECT @entregableId, 0, n.Nombre, n.Descripcion, n.IniPlan, n.FinPlan,
       n.IniReal, n.FinReal, n.AvancePct, n.Estado, @ahora, @actor
FROM @nuevas n
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoActividades a
    WHERE a.EntregableId = @entregableId AND a.Nombre = n.Nombre);

-- ══ PASO 3 · Orden cronológico y cierre del proyecto ══════════════════════
--    El orden se declara por nombre en vez de calcularse: hay tres pares que
--    cierran el mismo día (08-07/08, 08-13 y 08-27) y el desempate es de
--    criterio, no de fecha. El índice (EntregableId, Orden) no es único, así
--    que no hacen falta pasos intermedios.
DECLARE @orden TABLE (Orden int, Nombre nvarchar(400));
INSERT INTO @orden VALUES
 ( 1, N'Arranque del repositorio y estructura base'),
 ( 2, N'Importación de las 199 fichas de trámites'),
 ( 3, N'Gestión de expedientes y trámites'),
 ( 4, N'Reuniones, compromisos y registro de asistencia'),
 ( 5, N'Autenticación con certificado digital'),
 ( 6, N'Mesa de ayuda: notificaciones, tickets y chat'),
 ( 7, N'Plan de trabajo, informes y cronograma'),
 ( 8, N'Directorio de contactos institucionales'),
 ( 9, N'Biblioteca de recursos'),
 (10, N'Calendario y tableros de seguimiento'),
 (11, N'Trazabilidad y bitácora de expedientes'),
 (12, N'Identidad de producto, modo oscuro y accesibilidad'),
 (13, N'Inventario SIGER y conciliación con expedientes'),
 (14, N'Modelo de seguridad: roles administrables y permisos por acción'),
 (15, N'Migración de datos del portal legado'),
 (16, N'Módulo de seguimiento de proyectos'),
 (17, N'API pública v1 documentada'),
 (18, N'Integración con SIGER y HondurasÁgil'),
 (19, N'Estructura de proyectos: EDT, riesgos, interesados y GANTT'),
 (20, N'Manual de usuario y ayuda en línea'),
 (21, N'Validación interna del portal'),
 (22, N'Socialización para lanzamiento interno'),
 (23, N'Aprobación de la coordinación'),
 (24, N'Puesta en producción');

UPDATE a
   SET a.Orden     = o.Orden,
       a.UpdatedAt = CASE WHEN a.Orden <> o.Orden THEN @ahora ELSE a.UpdatedAt END,
       a.UpdatedBy = CASE WHEN a.Orden <> o.Orden THEN @actor ELSE a.UpdatedBy END
  FROM ProyectoActividades a
  JOIN @orden o ON o.Nombre = a.Nombre
 WHERE a.EntregableId = @entregableId;

-- Si el ambiente trae actividades que el orden declarado no contempla, mejor
-- saberlo que dejarlas sueltas en la posición 0.
DECLARE @huerfanas int = (
    SELECT COUNT(*) FROM ProyectoActividades a
     WHERE a.EntregableId = @entregableId
       AND NOT EXISTS (SELECT 1 FROM @orden o WHERE o.Nombre = a.Nombre));

IF @huerfanas > 0
BEGIN
    ROLLBACK TRAN;
    RAISERROR('Hay %d actividad(es) que el orden declarado no contempla. No se cambió nada; revise la lista @orden contra los nombres de su ambiente.', 16, 1, @huerfanas);
    RETURN;
END

-- ══ Compromisos de las actividades abiertas que ya existían ══════════════
--    Las tres finales venían sin fechas: son las únicas del proyecto con un
--    plan de verdad —hacia adelante, no reconstruido— y sin ellas el tramo
--    que va de hoy al cierre no se dibuja.
--
--    Solo se escriben si están vacías, para no pisar un ajuste hecho a mano
--    en una corrida posterior.
DECLARE @compromisos TABLE (Nombre nvarchar(400), IniPlan date, FinPlan date);
INSERT INTO @compromisos VALUES
 (N'Validación interna del portal', '2026-08-23', '2026-09-05'),
 (N'Aprobación de la coordinación', '2026-09-15', '2026-09-22'),
 (N'Puesta en producción',          '2026-09-23', @cierrePlan);

UPDATE a
   SET a.FechaInicioPlan = COALESCE(a.FechaInicioPlan, c.IniPlan),
       a.FechaFinPlan    = COALESCE(a.FechaFinPlan,    c.FinPlan),
       a.UpdatedAt       = @ahora,
       a.UpdatedBy       = @actor
  FROM ProyectoActividades a
  JOIN @compromisos c ON c.Nombre = a.Nombre
 WHERE a.EntregableId = @entregableId
   AND (a.FechaInicioPlan IS NULL OR a.FechaFinPlan IS NULL);

-- Una actividad completada sin cierre real no tiene de dónde reconstruirse.
DECLARE @sinCierre int = (
    SELECT COUNT(*) FROM ProyectoActividades
     WHERE EntregableId = @entregableId AND Estado = N'Completada' AND FechaFinReal IS NULL);

IF @sinCierre > 0
BEGIN
    ROLLBACK TRAN;
    RAISERROR('Hay %d actividad(es) completadas sin FechaFinReal. No se cambió nada.', 16, 1, @sinCierre);
    RETURN;
END

-- ══ PASO 4 · Línea base as-built ══════════════════════════════════════════
--    LAG sobre las completadas en orden de secuencia: el inicio de cada una es
--    el cierre de la anterior. La primera arranca en el inicio del proyecto.
--    Las actividades abiertas no se tocan: sus fechas plan son compromisos
--    reales hacia adelante y no deben mezclarse con la reconstrucción.
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

-- ══ Avance y cierre del proyecto ══════════════════════════════════════════
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

-- ══ Constancia en bitácora ════════════════════════════════════════════════
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
VALUES
 (@proyectoId, N'ModificacionEstructura',
  N'Puesta al día para la socialización. Se registran los frentes del 23 al 27 de agosto (API pública v1, integración SIGER–HondurasÁgil en curso, estructura de proyectos con GANTT), la actividad de socialización, y cuatro módulos entregados que faltaban en la ficha (contactos, biblioteca de recursos, migración del portal legado y manual de usuario), detectados al comparar el árbol del repositorio contra la estructura. Secuencia reordenada cronológicamente.',
  @actor, @ahora),
 (@proyectoId, N'ModificacionFicha',
  N'Línea base as-built: el proyecto se ejecutó sin planificación previa, así que las fechas plan de las actividades ya cerradas se fijan con las de ejecución real. El inicio de cada una se reconstruye como el cierre de la anterior; no está registrado en ningún lado y el repositorio no puede darlo. Cierre planificado fijado en '
    + CONVERT(nvarchar(10), @cierrePlan, 23) + N'. Avance recalculado a '
    + CAST(@avance AS nvarchar(10)) + N' %.',
  @actor, @ahora);

-- ══ Informe ═══════════════════════════════════════════════════════════════
SELECT Orden, Nombre, Estado, AvancePct AS Pct,
       FechaInicioPlan AS IniPlan, FechaFinPlan AS FinPlan,
       DATEDIFF(day, FechaInicioPlan, FechaFinPlan) AS Dias,
       FechaFinReal AS FinReal
  FROM ProyectoActividades
 WHERE EntregableId = @entregableId
 ORDER BY Orden;

SELECT Codigo, Estado, AvancePct AS Avance, FechaInicioPlan, FechaFinPlan
  FROM Proyectos WHERE Id = @proyectoId;

SELECT 'Actividades: '        + CAST(COUNT(*) AS varchar)
     + ' | completadas: '     + CAST(SUM(CASE WHEN Estado = N'Completada' THEN 1 ELSE 0 END) AS varchar)
     + ' | con barra: '       + CAST(SUM(CASE WHEN FechaInicioPlan IS NOT NULL AND FechaFinPlan IS NOT NULL THEN 1 ELSE 0 END) AS varchar)
     + ' | órdenes repetidos: ' + CAST(COUNT(*) - COUNT(DISTINCT Orden) AS varchar)
     + ' | fin<inicio: '      + CAST(SUM(CASE WHEN FechaFinPlan < FechaInicioPlan THEN 1 ELSE 0 END) AS varchar)
       AS Resumen
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
