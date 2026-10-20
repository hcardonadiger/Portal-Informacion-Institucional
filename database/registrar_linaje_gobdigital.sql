/*
================================================================================
  Estructura completa de PRY-2026-17 («Portal de Digitalización de Trámites»):
  el antecedente del proyecto (gobdigital) y el plan de despliegue y
  operación que sigue. Reemplaza a registrar_antecedente_gobdigital.sql y a
  plan_despliegue_pry_2026_17.sql — este archivo es ahora el único que hay
  que correr para dejar la ficha al día.

  CÓMO SE USA
  -----------
  1. Ejecútelo tal cual. Arranca en MODO SIMULACIÓN: no cambia nada, solo informa
     cómo quedaría la ficha.
  2. Revise el informe del final.
  3. Cambie @soloSimular a 0 y vuelva a ejecutarlo para aplicar.

      sqlcmd -S <servidor> -E -C -I -f 65001 -d <base> ^
             -i database\registrar_linaje_gobdigital.sql

  El -f 65001 es obligatorio: este archivo es UTF-8 y sin él sqlcmd destroza los
  acentos, con lo cual los nombres no casan y el guion se detiene diciendo que
  hay actividades que el orden no contempla.

  DOS CAMBIOS RESPECTO DE LA VERSIÓN QUE LLEGÓ POR CORREO
  --------------------------------------------------------
  · Venía con @soloSimular = 0, o sea aplicando de inmediato. Se pone en 1 como
    el resto de los guiones de esta carpeta: en un guion que borra filas y
    reescribe fechas, aplicar por omisión es una trampa.
  · La lista @orden del PASO 2 no contemplaba los seis frentes del 28-08 al
    01-09 que registra actualizar_actividades_desde_ramas_2026-09-01.sql, así
    que abortaba con «hay 6 actividades que este orden no contempla». Se
    agregaron en los órdenes 24 a 29; la secuencia pasa de 27 a 33.

  REQUISITO PREVIO
  -----------------
  Debe existir ya el entregable original (el que trae poner_al_dia_portal_
  digitalizacion.sql), identificado por contener la actividad «Arranque del
  repositorio y estructura base» — ya NO se exige que sea el único
  entregable del proyecto, precisamente porque este script le agrega tres
  más. Si en algún momento anterior el proyecto tenía exactamente 1
  entregable como condición, esa condición queda retirada aquí a propósito.

  QUÉ HACE, EN DOS PARTES
  -------------------------
  PARTE A — Antecedente (gobdigital), sobre el entregable original:
    0. Repara una fila que quedó con nombre y fechas de una corrida anterior
       («Ejecución de la idea…» → «Ejecución inicial de la idea en HTML…»).
    1. Crea, si no existen, las tres actividades del antecedente: análisis y
       diseño (16-may–04-jun-2026), ejecución inicial en HTML (04-jun–25-jun),
       reestructuración a arquitectura limpia (20-jun–26-jun).
    2. Reordena la secuencia completa del entregable original para que esas
       tres vayan primero.

  PARTE B — Plan de despliegue y operación, en tres entregables nuevos
  (fechas PLAN — nada de esto ha pasado todavía):
    3. «Coordinación y despliegue» (vence 07-sep-2026): despliegue de los
       últimos cambios → creación de usuarios, con dependencia entre ambas.
    4. «Capacitación» (vence 14-sep-2026): coordinación con las tres áreas
       (08–14 sep) y luego una actividad de capacitación por área — SIGER,
       Proyectos, Gerencia — sin fechas todavía, por definir con cada área.
       («Etapa3» se quita: era un nombre provisional, el área es Gerencia.)
    5. «Mantenimiento y seguimiento» (vence 21-sep-2026): mantenimiento,
       seguimiento de uso, talleres de retroalimentación, nuevas
       actualizaciones. Es trabajo continuo — la ventana plan es solo el
       arranque, va a necesitar que se le extienda la fecha de cierre.

  Y para cerrar:
    6. Avance del proyecto, recalculado como promedio de entregables (cada
       uno promedio de sus propias actividades) — ya no es un promedio plano,
       porque desde este script el proyecto pasa a tener 4 entregables.
    7. Bitácora: una entrada por cada parte, más la nota de contexto
       histórico general, todas con su propio guardado idempotente.

  ES IDEMPOTENTE
  ---------------
  Cada inserción trae su propio NOT EXISTS por nombre (entregables,
  actividades) o por el par correspondiente (dependencia). El avance se
  recalcula siempre. Las entradas de bitácora se insertan solo si no hay ya
  una con el mismo contenido. Volver a correr el script completo no duplica
  nada, sin importar cuántas veces ya se haya aplicado antes.

================================================================================
*/

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;

