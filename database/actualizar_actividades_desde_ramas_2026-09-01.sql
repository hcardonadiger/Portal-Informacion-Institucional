/*
================================================================================
  Pone al día PRY-2026-17 y PRY-2026-25 con lo que hicieron las ramas
  HenryC, Jamil y feature/arreglos-estabilizacion.
================================================================================

  CÓMO SE USA
  -----------
  1. Ejecútelo tal cual. Arranca en MODO SIMULACIÓN: no cambia nada, solo informa
     cómo quedarían las dos fichas.
  2. Revise los listados y el resumen del final.
  3. Cambie @soloSimular a 0 y vuelva a ejecutarlo para aplicar.

      sqlcmd -S <servidor> -E -C -I -f 65001 -d <base> ^
             -i database\actualizar_actividades_desde_ramas_2026-09-01.sql

  El -I (QUOTED_IDENTIFIER ON) es obligatorio por los índices filtrados del
  portal. El -f 65001 también: este archivo es UTF-8 y sin él sqlcmd destroza
  los acentos, con lo cual los nombres no casan y el guion se detiene diciendo
  que hay actividades que el orden no contempla.

  Los proyectos se buscan por CÓDIGO, no por Id: el Id cambia entre ambientes.

  CORRE DESPUÉS DE
  ----------------
      aplanar_entregables_espejo.sql
      poner_al_dia_portal_digitalizacion.sql

  Con registrar_linaje_gobdigital.sql no hay orden obligatorio: los dos declaran
  la misma secuencia de 33 actividades y ubican el entregable de desarrollo por
  una actividad marcadora, no por ser el único del proyecto. Corra el que quiera
  primero, y las veces que quiera.

  QUÉ HACE, EN CUATRO PASOS
  -------------------------
  1. Asigna responsable a las veinte actividades de desarrollo de PRY-2026-17.
     Hasta hoy ninguna tenía: la ficha decía qué se hizo pero no quién.

  2. Registra los seis frentes del 28-08 al 01-09 que ninguna ficha recoge —el
     guion anterior cierra el 27-08—: biblioteca documental, registro de
     descargas, vínculos entre módulos, tablero de indicadores, guiones de datos
     de SIGER y el renombrado del portal a GestionGD.

  3. Reordena la secuencia completa y deja las cuatro actividades de proceso
     (validación, socialización, aprobación y producción) al final.

  4. Sobre PRY-2026-25 pone responsable y fecha real de arranque al API
     intermediario, y registra los dos frentes que sí se cerraron: la publicación
     manual hacia HondurasÁgil y la trazabilidad del proyecto en el portal.

  DE DÓNDE SALE EL RESPONSABLE
  ----------------------------
  De la autoría de los commits, contada por módulo sobre el árbol de archivos.
  Son cuatro personas y dos de ellas se llaman Henry, así que conviene fijarlo:

      halexis11 <henry.ortez@gmail.com>      → Henry Ortez     (41 commits)
      JamilGarcia <garciajamil69@gmail.com>  → Jamil Garcia     (57)
      hcardonadiger <hcardona@diger.gob.hn>  → Henry Cardona    (10)
      BZDIGER <bzelaya@diger.gob.hn>         → Brizzio Zelaya    (2)

  CUATRO ATRIBUCIONES SON DISCUTIBLES
  -----------------------------------
  Contar archivos tocados es un proxy flojo: una pasada de formato pesa igual
  que construir el módulo. Donde el conteo quedó parejo se desempató con lo que
  dicen los mensajes de los commits. Estas cuatro quedaron cerradas por poco y
  se corrigen a mano en la ficha si el criterio es otro:

      Reuniones, compromisos y registro de asistencia   Jamil 12 / Ortez 11
      Biblioteca de recursos                            Ortez  2 / Jamil  1
      Calendario y tableros de seguimiento              tres manos, ninguna clara
      Migración de datos del portal legado              tres manos, ninguna clara

  LO QUE NO SE TOCA, A PROPÓSITO
  ------------------------------
  · Las cuatro actividades de proceso de PRY-2026-17 quedan SIN responsable: no
    son desarrollo y el repositorio no dice nada de ellas. Inventarlo sería peor
    que dejarlo vacío.
  · El 60 % de «Integración con SIGER y HondurasÁgil» y el 50 % del «API
    intermediario» se dejan como están. Los commits del 31-08 son evidencia de
    que el frente sigue vivo, no de cuánto avanzó.
  · Las cuatro primeras actividades de PRY-2026-25 (análisis, flujo, origen de
    datos, proyecto base) tampoco reciben responsable: son trabajo de análisis
    anterior al código y no dejaron rastro en el repositorio.

  SOBRE LAS FECHAS DE LO NUEVO
  ----------------------------
  Se sigue la misma convención de línea base «as-built» del guion anterior: la
  fecha de cierre sale del último commit del frente, el arranque se encadena con
  el cierre del frente previo, y plan = real porque no hubo planificación contra
  la cual medir. Dos actividades del 31-08 quedan con barra de un día: cerraron
  el mismo día en que arrancaron, y es correcto.

  ES IDEMPOTENTE
  --------------
  Las actividades se insertan solo si no existen por nombre, los responsables se
  reescriben con el mismo valor y el orden se aplica por nombre. Volver a
  ejecutarlo da el mismo resultado.

================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;

-- ── Parámetros ─────────────────────────────────────────────────────────────
DECLARE @soloSimular bit           = 1;                 -- <<< cambie a 0 para aplicar
DECLARE @actor       nvarchar(200) = N'Jamil Garcia';

DECLARE @ahora datetime2 = SYSUTCDATETIME();

-- ── Guardas de esquema ─────────────────────────────────────────────────────
IF OBJECT_ID('Proyectos')           IS NULL OR OBJECT_ID('ProyectoEntregables') IS NULL
OR OBJECT_ID('ProyectoActividades') IS NULL OR OBJECT_ID('BitacoraProyecto')    IS NULL
OR OBJECT_ID('Usuarios')            IS NULL
BEGIN
    RAISERROR('Falta alguna de las tablas requeridas. No se hizo nada.', 16, 1);
    RETURN;
END

-- ── Resolver los dos proyectos y su entregable único ───────────────────────
DECLARE @pry17 int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-17' AND IsDeleted = 0);
DECLARE @pry25 int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-25' AND IsDeleted = 0);

IF @pry17 IS NULL OR @pry25 IS NULL
BEGIN
    RAISERROR('No se encontraron los proyectos PRY-2026-17 y PRY-2026-25 activos. No se hizo nada.', 16, 1);
    RETURN;
END

-- El entregable de desarrollo se ubica por una actividad marcadora y NO por ser
-- el único del proyecto. La primera versión de este guion exigía exactamente 1,
-- y dejó de poder ejecutarse en cuanto registrar_linaje_gobdigital.sql le sumó
-- tres entregables de despliegue a PRY-2026-17: abortaba con «se esperaba 1
-- entregable y hay 4» sobre una base perfectamente sana. Con el marcador da
-- igual cuántos entregables tenga el proyecto y en qué orden se corran los dos
-- guiones.
DECLARE @ent17 int = (
    SELECT TOP 1 e.Id
      FROM ProyectoEntregables e
      JOIN ProyectoActividades a ON a.EntregableId = e.Id
     WHERE e.ProyectoId = @pry17 AND a.Nombre = N'Arranque del repositorio y estructura base');

DECLARE @ent25 int = (
    SELECT TOP 1 e.Id
      FROM ProyectoEntregables e
      JOIN ProyectoActividades a ON a.EntregableId = e.Id
     WHERE e.ProyectoId = @pry25 AND a.Nombre = N'API intermediario con el portal de digitalización');

IF @ent17 IS NULL OR @ent25 IS NULL
BEGIN
    RAISERROR('No se encontró el entregable de desarrollo de PRY-2026-17 (marcado por «Arranque del repositorio y estructura base») o el de PRY-2026-25 (marcado por «API intermediario con el portal de digitalización»). Corra antes aplanar_entregables_espejo.sql y poner_al_dia_portal_digitalizacion.sql. No se hizo nada.', 16, 1);
    RETURN;
END

-- ── Resolver los cuatro autores contra la tabla de usuarios ────────────────
DECLARE @idOrtez   uniqueidentifier = (SELECT Id FROM Usuarios WHERE Correo = N'hortez@diger.gob.hn');
DECLARE @idJamil   uniqueidentifier = (SELECT Id FROM Usuarios WHERE Correo = N'jgarcia@diger.gob.hn');
DECLARE @idCardona uniqueidentifier = (SELECT Id FROM Usuarios WHERE Correo = N'hcardona@diger.gob.hn');

IF @idOrtez IS NULL OR @idJamil IS NULL OR @idCardona IS NULL
BEGIN
    RAISERROR('Falta alguno de los usuarios hortez@, jgarcia@ o hcardona@diger.gob.hn. Sin ellos el responsable quedaría solo como texto, sin enlace a la persona. No se hizo nada.', 16, 1);
    RETURN;
END

DECLARE @nomOrtez   nvarchar(400) = (SELECT Nombre FROM Usuarios WHERE Id = @idOrtez);
DECLARE @nomJamil   nvarchar(400) = (SELECT Nombre FROM Usuarios WHERE Id = @idJamil);
DECLARE @nomCardona nvarchar(400) = (SELECT Nombre FROM Usuarios WHERE Id = @idCardona);

BEGIN TRAN;

-- ══ PASO 1 · Responsable de las actividades de desarrollo de PRY-2026-17 ═══
DECLARE @resp TABLE (Nombre nvarchar(600), Quien uniqueidentifier);

INSERT INTO @resp VALUES
 (N'Arranque del repositorio y estructura base',                      @idJamil),
 (N'Importación de las 199 fichas de trámites',                       @idJamil),
 (N'Gestión de expedientes y trámites',                               @idJamil),
 (N'Reuniones, compromisos y registro de asistencia',                 @idJamil),
 (N'Autenticación con certificado digital',                           @idJamil),
 (N'Mesa de ayuda: notificaciones, tickets y chat',                   @idOrtez),
 (N'Plan de trabajo, informes y cronograma',                          @idOrtez),
 (N'Directorio de contactos institucionales',                         @idJamil),
 (N'Biblioteca de recursos',                                          @idOrtez),
 (N'Calendario y tableros de seguimiento',                            @idCardona),
 (N'Trazabilidad y bitácora de expedientes',                          @idOrtez),
 (N'Identidad de producto, modo oscuro y accesibilidad',              @idOrtez),
 (N'Inventario SIGER y conciliación con expedientes',                 @idJamil),
 (N'Modelo de seguridad: roles administrables y permisos por acción', @idOrtez),
 (N'Migración de datos del portal legado',                            @idCardona),
 (N'Módulo de seguimiento de proyectos',                              @idOrtez),
 (N'API pública v1 documentada',                                      @idJamil),
 (N'Integración con SIGER y HondurasÁgil',                            @idJamil),
 (N'Estructura de proyectos: EDT, riesgos, interesados y GANTT',      @idOrtez),
 (N'Manual de usuario y ayuda en línea',                              @idOrtez);

UPDATE a
   SET a.ResponsableId = r.Quien,
       a.Responsable   = u.Nombre,
       a.UpdatedAt     = CASE WHEN ISNULL(a.ResponsableId, 0x0) <> r.Quien THEN @ahora ELSE a.UpdatedAt END,
       a.UpdatedBy     = CASE WHEN ISNULL(a.ResponsableId, 0x0) <> r.Quien THEN @actor ELSE a.UpdatedBy END
  FROM ProyectoActividades a
  JOIN @resp r   ON r.Nombre = a.Nombre
  JOIN Usuarios u ON u.Id     = r.Quien
 WHERE a.EntregableId = @ent17;

DECLARE @conResponsable int = @@ROWCOUNT;

-- ══ PASO 2 · Los seis frentes del 28-08 al 01-09 ═══════════════════════════
DECLARE @nuevas17 TABLE (
    Nombre nvarchar(600), Descripcion nvarchar(4000),
    Estado nvarchar(60), AvancePct int,
    IniReal date, FinReal date, Quien uniqueidentifier);

INSERT INTO @nuevas17 VALUES
 (N'Biblioteca documental por carpetas y carga por lote',
  N'La biblioteca del proyecto se puede ver en carpetas, agrupada o en lista. Carga de varios documentos en una sola operación, con el título escrito aplicado a todo el lote y el nombre compuesto «Título — nombre del archivo».',
  N'Completada', 100, '2026-08-27', '2026-08-30', @idOrtez),

 (N'Registro de descargas por versión de documento',
  N'Bitácora de quién descargó qué versión de qué documento y cuándo, visible en la biblioteca y en la ficha. Apunta a la versión y no al documento: saber que alguien se llevó el convenio no dice nada si no consta si fue la v1 o la v2 ya corregida.',
  N'Completada', 100, '2026-08-30', '2026-08-30', @idOrtez),

 (N'Vínculos de proyectos con reuniones, expedientes y tickets',
  N'Vínculo opcional por los dos lados: desde el proyecto y desde la reunión, el expediente o el ticket. Las secciones de vínculos se pliegan cuando están vacías para no ocupar la ficha con nada.',
  N'Completada', 100, '2026-08-30', '2026-08-31', @idOrtez),

 (N'Tablero de indicadores por proyecto',
  N'Tablero de seguimiento por proyecto, apertura de la ficha en la pestaña de Datos y reordenamiento de las pestañas por frecuencia de uso. Incluye la puesta al día de la ayuda del módulo.',
  N'Completada', 100, '2026-08-31', '2026-08-31', @idOrtez),

 (N'Guiones de datos de SIGER: institución e intercalación',
  N'Asignación de la institución a los trámites del inventario SIGER y unificación de la intercalación de cinco columnas, que venían con collations distintas y rompían las comparaciones.',
  N'Completada', 100, '2026-08-31', '2026-08-31', @idJamil),

 (N'Renombrado del portal a GestionGD',
  N'El sistema pasa a presentarse como GestionGD en pestaña, login, cabecera, ayuda, correos e informes. DIGER se conserva como institución. El nombre queda en una sola constante en lugar de repartido en literales.',
  N'Completada', 100, '2026-08-31', '2026-09-01', @idJamil);

INSERT INTO ProyectoActividades
    (EntregableId, Orden, Nombre, Descripcion,
     FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal,
     AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
SELECT @ent17, 0, n.Nombre, n.Descripcion,
       n.IniReal, n.FinReal, n.IniReal, n.FinReal,
       n.AvancePct, n.Estado, n.Quien, u.Nombre, @ahora, @actor
  FROM @nuevas17 n
  JOIN Usuarios u ON u.Id = n.Quien
 WHERE NOT EXISTS (SELECT 1 FROM ProyectoActividades a
                    WHERE a.EntregableId = @ent17 AND a.Nombre = n.Nombre);

DECLARE @insertadas17 int = @@ROWCOUNT;

-- ══ PASO 3 · Orden cronológico de PRY-2026-17 ══════════════════════════════
--    Las cuatro de proceso van al final aunque hayan arrancado antes: no son
--    desarrollo y leerlas intercaladas confunde la secuencia de entrega.
DECLARE @orden TABLE (Orden int, Nombre nvarchar(600));

-- Las tres primeras son el antecedente gobdigital y las inserta
-- registrar_linaje_gobdigital.sql, no este guion. Se listan igual para que los
-- dos declaren la MISMA secuencia: así da lo mismo cuál corra primero y volver
-- a correr cualquiera de los dos después del otro no descoloca nada. Si todavía
-- no existen, el JOIN simplemente no las encuentra y no pasa nada.
INSERT INTO @orden VALUES
 ( 1, N'Análisis y diseño del portal de Gobierno Digital (gobdigital)'),
 ( 2, N'Ejecución inicial de la idea en HTML (gobdigital)'),
 ( 3, N'Reestructuración de idea a arquitectura limpia'),
 ( 4, N'Arranque del repositorio y estructura base'),
 ( 5, N'Importación de las 199 fichas de trámites'),
 ( 6, N'Gestión de expedientes y trámites'),
 ( 7, N'Reuniones, compromisos y registro de asistencia'),
 ( 8, N'Autenticación con certificado digital'),
 ( 9, N'Mesa de ayuda: notificaciones, tickets y chat'),
 (10, N'Plan de trabajo, informes y cronograma'),
 (11, N'Directorio de contactos institucionales'),
 (12, N'Biblioteca de recursos'),
 (13, N'Calendario y tableros de seguimiento'),
 (14, N'Trazabilidad y bitácora de expedientes'),
 (15, N'Identidad de producto, modo oscuro y accesibilidad'),
 (16, N'Inventario SIGER y conciliación con expedientes'),
 (17, N'Modelo de seguridad: roles administrables y permisos por acción'),
 (18, N'Migración de datos del portal legado'),
 (19, N'Módulo de seguimiento de proyectos'),
 (20, N'API pública v1 documentada'),
 (21, N'Integración con SIGER y HondurasÁgil'),
 (22, N'Estructura de proyectos: EDT, riesgos, interesados y GANTT'),
 (23, N'Manual de usuario y ayuda en línea'),
 (24, N'Biblioteca documental por carpetas y carga por lote'),
 (25, N'Registro de descargas por versión de documento'),
 (26, N'Vínculos de proyectos con reuniones, expedientes y tickets'),
 (27, N'Tablero de indicadores por proyecto'),
 (28, N'Guiones de datos de SIGER: institución e intercalación'),
 (29, N'Renombrado del portal a GestionGD'),
 (30, N'Validación interna del portal'),
 (31, N'Socialización para lanzamiento interno'),
 (32, N'Aprobación de la coordinación'),
 (33, N'Puesta en producción');

-- Un nombre repetido en las listas de arriba haría que el UPDATE...JOIN eligiera
-- una fila cualquiera, en silencio. Se comprueba acá y no con una clave primaria
-- porque nvarchar(600) excede los 900 bytes que admite un índice agrupado y SQL
-- Server lo avisa en cada corrida.
IF EXISTS (SELECT 1 FROM @orden GROUP BY Nombre HAVING COUNT(*) > 1)
OR EXISTS (SELECT 1 FROM @resp  GROUP BY Nombre HAVING COUNT(*) > 1)
BEGIN
    ROLLBACK TRAN;
    RAISERROR('Hay nombres repetidos en las listas @orden o @resp del guion. No se cambió nada; corrija el guion.', 16, 1);
    RETURN;
END

UPDATE a
   SET a.Orden     = o.Orden,
       a.UpdatedAt = CASE WHEN a.Orden <> o.Orden THEN @ahora ELSE a.UpdatedAt END,
       a.UpdatedBy = CASE WHEN a.Orden <> o.Orden THEN @actor ELSE a.UpdatedBy END
  FROM ProyectoActividades a
  JOIN @orden o ON o.Nombre = a.Nombre
 WHERE a.EntregableId = @ent17;

-- Si el ambiente trae actividades que el orden declarado no contempla, mejor
-- saberlo que dejarlas sueltas en la posición 0.
DECLARE @huerfanas int = (
    SELECT COUNT(*) FROM ProyectoActividades a
     WHERE a.EntregableId = @ent17
       AND NOT EXISTS (SELECT 1 FROM @orden o WHERE o.Nombre = a.Nombre));

IF @huerfanas > 0
BEGIN
    ROLLBACK TRAN;
    RAISERROR('Hay %d actividad(es) de PRY-2026-17 que el orden declarado no contempla. No se cambió nada; revise la lista @orden contra los nombres de su ambiente. Si los acentos salen mal, le faltó el -f 65001.', 16, 1, @huerfanas);
    RETURN;
END

-- ══ PASO 4 · PRY-2026-25 · Ventanilla Única — Honduras Ágil ════════════════
--    El API intermediario existía sin responsable ni fecha de arranque. El
--    porcentaje NO se toca: los commits dicen que el frente sigue vivo, no
--    cuánto avanzó.
UPDATE ProyectoActividades
   SET ResponsableId   = @idJamil,
       Responsable     = @nomJamil,
       FechaInicioReal = ISNULL(FechaInicioReal, '2026-08-24'),
       FechaInicioPlan = ISNULL(FechaInicioPlan, '2026-08-24'),
       UpdatedAt       = @ahora,
       UpdatedBy       = @actor
 WHERE EntregableId = @ent25
   AND Nombre       = N'API intermediario con el portal de digitalización';

DECLARE @nuevas25 TABLE (
    Nombre nvarchar(600), Descripcion nvarchar(4000),
    IniReal date, FinReal date);

INSERT INTO @nuevas25 VALUES
 (N'Publicación manual de trámites hacia HondurasÁgil',
  N'La publicación deja de ser automática y pasa a ser un acto explícito con su propia pantalla: nada sale hacia la ventanilla sin que alguien lo decida. Incluye el bloqueo condicional de la ficha y el archivado del SIGER original antes de que el portal escriba encima.',
  '2026-08-24', '2026-08-24'),

 (N'Trazabilidad del proyecto en el portal',
  N'Registro del proyecto de la ventanilla única dentro del portal, con su descomposición y su cronograma, y reapuntado del portal y la API a la base unificada.',
  '2026-08-25', '2026-08-27');

INSERT INTO ProyectoActividades
    (EntregableId, Orden, Nombre, Descripcion,
     FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal,
     AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
SELECT @ent25,
       (SELECT ISNULL(MAX(Orden), 0) FROM ProyectoActividades WHERE EntregableId = @ent25)
         + ROW_NUMBER() OVER (ORDER BY n.IniReal),
       n.Nombre, n.Descripcion,
       n.IniReal, n.FinReal, n.IniReal, n.FinReal,
       100, N'Completada', @idJamil, @nomJamil, @ahora, @actor
  FROM @nuevas25 n
 WHERE NOT EXISTS (SELECT 1 FROM ProyectoActividades a
                    WHERE a.EntregableId = @ent25 AND a.Nombre = n.Nombre);

DECLARE @insertadas25 int = @@ROWCOUNT;

-- ══ PASO 5 · Recalcular el avance con la regla del dominio ═════════════════
--    Promedio de los entregables vigentes, y cada entregable el promedio de sus
--    propias actividades vigentes. NO es un promedio plano de todas las
--    actividades del proyecto: con más de un entregable, el plano le daría más
--    peso al que tenga más filas, en silencio y sin que nadie lo haya decidido.
--    Es la misma regla de Proyecto.RecalcularAvance y EntregableProyecto
--    .AvanceCalculado, incluida la salida por Estado cuando el entregable
--    todavía no tiene actividades. Se calcula y no se escribe a mano para que
--    coincida con lo que el portal muestra al recargar la ficha.
;WITH avancePorEntregable AS (
    SELECT e.ProyectoId, e.Id,
           COALESCE(
             (SELECT CONVERT(int, ROUND(AVG(CONVERT(float, a.AvancePct)), 0))
                FROM ProyectoActividades a
               WHERE a.EntregableId = e.Id AND a.Estado <> N'Cancelada'),
             CASE e.Estado WHEN N'Completado' THEN 100 WHEN N'EnProceso' THEN 50 ELSE 0 END
           ) AS Avance
      FROM ProyectoEntregables e
     WHERE e.ProyectoId IN (@pry17, @pry25) AND e.Estado <> N'Cancelado'
)
UPDATE p
   SET p.AvancePct = x.Pct,
       p.UpdatedAt = @ahora,
       p.UpdatedBy = @actor
  FROM Proyectos p
  CROSS APPLY (
      SELECT CONVERT(int, ROUND(AVG(CONVERT(float, ae.Avance)), 0)) AS Pct
        FROM avancePorEntregable ae
       WHERE ae.ProyectoId = p.Id) x
 WHERE p.Id IN (@pry17, @pry25);

-- ══ PASO 6 · Bitácora ══════════════════════════════════════════════════════
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT @pry17, N'ModificacionFicha',
       N'Puesta al día desde el historial de las ramas HenryC, Jamil y feature/arreglos-estabilizacion: '
       + CONVERT(nvarchar(10), @conResponsable) + N' actividades reciben responsable y se registran '
       + CONVERT(nvarchar(10), @insertadas17) + N' frentes del 28-08 al 01-09.',
       @actor, @ahora
 WHERE @conResponsable > 0 OR @insertadas17 > 0;

INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT @pry25, N'ModificacionFicha',
       N'Puesta al día desde el historial de las ramas: se registran '
       + CONVERT(nvarchar(10), @insertadas25) + N' frentes cerrados y se asigna responsable al API intermediario.',
       @actor, @ahora
 WHERE @insertadas25 > 0;

-- ══ Reporte ════════════════════════════════════════════════════════════════
SELECT N'PRY-2026-17' AS Proyecto, a.Orden, LEFT(a.Nombre, 62) AS Actividad,
       a.Estado, a.AvancePct AS Pct,
       CONVERT(varchar(10), a.FechaInicioPlan, 23) AS IniPlan,
       CONVERT(varchar(10), a.FechaFinPlan,    23) AS FinPlan,
       ISNULL(a.Responsable, N'—') AS Responsable
  FROM ProyectoActividades a
 WHERE a.EntregableId = @ent17
 ORDER BY a.Orden;

SELECT N'PRY-2026-25' AS Proyecto, a.Orden, LEFT(a.Nombre, 62) AS Actividad,
       a.Estado, a.AvancePct AS Pct,
       CONVERT(varchar(10), a.FechaInicioPlan, 23) AS IniPlan,
       CONVERT(varchar(10), a.FechaFinPlan,    23) AS FinPlan,
       ISNULL(a.Responsable, N'—') AS Responsable
  FROM ProyectoActividades a
 WHERE a.EntregableId = @ent25
 ORDER BY a.Orden;

SELECT p.Codigo, p.Estado, p.AvancePct AS Avance,
       (SELECT COUNT(*) FROM ProyectoActividades a WHERE a.EntregableId IN
            (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = p.Id)) AS Actividades,
       (SELECT COUNT(*) FROM ProyectoActividades a WHERE a.EntregableId IN
            (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = p.Id)
           AND a.Estado = N'Completada') AS Completadas,
       (SELECT COUNT(*) FROM ProyectoActividades a WHERE a.EntregableId IN
            (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = p.Id)
           AND a.Responsable IS NOT NULL) AS ConResponsable
  FROM Proyectos p WHERE p.Id IN (@pry17, @pry25) ORDER BY p.Codigo;

SELECT N'Responsables asignados' AS Resumen, @conResponsable AS Cantidad
UNION ALL SELECT N'Actividades nuevas en PRY-2026-17', @insertadas17
UNION ALL SELECT N'Actividades nuevas en PRY-2026-25', @insertadas25
UNION ALL SELECT N'Órdenes repetidos en PRY-2026-17 (debe ser 0)',
       (SELECT COUNT(*) FROM (SELECT Orden FROM ProyectoActividades
                               WHERE EntregableId = @ent17
                               GROUP BY Orden HAVING COUNT(*) > 1) d)
UNION ALL SELECT N'Actividades con fin anterior al inicio (debe ser 0)',
       (SELECT COUNT(*) FROM ProyectoActividades
         WHERE EntregableId IN (@ent17, @ent25)
           AND FechaFinPlan IS NOT NULL AND FechaInicioPlan IS NOT NULL
           AND FechaFinPlan < FechaInicioPlan);

IF @soloSimular = 1
BEGIN
    ROLLBACK TRAN;
    PRINT '';
    PRINT '*** MODO SIMULACION: nada se guardo. Ponga @soloSimular = 0 para aplicar. ***';
END
ELSE
BEGIN
    COMMIT TRAN;
    PRINT '';
    PRINT '*** Cambios aplicados. ***';
END
GO
