/*
    Concesiones del submódulo Proyectos.Documentos (repositorio documental).

    Mismas razones que otorgar_permisos_proyectos.sql para hacerlo a mano:
      - PermisosSeedService siembra UNA sola vez y en esta base ya corrió.
      - Una migración EF tampoco sirve: corre ANTES que PermissionCatalogSyncService, así que las
        claves todavía no existen en Permisos cuando la migración se aplica.
      - En una base NUEVA no hace falta: la siembra las deriva sola.

    IMPORTANTE: hay que arrancar la aplicación al menos una vez después de desplegar el código
    para que PermissionCatalogSyncService descubra por reflexión las cuatro claves nuevas e
    inserte sus filas en Permisos. Este script solo concede lo que ya existe de los dos lados;
    corrido antes, no otorga nada y lo dice en el resultado.

    Política aplicada (punto de partida; se ajusta desde /Accesos/Permisos sin tocar SQL):
      - Administrador ....... no lleva filas: tiene bypass por código.
      - Jefes ............... ver, subir, editar y archivar.
      - Empleado ............ ver y subir. Quien ejecuta el proyecto es quien tiene el acta en la
                              mano; archivar documentación ajena es otra cosa.
      - Consultor ........... solo ver (es EsSoloLectura: la matriz rechaza darle otra cosa).

    Idempotente: se puede correr dos veces sin duplicar concesiones ni bitácora.
*/
SET NOCOUNT ON;

DECLARE @actor nvarchar(200) = N'script-permisos-documentos';

DECLARE @conceder TABLE (RolId nvarchar(80), PermisoClave nvarchar(80));

INSERT INTO @conceder (RolId, PermisoClave) VALUES
    (N'JefeInstitucion', N'Proyectos.Documentos.Ver'),
    (N'JefeInstitucion', N'Proyectos.Documentos.Crear'),
    (N'JefeInstitucion', N'Proyectos.Documentos.Editar'),
    (N'JefeInstitucion', N'Proyectos.Documentos.Eliminar'),

    (N'JefeArea',        N'Proyectos.Documentos.Ver'),
    (N'JefeArea',        N'Proyectos.Documentos.Crear'),
    (N'JefeArea',        N'Proyectos.Documentos.Editar'),
    (N'JefeArea',        N'Proyectos.Documentos.Eliminar'),

    (N'JefeUnidad',      N'Proyectos.Documentos.Ver'),
    (N'JefeUnidad',      N'Proyectos.Documentos.Crear'),
    (N'JefeUnidad',      N'Proyectos.Documentos.Editar'),
    (N'JefeUnidad',      N'Proyectos.Documentos.Eliminar'),

    (N'Empleado',        N'Proyectos.Documentos.Ver'),
    (N'Empleado',        N'Proyectos.Documentos.Crear'),

    (N'Consultor',       N'Proyectos.Documentos.Ver');

/* Solo lo que existe de los dos lados y todavía no está concedido. */
DECLARE @nuevas TABLE (RolId nvarchar(80), PermisoClave nvarchar(80), PermisoNombre nvarchar(150));

INSERT INTO @nuevas (RolId, PermisoClave, PermisoNombre)
SELECT c.RolId, c.PermisoClave, p.Nombre
FROM @conceder c
JOIN Roles    r ON r.Id = c.RolId
JOIN Permisos p ON p.Id = c.PermisoClave
WHERE NOT EXISTS (
    SELECT 1 FROM RolPermisos rp
    WHERE rp.RolId = c.RolId AND rp.PermisoClave = c.PermisoClave
);

BEGIN TRANSACTION;

INSERT INTO RolPermisos (RolId, PermisoClave)
SELECT RolId, PermisoClave FROM @nuevas;

/* La bitácora es append-only: queda el rastro de que esto lo otorgó un script y no una persona. */
INSERT INTO PermisosAuditoria (RolId, PermisoClave, PermisoNombre, Accion, Actor, Fecha)
SELECT RolId, PermisoClave, PermisoNombre, N'Otorgado', @actor, SYSUTCDATETIME()
FROM @nuevas;

COMMIT;

SELECT CONCAT(N'Concesiones nuevas: ', (SELECT COUNT(*) FROM @nuevas)) AS Resultado;

/* Si esto sale vacío, la aplicación todavía no arrancó con el código nuevo. */
SELECT CONCAT(N'Claves descubiertas en el catálogo: ',
              (SELECT COUNT(*) FROM Permisos WHERE Id LIKE N'Proyectos.Documentos.%')) AS Catalogo;

SELECT rp.RolId, rp.PermisoClave
FROM RolPermisos rp
WHERE rp.PermisoClave LIKE N'Proyectos.Documentos.%'
ORDER BY rp.RolId, rp.PermisoClave;