-- ── Parámetros ─────────────────────────────────────────────────────────────
DECLARE @soloSimular bit           = 1;                 -- <<< cambie a 0 para aplicar
DECLARE @codigo      nvarchar(40)  = N'PRY-2026-17';
DECLARE @actor       nvarchar(200) = N'Henry Alexis Ortez Banegas';

DECLARE @ahora datetime2 = SYSUTCDATETIME();

-- ── Guardas previas ────────────────────────────────────────────────────────
IF OBJECT_ID('Proyectos') IS NULL OR OBJECT_ID('ProyectoEntregables') IS NULL
OR OBJECT_ID('ProyectoActividades') IS NULL OR OBJECT_ID('BitacoraProyecto') IS NULL
OR OBJECT_ID('ProyectoDependenciasActividad') IS NULL
BEGIN
    RAISERROR('Falta alguna de las tablas requeridas. No se hizo nada.', 16, 1);
    RETURN;
END

DECLARE @proyectoId int = (SELECT Id FROM Proyectos WHERE Codigo = @codigo AND IsDeleted = 0);

IF @proyectoId IS NULL
BEGIN
    RAISERROR('No se encontró un proyecto activo con código %s. Ajuste @codigo. No se hizo nada.', 16, 1, @codigo);
    RETURN;
END

-- El entregable original se ubica por una actividad marcadora, no por ser el
-- único: en cuanto la PARTE B corra una vez, el proyecto va a tener 4.
DECLARE @entregableId int = (
    SELECT TOP 1 en.Id
      FROM ProyectoEntregables en
      JOIN ProyectoActividades a ON a.EntregableId = en.Id
     WHERE en.ProyectoId = @proyectoId AND a.Nombre = N'Arranque del repositorio y estructura base');

IF @entregableId IS NULL
BEGIN
    RAISERROR('No se encontró el entregable original (con la actividad «Arranque del repositorio y estructura base»). Corra antes poner_al_dia_portal_digitalizacion.sql. No se hizo nada.', 16, 1);
    RETURN;
END

BEGIN TRAN;

-- ══ PARTE A · Antecedente (gobdigital) ══════════════════════════════════════

-- ── PASO 0 · Repara la fila que quedó con el nombre y fechas anteriores ────
UPDATE a
   SET a.Nombre          = N'Ejecución inicial de la idea en HTML (gobdigital)',
       a.Descripcion     = N'Primeras semanas de desarrollo del portal estático en HTML y JavaScript a '
         + N'partir de la idea y el diseño previos, en el repositorio hetchk69/gobdigital, hasta que '
         + N'arrancó la reestructuración hacia Clean Architecture. El repositorio siguió recibiendo '
         + N'commits después de esta fecha — 278 en total, el último el 12-ago-2026 — porque gobdigital '
         + N'corrió en paralelo a este portal hasta reemplazarse del todo.',
       a.FechaInicioPlan = '2026-06-04',
       a.FechaFinPlan    = '2026-06-25',
       a.FechaInicioReal = '2026-06-04',
       a.FechaFinReal    = '2026-06-25',
       a.UpdatedAt       = @ahora,
       a.UpdatedBy       = @actor
  FROM ProyectoActividades a
 WHERE a.EntregableId = @entregableId
   AND a.Nombre = N'Ejecución de la idea: desarrollo del portal en HTML (gobdigital)';

-- ── PASO 1 · Las tres actividades del antecedente ──────────────────────────
DECLARE @nuevas TABLE (
    Nombre nvarchar(400), Descripcion nvarchar(1000),
    IniReal date, FinReal date);

