/*
================================================================================
  Unifica los entregables «espejo» en un único entregable por proyecto.
================================================================================

  CÓMO SE USA
  -----------
  1. Ejecútelo tal cual. Arranca en MODO SIMULACIÓN: no cambia nada, solo informa
     qué proyectos entrarían y cuántas filas se moverían.
  2. Revise el listado.
  3. Cambie @soloSimular a 0 y vuelva a ejecutarlo para aplicar.

  Requiere la misma versión de esquema que el portal al 26-08-2026 (con las tablas
  ProyectoEntregables, ProyectoActividades, ProyectoAvances y BitacoraProyecto).
  Si falta alguna, se detiene y lo dice.

  EL PROBLEMA QUE RESUELVE
  ------------------------
  En algunos entornos cada hito se convirtió en DOS filas: un entregable y una
  actividad con el mismo nombre. La ficha muestra una escalera de pares repetidos
  y el nivel intermedio no aporta nada: el entregable no es un producto, es la
  misma tarea otra vez.

  QUÉ HACE
  --------
  Por cada proyecto afectado deja UN entregable con el nombre del proyecto y le
  cuelga todas las actividades, en el orden que traían. Las actividades no se
  tocan: conservan Id, nombre, fechas, avance, responsable y dependencias.

  A QUIÉN NO TOCA
  ---------------
  Un proyecto entra solo si TODOS sus entregables son espejo (un entregable con
  exactamente una actividad, del mismo nombre). Un proyecto con desglose real
  queda intacto sin necesidad de excluirlo a mano. Tampoco entra lo borrado
  lógicamente.

  QUÉ SE PRESERVA
  ---------------
    · Las imputaciones de la bitácora. Los avances que apuntan a un entregable
      que desaparece se repuntan al que queda ANTES de borrar: la FK es SetNull,
      así que borrar primero vaciaría la imputación en silencio.
    · Las dependencias entre actividades: apuntan a actividades, que no se borran.
    · El avance de cada proyecto. Con un entregable por actividad, el promedio de
      entregables ya era el promedio de actividades; al juntarlas da lo mismo.
      La simulación lo verifica y avisa si algún proyecto fuera a cambiar.

  DECISIÓN: el contenedor queda SIN responsable, igual que en la conversión de
  hitos anterior. Un entregable que agrupa actividades de varias personas no es de
  nadie en particular, y el responsable del proyecto ya está en la ficha.

  Idempotente y transaccional: si no hay nada que hacer, lo informa y sale.
================================================================================
*/

DECLARE @soloSimular bit = 1;      -- ← PONER EN 0 PARA APLICAR

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor nvarchar(200) = N'script-aplanar-entregables';

/* ── 0. Guarda de esquema ─────────────────────────────────────────────────── */
IF OBJECT_ID('ProyectoEntregables') IS NULL
   OR OBJECT_ID('ProyectoActividades') IS NULL
   OR OBJECT_ID('ProyectoAvances') IS NULL
   OR OBJECT_ID('BitacoraProyecto') IS NULL
BEGIN
    SELECT N'Esta base no tiene el esquema del módulo de proyectos. No se hizo nada.' AS Resultado;
    RETURN;
END

/* ── 1. Qué proyectos entran ──────────────────────────────────────────────────
   Dos casos, y los dos hacen falta:
     · VariosEspejo: más de un entregable espejo  → se unifican.
     · UnoEspejo:    un solo entregable espejo    → no hay nada que mover, pero su
       nombre es el de la actividad y hay que ponerle el del proyecto.
*/
DECLARE @objetivo TABLE (
    ProyectoId  int PRIMARY KEY,
    Codigo      nvarchar(30),
    Nombre      nvarchar(300),
    Sobrevive   int,
    Entregables int);

INSERT INTO @objetivo (ProyectoId, Codigo, Nombre, Sobrevive, Entregables)
SELECT p.Id, p.Codigo, p.Nombre,
       (SELECT TOP 1 e2.Id FROM ProyectoEntregables e2
         WHERE e2.ProyectoId = p.Id ORDER BY e2.Orden, e2.Id),
       (SELECT COUNT(*) FROM ProyectoEntregables e3 WHERE e3.ProyectoId = p.Id)
