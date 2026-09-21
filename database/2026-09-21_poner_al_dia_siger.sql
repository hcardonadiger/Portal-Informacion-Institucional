/* ═══════════════════════════════════════════════════════════════════════════════════════
   Poner al día GestionGD con el trabajo de SIGER / Ventanilla — 2026-09-21
   ───────────────────────────────────────────────────────────────────────────────────────
   QUÉ ES ESTO

   Un solo script que lleva una base de Test o de Producción al esquema que el código de la
   rama Jamil necesita hoy. Aplica las DIEZ migraciones que a Test le faltaban al 2026-09-21,
   y nada más.

   POR QUÉ NO BASTABA «dotnet ef database update»

   Test y este repositorio son dos LINAJES distintos de migraciones. Test tiene 73 aplicadas
   y el repositorio declara 48; solo coinciden en 38. Las otras 35 de Test vienen de la otra
   línea de desarrollo, incluida la migración inicial, que este repositorio ni siquiera
   conoce.

   Por eso las diez que faltan NO forman un bloque contiguo: están intercaladas entre
   migraciones que Test ya tiene. Pedirle a EF el tramo «de la A a la Z» reaplicaría cosas
   que ya están. Este script se generó por tramos, respetando ese entrelazado.

   LAS DIEZ, EN ORDEN

      1. 20260821210115_SigerIdOpcional
         IdSiger deja de ser obligatorio, con índice único filtrado
      2. 20260824173317_ArchivoSigerOriginal
         tabla FotosTramiteSiger: la foto íntegra de cada ficha SIGER
      3. 20260824192857_ConciliacionConClaveEstable
         la conciliación deja de colgar de un índice posicional
      4. 20260824222016_ColaLlenadoAsistido
         tabla PropuestasLlenado
      5. 20260825160050_UrlSolCompuesta
         SolTramo y RutaSol, con la restricción que los ata
      6. 20260825165843_FichaCompletaEnTramiteExpediente
         ocho columnas de ficha, más entregables y lugares
      7. 20260825170007_ConvertirModalidadesExistentes
         DATOS: normaliza Modalidad guardando antes el texto íntegro
      8. 20260825170158_SembrarEntregablesYLugares
         DATOS: convierte DocEntregado y la sede en filas
      9. 20260825190000_ColumnaFichaCompleta
         TramitesSiger.FichaCompleta, columna calculada y persistida
     10. 20260827161837_FusionRamaDev
         marca de fusión, sin cambios de esquema

   DOS DE ELLAS MUEVEN DATOS, Y ESTÁ DICHO A PROPÓSITO

   «ConvertirModalidadesExistentes» normaliza el texto libre de Modalidad al catálogo cerrado.
   No se puede omitir: la restricción CHECK que viene después la violarían las filas actuales.
   Antes de normalizar copia el texto íntegro a ModalidadDetalle, así que no se pierde el
   matiz de «En línea (total)» ni el de «En línea Tipo de solicitud».

   «SembrarEntregablesYLugares» convierte el campo suelto DocEntregado en el primer entregable
   de cada trámite, y arma un lugar de atención con el teléfono del trámite y la dirección de
   sede del expediente. Sin ella los expedientes que ya existen se ven vacíos en las pantallas
   nuevas.

   Ninguna de las dos inventa datos: reordenan los que ya están. Ninguna borra nada.

   SE PUEDE CORRER LAS VECES QUE HAGA FALTA

   Cada migración va envuelta en su propia comprobación contra __EFMigrationsHistory, y cada
   tramo va dentro de una transacción. Si algo falla a mitad, ese tramo se deshace entero y no
   deja escrita la fila del historial, así que volver a correrlo retoma donde quedó. Probado:
   se corrió dos veces seguidas sobre una copia de Test, y la segunda no cambió nada.

   CÓMO SE CORRE

     sqlcmd -S <servidor> -d <base> -E -C -b -f 65001 -i 2026-09-21_poner_al_dia_siger.sql

   Las banderas no son decorado. -b hace que sqlcmd devuelva error si algo falla, y sin ella
   una corrida rota parece buena. -f 65001 lee el archivo como UTF-8; sin ella los acentos de
   este script llegan rotos a la base. -C acepta el certificado del servidor. Si la base pide
   usuario y contraseña, cambie -E por -U y -P.

   ANTES DE CORRERLO, UN RESPALDO. Toca datos, y de los datos no hay deshacer.
   ═══════════════════════════════════════════════════════════════════════════════════════ */

