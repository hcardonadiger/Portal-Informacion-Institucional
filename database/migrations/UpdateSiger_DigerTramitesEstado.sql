BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [TramitesSiger] (
        [Id] int NOT NULL IDENTITY,
        [IdSiger] int NOT NULL,
        [Codigo] nvarchar(20) NOT NULL,
        [Nombre] nvarchar(600) NOT NULL,
        [Institucion] nvarchar(200) NOT NULL,
        [Sigla] nvarchar(30) NULL,
        [Dependencia] nvarchar(400) NULL,
        [Descripcion] nvarchar(4000) NULL,
        [Objetivo] nvarchar(4000) NULL,
        [DirigidoA] nvarchar(500) NULL,
        [EstadoSiger] nvarchar(30) NULL,
        [Publicado] bit NOT NULL,
        [DisponibleEnLinea] bit NOT NULL,
        [EnPlanDigitalizacion] bit NOT NULL,
        [VigenciaDocumento] nvarchar(60) NULL,
        [Temporalidad] nvarchar(60) NULL,
        [DiagramaUrl] nvarchar(600) NULL,
        [EnlacePrincipal] nvarchar(600) NULL,
        [ObservacionesDiger] nvarchar(4000) NULL,
        [FechaIngreso] datetime2 NULL,
        [UltimaModificacion] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_TramitesSiger] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [EnlacesSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Numero] int NOT NULL,
        [Url] nvarchar(600) NOT NULL,
        [Tipo] nvarchar(60) NULL,
        CONSTRAINT [PK_EnlacesSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EnlacesSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [EntregablesSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Numero] int NOT NULL,
        [Entregable] nvarchar(600) NOT NULL,
        [Formato] nvarchar(60) NULL,
        [Presentacion] nvarchar(60) NULL,
        CONSTRAINT [PK_EntregablesSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EntregablesSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [LugaresAtencionSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Numero] int NOT NULL,
        [Lugar] nvarchar(600) NOT NULL,
        [Ciudad] nvarchar(100) NULL,
        [Direccion] nvarchar(400) NULL,
        [Telefonos] nvarchar(200) NULL,
        CONSTRAINT [PK_LugaresAtencionSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LugaresAtencionSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [PasosSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [NumeroPaso] int NOT NULL,
        [Descripcion] nvarchar(2000) NOT NULL,
        [LugarDependencia] nvarchar(400) NULL,
        [SalidaResultado] nvarchar(600) NULL,
        [TiempoRegistrado] nvarchar(60) NULL,
        CONSTRAINT [PK_PasosSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PasosSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [RequisitosSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Numero] int NOT NULL,
        [Requisito] nvarchar(1000) NOT NULL,
        [Tipo] nvarchar(60) NULL,
        [DocumentoSoporte] nvarchar(400) NULL,
        [Formato] nvarchar(60) NULL,
        CONSTRAINT [PK_RequisitosSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RequisitosSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE TABLE [TareasDigitalizacionSiger] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [NumeroTarea] int NOT NULL,
        [Descripcion] nvarchar(400) NOT NULL,
        [Estado] nvarchar(30) NULL,
        [FechaCumplimiento] datetime2 NULL,
        CONSTRAINT [PK_TareasDigitalizacionSiger] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TareasDigitalizacionSiger_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_EnlacesSiger_TramiteSigerId_Numero] ON [EnlacesSiger] ([TramiteSigerId], [Numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_EntregablesSiger_TramiteSigerId_Numero] ON [EntregablesSiger] ([TramiteSigerId], [Numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_LugaresAtencionSiger_TramiteSigerId_Numero] ON [LugaresAtencionSiger] ([TramiteSigerId], [Numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_PasosSiger_TramiteSigerId_NumeroPaso] ON [PasosSiger] ([TramiteSigerId], [NumeroPaso]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_RequisitosSiger_TramiteSigerId_Numero] ON [RequisitosSiger] ([TramiteSigerId], [Numero]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TareasDigitalizacionSiger_TramiteSigerId_NumeroTarea] ON [TareasDigitalizacionSiger] ([TramiteSigerId], [NumeroTarea]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TramitesSiger_Codigo] ON [TramitesSiger] ([Codigo]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_DisponibleEnLinea] ON [TramitesSiger] ([DisponibleEnLinea]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_EnPlanDigitalizacion] ON [TramitesSiger] ([EnPlanDigitalizacion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_EstadoSiger] ON [TramitesSiger] ([EstadoSiger]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE UNIQUE INDEX [IX_TramitesSiger_IdSiger] ON [TramitesSiger] ([IdSiger]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_Institucion] ON [TramitesSiger] ([Institucion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_Publicado] ON [TramitesSiger] ([Publicado]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_Sigla] ON [TramitesSiger] ([Sigla]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801022203_AddInventarioSiger'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801022203_AddInventarioSiger', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TramitesSiger]') AND [c].[name] = N'DirigidoA');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [TramitesSiger] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [TramitesSiger] ALTER COLUMN [DirigidoA] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RequisitosSiger]') AND [c].[name] = N'Requisito');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [RequisitosSiger] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [RequisitosSiger] ALTER COLUMN [Requisito] nvarchar(2000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RequisitosSiger]') AND [c].[name] = N'DocumentoSoporte');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [RequisitosSiger] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [RequisitosSiger] ALTER COLUMN [DocumentoSoporte] nvarchar(600) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[PasosSiger]') AND [c].[name] = N'SalidaResultado');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [PasosSiger] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [PasosSiger] ALTER COLUMN [SalidaResultado] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Lugar');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Lugar] nvarchar(1000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EntregablesSiger]') AND [c].[name] = N'Entregable');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [EntregablesSiger] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [EntregablesSiger] ALTER COLUMN [Entregable] nvarchar(1000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801023442_AjustarLongitudesSiger'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801023442_AjustarLongitudesSiger', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801024207_AjustarFormatoRequisito'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RequisitosSiger]') AND [c].[name] = N'Formato');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [RequisitosSiger] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [RequisitosSiger] ALTER COLUMN [Formato] nvarchar(600) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801024207_AjustarFormatoRequisito'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801024207_AjustarFormatoRequisito', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801024942_AjustarDocSoporte'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RequisitosSiger]') AND [c].[name] = N'Tipo');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [RequisitosSiger] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [RequisitosSiger] ALTER COLUMN [Tipo] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801024942_AjustarDocSoporte'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RequisitosSiger]') AND [c].[name] = N'DocumentoSoporte');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [RequisitosSiger] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [RequisitosSiger] ALTER COLUMN [DocumentoSoporte] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801024942_AjustarDocSoporte'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801024942_AjustarDocSoporte', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Telefonos');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Telefonos] nvarchar(400) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Direccion');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Direccion] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Ciudad');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Ciudad] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EntregablesSiger]') AND [c].[name] = N'Presentacion');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [EntregablesSiger] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [EntregablesSiger] ALTER COLUMN [Presentacion] nvarchar(600) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EntregablesSiger]') AND [c].[name] = N'Formato');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [EntregablesSiger] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [EntregablesSiger] ALTER COLUMN [Formato] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EnlacesSiger]') AND [c].[name] = N'Url');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [EnlacesSiger] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [EnlacesSiger] ALTER COLUMN [Url] nvarchar(1000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EnlacesSiger]') AND [c].[name] = N'Tipo');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [EnlacesSiger] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [EnlacesSiger] ALTER COLUMN [Tipo] nvarchar(100) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801030023_AmpliarCamposSiger'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801030023_AmpliarCamposSiger', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TareasDigitalizacionSiger]') AND [c].[name] = N'Estado');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [TareasDigitalizacionSiger] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [TareasDigitalizacionSiger] ALTER COLUMN [Estado] nvarchar(60) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TareasDigitalizacionSiger]') AND [c].[name] = N'Descripcion');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [TareasDigitalizacionSiger] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [TareasDigitalizacionSiger] ALTER COLUMN [Descripcion] nvarchar(1000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Telefonos');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Telefonos] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Direccion');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Direccion] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[LugaresAtencionSiger]') AND [c].[name] = N'Ciudad');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [LugaresAtencionSiger] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [LugaresAtencionSiger] ALTER COLUMN [Ciudad] nvarchar(2000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801031904_AmpliarCamposSiger2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801031904_AmpliarCamposSiger2', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN
    ALTER TABLE [TramitesSiger] ADD [InstitucionId] nvarchar(120) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN

                    INSERT INTO [Instituciones] ([Id], [Nombre], [Activo], [CreatedAt])
                    SELECT
                        norm.NormSigla,
                        norm.Institucion,
                        1,
                        GETUTCDATE()
                    FROM (
                        SELECT DISTINCT
                            UPPER(TRANSLATE(ts.[Sigla], N'áéíóúÁÉÍÓÚñÑ', N'aeiouAEIOUnN')) AS NormSigla,
                            ts.[Institucion]
                        FROM [TramitesSiger] ts
                        WHERE ts.[Sigla] IS NOT NULL AND ts.[Sigla] <> ''
                    ) norm
                    WHERE norm.NormSigla NOT IN (SELECT [Id] FROM [Instituciones])
                      AND norm.NormSigla IS NOT NULL;
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN

                    UPDATE [TramitesSiger]
                    SET [InstitucionId] = UPPER(TRANSLATE([Sigla], N'áéíóúÁÉÍÓÚñÑ', N'aeiouAEIOUnN'))
                    WHERE [Sigla] IS NOT NULL AND [Sigla] <> ''
                      AND UPPER(TRANSLATE([Sigla], N'áéíóúÁÉÍÓÚñÑ', N'aeiouAEIOUnN'))
                          IN (SELECT [Id] FROM [Instituciones]);
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN
    CREATE INDEX [IX_TramitesSiger_InstitucionId] ON [TramitesSiger] ([InstitucionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN
    ALTER TABLE [TramitesSiger] ADD CONSTRAINT [FK_TramitesSiger_Instituciones_InstitucionId] FOREIGN KEY ([InstitucionId]) REFERENCES [Instituciones] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260801041054_LinkSigerInstitucion'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260801041054_LinkSigerInstitucion', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803023243_LinkExpedienteTramiteSiger'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [TramiteSigerId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803023243_LinkExpedienteTramiteSiger'
)
BEGIN
    CREATE INDEX [IX_ExpedienteTramites_TramiteSigerId] ON [ExpedienteTramites] ([TramiteSigerId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803023243_LinkExpedienteTramiteSiger'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD CONSTRAINT [FK_ExpedienteTramites_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260803023243_LinkExpedienteTramiteSiger'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260803023243_LinkExpedienteTramiteSiger', N'9.0.0');
END;

COMMIT;
GO