INSERT INTO @nuevas VALUES
 (N'Análisis y diseño del portal de Gobierno Digital (gobdigital)',
  N'Concepción y diseño del sitio de Gobierno Digital de DIGER, antes de que existiera el repositorio. '
  + N'No hay registro en git de esta etapa: el inicio (16-may-2026) es una fecha aproximada dada por el '
  + N'propietario del proyecto, no verificable. Cierra el 04-jun-2026, cuando el primer commit del '
  + N'repositorio hetchk69/gobdigital sube los archivos ya construidos ("Add files via upload").',
  '2026-05-16', '2026-06-04'),

 (N'Ejecución inicial de la idea en HTML (gobdigital)',
  N'Primeras semanas de desarrollo del portal estático en HTML y JavaScript a partir de la idea y el '
  + N'diseño previos, en el repositorio hetchk69/gobdigital, hasta que arrancó la reestructuración hacia '
  + N'Clean Architecture. El repositorio siguió recibiendo commits después de esta fecha — 278 en total, '
  + N'el último el 12-ago-2026 — porque gobdigital corrió en paralelo a este portal hasta reemplazarse '
  + N'del todo.',
  '2026-06-04', '2026-06-25'),

 (N'Reestructuración de idea a arquitectura limpia',
  N'Decisión de abandonar el HTML estático y reestructurar hacia Clean Architecture en .NET, entre el '
  + N'desarrollo de gobdigital y el arranque de este repositorio. Sin evidencia de git de esta etapa: '
  + N'es previa al primer commit del portal (26-jun-2026).',
  '2026-06-20', '2026-06-26');

INSERT INTO ProyectoActividades
    (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan,
     FechaInicioReal, FechaFinReal, AvancePct, Estado, CreatedAt, CreatedBy)
SELECT @entregableId, 0, n.Nombre, n.Descripcion, n.IniReal, n.FinReal,
       n.IniReal, n.FinReal, 100, N'Completada', @ahora, @actor
FROM @nuevas n
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoActividades a
    WHERE a.EntregableId = @entregableId AND a.Nombre = n.Nombre);

-- ── PASO 2 · Reordenar: las tres nuevas van primero ────────────────────────
-- Los órdenes 24 a 29 son los seis frentes del 28-08 al 01-09 que registra
-- actualizar_actividades_desde_ramas_2026-09-01.sql. El script llegó sin ellos
-- —se escribió contra un ambiente que todavía no los tenía— y por eso abortaba
-- acá con «hay 6 actividades que este orden no contempla». Los dos guiones
-- declaran ahora la MISMA secuencia de 33, así que da lo mismo cuál corra
-- primero y volver a correr cualquiera después del otro no descoloca nada.
DECLARE @orden TABLE (Orden int, Nombre nvarchar(400));
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

UPDATE a
   SET a.Orden     = o.Orden,
       a.UpdatedAt = CASE WHEN a.Orden <> o.Orden THEN @ahora ELSE a.UpdatedAt END,
       a.UpdatedBy = CASE WHEN a.Orden <> o.Orden THEN @actor ELSE a.UpdatedBy END
  FROM ProyectoActividades a
  JOIN @orden o ON o.Nombre = a.Nombre
 WHERE a.EntregableId = @entregableId;

DECLARE @huerfanas int = (
    SELECT COUNT(*) FROM ProyectoActividades a
     WHERE a.EntregableId = @entregableId
       AND NOT EXISTS (SELECT 1 FROM @orden o WHERE o.Nombre = a.Nombre));

IF @huerfanas > 0
BEGIN
    ROLLBACK TRAN;
    RAISERROR('Hay %d actividad(es) en el entregable original que este orden no contempla. No se cambió nada; corra antes poner_al_dia_portal_digitalizacion.sql o revise los nombres.', 16, 1, @huerfanas);
    RETURN;
END

-- ══ PARTE B · Plan de despliegue y operación ════════════════════════════════

-- ── PASO 3 · Los tres entregables nuevos ───────────────────────────────────
DECLARE @ordenBase int = (SELECT ISNULL(MAX(Orden), 0) FROM ProyectoEntregables WHERE ProyectoId = @proyectoId);

