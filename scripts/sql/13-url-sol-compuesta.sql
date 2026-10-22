-- =============================================================================
-- 13 — URL de SOL compuesta (plan de promoción SIGER, Fase 7)
--
-- QUÉ HACE
--   • Agrega TramitesSiger.SolTramo  (nvarchar(300), NULL) — el tramo final que
--     captura el trámite. Lo que va delante lo pone la institución.
--   • Agrega Instituciones.RutaSol   (nvarchar(300), NULL) — la ruta de esa
--     institución dentro de SOL. NULL es lo normal y significa «vale su llave».
--   • Rehace el índice IX_TramitesSiger_EstaEnSol para que también cubra SolTramo.
--   • Rehace el CHECK CK_TramitesSiger_Sol: desde ahora un trámite marcado como
--     «está en SOL» vale con el tramo NUEVO o con la URL completa heredada.
--     Sin esa segunda rama, guardar una ficha capturada como manda D-13 fallaría
--     contra la restricción.
--
-- QUÉ NO HACE
--   NO TOCA DATOS EXISTENTES. Las dos columnas nacen en NULL y ninguna fila se
--   modifica. Las direcciones ya cargadas se respetan tal cual (D-14).
--
-- ORDEN RESPECTO AL SCRIPT 12
--   Es independiente: 12 crea la tabla PropuestasLlenado y este no la mira. Se
--   puede aplicar antes o después. Cada bloque se guarda por su propia fila de
--   __EFMigrationsHistory, así que correrlo dos veces no hace nada la segunda.
--
-- CÓMO SE CORRE
--   sqlcmd -S <servidor> -d <base> -U <usuario> -P <clave> -f 65001 -i 13-url-sol-compuesta.sql
--
--   El -f 65001 no es opcional: el archivo va en UTF-8 y sin él los acentos de
--   los comentarios llegan rotos.
--
-- POR QUÉ EL SET DE ABAJO
--   El índice es FILTRADO (WHERE [EstaEnSol] = 1) y SQL Server exige
--   QUOTED_IDENTIFIER ON para crearlo; sqlcmd arranca con la opción en OFF y
--   fallaría con «Msg 1934». Que EF lo envuelva en EXEC(N'...') no salva: el SQL
--   dinámico hereda la opción del lote que lo llama, no la arregla.
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    DROP INDEX [IX_TramitesSiger_EstaEnSol] ON [TramitesSiger];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    ALTER TABLE [TramitesSiger] DROP CONSTRAINT [CK_TramitesSiger_Sol];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    ALTER TABLE [TramitesSiger] ADD [SolTramo] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    ALTER TABLE [Instituciones] ADD [RutaSol] nvarchar(300) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    EXEC(N'CREATE INDEX [IX_TramitesSiger_EstaEnSol] ON [TramitesSiger] ([EstaEnSol]) INCLUDE ([SolUrl], [SolTramo]) WHERE [EstaEnSol] = 1');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    EXEC(N'ALTER TABLE [TramitesSiger] ADD CONSTRAINT [CK_TramitesSiger_Sol] CHECK ([EstaEnSol] = 0 OR [SolTramo] IS NOT NULL OR ([SolUrl] IS NOT NULL AND ([SolUrl] LIKE ''http://%'' OR [SolUrl] LIKE ''https://%'')))');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260825160050_UrlSolCompuesta'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260825160050_UrlSolCompuesta', N'9.0.0');
END;

COMMIT;
GO

