-- ============================================================
-- Migración: AddExpedienteAnalistaUsuario
-- Fecha:     2026-07-22
-- Descripción: Columna AnalistaId (Guid, FK lógica a Usuarios)
--              en Expedientes. El analista DIGER responsable es
--              un usuario del sistema; la columna Analista queda
--              como snapshot del nombre para visualización.
--              Incluye backfill: vincula expedientes existentes
--              cuyo Analista coincide exactamente con el nombre
--              de un único usuario activo.
-- Idempotente: solo ejecuta si la migración no está aplicada.
-- Requiere:   AddMetaResponsableUsuario aplicada antes.
-- ============================================================

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722201107_AddExpedienteAnalistaUsuario'
)
BEGIN
    ALTER TABLE [Expedientes] ADD [AnalistaId] uniqueidentifier NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722201107_AddExpedienteAnalistaUsuario'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722201107_AddExpedienteAnalistaUsuario', N'9.0.0');
END;

COMMIT;
GO

-- ── Backfill (batch separado: la columna ya existe al compilar) ──────────
-- Vincula expedientes cuyo Analista coincide exactamente con el nombre de
-- UN único usuario activo; los nombres ambiguos o sin match quedan en NULL.
-- QUOTED_IDENTIFIER requerido: Expedientes tiene índices filtrados.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
UPDATE e
SET e.AnalistaId = u.Id
FROM [Expedientes] e
INNER JOIN [Usuarios] u ON u.Nombre = e.Analista AND u.Activo = 1
WHERE e.AnalistaId IS NULL
  AND (SELECT COUNT(*) FROM [Usuarios] u2
       WHERE u2.Nombre = e.Analista AND u2.Activo = 1) = 1;
GO

