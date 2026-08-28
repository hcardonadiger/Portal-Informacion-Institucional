/*
    Concesiones iniciales del módulo Proyectos.

    Por qué a mano y no en una migración ni en el seed:
      - PermisosSeedService siembra UNA sola vez (guarda: solo corre si RolPermisos y
        PermisosAuditoria están ambas vacías). En una base ya usada no vuelve a correr, así que
        las claves nuevas no las hereda nadie.
      - Una migración EF tampoco sirve: corren ANTES que los hosted services, cuando
        PermissionCatalogSyncService todavía no insertó las claves en Permisos.
      - En una base NUEVA no hace falta ejecutar este script: la siembra deriva las concesiones sola.

    Política aplicada (punto de partida; se ajusta desde /Accesos/Permisos sin tocar SQL):
      - Administrador ....... no lleva filas: tiene bypass por código.
      - JefeInstitucion ..... ver, crear, editar y reportar avances.
      - JefeArea/JefeUnidad . ver, crear, editar y reportar avances.
      - Empleado ............ ver y reportar avances (ejecuta, no define el proyecto).
      - Consultor ........... solo ver (es EsSoloLectura: la matriz rechaza darle otra cosa).
      - Eliminar ............ nadie más que el Administrador.

    Idempotente: se puede correr dos veces sin duplicar concesiones ni bitácora.
*/
SET NOCOUNT ON;

DECLARE @actor nvarchar(200) = N'script-permisos-proyectos';

DECLARE @conceder TABLE (RolId nvarchar(80), PermisoClave nvarchar(80));

INSERT INTO @conceder (RolId, PermisoClave) VALUES
    (N'JefeInstitucion', N'Proyectos.Ver'),
    (N'JefeInstitucion', N'Proyectos.Crear'),
    (N'JefeInstitucion', N'Proyectos.Editar'),
    (N'JefeInstitucion', N'Proyectos.Avance.Crear'),

    (N'JefeArea',        N'Proyectos.Ver'),
    (N'JefeArea',        N'Proyectos.Crear'),
    (N'JefeArea',        N'Proyectos.Editar'),
    (N'JefeArea',        N'Proyectos.Avance.Crear'),

    (N'JefeUnidad',      N'Proyectos.Ver'),
    (N'JefeUnidad',      N'Proyectos.Crear'),
    (N'JefeUnidad',      N'Proyectos.Editar'),
    (N'JefeUnidad',      N'Proyectos.Avance.Crear'),

    (N'Empleado',        N'Proyectos.Ver'),
    (N'Empleado',        N'Proyectos.Avance.Crear'),

    (N'Consultor',       N'Proyectos.Ver');

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

SELECT rp.RolId, rp.PermisoClave
FROM RolPermisos rp
WHERE rp.PermisoClave LIKE N'Proyectos%'
ORDER BY rp.RolId, rp.PermisoClave;
