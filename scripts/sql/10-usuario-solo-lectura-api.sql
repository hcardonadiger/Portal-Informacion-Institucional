/* ═══════════════════════════════════════════════════════════════════════════
   Usuario de SOLO LECTURA para la API pública (src/Api)
   ───────────────────────────────────────────────────────────────────────────
   ── Por qué existe ─────────────────────────────────────────────────────────
   La API pública es la única cara del sistema expuesta a la red, y hasta ahora
   se conectaba con las mismas credenciales que el portal interno: alcance de
   ESCRITURA sobre las 64 tablas, incluidas expedientes, reuniones, chat y
   permisos. No las usa, pero podría.

   Medido: la API solo lee NUEVE tablas. Este usuario le da esas nueve y nada
   más. Si mañana aparece un fallo en la API, el daño queda acotado por el
   motor de base de datos, no por la buena conducta del código.

   ── Las nueve, y por qué cada una ──────────────────────────────────────────
       TramitesSiger          el catálogo
       PasosSiger             \
       RequisitosSiger         |
       EntregablesSiger        |  el detalle de cada ficha
       LugaresAtencionSiger    |
       EnlacesSiger           /
       Instituciones          quién publica cada trámite
       CategoriasTramite      cómo se agrupan
       Roles                  se lee UNA vez al arrancar (RolCatalogo). La API
                              no usa roles, pero sin esta lectura el host no
                              levanta. Es lectura de arranque, no de servicio.

   ── Cómo se ejecuta ────────────────────────────────────────────────────────
   La contraseña NO está en este archivo: el archivo se versiona. Se pasa como
   variable, y así nunca entra en la historia del repositorio.

     -- Aplicar:
     SQLCMD -S <servidor> -E -b -I -f 65001 -d <base> ^
            -v Usuario="api_portaldigital_lectura" Clave="<la-que-elija>" Accion="DO" ^
            -i 10-usuario-solo-lectura-api.sql

     -- Revertir:
     SQLCMD -S <servidor> -E -b -I -f 65001 -d <base> ^
            -v Usuario="api_portaldigital_lectura" Clave="x" Accion="UNDO" ^
            -i 10-usuario-solo-lectura-api.sql

   ── POR QUÉ EL NOMBRE ES UNA VARIABLE, y no una constante ──────────────────
   Porque un LOGIN pertenece al SERVIDOR, no a la base. Si ensayo y producción
   viven en la misma instancia y comparten el nombre, comparten también la
   contraseña: aplicar el script en un entorno DEJA SIN ACCESO al otro.

   Medido el 18-08-2026: al fijar la clave de producción, la API de ensayo
   empezó a recibir «Login failed». No fue una suposición, pasó.

   Con servidores separados da igual y puede dejar el nombre por omisión. Si
   comparten instancia, use un nombre por entorno:

       -v Usuario="api_portaldigital_lectura_ensayo"
       -v Usuario="api_portaldigital_lectura_prod"

   -I es obligatorio (QUOTED_IDENTIFIER ON): esta base tiene índices filtrados
   y sin él cualquier escritura falla con Msg 1934.
   -b hace que sqlcmd devuelva código de error; sin él un fallo pasa inadvertido.
   -f 65001 para que los acentos de los mensajes no se conviertan en basura.

   ── Qué NO hace ────────────────────────────────────────────────────────────
   No toca ni una fila de datos. Solo crea un principal y reparte permisos.
   ═══════════════════════════════════════════════════════════════════════════ */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;  SET ANSI_NULLS ON;
GO

:on error exit

DECLARE @accion sysname = N'$(Accion)';
IF @accion NOT IN (N'DO', N'UNDO', N'DESBLOQUEAR')
BEGIN
    RAISERROR(N'Indique -v Accion="DO", "UNDO" o "DESBLOQUEAR".', 16, 1);
END
GO


