/*
    Corrección posterior a `actualizar_proyectos_ayudas_memoria.sql`.

    Ese script actualiza los hitos por NOMBRE. Al renombrar «Identificación de dos trámites
    digitalizables» a «Identificación de los trámites a digitalizar» —el acta muestra más de
    dos servicios en juego— el nombre dejó de coincidir y en vez de actualizarse se insertó
    uno nuevo, quedando los dos. Se retira el viejo (no tiene avances imputados) y se
    renumeran los hitos de los cuatro proyectos tocados, que quedaron con huecos.

    Lección para la próxima: emparejar por nombre solo funciona mientras el nombre no cambia.
    Si un hito se renombra, hay que decirlo explícitamente en el script.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

DELETE h
FROM ProyectoEntregables h
JOIN Proyectos p ON p.Id = h.ProyectoId
WHERE p.Codigo = N'PRY-2026-12'
  AND h.Nombre = N'Identificación de dos trámites digitalizables'
  AND NOT EXISTS (SELECT 1 FROM ProyectoAvances a WHERE a.EntregableId = h.Id);

WITH ordenados AS (
    SELECT h.Id, ROW_NUMBER() OVER (PARTITION BY h.ProyectoId ORDER BY h.Orden, h.Id) AS Nuevo
    FROM ProyectoEntregables h
    JOIN Proyectos p ON p.Id = h.ProyectoId
    WHERE p.Codigo IN (N'PRY-2026-06', N'PRY-2026-09', N'PRY-2026-12', N'PRY-2026-14')
)
UPDATE h SET h.Orden = o.Nuevo
FROM ProyectoEntregables h JOIN ordenados o ON o.Id = h.Id;

COMMIT;

SELECT p.Codigo, h.Orden, LEFT(h.Nombre, 52) AS Hito, h.Estado
FROM ProyectoEntregables h JOIN Proyectos p ON p.Id = h.ProyectoId
WHERE p.Codigo = N'PRY-2026-12' ORDER BY h.Orden;
