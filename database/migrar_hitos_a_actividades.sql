/*
    Reacomoda el portafolio existente al modelo EDT (proyecto → entregable → actividad).

    Los ~191 hitos que hoy quedaron como ENTREGABLES eran en realidad actividades: tareas con
    fecha y responsable, no productos entregables. Este script las baja un nivel y le pone a cada
    proyecto un único entregable contenedor con el nombre del proyecto, para que el árbol quede
    armado y el desglose real se haga a mano desde la ficha.

        Antes                          Después
        ─────                          ───────
        Proyecto                       Proyecto
        └── hito 1  (entregable)       └── «Nombre del proyecto»  (entregable contenedor)
        └── hito 2  (entregable)           ├── actividad 1   (era el hito 1)
        └── hito 3  (entregable)           ├── actividad 2   (era el hito 2)
                                           └── actividad 3   (era el hito 3)

    ── LO QUE HAY QUE SABER ────────────────────────────────────────────────

    1. LA BITÁCORA NO PIERDE SU IMPUTACIÓN. Las entradas de ProyectoAvances que apuntaban a un
       hito quedan apuntando a la actividad en que se convirtió, y además al entregable
       contenedor. Nada queda desimputado.

    2. EL PORCENTAJE DE CADA ACTIVIDAD SALE DE SU ESTADO, con la regla 0/50/100 del PMI:
       Completado → 100 %, En proceso → 50 %, Pendiente → 0 %. Es la misma regla con la que el
       portal venía calculando el avance de esos entregables, así que el número que muestra cada
       proyecto NO CAMBIA con esta reorganización.

    3. LAS FECHAS. La fecha comprometida del hito pasa a ser la FECHA DE FIN de la actividad; la
       de INICIO queda vacía porque nadie la registró nunca — hay que llenarla a mano. La fecha
       real solo se copia en las actividades que están al 100 %.

    4. EL CONTENEDOR NO INVENTA DATOS. Su fecha comprometida es la del proyecto y, si el proyecto
       no tiene, la más lejana de sus actividades. Queda sin responsable a propósito: el
       responsable de un entregable tiene que ser un interesado del proyecto, y eso se decide
       persona por persona, no por script.

    5. IDEMPOTENTE. Solo toca proyectos que tengan entregables y NINGUNA actividad. Un proyecto
       que alguien ya empezó a desglosar a mano se salta entero, para no pisarle el trabajo.

        sqlcmd -S <servidor> -d <base> -i migrar_hitos_a_actividades.sql
*/

-- QUOTED_IDENTIFIER encendido y en su propio lote: Proyectos.Codigo lleva un índice único
-- filtrado y SQL Server rechaza la escritura si viene apagada, que es como la deja sqlcmd.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRAN;

DECLARE @ahora datetime2(7) = SYSUTCDATETIME();
DECLARE @actor nvarchar(150) = N'Migración a EDT';

-- ── 1. Qué proyectos se migran ──────────────────────────────────────────
DECLARE @pendientes TABLE (
    ProyectoId   int PRIMARY KEY,
    Nombre       nvarchar(300),
    FechaFinPlan date
);

INSERT INTO @pendientes (ProyectoId, Nombre, FechaFinPlan)
SELECT p.Id, p.Nombre, p.FechaFinPlan
FROM   Proyectos p
WHERE  EXISTS (SELECT 1 FROM ProyectoEntregables e WHERE e.ProyectoId = p.Id)
       -- Ninguna actividad todavía: si ya hay desglose manual, el proyecto se salta.
  AND  NOT EXISTS (
           SELECT 1
           FROM   ProyectoActividades a
           JOIN   ProyectoEntregables e ON e.Id = a.EntregableId
           WHERE  e.ProyectoId = p.Id);

-- ── 2. El entregable contenedor, uno por proyecto ───────────────────────
-- MERGE en vez de INSERT porque su OUTPUT sí puede devolver columnas del origen: hace falta
-- saber qué contenedor le tocó a cada proyecto, y un INSERT normal solo devuelve lo insertado.
DECLARE @contenedor TABLE (ProyectoId int PRIMARY KEY, EntregableId int);

MERGE ProyectoEntregables AS destino
USING (
    SELECT n.ProyectoId,
           n.Nombre,
           -- La del proyecto; si no tiene, la más lejana de lo que va a contener.
           FechaPlan = COALESCE(
               n.FechaFinPlan,
               (SELECT MAX(e.FechaPlan) FROM ProyectoEntregables e WHERE e.ProyectoId = n.ProyectoId)),
           Estado = CASE
               WHEN NOT EXISTS (SELECT 1 FROM ProyectoEntregables e
                                WHERE e.ProyectoId = n.ProyectoId
                                  AND e.Estado NOT IN ('Completado', 'Cancelado'))
                   THEN 'Completado'
               WHEN EXISTS (SELECT 1 FROM ProyectoEntregables e
                            WHERE e.ProyectoId = n.ProyectoId
                              AND e.Estado IN ('Completado', 'EnProceso'))
                   THEN 'EnProceso'
               ELSE 'Pendiente'
           END
    FROM @pendientes n
) AS origen
ON 1 = 0   -- nunca coincide: el MERGE es solo para poder usar OUTPUT con el origen
WHEN NOT MATCHED THEN
    INSERT (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal,
            Estado, ResponsableId, Responsable)
    VALUES (origen.ProyectoId, 1, origen.Nombre,
            N'Entregable creado al pasar el proyecto a la estructura de desglose. Contiene lo que ' +
            N'antes eran los hitos del proyecto, ahora actividades. Divídalo en los entregables ' +
            N'reales cuando estén definidos.',
            origen.FechaPlan, NULL, origen.Estado, NULL, NULL)