/* ═══ DESBLOQUEAR · cuando la API deja de conectar de golpe ═════════════════
   El login se crea con CHECK_POLICY = ON, que hereda la política de bloqueo de
   Windows. Eso significa que unos pocos intentos con la contraseña equivocada
   —una cadena de conexión vieja en cualquier aplicación, un despliegue a medias—
   BLOQUEAN la cuenta, y la API deja de conectar sin que nadie haya tocado nada.

   El síntoma engaña: sqlcmd dice «Login failed for user», que se lee como
   «contraseña mala». Para saber si en realidad está bloqueado:

       SELECT name, LOGINPROPERTY(name,'IsLocked'), LOGINPROPERTY(name,'BadPasswordCount')
       FROM   sys.sql_logins WHERE name LIKE 'api_portaldigital%';

   Medido el 18-08-2026: pasó exactamente eso.

       SQLCMD ... -v Usuario="..." Clave="<la de siempre>" Accion="DESBLOQUEAR" -i este.sql   */

IF N'$(Accion)' = N'DESBLOQUEAR'
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'$(Usuario)')
        RAISERROR(N'Ese login no existe. ¿Escribió bien el nombre?', 16, 1);

    PRINT '· Desbloqueando y refijando la contraseña...';
    -- UNLOCK exige indicar la contraseña: es la misma operación, no dos.
    ALTER LOGIN [$(Usuario)] WITH PASSWORD = N'$(Clave)' UNLOCK;

    SELECT name                                   AS Login_,
           LOGINPROPERTY(name, 'IsLocked')        AS SigueBloqueado,
           LOGINPROPERTY(name, 'BadPasswordCount') AS IntentosFallidos
    FROM   sys.sql_logins
    WHERE  name = N'$(Usuario)';

    PRINT '=== Desbloqueado. Si vuelve a pasar, busque quién usa la clave vieja. ===';
END
GO

IF N'$(Accion)' = N'DESBLOQUEAR'  SET NOEXEC ON;
GO


/* ═══ UNDO · quitar el usuario y su login ═══════════════════════════════════
   Se hace primero para que el script sea idempotente: aplicar DO sobre un
   estado ya aplicado no debe fallar, así que DO también pasa por aquí.       */

IF EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'')
BEGIN
    PRINT '· Quitando el usuario de la base...';
    DROP USER [$(Usuario)];
END
GO

IF N'$(Accion)' = N'UNDO'
BEGIN
    /* El login es del servidor, no de la base: solo se elimina al revertir.

       Ojo con esto, que costó descubrirlo: DROP LOGIN falla con Msg 15434 si el
       usuario tiene una sesión abierta —por ejemplo, la API en marcha—. El
       DROP USER de arriba sí funciona, así que sin este paso quedaba el login
       huérfano en el servidor y el script anunciaba «REVERTIDO» habiendo hecho
       la mitad. Se cierran las sesiones primero, y si algo queda mal se dice.  */

    DECLARE @matar nvarchar(max) = N'';
    SELECT @matar += N'KILL ' + CAST(session_id AS nvarchar(10)) + N'; '
    FROM   sys.dm_exec_sessions
    WHERE  login_name = N'';

    IF @matar <> N''
    BEGIN
        PRINT '· Cerrando las sesiones abiertas de ese usuario...';
        EXEC sp_executesql @matar;
    END

    IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'')
    BEGIN
        PRINT '· Quitando el login del servidor...';
        DROP LOGIN [$(Usuario)];
    END

    -- Se comprueba el resultado en vez de darlo por hecho.
    IF EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'')
       OR EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'')
    BEGIN
        RAISERROR(N'REVERSIÓN INCOMPLETA: el principal sigue existiendo. Revise a mano.', 16, 1);
    END

    PRINT '';
    PRINT '=== REVERTIDO. La API volvera a necesitar sus credenciales anteriores. ===';
END
GO