DECLARE @entregablesNuevos TABLE (Orden int, Nombre nvarchar(400), Descripcion nvarchar(1000), FechaPlan date);
INSERT INTO @entregablesNuevos VALUES
 (@ordenBase + 1, N'Coordinación y despliegue',
  N'Puesta en producción de los últimos cambios de la plataforma y alta de usuarios para las personas que la van a operar. Primera etapa del plan de despliegue.',
  '2026-09-07'),
 (@ordenBase + 2, N'Capacitación',
  N'Capacitación por área a las personas que van a usar la plataforma, sobre los módulos que les corresponden.',
  '2026-09-14'),
 (@ordenBase + 3, N'Mantenimiento y seguimiento',
  N'Operación continua después del despliegue: mantenimiento, seguimiento de uso, retroalimentación de usuarios y las actualizaciones que resulten. La ventana plan (15 al 21-sep-2026) es solo el arranque — es trabajo continuo y va a necesitar que se le extienda la fecha de cierre más adelante.',
  '2026-09-21');

INSERT INTO ProyectoEntregables
    (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable,
     CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
SELECT @proyectoId, en.Orden, en.Nombre, en.Descripcion, en.FechaPlan, NULL, N'Pendiente', NULL, NULL,
       @ahora, @actor, NULL, NULL
FROM @entregablesNuevos en
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoEntregables x WHERE x.ProyectoId = @proyectoId AND x.Nombre = en.Nombre);

-- ── PASO 4 · Sus actividades ────────────────────────────────────────────────
DECLARE @actividadesNuevas TABLE (
    EntregableNombre nvarchar(400), Orden int, Nombre nvarchar(400), Descripcion nvarchar(1000),
    IniPlan date, FinPlan date);

INSERT INTO @actividadesNuevas VALUES
 (N'Coordinación y despliegue', 1, N'Despliegue de últimos cambios realizados en la plataforma',
  N'Publicar en producción los cambios ya construidos y probados.', '2026-09-01', '2026-09-04'),
 (N'Coordinación y despliegue', 2, N'Creación de usuarios de las personas que la van a utilizar',
  N'Alta de cuentas y asignación de roles para el personal operativo, sobre la versión ya desplegada.',
  '2026-09-04', '2026-09-07'),

 (N'Capacitación', 1, N'Coordinación de capacitaciones con diferentes áreas de DIGER',
  N'Coordinar agenda, logística y disponibilidad con las tres áreas (SIGER, Proyectos, Gerencia) para programar sus sesiones de capacitación.',
  '2026-09-08', '2026-09-14'),
 (N'Capacitación', 2, N'Capacitación: SIGER',
  N'Sesión de capacitación sobre el módulo de inventario y conciliación SIGER. Fecha por definir, sujeta a la coordinación con el área.', NULL, NULL),
 (N'Capacitación', 3, N'Capacitación: Proyectos',
  N'Sesión de capacitación sobre el módulo de seguimiento de proyectos. Fecha por definir, sujeta a la coordinación con el área.', NULL, NULL),
 (N'Capacitación', 4, N'Capacitación: Gerencia',
  N'Sesión de capacitación para Gerencia. Fecha por definir, sujeta a la coordinación con el área.', NULL, NULL),

 (N'Mantenimiento y seguimiento', 1, N'Mantenimiento de la plataforma',
  N'Atención de incidencias y ajustes operativos post-despliegue. Trabajo continuo; la fecha de cierre plan es solo el arranque.',
  '2026-09-15', '2026-09-21'),
 (N'Mantenimiento y seguimiento', 2, N'Seguimiento de uso de la plataforma',
  N'Monitoreo de adopción y uso real de la plataforma por parte de las áreas capacitadas.',
  '2026-09-15', '2026-09-21'),
 (N'Mantenimiento y seguimiento', 3, N'Talleres de retroalimentación de la plataforma',
  N'Sesiones con los usuarios para recoger observaciones sobre el uso real, después de la capacitación y el arranque de uso.',
  '2026-09-15', '2026-09-21'),
 (N'Mantenimiento y seguimiento', 4, N'Nuevas actualizaciones',
  N'Cambios que resulten del mantenimiento, el seguimiento y los talleres de retroalimentación.',
  '2026-09-15', '2026-09-21');

INSERT INTO ProyectoActividades
    (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan,
     FechaInicioReal, FechaFinReal, AvancePct, Estado, CreatedAt, CreatedBy)
