/* =====================================================================
   Actualización de PRODUCCIÓN (GestionGD) — 2 migraciones pendientes
   ---------------------------------------------------------------------
   Aplica el esquema de estas dos migraciones que la rama tiene pero prod no:
     · 20260902010828_DuracionReunionEnMinutos
     · 20260902013548_TokenCalendarioUsuario
   Ambas son ADITIVAS e INDEPENDIENTES de las 7 migraciones que prod ya
   tiene y esta rama no (Proyectos/Trámites/BorradoLogicoUsuario/ModuloAmbito),
   así que se pueden aplicar sin conflicto.

   Idempotente (guardas COL_LENGTH / sys.indexes / __EFMigrationsHistory) y
   transaccional (DDL de SQL Server es transaccional: o entra todo o nada).

   Registra las 2 migraciones en __EFMigrationsHistory para que un futuro
   'dotnet ef database update' NO intente reaplicarlas.

   IMPORTANTE — ver notas al final: el historial de migraciones de esta rama
   DIVERGIÓ de producción; este script NO arregla esa divergencia (es de git),
   solo pone en prod el esquema de estas 2 migraciones de forma segura.
   ===================================================================== */

SET NOCOUNT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;   -- requerido para crear el índice filtrado
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* ── 1) 20260902010828_DuracionReunionEnMinutos ─────────────────────── */

-- 1.a  Nueva columna numérica
IF COL_LENGTH('dbo.Reuniones', 'DuracionMinutos') IS NULL
    ALTER TABLE dbo.Reuniones ADD DuracionMinutos int NULL;

-- 1.b  Migrar el texto libre 'Duracion' a minutos, y limpiar valores basura.
--      En SQL dinámico porque DuracionMinutos se acaba de crear en esta misma
--      transacción (resolución de nombre diferida). Solo si 'Duracion' existe.
IF COL_LENGTH('dbo.Reuniones', 'Duracion') IS NOT NULL
BEGIN
    EXEC sys.sp_executesql N'
        WITH origen AS (
            SELECT  Id,
                    REPLACE(LOWER(LTRIM(RTRIM(Duracion))), '','', ''.'') AS t
            FROM    dbo.Reuniones
            WHERE   Duracion IS NOT NULL AND LTRIM(RTRIM(Duracion)) <> ''''
        ),
        interpretado AS (
            SELECT  Id, t,
                    TRY_CONVERT(decimal(9,2),
                        NULLIF(LEFT(t, PATINDEX(''%[^0-9.]%'', t + ''|'') - 1), '''')) AS num
            FROM    origen
        )
        UPDATE  r
        SET     r.DuracionMinutos =
                    CASE
                        WHEN i.num IS NULL      THEN NULL
                        WHEN i.t LIKE ''%min%'' THEN CAST(ROUND(i.num, 0) AS int)
                        WHEN i.t LIKE ''%h%''   THEN CAST(ROUND(i.num * 60, 0) AS int)
                        WHEN i.num <= 12        THEN CAST(ROUND(i.num * 60, 0) AS int)
                        ELSE                         CAST(ROUND(i.num, 0) AS int)
                    END
        FROM    dbo.Reuniones r
        JOIN    interpretado i ON i.Id = r.Id;

        UPDATE dbo.Reuniones SET DuracionMinutos = NULL WHERE DuracionMinutos <= 0;';

    -- 1.c  Quitar cualquier default sobre 'Duracion' antes de eliminar la columna.
    DECLARE @df sysname =
        (SELECT dc.name FROM sys.default_constraints dc
         JOIN sys.columns c ON c.default_object_id = dc.object_id
         WHERE c.object_id = OBJECT_ID('dbo.Reuniones') AND c.name = 'Duracion');
    IF @df IS NOT NULL
        EXEC('ALTER TABLE dbo.Reuniones DROP CONSTRAINT ' + @df);

    ALTER TABLE dbo.Reuniones DROP COLUMN Duracion;
END

/* ── 2) 20260902013548_TokenCalendarioUsuario ───────────────────────── */

IF COL_LENGTH('dbo.Usuarios', 'CalendarioToken') IS NULL
    ALTER TABLE dbo.Usuarios ADD CalendarioToken uniqueidentifier NULL;

-- En SQL dinámico porque CalendarioToken se agregó en esta misma transacción.
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = 'IX_Usuarios_CalendarioToken'
                 AND object_id = OBJECT_ID('dbo.Usuarios'))
    EXEC sys.sp_executesql N'
        CREATE UNIQUE INDEX IX_Usuarios_CalendarioToken
            ON dbo.Usuarios (CalendarioToken)
            WHERE CalendarioToken IS NOT NULL;';

/* ── 3) Registrar en el historial de EF Core ────────────────────────── */

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId = '20260902010828_DuracionReunionEnMinutos')
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260902010828_DuracionReunionEnMinutos', '9.0.0');

IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
               WHERE MigrationId = '20260902013548_TokenCalendarioUsuario')
    INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260902013548_TokenCalendarioUsuario', '9.0.0');

COMMIT TRANSACTION;
PRINT 'Producción actualizada: DuracionMinutos + CalendarioToken aplicados.';
