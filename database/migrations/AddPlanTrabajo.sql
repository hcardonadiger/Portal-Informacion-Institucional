-- ============================================================
-- Migración: AddPlanTrabajo
-- Fecha:     2026-07-22
-- Descripción: Plan de trabajo anual por institución (PlanTrabajo)
--              y sus trámites planificados (PlanTrabajoMetas).
--              Índice único por (InstitucionId, Anio).
-- Idempotente: solo ejecuta si la migración no está aplicada.
-- Requiere:   AddLevantamientosAndCronograma aplicada antes.
-- ============================================================

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722182710_AddPlanTrabajo'
)
BEGIN
    CREATE TABLE [PlanTrabajo] (
        [Id] int NOT NULL IDENTITY,
        [IsDeleted] bit NOT NULL,
        [InstitucionId] nvarchar(120) NOT NULL,
        [Institucion] nvarchar(200) NOT NULL,
        [Anio] int NOT NULL,
        [Estado] nvarchar(30) NOT NULL,
        [Observaciones] nvarchar(2000) NULL,
        [AprobadoPorId] uniqueidentifier NULL,
        [FechaAprobacion] date NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PlanTrabajo] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722182710_AddPlanTrabajo'
)
BEGIN
    CREATE TABLE [PlanTrabajoMetas] (
        [Id] int NOT NULL IDENTITY,
        [PlanTrabajoId] int NOT NULL,
        [Orden] int NOT NULL,
        [NombreTramite] nvarchar(300) NOT NULL,
        [FechaEstimadaInicio] date NULL,
        [FechaEstimadaFin] date NULL,
        [FechaRealFin] date NULL,
        [Responsable] nvarchar(200) NULL,
        [Estado] nvarchar(30) NOT NULL,
        [Observaciones] nvarchar(2000) NULL,
        [ExpedienteId] int NULL,
        CONSTRAINT [PK_PlanTrabajoMetas] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanTrabajoMetas_PlanTrabajo_PlanTrabajoId] FOREIGN KEY ([PlanTrabajoId]) REFERENCES [PlanTrabajo] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722182710_AddPlanTrabajo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlanTrabajo_InstitucionId_Anio] ON [PlanTrabajo] ([InstitucionId], [Anio]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722182710_AddPlanTrabajo'
)
BEGIN
    CREATE INDEX [IX_PlanTrabajoMetas_PlanTrabajoId_Orden] ON [PlanTrabajoMetas] ([PlanTrabajoId], [Orden]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722182710_AddPlanTrabajo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722182710_AddPlanTrabajo', N'9.0.0');
END;

COMMIT;
GO

