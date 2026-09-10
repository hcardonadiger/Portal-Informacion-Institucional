-- ============================================================
-- Migración: AddAsistenteInstitucionCatalogo
-- Fecha:     2026-07-22
-- Descripción: Columna InstitucionId (FK lógica al catálogo de
--              Instituciones) en Asistentes de reuniones. La
--              columna Institucion queda como snapshot del nombre.
--              Incluye backfill: vincula asistentes existentes
--              cuyo texto coincide exactamente con el nombre de
--              una institución del catálogo.
-- Idempotente: solo ejecuta si la migración no está aplicada.
-- Requiere:   AddExpedienteAnalistaUsuario aplicada antes.
-- ============================================================

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203223_AddAsistenteInstitucionCatalogo'
)
BEGIN
    ALTER TABLE [Asistentes] ADD [InstitucionId] nvarchar(120) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203223_AddAsistenteInstitucionCatalogo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722203223_AddAsistenteInstitucionCatalogo', N'9.0.0');
END;

COMMIT;
GO

-- ── Backfill (batch separado: la columna ya existe al compilar) ──────────
-- Vincula asistentes cuyo texto de institución coincide exactamente con el
-- nombre de una institución del catálogo (Nombre tiene índice único).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
UPDATE a
SET a.InstitucionId = i.Id
FROM [Asistentes] a
INNER JOIN [Instituciones] i ON i.Nombre = a.Institucion
WHERE a.InstitucionId IS NULL
  AND a.Institucion IS NOT NULL;
GO

