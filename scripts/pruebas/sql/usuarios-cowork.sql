-- Los tres usuarios del recorrido de la separacion de Honduras Simple.
--
-- Hacen falta porque la separacion no se ve en una pantalla: se ve en lo que alcanza cada
-- quien. Con un solo usuario -y menos si es administrador, que se salta la tabla de permisos
-- por codigo- no se comprueba nada.
--
-- Los tres comparten la contrasena Pruebas#2026. El hash esta escrito aqui a proposito: es
-- una contrasena de juguete para una base desechable, y tenerla a la vista evita el paso de
-- "y ahora como le pongo contrasena a este usuario".
--
-- Se vuelve a correr despues de cada Refrescar-Sandbox.ps1 -Rehacer, que borra la copia
-- entera y con ella estos usuarios:
--
--   sqlcmd -S "LP-GD-JAGM\SQLEXPRESS" -U sa -P admin123 -C -I -d DigerTramitesEstado_Unificada_Sandbox ^
--          -i scripts\pruebas\sql\usuarios-cowork.sql
--
-- Es idempotente: borra y recrea, asi que correrlo diez veces deja lo mismo que correrlo una.
-- Y se niega a tocar cualquier base cuyo nombre no termine en _Sandbox.

SET NOCOUNT ON;
IF RIGHT(DB_NAME(), 8) <> '_Sandbox'
BEGIN
    RAISERROR('ALTO: esta base no termina en _Sandbox. No se toca nada.', 16, 1);
    RETURN;
END

DECLARE @Hash nvarchar(400) = N'100000.ifUKj62rToZ8eN54h7RFfw==.PzOEUb7uNxE84pa3y6vcSQ327vWOFDKrqsS5LBwRE4Y=';

-- Limpieza previa: deja el estado igual corra una vez o diez.
DELETE FROM AsignacionesUsuario
WHERE UsuarioId IN (SELECT Id FROM Usuarios WHERE Correo LIKE '%.cowork@diger.gob.hn');
DELETE FROM Usuarios WHERE Correo LIKE '%.cowork@diger.gob.hn';

DECLARE @Cuentas TABLE (Id uniqueidentifier DEFAULT NEWID(), Correo nvarchar(200),
                        Nombre nvarchar(200), Rol nvarchar(50),
                        Institucion nvarchar(50), Area nvarchar(50) NULL);

-- El operador va en CONSUCOOP y no en DIGER. En el sandbox NO hay un solo expediente de
-- DIGER —estan repartidos entre CONSUCOOP, IHADFA, FOSOVI e INPREMA— y el filtro RLS ancla
-- todo rol no global en su propia institucion. Con el operador en DIGER, Conciliacion abria
-- vacia y el filtro devolvia cero: la pantalla funcionaba, el usuario de prueba no podia ver
-- nada. Eso fue el hallazgo H-01 de la corrida, y era del terreno, no del portal.
--
-- El area queda en NULL a proposito: GOBDIG es un area de DIGER y no existe bajo CONSUCOOP.
-- Los expedientes del sandbox tienen AreaId nulo, y la rama de alcance Area del filtro admite
-- AreaId == null, asi que un jefe de area sin area fijada los ve en cuanto coincide la
-- institucion.
INSERT INTO @Cuentas (Correo, Nombre, Rol, Institucion, Area) VALUES
    (N'admin.cowork@diger.gob.hn',      N'Cowork - Administrador',      N'Administrador', N'DIGER',     NULL),
    (N'inventario.cowork@diger.gob.hn', N'Cowork - Solo inventario',    N'Consultor',     N'DIGER',     NULL),
    (N'operador.cowork@diger.gob.hn',   N'Cowork - Operador H. Simple', N'JefeArea',      N'CONSUCOOP', NULL);

INSERT INTO Usuarios (Id, Nombre, Correo, PasswordHash, Activo, CreatedAt, CreatedBy)
SELECT Id, Nombre, Correo, @Hash, 1, SYSDATETIME(), N'preparacion-pruebas-cowork' FROM @Cuentas;

INSERT INTO AsignacionesUsuario (Id, UsuarioId, InstitucionId, AreaId, UnidadId, Rol, CreatedAt, CreatedBy)
SELECT NEWID(), Id, Institucion, Area, NULL, Rol, SYSDATETIME(), N'preparacion-pruebas-cowork' FROM @Cuentas;

SELECT u.Correo + '  ->  ' + a.Rol AS Listo
FROM Usuarios u JOIN AsignacionesUsuario a ON a.UsuarioId = u.Id
WHERE u.Correo LIKE '%.cowork@diger.gob.hn'
ORDER BY u.Correo;

-- ---------------------------------------------------------------------------
-- El operador necesita las ocho llaves de Honduras Simple.
--
-- El traslado que corre al encender copia una por una las concesiones que el rol
-- tenia bajo SIGER. Jefe de Area tenia cuatro: Ver, Editar, Eliminar y Conciliacion.
-- Nunca tuvo Llenado ni Publicado, porque bajo SIGER esas dos pantallas solo las
-- alcanzaban los administradores, que se saltan la matriz por codigo. Eso viene de
-- antes de la separacion y no lo cambia el traslado.
--
-- Para el recorrido de pruebas hacen falta las ocho: el paso 7 comprueba justamente
-- que un operador que NO es administrador entra a las siete pantallas. Sin estas
-- cuatro, Llenado y Publicado le cierran la puerta y el recorrido reporta un fallo
-- que no existe.
-- ---------------------------------------------------------------------------
INSERT INTO RolPermisos (RolId, PermisoClave)
SELECT N'JefeArea', v.Clave
FROM (VALUES
    (N'HondurasSimple.Llenado.Ver'),
    (N'HondurasSimple.Llenado.Editar'),
    (N'HondurasSimple.Publicacion.Ver'),
    (N'HondurasSimple.Publicacion.Editar')
) v(Clave)
WHERE NOT EXISTS (
    SELECT 1 FROM RolPermisos r
    WHERE r.RolId = N'JefeArea' AND r.PermisoClave = v.Clave);

SELECT N'JefeArea  ->  ' + PermisoClave AS [Permisos del operador]
FROM RolPermisos
WHERE RolId = N'JefeArea' AND PermisoClave LIKE N'HondurasSimple%'
ORDER BY PermisoClave;
