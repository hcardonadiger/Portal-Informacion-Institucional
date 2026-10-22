-- ============================================================================
-- Esquema SIGER — fases 1 a 3 del plan de promoción (docs/promocion-siger)
--
-- Para el Producción real, que Aplicar-CambiosProduccion.ps1 NO migra: ese
-- script solo aplica los .sql de esta carpeta. Desplegar.ps1 sí corre
-- "dotnet ef database update", así que quien despliegue con él no necesita esto.
--
-- Cubre tres migraciones, en este orden:
--   20260821210115_SigerIdOpcional             IdSiger pasa a opcional, con
--                                              índice único FILTRADO por IS NOT NULL
--                                              (SQL Server solo admite un nulo sin filtro).
--   20260824173317_ArchivoSigerOriginal        Crea FotosTramiteSiger, el archivo del
--                                              inventario tal como llegó de SIGER.
--   20260824192857_ConciliacionConClaveEstable Da identidad estable al trámite de
--                                              expediente y reapunta las decisiones de
--                                              conciliación a ella.
--
-- ES IDEMPOTENTE: cada bloque comprueba __EFMigrationsHistory antes de actuar, así
-- que se puede ejecutar más de una vez sin daño. Va dentro de una transacción.
--
-- DOS COSAS QUE HAY QUE SABER ANTES DE CORRERLO:
--
--   1. Lleva un UPDATE de datos, no solo cambios de esquema. Rellena la clave de las
--      conciliaciones existentes cruzando contra ExpedienteTramites, y BORRA las que
--      apuntaran a un trámite que ya no existe. Conviene respaldar antes.
--
--   2. ClaveEstable se crea con DEFAULT NEWID() para que cada fila existente reciba
--      una clave distinta. Si alguien lo cambia por un valor fijo, todas quedarán
--      iguales y el índice único fallará.
--
-- Ejecutar con codificación 65001 (ver "Codificación: por qué estos scripts llevan
-- BOM" en scripts/DESPLIEGUE.md):
--   sqlcmd -S <servidor> -d <base> -f 65001 -i 11-esquema-siger-fase1-a-fase3.sql
-- ============================================================================

-- Obligatorias, y no son decoración: la migración SigerIdOpcional crea un índice
-- FILTRADO (WHERE [IdSiger] IS NOT NULL) y SQL Server rechaza crearlo si
-- QUOTED_IDENTIFIER está apagado. sqlcmd arranca con esa opción en OFF, así que sin
-- estas dos líneas el script falla con "Msg 1934" en la primera migración —y falla
-- solo contra una base sin migrar, es decir, justo en Producción.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821210115_SigerIdOpcional'
)
BEGIN
    DROP INDEX [IX_TramitesSiger_IdSiger] ON [TramitesSiger];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821210115_SigerIdOpcional'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TramitesSiger]') AND [c].[name] = N'IdSiger');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TramitesSiger] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TramitesSiger] ALTER COLUMN [IdSiger] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821210115_SigerIdOpcional'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TramitesSiger_IdSiger] ON [TramitesSiger] ([IdSiger]) WHERE [IdSiger] IS NOT NULL');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821210115_SigerIdOpcional'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821210115_SigerIdOpcional', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824173317_ArchivoSigerOriginal'
)
BEGIN
    CREATE TABLE [FotosTramiteSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Version] int NOT NULL,
        [Origen] nvarchar(40) NOT NULL,
        [Codigo] nvarchar(20) NOT NULL,
        [IdSiger] int NULL,
        [CapturadaEl] datetime2 NOT NULL,
        [Contenido] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_FotosTramiteSiger] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824173317_ArchivoSigerOriginal'
)
BEGIN
    CREATE INDEX [IX_FotosTramiteSiger_Codigo] ON [FotosTramiteSiger] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824173317_ArchivoSigerOriginal'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FotosTramiteSiger_TramiteSigerId_Version] ON [FotosTramiteSiger] ([TramiteSigerId], [Version]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824173317_ArchivoSigerOriginal'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824173317_ArchivoSigerOriginal', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [FK_ConciliacionesSiger_ExpedienteTramites_ExpedienteTramiteId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DROP INDEX [IX_ConciliacionesSiger_ExpedienteTramiteId] ON [ConciliacionesSiger];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [ClaveEstable] uniqueidentifier NOT NULL DEFAULT (NEWID());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] ADD [ClaveTramite] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] ADD [ExpedienteId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    -- Va dentro de EXEC porque SQL Server compila el lote completo ANTES de evaluar el
    -- IF de arriba, y ExpedienteTramiteId ya no existe en una base que ya aplicó esta
    -- migración. Sin esto, la segunda corrida del script fallaba con "Invalid column
    -- name" pese a estar guardada — es decir, el script decía ser idempotente y no lo era.
    EXEC(N'UPDATE c
              SET c.ClaveTramite = t.ClaveEstable,
                  c.ExpedienteId = t.ExpedienteId
             FROM ConciliacionesSiger c
            INNER JOIN ExpedienteTramites t ON t.Id = c.ExpedienteTramiteId;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    -- En EXEC por lo mismo que el UPDATE de arriba: ClaveTramite se agrega en este mismo
    -- lote, y SQL Server lo compila entero antes de ejecutar nada. Contra una base ya
    -- migrada pasa —la columna existe— y por eso este fallo solo habría salido en el
    -- Producción real, que es donde peor se descubre.
    EXEC(N'DELETE FROM ConciliacionesSiger WHERE ClaveTramite IS NULL OR ExpedienteId IS NULL;');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConciliacionesSiger]') AND [c].[name] = N'ClaveTramite');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [ConciliacionesSiger] ALTER COLUMN [ClaveTramite] uniqueidentifier NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConciliacionesSiger]') AND [c].[name] = N'ExpedienteId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [ConciliacionesSiger] ALTER COLUMN [ExpedienteId] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConciliacionesSiger]') AND [c].[name] = N'ExpedienteTramiteId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [ConciliacionesSiger] DROP COLUMN [ExpedienteTramiteId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExpedienteTramites_ClaveEstable] ON [ExpedienteTramites] ([ClaveEstable]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ConciliacionesSiger_ClaveTramite] ON [ConciliacionesSiger] ([ClaveTramite]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    CREATE INDEX [IX_ConciliacionesSiger_ExpedienteId] ON [ConciliacionesSiger] ([ExpedienteId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] ADD CONSTRAINT [FK_ConciliacionesSiger_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824192857_ConciliacionConClaveEstable', N'9.0.0');
END;

COMMIT;
GO

