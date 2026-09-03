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
                        Nombre nvarchar(200), Rol nvarchar(50), Area nvarchar(50) NULL);
INSERT INTO @Cuentas (Correo, Nombre, Rol, Area) VALUES
    (N'admin.cowork@diger.gob.hn',      N'Cowork - Administrador',      N'Administrador', NULL),
    (N'inventario.cowork@diger.gob.hn', N'Cowork - Solo inventario',    N'Consultor',     NULL),
    (N'operador.cowork@diger.gob.hn',   N'Cowork - Operador H. Simple', N'JefeArea',      N'GOBDIG');

INSERT INTO Usuarios (Id, Nombre, Correo, PasswordHash, Activo, CreatedAt, CreatedBy)
SELECT Id, Nombre, Correo, @Hash, 1, SYSDATETIME(), N'preparacion-pruebas-cowork' FROM @Cuentas;

INSERT INTO AsignacionesUsuario (Id, UsuarioId, InstitucionId, AreaId, UnidadId, Rol, CreatedAt, CreatedBy)
SELECT NEWID(), Id, N'DIGER', Area, NULL, Rol, SYSDATETIME(), N'preparacion-pruebas-cowork' FROM @Cuentas;

SELECT u.Correo + '  ->  ' + a.Rol AS Listo
FROM Usuarios u JOIN AsignacionesUsuario a ON a.UsuarioId = u.Id
WHERE u.Correo LIKE '%.cowork@diger.gob.hn'
ORDER BY u.Correo;
