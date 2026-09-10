-- ============================================================
-- Migración: AddMetaResponsableUsuario
-- Fecha:     2026-07-22
-- Descripción: Columna ResponsableId (Guid, FK lógica a Usuarios)
--              en PlanTrabajoMetas. El responsable de una meta es
--              un usuario del sistema; la columna Responsable
--              queda como snapshot del nombre para visualización.
-- Idempotente: solo ejecuta si la migración no está aplicada.
-- Requiere:   AddMetaTramiteIndex aplicada antes.
-- ============================================================

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722200241_AddMetaResponsableUsuario'
)
BEGIN
    ALTER TABLE [PlanTrabajoMetas] ADD [ResponsableId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722200241_AddMetaResponsableUsuario'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722200241_AddMetaResponsableUsuario', N'9.0.0');
END;

COMMIT;
GO

