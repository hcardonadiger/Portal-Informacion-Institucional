-- ============================================================
-- Migración: AddAcuerdoResponsableContacto
-- Fecha:     2026-07-22
-- Descripción: Columna ResponsableContactoId (FK a Contactos) en
--              AcuerdosReunion. El responsable de un compromiso se
--              vincula al directorio de contactos; la columna
--              Responsable queda como snapshot del nombre.
--              Incluye backfill: vincula compromisos cuyo texto
--              coincide exactamente con el nombre de UN único
--              contacto activo del directorio.
-- Idempotente: solo ejecuta si la migración no está aplicada.
-- Requiere:   AddAsistenteInstitucionCatalogo aplicada antes.
-- ============================================================

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203942_AddAcuerdoResponsableContacto'
)
BEGIN
    ALTER TABLE [AcuerdosReunion] ADD [ResponsableContactoId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203942_AddAcuerdoResponsableContacto'
)
BEGIN
    CREATE INDEX [IX_AcuerdosReunion_ResponsableContactoId] ON [AcuerdosReunion] ([ResponsableContactoId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203942_AddAcuerdoResponsableContacto'
)
BEGIN
    ALTER TABLE [AcuerdosReunion] ADD CONSTRAINT [FK_AcuerdosReunion_Contactos_ResponsableContactoId] FOREIGN KEY ([ResponsableContactoId]) REFERENCES [Contactos] ([Id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260722203942_AddAcuerdoResponsableContacto'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260722203942_AddAcuerdoResponsableContacto', N'9.0.0');
END;

COMMIT;
GO

-- ── Backfill (batch separado: la columna ya existe al compilar) ──────────
-- Vincula compromisos cuyo responsable coincide exactamente con el nombre
-- de UN único contacto no eliminado; ambiguos o sin match quedan en NULL.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
UPDATE a
SET a.ResponsableContactoId = c.Id
FROM [AcuerdosReunion] a
INNER JOIN [Contactos] c ON c.Nombre = a.Responsable AND c.IsDeleted = 0
WHERE a.ResponsableContactoId IS NULL
  AND a.Responsable IS NOT NULL
  AND (SELECT COUNT(*) FROM [Contactos] c2
       WHERE c2.Nombre = a.Responsable AND c2.IsDeleted = 0) = 1;
GO

