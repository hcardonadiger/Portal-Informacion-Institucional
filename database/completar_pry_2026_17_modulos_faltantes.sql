/*
================================================================================
  Completa PRY-2026-17 con los módulos que están en el repositorio y no
  estaban en la ficha, y deja la secuencia en orden cronológico.
================================================================================

  CÓMO SE USA
  -----------
  1. Ejecútelo tal cual. Arranca en MODO SIMULACIÓN: no cambia nada, solo informa
     cómo quedaría la ficha y qué avance daría.
  2. Revise el listado.
  3. Cambie @soloSimular a 0 y vuelva a ejecutarlo para aplicar.

  Corre después de actualizar_pry_2026_17_socializacion.sql.

  QUÉ PROBLEMA RESUELVE
  ---------------------
  Al comparar el árbol del repositorio contra la ficha aparecieron cuatro
  módulos entregados que nadie había registrado:

    · Contactos                — 5 comandos CQRS y 2 páginas
    · Biblioteca de recursos   — 4 comandos y su página
    · Migración del portal legado — importadores Supabase bajo Pages/Admin
    · Manual de usuario y ayuda   — Pages/Ayuda, más sus pruebas

  Se deja fuera Application/AI: es solo IAgenteService.cs, una interfaz sin
  implementación. No es trabajo entregado y registrarlo inflaría el avance.

  SOBRE LAS FECHAS
  ----------------
  Solo se carga FechaFinReal, igual que las trece actividades históricas.
  NO se inventan fechas de inicio: el repositorio se sembró con dos volcados
  masivos —187 archivos el 26-06 y 427 el 03-07— así que el historial devuelve
  esas dos fechas para todo lo que ya existía, y no distingue cuándo empezó
  cada módulo. Escribir «26-06» como inicio de ocho actividades distintas sería
  precisión falsa, y dibujaría un GANTT donde todo arranca el mismo día.

  Las fechas de cierre sí son confiables: salen del último commit que tocó los
  archivos de cada módulo.

  SOBRE EL AVANCE
  ---------------
  Sube de 81 % a 84 %: entran cuatro actividades ya terminadas. No es que se
  haya avanzado hoy — es trabajo que ya estaba hecho y no estaba contado.

  ES IDEMPOTENTE
  --------------
  Las actividades se insertan por nombre solo si no existen, y el reordenamiento
  se hace por nombre, así que volver a correrlo no duplica ni descoloca nada.

================================================================================
*/

SET NOCOUNT ON;

-- sqlcmd arranca con QUOTED_IDENTIFIER en OFF y las tablas del portal tienen
-- índices filtrados; sin esto, el primer UPDATE revienta con el error 1934.
SET QUOTED_IDENTIFIER ON;

DECLARE @soloSimular bit = 1;          -- <<< cambie a 0 para aplicar

DECLARE @proyectoId int = 17;
DECLARE @actor  nvarchar(200) = N'Henry Alexis Ortez Banegas';
DECLARE @ahora  datetime2 = SYSUTCDATETIME();

IF OBJECT_ID('ProyectoActividades') IS NULL OR OBJECT_ID('BitacoraProyecto') IS NULL
BEGIN
    RAISERROR('Faltan tablas requeridas. No se hizo nada.', 16, 1);
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

-- ── 1. Los cuatro módulos que faltaban ─────────────────────────────────────
DECLARE @nuevas TABLE (Nombre nvarchar(400), Descripcion nvarchar(1000), FinReal date);

INSERT INTO @nuevas VALUES
 (N'Directorio de contactos institucionales',
  N'Alta, edición, baja y reactivación de contactos por institución, con su página de consulta.',
  '2026-07-23'),

 (N'Biblioteca de recursos',
  N'Publicación de recursos descargables con registro de descargas.',
  '2026-07-24'),

 (N'Migración de datos del portal legado',
  N'Importadores idempotentes desde Supabase: expedientes, reuniones y catálogos, con pantalla de migración unificada.',
  '2026-08-13'),

 (N'Manual de usuario y ayuda en línea',
  N'Página de ayuda del portal enlazada desde el menú para todos los roles, con sus pruebas.',
  '2026-08-27');

INSERT INTO ProyectoActividades
    (EntregableId, Orden, Nombre, Descripcion, FechaFinReal, AvancePct, Estado, CreatedAt, CreatedBy)
SELECT @entregableId, 0, n.Nombre, n.Descripcion, n.FinReal, 100, N'Completada', @ahora, @actor
FROM @nuevas n
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoActividades a
    WHERE a.EntregableId = @entregableId AND a.Nombre = n.Nombre);

-- ── 2. Reordenar toda la secuencia en orden cronológico ────────────────────
--    El orden se declara por nombre en vez de calcularse: dos actividades
--    cierran el mismo día (08-13 y 08-27) y el desempate es de criterio, no
--    de fecha. El índice (EntregableId, Orden) no es único, así que no hacen
--    falta pasos intermedios.
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
   SET a.Orden = o.Orden,
       a.UpdatedAt = CASE WHEN a.Orden <> o.Orden THEN @ahora ELSE a.UpdatedAt END,
       a.UpdatedBy = CASE WHEN a.Orden <> o.Orden THEN @actor ELSE a.UpdatedBy END
  FROM ProyectoActividades a
  JOIN @orden o ON o.Nombre = a.Nombre
 WHERE a.EntregableId = @entregableId;

-- Si el orden declarado no cubre todo lo que hay, mejor saberlo que dejar
-- actividades sueltas en la posición 0.
DECLARE @huerfanas int = (
    SELECT COUNT(*) FROM ProyectoActividades a
     WHERE a.EntregableId = @entregableId
       AND NOT EXISTS (SELECT 1 FROM @orden o WHERE o.Nombre = a.Nombre));

IF @huerfanas > 0
BEGIN
    ROLLBACK TRAN;
    RAISERROR('Hay %d actividad(es) que el orden declarado no contempla. No se cambió nada; revise la lista @orden.', 16, 1, @huerfanas);
    RETURN;
END

-- ── 3. Avance recalculado con la regla del dominio ─────────────────────────
DECLARE @avance int = (
    SELECT CAST(ROUND(AVG(CAST(AvancePct AS float)), 0) AS int)
      FROM ProyectoActividades
     WHERE EntregableId = @entregableId AND Estado <> N'Cancelada');

UPDATE Proyectos
   SET AvancePct = @avance, UpdatedAt = @ahora, UpdatedBy = @actor
 WHERE Id = @proyectoId;

-- ── 4. Constancia en bitácora ──────────────────────────────────────────────
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
VALUES (@proyectoId, N'ModificacionEstructura',
  N'Se registran cuatro módulos entregados que faltaban en la ficha (contactos, biblioteca de recursos, migración del portal legado y manual de usuario), detectados al comparar el árbol del repositorio contra la estructura. Secuencia reordenada por fecha de cierre. Avance recalculado a '
    + CAST(@avance AS nvarchar(10)) + N' %.',
  @actor, @ahora);

-- ── Informe ────────────────────────────────────────────────────────────────
SELECT Orden, Nombre, Estado, AvancePct AS Pct, FechaFinReal AS FinReal,
       FechaInicioPlan AS IniPlan, FechaFinPlan AS FinPlan
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