FROM Proyectos p
WHERE p.IsDeleted = 0
  AND EXISTS (SELECT 1 FROM ProyectoEntregables e WHERE e.ProyectoId = p.Id)
  /* Todos sus entregables tienen que ser espejo. */
  AND NOT EXISTS (
      SELECT 1 FROM ProyectoEntregables e
       WHERE e.ProyectoId = p.Id
         AND ( (SELECT COUNT(*) FROM ProyectoActividades a WHERE a.EntregableId = e.Id) <> 1
               OR NOT EXISTS (SELECT 1 FROM ProyectoActividades a
                               WHERE a.EntregableId = e.Id AND a.Nombre = e.Nombre) ) )
  /* Y algo tiene que quedar por corregir: o hay varios, o el único está mal nombrado. */
  AND ( (SELECT COUNT(*) FROM ProyectoEntregables e WHERE e.ProyectoId = p.Id) > 1
        OR EXISTS (SELECT 1 FROM ProyectoEntregables e
                    WHERE e.ProyectoId = p.Id AND e.Nombre <> p.Nombre) );

IF NOT EXISTS (SELECT 1 FROM @objetivo)
BEGIN
    SELECT N'No hay proyectos con entregables espejo. No se hizo nada.' AS Resultado;
    RETURN;
END

/* ── 2. Qué se va a hacer ─────────────────────────────────────────────────── */
SELECT N'Proyectos afectados' AS Resumen, COUNT(*) AS Cantidad FROM @objetivo
UNION ALL SELECT N'Entregables que se borran',
       (SELECT COUNT(*) FROM ProyectoEntregables e JOIN @objetivo o ON o.ProyectoId = e.ProyectoId
         WHERE e.Id <> o.Sobrevive)
UNION ALL SELECT N'Actividades que se mueven',
       (SELECT COUNT(*) FROM ProyectoActividades a JOIN ProyectoEntregables e ON e.Id = a.EntregableId
          JOIN @objetivo o ON o.ProyectoId = e.ProyectoId)
UNION ALL SELECT N'Avances que se repuntan',
       (SELECT COUNT(*) FROM ProyectoAvances av JOIN ProyectoEntregables e ON e.Id = av.EntregableId
          JOIN @objetivo o ON o.ProyectoId = e.ProyectoId WHERE av.EntregableId <> o.Sobrevive);

SELECT o.Codigo, LEFT(o.Nombre, 48) AS Proyecto, o.Entregables AS EntregablesHoy,
       (SELECT COUNT(*) FROM ProyectoActividades a
          JOIN ProyectoEntregables e ON e.Id = a.EntregableId
         WHERE e.ProyectoId = o.ProyectoId) AS ActividadesQueQuedan
FROM @objetivo o ORDER BY o.Codigo;

/* Control de avance: con la estructura espejo estos números tienen que coincidir.
   Si alguna fila sale acá, PARE y revísela antes de aplicar. */
SELECT N'ATENCIÓN: el avance de este proyecto cambiaría' AS Aviso,
       p.Codigo, p.AvancePct AS Ahora,
       CAST(ROUND(AVG(CAST(a.AvancePct AS float)), 0) AS int) AS Despues
FROM Proyectos p
JOIN @objetivo o ON o.ProyectoId = p.Id
JOIN ProyectoEntregables e ON e.ProyectoId = p.Id
JOIN ProyectoActividades a ON a.EntregableId = e.Id
WHERE a.Estado <> 'Cancelada'
GROUP BY p.Codigo, p.AvancePct
HAVING p.AvancePct <> CAST(ROUND(AVG(CAST(a.AvancePct AS float)), 0) AS int);

IF @soloSimular = 1
BEGIN
    SELECT N'SIMULACIÓN: no se modificó nada. Ponga @soloSimular = 0 para aplicar.' AS Resultado;
    RETURN;
END

/* ── 3. Aplicar ───────────────────────────────────────────────────────────── */
BEGIN TRANSACTION;

/* 3.1 Repuntar la bitácora ANTES de borrar. La FK EntregableId es SetNull: si se
       borrara primero, la imputación quedaría en NULL sin avisar. */
UPDATE av
   SET av.EntregableId = o.Sobrevive
  FROM ProyectoAvances av
  JOIN ProyectoEntregables e ON e.Id = av.EntregableId
  JOIN @objetivo o           ON o.ProyectoId = e.ProyectoId
 WHERE av.EntregableId <> o.Sobrevive;

DECLARE @avances int = @@ROWCOUNT;

/* 3.2 Mover las actividades. El orden nuevo respeta el que tenían los entregables:
       es el orden original de los hitos, o sea la secuencia real del frente. */
;WITH nuevo AS (
    SELECT a.Id,
           o.Sobrevive AS DestinoId,
           ROW_NUMBER() OVER (PARTITION BY o.ProyectoId
                              ORDER BY e.Orden, e.Id, a.Orden) AS OrdenNuevo
      FROM ProyectoActividades a
      JOIN ProyectoEntregables e ON e.Id = a.EntregableId
      JOIN @objetivo o           ON o.ProyectoId = e.ProyectoId)