IF N'$(Accion)' = N'UNDO'  SET NOEXEC ON;   -- lo que sigue es solo del DO
GO


/* ═══ DO · crear el login, el usuario y los nueve permisos ══════════════════ */

IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'')
BEGIN
    PRINT '· Creando el login...';
    -- CHECK_POLICY ON: la contraseña obedece la política del servidor. Si la
    -- rechaza, es la política hablando, no un error del script.
    CREATE LOGIN [$(Usuario)]
        WITH PASSWORD = N'$(Clave)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
END
ELSE
BEGIN
    PRINT '· El login ya existía; se le fija la contraseña indicada.';
    ALTER LOGIN [$(Usuario)] WITH PASSWORD = N'$(Clave)';
END
GO

PRINT '· Creando el usuario en esta base...';
CREATE USER [$(Usuario)] FOR LOGIN [$(Usuario)];
GO

/* Red de seguridad. En SQL Server DENY gana a GRANT, así que si alguien mañana
   mete este usuario en db_datawriter «para probar algo», seguirá sin poder
   escribir. Salvedad honesta: DENY no alcanza a sysadmin ni a db_owner — a
   quien tenga esos roles no lo detiene nada de esto.                          */
PRINT '· Denegando toda escritura sobre el esquema...';
DENY INSERT, UPDATE, DELETE, ALTER, EXECUTE ON SCHEMA::dbo TO [$(Usuario)];
GO

PRINT '· Concediendo SELECT sobre las nueve tablas...';
GRANT SELECT ON dbo.TramitesSiger        TO [$(Usuario)];
GRANT SELECT ON dbo.PasosSiger           TO [$(Usuario)];
GRANT SELECT ON dbo.RequisitosSiger      TO [$(Usuario)];
GRANT SELECT ON dbo.EntregablesSiger     TO [$(Usuario)];
GRANT SELECT ON dbo.LugaresAtencionSiger TO [$(Usuario)];
GRANT SELECT ON dbo.EnlacesSiger         TO [$(Usuario)];
GRANT SELECT ON dbo.Instituciones        TO [$(Usuario)];
GRANT SELECT ON dbo.CategoriasTramite    TO [$(Usuario)];
GRANT SELECT ON dbo.Roles                TO [$(Usuario)];
GO

/* CONNECT basta para abrir la conexión; VIEW DEFINITION lo necesita EF Core
   para leer el esquema al construir el modelo. Sin él, el arranque falla con
   un error que no se parece en nada a «me faltan permisos».                  */
GRANT VIEW DEFINITION ON SCHEMA::dbo TO [$(Usuario)];
GO


/* ═══ Comprobación · que el resultado sea el que se pretendía ═══════════════ */

PRINT '';
PRINT '=== Tablas que este usuario puede leer (se esperan 9 de 64) ===';
SELECT   o.name AS Tabla
FROM     sys.database_permissions p
JOIN     sys.objects o           ON o.object_id = p.major_id
JOIN     sys.database_principals u ON u.principal_id = p.grantee_principal_id
WHERE    u.name = N''
  AND    p.permission_name = 'SELECT'
  AND    p.state_desc = 'GRANT'
ORDER BY o.name;

PRINT '';
PRINT '=== Escrituras denegadas sobre el esquema (se esperan 5) ===';
SELECT   p.permission_name AS Denegado
FROM     sys.database_permissions p
JOIN     sys.database_principals u ON u.principal_id = p.grantee_principal_id
WHERE    u.name = N''
  AND    p.state_desc = 'DENY'
ORDER BY p.permission_name;

PRINT '';
PRINT '=== LISTO. Ahora la API debe conectarse con: ===';
PRINT '    User ID=;Password=<la que indicó>';
PRINT '';
PRINT '    La contraseña NO va en appsettings.json (se versiona). En IIS va como';
PRINT '    variable de entorno del grupo de aplicaciones.';
GO

SET NOEXEC OFF;
GO
