-- ============================================================
-- Migración: AddMetaTramiteIndex
-- Fecha:     2026-07-22
-- Descripción: Columna ExpedienteTramiteIndex en PlanTrabajoMetas
--              para vincular una meta del plan a un trámite
--              específico del expediente (null = expediente completo).
-- Idempotente: solo ejecuta si la migración no está aplicada.
-- Requiere:   AddPlanTrabajo aplicada antes.
-- ============================================================

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722194801_AddMetaTramiteIndex'
)
BEGIN
    ALTER TABLE [PlanTrabajoMetas] ADD [ExpedienteTramiteIndex] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722194801_AddMetaTramiteIndex'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722194801_AddMetaTramiteIndex', N'9.0.0');
END;

COMMIT;
GO