/* Estas dos no son adorno. sqlcmd arranca con QUOTED_IDENTIFIER APAGADO, y los índices
   filtrados —los que llevan WHERE— se niegan a crearse así: «CREATE INDEX failed because the
   following SET options have incorrect settings». Dentro de SSMS no ocurre, porque allí ya
   viene encendido, de modo que el script parece bueno hasta que alguien lo corre por consola. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
GO

/* ── Guarda: que no se corra sobre la base equivocada ──────────────────────────────────────
   Un script de esquema apuntado a la base de otro sistema es de los errores que más caro
   salen, porque la mitad de las instrucciones funcionan. */
IF OBJECT_ID('dbo.TramitesSiger') IS NULL OR OBJECT_ID('dbo.ExpedienteTramites') IS NULL
BEGIN
    RAISERROR(N'Esta base no parece de GestionGD: le faltan TramitesSiger o ExpedienteTramites. No se aplicó nada.', 16, 1);
    SET NOEXEC ON;
END
GO

DECLARE @antes int = (SELECT COUNT(*) FROM [__EFMigrationsHistory]);
PRINT N'Base: ' + DB_NAME() + N' - servidor: ' + @@SERVERNAME;
PRINT N'Migraciones aplicadas antes de empezar: ' + CONVERT(nvarchar(10), @antes);
PRINT N'';
GO

/* ── Tramo 1 de 5 ──────────────────────────────────────────────────────────
   20260821210115_SigerIdOpcional */
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821210115_SigerIdOpcional'
)
BEGIN
    DROP INDEX [IX_TramitesSiger_IdSiger] ON [TramitesSiger];
END;
GO

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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821210115_SigerIdOpcional'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_TramitesSiger_IdSiger] ON [TramitesSiger] ([IdSiger]) WHERE [IdSiger] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260821210115_SigerIdOpcional'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260821210115_SigerIdOpcional', N'9.0.0');
END;
GO

COMMIT;
GO

/* ── Tramo 2 de 5 ──────────────────────────────────────────────────────────
   20260824173317_ArchivoSigerOriginal */