SELECT en.Id, ac.Orden, ac.Nombre, ac.Descripcion, ac.IniPlan, ac.FinPlan, NULL, NULL, 0, N'Pendiente', @ahora, @actor
FROM @actividadesNuevas ac
JOIN ProyectoEntregables en ON en.ProyectoId = @proyectoId AND en.Nombre = ac.EntregableNombre
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoActividades x WHERE x.EntregableId = en.Id AND x.Nombre = ac.Nombre);

-- ── PASO 4b · Repara capacitaciones que ya estaban cargadas con otro nombre
--             o con fechas puestas (de una corrida manual o anterior) ──────
-- Quita las fechas a las capacitaciones por área que ya existan con fecha.
UPDATE a
   SET a.FechaInicioPlan = NULL,
       a.FechaFinPlan    = NULL,
       a.UpdatedAt       = @ahora,
       a.UpdatedBy       = @actor
  FROM ProyectoActividades a
  JOIN ProyectoEntregables en ON en.Id = a.EntregableId AND en.ProyectoId = @proyectoId AND en.Nombre = N'Capacitación'
 WHERE a.Nombre IN (N'Capacitación: SIGER', N'Capacitación: Proyectos', N'Capacitación: Gerencia')
   AND (a.FechaInicioPlan IS NOT NULL OR a.FechaFinPlan IS NOT NULL);

-- «Capacitación: Etapa3» queda reemplazada por «Capacitación: Gerencia». Se
-- quita solo si sigue sin avance y sin nada que dependa de ella — si alguien
-- ya reportó algo sobre ella, mejor que la borre a mano quien lo sepa.
DELETE a
  FROM ProyectoActividades a
  JOIN ProyectoEntregables en ON en.Id = a.EntregableId AND en.ProyectoId = @proyectoId AND en.Nombre = N'Capacitación'
 WHERE a.Nombre = N'Capacitación: Etapa3'
   AND a.AvancePct = 0
   AND NOT EXISTS (SELECT 1 FROM ProyectoAvances av WHERE av.ActividadId = a.Id)
   AND NOT EXISTS (SELECT 1 FROM ProyectoDependenciasActividad d WHERE d.PredecesoraId = a.Id);

-- «Despliegue de últimos cambios» y «Creación de usuarios» pueden haber
-- quedado con la misma semana completa (01–07-sep) de una corrida anterior
-- a que se partiera el rango. Si es así, se separan para que la dependencia
-- entre ambas no se marque como choque en el cronograma.
UPDATE a
   SET a.FechaInicioPlan = '2026-09-01', a.FechaFinPlan = '2026-09-04',
       a.UpdatedAt = @ahora, a.UpdatedBy = @actor
  FROM ProyectoActividades a
  JOIN ProyectoEntregables en ON en.Id = a.EntregableId AND en.ProyectoId = @proyectoId AND en.Nombre = N'Coordinación y despliegue'
 WHERE a.Nombre = N'Despliegue de últimos cambios realizados en la plataforma'
   AND (a.FechaInicioPlan <> '2026-09-01' OR a.FechaFinPlan <> '2026-09-04');

UPDATE a
   SET a.FechaInicioPlan = '2026-09-04', a.FechaFinPlan = '2026-09-07',
       a.UpdatedAt = @ahora, a.UpdatedBy = @actor
  FROM ProyectoActividades a
  JOIN ProyectoEntregables en ON en.Id = a.EntregableId AND en.ProyectoId = @proyectoId AND en.Nombre = N'Coordinación y despliegue'
 WHERE a.Nombre = N'Creación de usuarios de las personas que la van a utilizar'
   AND (a.FechaInicioPlan <> '2026-09-04' OR a.FechaFinPlan <> '2026-09-07');

-- ── PASO 5 · Dependencia: crear usuarios espera al despliegue ─────────────
INSERT INTO ProyectoDependenciasActividad (SucesoraId, PredecesoraId, Tipo)
SELECT suc.Id, pre.Id, N'FinComienzo'
FROM ProyectoEntregables en
JOIN ProyectoActividades suc ON suc.EntregableId = en.Id AND suc.Nombre = N'Creación de usuarios de las personas que la van a utilizar'
JOIN ProyectoActividades pre ON pre.EntregableId = en.Id AND pre.Nombre = N'Despliegue de últimos cambios realizados en la plataforma'
WHERE en.ProyectoId = @proyectoId AND en.Nombre = N'Coordinación y despliegue'
  AND NOT EXISTS (SELECT 1 FROM ProyectoDependenciasActividad x WHERE x.SucesoraId = suc.Id AND x.PredecesoraId = pre.Id);

