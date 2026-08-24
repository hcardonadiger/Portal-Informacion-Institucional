-- ============================================================================
-- Cola del llenado asistido — Fase 5 del plan de promoción (docs/promocion-siger)
--
-- Para el Producción real, que Aplicar-CambiosProduccion.ps1 NO migra: ese
-- script solo aplica los .sql de esta carpeta. Desplegar.ps1 sí corre
-- "dotnet ef database update", así que quien despliegue con él no necesita esto.
--
-- Cubre una migración:
--   20260824222016_ColaLlenadoAsistido   Crea PropuestasLlenado, la cola donde se
--                                        revisa lo que el sistema propone para los
--                                        campos vacíos de las fichas SIGER.
--
-- Requiere aplicado antes el 11-esquema-siger-fase1-a-fase3.sql.
--
-- NO TOCA DATOS EXISTENTES. Crea una tabla vacía, dos índices y su llave foránea;
-- ninguna fila de TramitesSiger ni de ninguna otra tabla se modifica. La cola nace
-- vacía y se llena desde la pantalla (SIGER › Llenado asistido), nunca desde acá.
--
-- Ejecutar con codificación 65001 (ver "Codificación: por qué estos scripts llevan
-- BOM" en scripts/DESPLIEGUE.md):
--   sqlcmd -S <servidor> -d <base> -f 65001 -i 12-cola-llenado-asistido.sql
-- ============================================================================

-- Obligatorias, y no son decoración: esta migración crea un índice único FILTRADO
-- (WHERE [Estado] = 0) y SQL Server rechaza crearlo si QUOTED_IDENTIFIER está
-- apagado. sqlcmd arranca con esa opción en OFF. Que EF haya envuelto el CREATE
-- INDEX en un EXEC() no salva: el SQL dinámico hereda la opción del lote que lo
-- llama, así que sin estas dos líneas el script falla con "Msg 1934" —y falla solo
-- contra una base sin migrar, es decir, justo en Producción.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
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

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824222016_ColaLlenadoAsistido'
)
BEGIN
    CREATE INDEX [IX_PropuestasLlenado_Estado_Certeza] ON [PropuestasLlenado] ([Estado], [Certeza]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824222016_ColaLlenadoAsistido'
)
BEGIN
    EXEC(N'CREATE UNIQUE INDEX [IX_PropuestasLlenado_TramiteSigerId_Campo] ON [PropuestasLlenado] ([TramiteSigerId], [Campo]) WHERE [Estado] = 0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260824222016_ColaLlenadoAsistido'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260824222016_ColaLlenadoAsistido', N'9.0.0');
END;

COMMIT;
GO

