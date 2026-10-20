/* =============================================================================
   Las cinco columnas que la rama dev anadio al modelo sin dejar migracion.

   Se descubrieron al fusionar dev en Jamil: el codigo las consulta, pero
   ninguna base las tenia porque en dev se aplicaron a mano. Las pruebas no lo
   veian —corren sobre SQLite con EnsureCreated, que construye el esquema desde
   el modelo— y el portal arranca igual: la pagina de login no consulta ninguna
   de estas tablas. El fallo aparece en la primera consulta real, como
   «Invalid column name».

   Idempotente: se puede correr las veces que haga falta.

   Hay que aplicarlo a CADA base antes de desplegar esta rama.
   ============================================================================= */

SET NOCOUNT ON;
GO

/* ── Roles: capacidades administrables desde /Accesos/Roles ───────────────── */
IF COL_LENGTH('Roles', 'EsJefeDeArea') IS NULL
    ALTER TABLE Roles ADD EsJefeDeArea bit NOT NULL CONSTRAINT DF_Roles_EsJefeDeArea DEFAULT 0;
GO

IF COL_LENGTH('Roles', 'EsPmo') IS NULL
    ALTER TABLE Roles ADD EsPmo bit NOT NULL CONSTRAINT DF_Roles_EsPmo DEFAULT 0;
GO

/* ── ExpedienteTramites: como se clasifica el tramite ─────────────────────────
   Nullable a proposito: null significa que todavia nadie lo clasifico, que es
   un dato distinto de haberlo clasificado por defecto. */
IF COL_LENGTH('ExpedienteTramites', 'Accion') IS NULL
    ALTER TABLE ExpedienteTramites ADD Accion nvarchar(60) NULL;
GO

/* ── Usuarios: borrado logico ─────────────────────────────────────────────────
   Un filtro global de consulta excluye las filas marcadas. Sin la columna, ese
   filtro no se puede traducir y TODA consulta de usuarios falla — incluido el
   login. */
IF COL_LENGTH('Usuarios', 'IsDeleted') IS NULL
    ALTER TABLE Usuarios ADD IsDeleted bit NOT NULL CONSTRAINT DF_Usuarios_IsDeleted DEFAULT 0;
GO

/* ── ProyectoInteresados: interesado puesto por la sincronizacion ─────────────
   Marca los que pone InteresadosAutomaticosSyncService; los automaticos no se
   pueden quitar desde la ficha. */
IF COL_LENGTH('ProyectoInteresados', 'Automatico') IS NULL
    ALTER TABLE ProyectoInteresados ADD Automatico bit NOT NULL CONSTRAINT DF_ProyectoInteresados_Automatico DEFAULT 0;
GO

PRINT '';
PRINT '--- Resultado ---';
SELECT 'Roles.EsJefeDeArea'             AS Columna, COUNT(*) AS Existe FROM sys.columns WHERE object_id = OBJECT_ID('Roles')               AND name = 'EsJefeDeArea'
UNION ALL SELECT 'Roles.EsPmo',                     COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('Roles')               AND name = 'EsPmo'
UNION ALL SELECT 'ExpedienteTramites.Accion',       COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('ExpedienteTramites') AND name = 'Accion'
UNION ALL SELECT 'Usuarios.IsDeleted',              COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('Usuarios')           AND name = 'IsDeleted'
UNION ALL SELECT 'ProyectoInteresados.Automatico',  COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID('ProyectoInteresados') AND name = 'Automatico';
GO