-- ── PASO 5b · Retirar «Puesta en producción», que este guion reemplaza ─────
-- «Despliegue de últimos cambios realizados en la plataforma» (01–04 sep) es el
-- mismo trabajo que la actividad «Puesta en producción» que ya traía el
-- entregable original con plan 23–30 sep. Dejar las dos dibujaba el despliegue
-- dos veces en el cronograma, en semanas que se contradicen, y ninguna de las
-- dos decía cuál era la buena.
--
-- Se CANCELA en vez de borrarse: cancelada sale del promedio igual que si no
-- estuviera, pero queda el rastro de que existió y por qué se retiró. Borrarla
-- dejaría un hueco sin explicación en una ficha que alguien va a leer dentro de
-- seis meses.
--
-- Solo se cancela si la actividad que la reemplaza ya está creada: si el guion
-- se corriera a medias, cancelar la vieja sin tener la nueva dejaría el
-- proyecto sin despliegue en ninguna parte.
UPDATE a
   SET a.Estado    = N'Cancelada',
       a.UpdatedAt = @ahora,
       a.UpdatedBy = @actor
  FROM ProyectoActividades a
 WHERE a.EntregableId = @entregableId
   AND a.Nombre       = N'Puesta en producción'
   AND a.Estado      <> N'Cancelada'
   AND EXISTS (
       SELECT 1
         FROM ProyectoActividades n
         JOIN ProyectoEntregables en ON en.Id = n.EntregableId
        WHERE en.ProyectoId = @proyectoId
          AND en.Nombre     = N'Coordinación y despliegue'
          AND n.Nombre      = N'Despliegue de últimos cambios realizados en la plataforma');

-- ══ PASO 6 · Avance del proyecto ═════════════════════════════════════════════
-- Promedio de entregables, cada uno promedio de sus propias actividades — ya
-- no es un promedio plano de todas las actividades: con 4 entregables, un
-- promedio plano le daría más peso silencioso a los que tienen más filas.
;WITH avancePorEntregable AS (
    SELECT e.Id,
           COALESCE(
             (SELECT CAST(ROUND(AVG(CAST(a.AvancePct AS float)), 0) AS int)
                FROM ProyectoActividades a
               WHERE a.EntregableId = e.Id AND a.Estado <> N'Cancelada'),
             CASE e.Estado WHEN N'Completado' THEN 100 WHEN N'EnProceso' THEN 50 ELSE 0 END
           ) AS Avance
      FROM ProyectoEntregables e
     WHERE e.ProyectoId = @proyectoId AND e.Estado <> N'Cancelado'
)
UPDATE Proyectos
   SET AvancePct = (SELECT CAST(ROUND(AVG(CAST(Avance AS float)), 0) AS int) FROM avancePorEntregable),
       UpdatedAt = @ahora,
       UpdatedBy = @actor
 WHERE Id = @proyectoId;

-- ══ PASO 7 · Bitácora ═══════════════════════════════════════════════════════
IF NOT EXISTS (
    SELECT 1 FROM BitacoraProyecto
     WHERE ProyectoId = @proyectoId
       AND Tipo = N'ModificacionEstructura'
       AND Detalle LIKE N'%Análisis y diseño del portal de Gobierno Digital%')
BEGIN
    INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
    VALUES (@proyectoId, N'ModificacionEstructura',
      N'Se agregan al inicio de la secuencia del entregable original las tres actividades del antecedente '
      + N'del proyecto: «Análisis y diseño del portal de Gobierno Digital (gobdigital)» (16-may al '
      + N'04-jun-2026), «Ejecución inicial de la idea en HTML (gobdigital)» (04-jun al 25-jun-2026) y '
      + N'«Reestructuración de idea a arquitectura limpia» (20-jun al 26-jun-2026). Secuencia reordenada.',
      @actor, @ahora);