OUTPUT origen.ProyectoId, inserted.Id INTO @contenedor (ProyectoId, EntregableId);

-- ── 3. Los hitos bajan a actividades ────────────────────────────────────
DECLARE @map TABLE (EntregableViejoId int PRIMARY KEY, ActividadId int);

MERGE ProyectoActividades AS destino
USING (
    SELECT e.Id AS EntregableViejoId,
           c.EntregableId,
           e.Orden,
           e.Nombre,
           e.Descripcion,
           -- La fecha comprometida del hito es el cierre de la actividad. El inicio nadie lo
           -- registró nunca, así que queda vacío en vez de inventarse.
           FechaFinPlan = e.FechaPlan,
           FechaFinReal = CASE WHEN e.Estado = 'Completado' THEN e.FechaReal END,
           AvancePct = CASE e.Estado
               WHEN 'Completado' THEN 100
               WHEN 'EnProceso'  THEN 50
               ELSE 0
           END,
           Estado = CASE e.Estado
               WHEN 'Completado' THEN 'Completada'
               WHEN 'EnProceso'  THEN 'EnProceso'
               WHEN 'Cancelado'  THEN 'Cancelada'
               ELSE 'Pendiente'
           END,
           e.ResponsableId,
           e.Responsable
    FROM   ProyectoEntregables e
    JOIN   @contenedor c ON c.ProyectoId = e.ProyectoId
    WHERE  e.Id <> c.EntregableId          -- el contenedor recién creado no se migra a sí mismo
) AS origen
ON 1 = 0
WHEN NOT MATCHED THEN
    INSERT (EntregableId, Orden, Nombre, Descripcion,
            FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal,
            AvancePct, Estado, ResponsableId, Responsable)
    VALUES (origen.EntregableId, origen.Orden, origen.Nombre, origen.Descripcion,
            NULL, origen.FechaFinPlan, NULL, origen.FechaFinReal,
            origen.AvancePct, origen.Estado, origen.ResponsableId, origen.Responsable)
OUTPUT origen.EntregableViejoId, inserted.Id INTO @map (EntregableViejoId, ActividadId);

-- ── 4. La bitácora sigue a lo que apuntaba ──────────────────────────────
-- El JOIN lee a.EntregableId antes de que el UPDATE lo pise, así que la reasignación es correcta.
UPDATE a
SET    a.ActividadId  = m.ActividadId,
       a.EntregableId = c.EntregableId
FROM   ProyectoAvances a
JOIN   @map m        ON m.EntregableViejoId = a.EntregableId
JOIN   @contenedor c ON c.ProyectoId        = a.ProyectoId;

-- ── 5. Se van los entregables viejos, ya vacíos de referencias ──────────
DELETE e
FROM   ProyectoEntregables e
JOIN   @map m ON m.EntregableViejoId = e.Id;

-- ── 6. Recálculo del avance, con la misma regla del dominio ─────────────
-- Entregable con actividades: promedio de las vigentes. Sin actividades: 0/50/100 por estado.
-- Proyecto: promedio de sus entregables vigentes. Debe dar lo mismo que antes de correr esto.
UPDATE p
SET    p.AvancePct = ISNULL(x.Avance, 0)
FROM   Proyectos p
JOIN   @pendientes n ON n.ProyectoId = p.Id
OUTER APPLY (
    SELECT CAST(ROUND(AVG(CAST(y.Pct AS float)), 0) AS int) AS Avance
    FROM (
        SELECT Pct = CASE
            WHEN EXISTS (SELECT 1 FROM ProyectoActividades a
                         WHERE a.EntregableId = e.Id AND a.Estado <> 'Cancelada')
            THEN (SELECT ROUND(AVG(CAST(a.AvancePct AS float)), 0)
                  FROM ProyectoActividades a
                  WHERE a.EntregableId = e.Id AND a.Estado <> 'Cancelada')
            ELSE CASE e.Estado
                     WHEN 'Completado' THEN 100
                     WHEN 'EnProceso'  THEN 50
                     ELSE 0
                 END
        END
        FROM   ProyectoEntregables e
        WHERE  e.ProyectoId = p.Id AND e.Estado <> 'Cancelado'
    ) y
) x;

-- ── 7. Rastro en la auditoría ───────────────────────────────────────────
-- Una reorganización masiva que no deja rastro es indistinguible de una edición manual.
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT c.ProyectoId,
       'ModificacionEstructura',
       N'Se pasó el proyecto a la estructura de desglose: sus ' +
       CAST((SELECT COUNT(*) FROM ProyectoActividades a WHERE a.EntregableId = c.EntregableId) AS nvarchar(10)) +
       N' hitos quedaron como actividades del entregable «' + n.Nombre + N'».',
       @actor,
       @ahora
FROM   @contenedor c
JOIN   @pendientes n ON n.ProyectoId = c.ProyectoId;

COMMIT;

-- ── Resumen ─────────────────────────────────────────────────────────────
SELECT 'Proyectos migrados'     AS Concepto, COUNT(*) AS Cantidad FROM @contenedor
UNION ALL
SELECT 'Hitos → actividades',   COUNT(*) FROM @map
UNION ALL
SELECT 'Entregables totales',   COUNT(*) FROM ProyectoEntregables
UNION ALL
SELECT 'Actividades totales',   COUNT(*) FROM ProyectoActividades
UNION ALL
SELECT 'Avances imputados a actividad', COUNT(*) FROM ProyectoAvances WHERE ActividadId IS NOT NULL
UNION ALL
SELECT 'Avances sin imputar',   COUNT(*) FROM ProyectoAvances WHERE EntregableId IS NULL;
GO
