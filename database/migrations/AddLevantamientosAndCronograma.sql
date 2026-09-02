-- ============================================================
-- Migración: AddLevantamientosAndCronograma
-- Fecha:     2026-07-22
-- Descripción: Módulo de levantamientos de campo (encabezado,
--              documentos, equipo, trámites) y cronograma de
--              etapas por trámite del expediente.
-- Idempotente: solo ejecuta si la migración no está aplicada.
-- Nota: la tabla ExpedienteEtapaCronogramas puede existir ya si
--       se corrió el script manual AddExpedienteEtapaCronograma
--       (id ficticio 20260722100000); por eso su bloque también
--       verifica OBJECT_ID. Este script lo reemplaza.
-- ============================================================

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
AND OBJECT_ID(N'[ExpedienteEtapaCronogramas]') IS NULL
BEGIN
    CREATE TABLE [ExpedienteEtapaCronogramas] (
        [Id] int NOT NULL IDENTITY,
        [ExpedienteId] int NOT NULL,
        [TramiteIndex] int NOT NULL,
        [EtapaNum] nvarchar(5) NOT NULL,
        [FechaInicio] date NULL,
        [FechaFin] date NULL,
        [FechaRealFin] date NULL,
        [Responsable] nvarchar(150) NULL,
        [Observacion] nvarchar(1000) NULL,
        CONSTRAINT [PK_ExpedienteEtapaCronogramas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteEtapaCronogramas_Expedientes_ExpedienteId] FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE TABLE [Levantamientos] (
        [Id] int NOT NULL IDENTITY,
        [Institucion] nvarchar(120) NOT NULL,
        [Encargado] nvarchar(150) NOT NULL,
        [Correo] nvarchar(200) NULL,
        [Celular] nvarchar(40) NULL,
        [Estado] nvarchar(30) NOT NULL,
        [ObsEstado] nvarchar(1000) NULL,
        [MigradaSOL] bit NOT NULL,
        [Limitante] bit NOT NULL,
        [LimitanteObs] nvarchar(1000) NULL,
        [Personal] bit NOT NULL,
        [PersonalObs] nvarchar(1000) NULL,
        [RequiereAcompanamiento] bit NOT NULL,
        [Habilidad] bit NOT NULL,
        [HabilidadObs] nvarchar(1000) NULL,
        [ObsGenerales] nvarchar(2000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_Levantamientos] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE TABLE [LevantamientoDocumentos] (
        [Id] int NOT NULL IDENTITY,
        [LevantamientoId] int NOT NULL,
        [Nombre] nvarchar(300) NOT NULL,
        [Tipo] nvarchar(80) NULL,
        [Url] nvarchar(600) NOT NULL,
        [FechaDocumento] date NULL,
        [FechaRegistro] datetime2 NOT NULL,
        CONSTRAINT [PK_LevantamientoDocumentos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LevantamientoDocumentos_Levantamientos_LevantamientoId] FOREIGN KEY ([LevantamientoId]) REFERENCES [Levantamientos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE TABLE [LevantamientoEquipo] (
        [Id] int NOT NULL IDENTITY,
        [LevantamientoId] int NOT NULL,
        [Funcion] nvarchar(150) NOT NULL,
        [Nombre] nvarchar(150) NOT NULL,
        [Contacto] nvarchar(200) NULL,
        [Orden] int NOT NULL,
        CONSTRAINT [PK_LevantamientoEquipo] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LevantamientoEquipo_Levantamientos_LevantamientoId] FOREIGN KEY ([LevantamientoId]) REFERENCES [Levantamientos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE TABLE [LevantamientoTramites] (
        [Id] int NOT NULL IDENTITY,
        [LevantamientoId] int NOT NULL,
        [NombreTramite] nvarchar(400) NOT NULL,
        [Orden] int NOT NULL,
        [ActaFirmada] bit NOT NULL,
        [RequiereMejoras] bit NOT NULL,
        [TieneInstructivo] bit NOT NULL,
        [Socializado] bit NOT NULL,
        [Observaciones] nvarchar(2000) NULL,
        CONSTRAINT [PK_LevantamientoTramites] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LevantamientoTramites_Levantamientos_LevantamientoId] FOREIGN KEY ([LevantamientoId]) REFERENCES [Levantamientos] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
AND NOT EXISTS (
    SELECT * FROM sys.indexes
    WHERE [name] = N'IX_ExpedienteEtapaCronogramas_ExpedienteId_TramiteIndex_EtapaNum'
      AND [object_id] = OBJECT_ID(N'[ExpedienteEtapaCronogramas]')
)
BEGIN
    CREATE UNIQUE INDEX [IX_ExpedienteEtapaCronogramas_ExpedienteId_TramiteIndex_EtapaNum] ON [ExpedienteEtapaCronogramas] ([ExpedienteId], [TramiteIndex], [EtapaNum]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_LevantamientoDocumentos_LevantamientoId] ON [LevantamientoDocumentos] ([LevantamientoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_LevantamientoEquipo_LevantamientoId] ON [LevantamientoEquipo] ([LevantamientoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_Levantamientos_CreatedAt] ON [Levantamientos] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_Levantamientos_Estado] ON [Levantamientos] ([Estado]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_Levantamientos_Institucion] ON [Levantamientos] ([Institucion]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    CREATE INDEX [IX_LevantamientoTramites_LevantamientoId_Orden] ON [LevantamientoTramites] ([LevantamientoId], [Orden]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722173346_AddLevantamientosAndCronograma'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722173346_AddLevantamientosAndCronograma', N'9.0.0');
END;

-- Retira el id ficticio del script manual previo, ya reemplazado por esta migración
DELETE FROM [__EFMigrationsHistory]
WHERE [MigrationId] = N'20260722100000_AddExpedienteEtapaCronograma';

COMMIT;
GO