END

IF NOT EXISTS (
    SELECT 1 FROM BitacoraProyecto
     WHERE ProyectoId = @proyectoId AND Tipo = N'ModificacionFicha' AND Detalle LIKE N'Contexto histórico:%')
BEGIN
    INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
    VALUES (@proyectoId, N'ModificacionFicha',
      N'Contexto histórico: este portal sucede al sitio de Gobierno Digital de DIGER publicado como '
      + N'HTML estático en el repositorio hetchk69/gobdigital, sin backend ni base de datos propia. '
      + N'Se publicaba con GitHub Pages; hoy esa publicación está deshabilitada y el sitio ya no está '
      + N'en línea. De páginas HTML sueltas, sin autenticación ni control de acceso, se pasó a esta '
      + N'aplicación .NET con Clean Architecture, base de datos relacional y permisos por rol.',
      @actor, @ahora);
END

IF NOT EXISTS (
    SELECT 1 FROM BitacoraProyecto
     WHERE ProyectoId = @proyectoId AND Tipo = N'ModificacionEstructura'
       AND Detalle LIKE N'%Coordinación y despliegue%')
BEGIN
    INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
    VALUES (@proyectoId, N'ModificacionEstructura',
      N'Se agregan tres entregables para la fase de despliegue y operación: «Coordinación y despliegue» '
      + N'(01–07-sep-2026), «Capacitación» (08–14-sep-2026, tres áreas: SIGER, Proyectos, Gerencia) y '
      + N'«Mantenimiento y seguimiento» (15–21-sep-2026, ventana inicial de trabajo continuo). '
      + N'El proyecto pasa a tener 4 entregables. Fechas en plan — nada de esto ha ocurrido todavía.',
      @actor, @ahora);
END

IF NOT EXISTS (
    SELECT 1 FROM BitacoraProyecto
     WHERE ProyectoId = @proyectoId AND Tipo = N'ModificacionEstructura'
       AND Detalle LIKE N'%se cancela «Puesta en producción»%')
BEGIN
    INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
    VALUES (@proyectoId, N'ModificacionEstructura',
      N'Por quedar duplicada con «Despliegue de últimos cambios realizados en la plataforma» '
      + N'(01–04-sep-2026), del entregable «Coordinación y despliegue», se cancela «Puesta en '
      + N'producción», que traía plan 23–30-sep-2026. Era el mismo trabajo registrado dos veces '
      + N'en semanas que se contradecían. Se cancela y no se borra para que quede el rastro.',
      @actor, @ahora);
END

IF NOT EXISTS (
    SELECT 1 FROM BitacoraProyecto
     WHERE ProyectoId = @proyectoId AND Tipo = N'ModificacionFicha'
       AND Detalle LIKE N'Sobre la caída del avance%')
BEGIN
    INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
    VALUES (@proyectoId, N'ModificacionFicha',
      N'Sobre la caída del avance: al pasar el proyecto de 1 a 4 entregables, el avance baja de '
      + N'87 % a 22 % aunque no se haya perdido nada de lo construido. El portal promedia los '
      + N'entregables sin ponderar —así está definido en Proyecto.RecalcularAvance— y tres de los '
      + N'cuatro son fases que no han empezado. El desarrollo sigue en 88 %. El 22 % dice que '
      + N'arranca la fase de despliegue, no que el proyecto vaya atrasado.',
      @actor, @ahora);
END

-- ══ Informe ═══════════════════════════════════════════════════════════════
SELECT e.Orden AS OrdEnt, e.Nombre AS Entregable, e.FechaPlan AS EntregaPlan,
       a.Orden AS OrdAct, a.Nombre AS Actividad, a.Estado,
       a.FechaInicioPlan AS IniPlan, a.FechaFinPlan AS FinPlan,
       a.FechaInicioReal AS IniReal, a.FechaFinReal AS FinReal
  FROM ProyectoEntregables e
  LEFT JOIN ProyectoActividades a ON a.EntregableId = e.Id
 WHERE e.ProyectoId = @proyectoId
 ORDER BY e.Orden, a.Orden;

SELECT Codigo, Estado, AvancePct AS Avance FROM Proyectos WHERE Id = @proyectoId;

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