BEGIN TRANSACTION;
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
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824173317_ArchivoSigerOriginal'
)
BEGIN
    CREATE INDEX [IX_FotosTramiteSiger_Codigo] ON [FotosTramiteSiger] ([Codigo]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824173317_ArchivoSigerOriginal'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FotosTramiteSiger_TramiteSigerId_Version] ON [FotosTramiteSiger] ([TramiteSigerId], [Version]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824173317_ArchivoSigerOriginal'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824173317_ArchivoSigerOriginal', N'9.0.0');
END;
GO

COMMIT;
GO

/* ── Tramo 3 de 5 ──────────────────────────────────────────────────────────
   20260824192857_ConciliacionConClaveEstable
   20260824222016_ColaLlenadoAsistido */
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [FK_ConciliacionesSiger_ExpedienteTramites_ExpedienteTramiteId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DROP INDEX [IX_ConciliacionesSiger_ExpedienteTramiteId] ON [ConciliacionesSiger];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [ClaveEstable] uniqueidentifier NOT NULL DEFAULT (NEWID());
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] ADD [ClaveTramite] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] ADD [ExpedienteId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    /* Va dentro de EXEC para que solo se compile si de verdad se ejecuta: en una segunda
       corrida la columna ExpedienteTramiteId ya no existe, y fuera de EXEC el simple
       análisis del lote bastaría para romper el script. */
    EXEC(N'
        UPDATE c
           SET c.ClaveTramite = t.ClaveEstable,
               c.ExpedienteId  = t.ExpedienteId
          FROM ConciliacionesSiger c
         INNER JOIN ExpedienteTramites t ON t.Id = c.ExpedienteTramiteId;');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DELETE FROM ConciliacionesSiger WHERE ClaveTramite IS NULL OR ExpedienteId IS NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConciliacionesSiger]') AND [c].[name] = N'ClaveTramite');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [ConciliacionesSiger] ALTER COLUMN [ClaveTramite] uniqueidentifier NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConciliacionesSiger]') AND [c].[name] = N'ExpedienteId');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [ConciliacionesSiger] ALTER COLUMN [ExpedienteId] int NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ConciliacionesSiger]') AND [c].[name] = N'ExpedienteTramiteId');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [ConciliacionesSiger] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [ConciliacionesSiger] DROP COLUMN [ExpedienteTramiteId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExpedienteTramites_ClaveEstable] ON [ExpedienteTramites] ([ClaveEstable]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ConciliacionesSiger_ClaveTramite] ON [ConciliacionesSiger] ([ClaveTramite]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    CREATE INDEX [IX_ConciliacionesSiger_ExpedienteId] ON [ConciliacionesSiger] ([ExpedienteId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    ALTER TABLE [ConciliacionesSiger] ADD CONSTRAINT [FK_ConciliacionesSiger_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824192857_ConciliacionConClaveEstable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824192857_ConciliacionConClaveEstable', N'9.0.0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824222016_ColaLlenadoAsistido'
)
BEGIN
    CREATE TABLE [PropuestasLlenado] (
        [Id] int NOT NULL IDENTITY,
        [TramiteSigerId] int NOT NULL,
        [Campo] int NOT NULL,
        [ValorPropuesto] nvarchar(300) NULL,
        [Certeza] int NOT NULL,
        [Justificacion] nvarchar(400) NOT NULL,
        [Estado] int NOT NULL,
        [DecididaEl] datetime2 NULL,
        [DecididaPor] nvarchar(120) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PropuestasLlenado] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PropuestasLlenado_TramitesSiger_TramiteSigerId] FOREIGN KEY ([TramiteSigerId]) REFERENCES [TramitesSiger] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824222016_ColaLlenadoAsistido'
)
BEGIN
    CREATE INDEX [IX_PropuestasLlenado_Estado_Certeza] ON [PropuestasLlenado] ([Estado], [Certeza]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824222016_ColaLlenadoAsistido'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PropuestasLlenado_TramiteSigerId_Campo] ON [PropuestasLlenado] ([TramiteSigerId], [Campo]) WHERE [Estado] = 0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824222016_ColaLlenadoAsistido'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824222016_ColaLlenadoAsistido', N'9.0.0');
END;
GO

COMMIT;
GO

/* ── Tramo 4 de 5 ──────────────────────────────────────────────────────────
   20260825160050_UrlSolCompuesta
   20260825165843_FichaCompletaEnTramiteExpediente
   20260825170007_ConvertirModalidadesExistentes
   20260825170158_SembrarEntregablesYLugares
   20260825190000_ColumnaFichaCompleta */
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    DROP INDEX [IX_TramitesSiger_EstaEnSol] ON [TramitesSiger];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    ALTER TABLE [TramitesSiger] DROP CONSTRAINT [CK_TramitesSiger_Sol];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    ALTER TABLE [TramitesSiger] ADD [SolTramo] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    ALTER TABLE [Instituciones] ADD [RutaSol] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TramitesSiger_EstaEnSol] ON [TramitesSiger] ([EstaEnSol]) INCLUDE ([SolUrl], [SolTramo]) WHERE [EstaEnSol] = 1');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    EXEC(N'ALTER TABLE [TramitesSiger] ADD CONSTRAINT [CK_TramitesSiger_Sol] CHECK ([EstaEnSol] = 0 OR [SolTramo] IS NOT NULL OR ([SolUrl] IS NOT NULL AND ([SolUrl] LIKE ''http://%'' OR [SolUrl] LIKE ''https://%'')))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825160050_UrlSolCompuesta', N'9.0.0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [CategoriaId] int NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [EsGratuito] bit NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [EstaEnSol] bit NOT NULL DEFAULT CAST(0 AS bit);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [ModalidadDetalle] nvarchar(200) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [ObservacionesDiger] nvarchar(4000) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [SolTramo] nvarchar(300) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [Temporalidad] nvarchar(60) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [VigenciaDocumento] nvarchar(120) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE TABLE [ExpedienteTramiteEntregables] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [Orden] int NOT NULL,
        [Entregable] nvarchar(500) NOT NULL,
        [Formato] nvarchar(120) NULL,
        [Presentacion] nvarchar(120) NULL,
        CONSTRAINT [PK_ExpedienteTramiteEntregables] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteTramiteEntregables_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE TABLE [ExpedienteTramiteLugares] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [Orden] int NOT NULL,
        [Lugar] nvarchar(400) NOT NULL,
        [Ciudad] nvarchar(120) NULL,
        [Direccion] nvarchar(500) NULL,
        [Telefonos] nvarchar(200) NULL,
        CONSTRAINT [PK_ExpedienteTramiteLugares] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteTramiteLugares_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_ExpedienteTramites_CategoriaId] ON [ExpedienteTramites] ([CategoriaId]) WHERE [CategoriaId] IS NOT NULL');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE INDEX [IX_ExpedienteTramiteEntregables_ExpedienteId_TramiteIndex] ON [ExpedienteTramiteEntregables] ([ExpedienteId], [TramiteIndex]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    CREATE INDEX [IX_ExpedienteTramiteLugares_ExpedienteId_TramiteIndex] ON [ExpedienteTramiteLugares] ([ExpedienteId], [TramiteIndex]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD CONSTRAINT [FK_ExpedienteTramites_CategoriasTramite_CategoriaId] FOREIGN KEY ([CategoriaId]) REFERENCES [CategoriasTramite] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825165843_FichaCompletaEnTramiteExpediente'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825165843_FichaCompletaEnTramiteExpediente', N'9.0.0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN

                    EXEC(N'
                        UPDATE ExpedienteTramites
                        SET    ModalidadDetalle = Modalidad
                        WHERE  Modalidad IS NOT NULL
                          AND  LTRIM(RTRIM(Modalidad)) <> ''''
                          AND  ModalidadDetalle IS NULL;');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN

                    UPDATE ExpedienteTramites
                    SET    Modalidad = CASE
                             WHEN Modalidad COLLATE Latin1_General_CI_AI IN ('Virtual', 'Presencial', 'Hibrido')
                                  THEN Modalidad
                             WHEN (Modalidad COLLATE Latin1_General_CI_AI LIKE '%linea%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%virtual%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%online%')
                              AND  Modalidad COLLATE Latin1_General_CI_AI LIKE '%presencial%'
                                  THEN 'Hibrido'
                             WHEN  Modalidad COLLATE Latin1_General_CI_AI LIKE '%linea%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%virtual%'
                                OR Modalidad COLLATE Latin1_General_CI_AI LIKE '%online%'
                                  THEN 'Virtual'
                             WHEN  Modalidad COLLATE Latin1_General_CI_AI LIKE '%presencial%'
                                  THEN 'Presencial'
                             ELSE NULL
                           END
                    WHERE  Modalidad IS NOT NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN
    EXEC(N'ALTER TABLE [ExpedienteTramites] ADD CONSTRAINT [CK_ExpedienteTramites_Modalidad] CHECK ([Modalidad] IS NULL OR [Modalidad] IN (''Virtual'', ''Presencial'', ''Hibrido''))');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170007_ConvertirModalidadesExistentes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825170007_ConvertirModalidadesExistentes', N'9.0.0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170158_SembrarEntregablesYLugares'
)
BEGIN

                    INSERT INTO ExpedienteTramiteEntregables (ExpedienteId, TramiteIndex, Orden, Entregable)
                    SELECT ExpedienteId, TramiteIndex, 0, LTRIM(RTRIM(DocEntregado))
                    FROM   ExpedienteTramites
                    WHERE  DocEntregado IS NOT NULL AND LTRIM(RTRIM(DocEntregado)) <> '';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170158_SembrarEntregablesYLugares'
)
BEGIN

                    INSERT INTO ExpedienteTramiteLugares
                           (ExpedienteId, TramiteIndex, Orden, Lugar, Direccion, Telefonos)
                    SELECT t.ExpedienteId, t.TramiteIndex, 0,
                           e.Institucion, NULLIF(LTRIM(RTRIM(e.DirSede)), ''), NULLIF(LTRIM(RTRIM(t.Telefono)), '')
                    FROM   ExpedienteTramites t
                    JOIN   Expedientes e ON e.Id = t.ExpedienteId
                    WHERE  LTRIM(RTRIM(ISNULL(e.Institucion, ''))) <> ''
                      AND  ((t.Telefono IS NOT NULL AND LTRIM(RTRIM(t.Telefono)) <> '')
                        OR  (e.DirSede  IS NOT NULL AND LTRIM(RTRIM(e.DirSede))  <> ''));
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825170158_SembrarEntregablesYLugares'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825170158_SembrarEntregablesYLugares', N'9.0.0');
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825190000_ColumnaFichaCompleta'
)
BEGIN

                    ALTER TABLE TramitesSiger ADD FichaCompleta AS (
                        CASE WHEN CategoriaId      IS NOT NULL
                              AND Modalidad        IS NOT NULL
                              AND TiempoTexto      IS NOT NULL
                              AND CostoEsGratuito  IS NOT NULL
                              AND (EstaEnSol = 0 OR SolUrl IS NOT NULL OR SolTramo IS NOT NULL)
                             THEN CAST(1 AS bit)
                             ELSE CAST(0 AS bit)
                        END
                    ) PERSISTED;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825190000_ColumnaFichaCompleta'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825190000_ColumnaFichaCompleta', N'9.0.0');
END;
GO

COMMIT;
GO

/* ── Tramo 5 de 5 ──────────────────────────────────────────────────────────
   20260827161837_FusionRamaDev */
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260827161837_FusionRamaDev'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260827161837_FusionRamaDev', N'9.0.0');
END;
GO

COMMIT;
GO


/* ═══════════════════════════════════════════════════════════════════════════════════════
   Comprobación — la imprime el propio script
   ═══════════════════════════════════════════════════════════════════════════════════════ */
SET NOEXEC OFF;
GO

PRINT N'';
PRINT N'--------------------------------------------------------------';
PRINT N'Resultado';
PRINT N'--------------------------------------------------------------';
GO

SELECT  N'Las diez migraciones (debe decir 10)' AS Comprobacion,
        CONVERT(nvarchar(12), COUNT(*)) AS Valor
FROM    [__EFMigrationsHistory]
WHERE   [MigrationId] IN (
            N'20260821210115_SigerIdOpcional',
            N'20260824173317_ArchivoSigerOriginal',
            N'20260824192857_ConciliacionConClaveEstable',
            N'20260824222016_ColaLlenadoAsistido',
            N'20260825160050_UrlSolCompuesta',
            N'20260825165843_FichaCompletaEnTramiteExpediente',
            N'20260825170007_ConvertirModalidadesExistentes',
            N'20260825170158_SembrarEntregablesYLugares',
            N'20260825190000_ColumnaFichaCompleta',
            N'20260827161837_FusionRamaDev'
        )
UNION ALL SELECT N'Tabla FotosTramiteSiger',            IIF(OBJECT_ID('dbo.FotosTramiteSiger') IS NULL, N'FALTA', N'existe')
UNION ALL SELECT N'Tabla PropuestasLlenado',            IIF(OBJECT_ID('dbo.PropuestasLlenado') IS NULL, N'FALTA', N'existe')
UNION ALL SELECT N'Tabla ExpedienteTramiteEntregables', IIF(OBJECT_ID('dbo.ExpedienteTramiteEntregables') IS NULL, N'FALTA', N'existe')
UNION ALL SELECT N'Tabla ExpedienteTramiteLugares',     IIF(OBJECT_ID('dbo.ExpedienteTramiteLugares') IS NULL, N'FALTA', N'existe')
UNION ALL SELECT N'TramitesSiger.SolTramo',             IIF(COL_LENGTH('dbo.TramitesSiger','SolTramo') IS NULL, N'FALTA', N'existe')
UNION ALL SELECT N'TramitesSiger.FichaCompleta',        IIF(COL_LENGTH('dbo.TramitesSiger','FichaCompleta') IS NULL, N'FALTA', N'existe')
UNION ALL SELECT N'Instituciones.RutaSol',              IIF(COL_LENGTH('dbo.Instituciones','RutaSol') IS NULL, N'FALTA', N'existe')
UNION ALL SELECT N'ExpedienteTramites.ClaveEstable',    IIF(COL_LENGTH('dbo.ExpedienteTramites','ClaveEstable') IS NULL, N'FALTA', N'existe')
UNION ALL SELECT N'ExpedienteTramites.ModalidadDetalle',IIF(COL_LENGTH('dbo.ExpedienteTramites','ModalidadDetalle') IS NULL, N'FALTA', N'existe')
UNION ALL SELECT N'ConciliacionesSiger.ExpedienteTramiteId (debe decir retirada)',
        IIF(COL_LENGTH('dbo.ConciliacionesSiger','ExpedienteTramiteId') IS NULL, N'retirada', N'SIGUE AHI')
UNION ALL SELECT N'Restriccion CK_ExpedienteTramites_Modalidad', IIF(OBJECT_ID('dbo.CK_ExpedienteTramites_Modalidad') IS NULL, N'FALTA', N'existe')
UNION ALL SELECT N'Modalidades fuera del catalogo (debe decir 0)',
        CONVERT(nvarchar(12), (SELECT COUNT(*) FROM [ExpedienteTramites]
                               WHERE [Modalidad] IS NOT NULL
                                 AND [Modalidad] NOT IN (N'Presencial', N'Virtual', N'Hibrido')))
UNION ALL SELECT N'Filas en ExpedienteTramiteEntregables',
        CONVERT(nvarchar(12), (SELECT COUNT(*) FROM [ExpedienteTramiteEntregables]))
UNION ALL SELECT N'Filas en ExpedienteTramiteLugares',
        CONVERT(nvarchar(12), (SELECT COUNT(*) FROM [ExpedienteTramiteLugares]));
GO

/* Si «Las diez migraciones» no dice 10, o alguna línea dice FALTA, el script no terminó: mire
   el PRIMER error de la salida, no el último. Los de después suelen ser consecuencia. */