UPDATE a
   SET a.EntregableId = n.DestinoId,
       a.Orden        = n.OrdenNuevo
  FROM ProyectoActividades a
  JOIN nuevo n ON n.Id = a.Id;

DECLARE @actividades int = @@ROWCOUNT;

/* 3.3 El contenedor toma el nombre del proyecto. La fecha comprometida pasa a ser
       la más lejana de las que se juntan: no está entregado hasta que termina lo
       último que contiene. */
UPDATE e
   SET e.Nombre        = o.Nombre,
       e.Orden         = 1,
       e.Descripcion   = N'Contenedor creado al unificar los entregables espejo. '
                       + N'El desglose real en productos verificables está pendiente.',
       e.ResponsableId = NULL,
       e.Responsable   = NULL,
       e.FechaPlan     = (SELECT MAX(e2.FechaPlan) FROM ProyectoEntregables e2
                           WHERE e2.ProyectoId = o.ProyectoId),
       e.UpdatedAt     = SYSUTCDATETIME(),
       e.UpdatedBy     = @actor
  FROM ProyectoEntregables e
  JOIN @objetivo o ON o.Sobrevive = e.Id;

/* 3.4 Borrar los que quedaron vacíos. */
DELETE e
  FROM ProyectoEntregables e
  JOIN @objetivo o ON o.ProyectoId = e.ProyectoId
 WHERE e.Id <> o.Sobrevive;

DECLARE @borrados int = @@ROWCOUNT;

/* 3.5 Estado y fecha real del contenedor, derivados de sus actividades: el estado
       del hito viejo ya no describe al conjunto. */
UPDATE e
   SET e.Estado = CASE
        WHEN NOT EXISTS (SELECT 1 FROM ProyectoActividades a
                          WHERE a.EntregableId = e.Id
                            AND a.Estado NOT IN ('Completada', 'Cancelada'))
             THEN 'Completado'
        WHEN EXISTS (SELECT 1 FROM ProyectoActividades a
                      WHERE a.EntregableId = e.Id AND a.AvancePct > 0
                        AND a.Estado <> 'Cancelada')
             THEN 'EnProceso'
        ELSE 'Pendiente' END,
       e.FechaReal = CASE
        WHEN NOT EXISTS (SELECT 1 FROM ProyectoActividades a
                          WHERE a.EntregableId = e.Id
                            AND a.Estado NOT IN ('Completada', 'Cancelada'))
             THEN (SELECT MAX(a.FechaFinReal) FROM ProyectoActividades a
                    WHERE a.EntregableId = e.Id)
        ELSE NULL END
  FROM ProyectoEntregables e
  JOIN @objetivo o ON o.Sobrevive = e.Id;

/* 3.6 Auditoría: que quede el rastro de que esto lo hizo un script. */
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT o.ProyectoId, N'ModificacionEstructura',
       N'Se unificaron los entregables espejo en uno solo con el nombre del proyecto. '
     + N'Las actividades conservan su nombre, fechas, avance y dependencias.',
       @actor, SYSUTCDATETIME()
FROM @objetivo o;

COMMIT;

SELECT N'Aplicado.' AS Resultado,
       @actividades AS ActividadesMovidas,
       @borrados    AS EntregablesBorrados,
       @avances     AS AvancesRepuntados;

/* ── 4. Comprobación ──────────────────────────────────────────────────────── */
SELECT N'Entregables por proyecto (después)' AS Control, n.E AS Entregables, COUNT(*) AS Proyectos
FROM (SELECT p.Id, COUNT(e.Id) E
        FROM Proyectos p LEFT JOIN ProyectoEntregables e ON e.ProyectoId = p.Id
       WHERE p.IsDeleted = 0 GROUP BY p.Id) n
GROUP BY n.E ORDER BY n.E;

SELECT N'Espejos que quedan (deberían ser 0, salvo en proyectos borrados)' AS Control,
       COUNT(*) AS Cantidad
FROM ProyectoEntregables e
JOIN Proyectos p ON p.Id = e.ProyectoId AND p.IsDeleted = 0
JOIN ProyectoActividades a ON a.EntregableId = e.Id AND a.Nombre = e.Nombre;

SELECT N'Avances que perdieron su imputación (debería ser 0)' AS Control, COUNT(*) AS Cantidad
FROM ProyectoAvances WHERE EntregableId IS NULL AND ActividadId IS NOT NULL;
