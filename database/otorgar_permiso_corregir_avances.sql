/*
    Otorga la clave nueva «Proyectos.Avance.Editar» (corregir entradas de la bitácora) a los
    mismos roles que ya podían registrar avances.

    Por qué hace falta un script: `PermisosSeedService` siembra una sola vez, así que una clave
    creada después —como esta— no la tiene nadie aunque el catálogo la descubra al arrancar.
    Es la misma situación que ya se resolvió a mano con `Tickets.Crear` y `Contactos.Estado.Editar`.

    La política que replica: quien puede reportar un avance puede corregirlo. El acceso real
    queda igualmente acotado por la guarda de propiedad del comando —solo el responsable del
    proyecto—, así que esto abre la puerta, no la casa.

    No hay FK de RolPermisos hacia Permisos, así que la concesión puede insertarse antes de que
    el catálogo registre la clave; empieza a surtir efecto cuando la app arranca y la sincroniza.

    Idempotente: no reotorga lo ya otorgado.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/otorgar_permiso_corregir_avances.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor  nvarchar(200) = N'script-permiso-corregir-avances';
DECLARE @clave  nvarchar(200) = N'Proyectos.Avance.Editar';
DECLARE @nombre nvarchar(200) = N'Corregir avances de proyecto';

BEGIN TRANSACTION;

DECLARE @nuevas TABLE (RolId nvarchar(100), PermisoClave nvarchar(200));

INSERT INTO @nuevas (RolId, PermisoClave)
SELECT DISTINCT rp.RolId, @clave
FROM RolPermisos rp
WHERE rp.PermisoClave = N'Proyectos.Avance.Crear'
  AND NOT EXISTS (SELECT 1 FROM RolPermisos x
                  WHERE x.RolId = rp.RolId AND x.PermisoClave = @clave);

INSERT INTO RolPermisos (RolId, PermisoClave)
SELECT RolId, PermisoClave FROM @nuevas;

/* La bitácora de accesos es append-only: queda el rastro de que esto lo otorgó un script. */
INSERT INTO PermisosAuditoria (RolId, PermisoClave, PermisoNombre, Accion, Actor, Fecha)
SELECT RolId, PermisoClave, @nombre, N'Otorgado', @actor, SYSUTCDATETIME()
FROM @nuevas;

COMMIT;

SELECT CONCAT(N'Concesiones nuevas: ', (SELECT COUNT(*) FROM @nuevas)) AS Resultado;

SELECT rp.RolId, rp.PermisoClave
FROM RolPermisos rp
WHERE rp.PermisoClave LIKE N'Proyectos.Avance.%'
ORDER BY rp.RolId, rp.PermisoClave;
