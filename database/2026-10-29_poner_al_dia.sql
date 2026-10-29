/* ═══════════════════════════════════════════════════════════════════════════════════════
   Poner al día la base con los cambios al 2026-10-29
   ───────────────────────────────────────────────────────────────────────────────────────
   Va DESPUÉS de 2026-10-27_poner_al_dia.sql. Corra antes 00_diagnostico.sql.

   Qué trae: el calendario de Outlook entra al portal (spec 2026-10-29). Cinco columnas en
   Reuniones, Asistentes.DeOutlook (los invitados de la cita entran como pre-registrados) y
   dos índices, todos de la migración EF 20261029212806_CalendarioOutlookAlPortal, que se
   registra al final en __EFMigrationsHistory. No hay nada fuera de EF.

   Cada paso comprueba SU condición contra el catálogo; la fila del historial va al final.
   Se puede correr las veces que haga falta: la segunda no hace nada.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-29_poner_al_dia.sql
   o pegarlo en SSMS y ejecutar.

     -b  detiene en el primer error. IMPORTANTE: sin esa bandera sqlcmd sigue de largo.
     -I  QUOTED_IDENTIFIER ON.
   ═══════════════════════════════════════════════════════════════════════════════════════ */

SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT '── 0. Comprobación previa ──────────────────────────────────────────';
IF OBJECT_ID(N'[Reuniones]', 'U') IS NULL
   OR OBJECT_ID(N'[Asistentes]', 'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NULL
   OR COL_LENGTH('Reuniones', 'OutlookEventId') IS NULL
    THROW 50000,
        'Falta Reuniones, Asistentes, __EFMigrationsHistory o Reuniones.OutlookEventId. Corra 00_diagnostico.sql: a esta base le falta 2026-10-27_poner_al_dia.sql o un script anterior.',
        1;

BEGIN TRANSACTION;

PRINT '── 1. Columnas ─────────────────────────────────────────────────────';
IF COL_LENGTH('Reuniones', 'OutlookImportadoEventId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookImportadoEventId] nvarchar(400) NULL; PRINT '  OutlookImportadoEventId ... agregada'; END
ELSE PRINT '  OutlookImportadoEventId ... ya estaba';

IF COL_LENGTH('Reuniones', 'OutlookICalUId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookICalUId] nvarchar(400) NULL; PRINT '  OutlookICalUId .......... agregada'; END
ELSE PRINT '  OutlookICalUId .......... ya estaba';

IF COL_LENGTH('Reuniones', 'OutlookImportadaPorId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookImportadaPorId] uniqueidentifier NULL; PRINT '  OutlookImportadaPorId ... agregada'; END
ELSE PRINT '  OutlookImportadaPorId ... ya estaba';

IF COL_LENGTH('Reuniones', 'OutlookModificadaEl') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookModificadaEl] datetime2 NULL; PRINT '  OutlookModificadaEl ..... agregada'; END
ELSE PRINT '  OutlookModificadaEl ..... ya estaba';

IF COL_LENGTH('Reuniones', 'CanceladaEnOutlookEl') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [CanceladaEnOutlookEl] datetime2 NULL; PRINT '  CanceladaEnOutlookEl .... agregada'; END
ELSE PRINT '  CanceladaEnOutlookEl .... ya estaba';

IF COL_LENGTH('Asistentes', 'DeOutlook') IS NULL
BEGIN ALTER TABLE [Asistentes] ADD [DeOutlook] bit NOT NULL CONSTRAINT [DF_Asistentes_DeOutlook] DEFAULT CAST(0 AS bit); PRINT '  Asistentes.DeOutlook .... agregada'; END
ELSE PRINT '  Asistentes.DeOutlook .... ya estaba';

COMMIT TRANSACTION;
GO

/* Lote aparte: un índice no puede referirse a una columna creada en el mismo lote. */
SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

PRINT '── 2. Índices ──────────────────────────────────────────────────────';
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name = N'IX_Reuniones_OutlookImportadaPorId')
BEGIN
    CREATE INDEX [IX_Reuniones_OutlookImportadaPorId] ON [Reuniones] ([OutlookImportadaPorId]) WHERE [OutlookImportadaPorId] IS NOT NULL;
    PRINT '  IX_Reuniones_OutlookImportadaPorId ... creado';
END
ELSE PRINT '  IX_Reuniones_OutlookImportadaPorId ... ya estaba';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name = N'IX_Reuniones_OutlookICalUId')
BEGIN
    CREATE INDEX [IX_Reuniones_OutlookICalUId] ON [Reuniones] ([OutlookICalUId]) WHERE [OutlookICalUId] IS NOT NULL;
    PRINT '  IX_Reuniones_OutlookICalUId .......... creado';
END
ELSE PRINT '  IX_Reuniones_OutlookICalUId .......... ya estaba';

PRINT '── 3. Historial de EF ──────────────────────────────────────────────';
IF COL_LENGTH('Reuniones', 'CanceladaEnOutlookEl') IS NOT NULL
   AND COL_LENGTH('Asistentes', 'DeOutlook') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name = N'IX_Reuniones_OutlookICalUId')
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261029212806_CalendarioOutlookAlPortal')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'20261029212806_CalendarioOutlookAlPortal', N'9.0.0');
    PRINT '  CalendarioOutlookAlPortal ... registrada';
END
ELSE PRINT '  CalendarioOutlookAlPortal ... ya estaba';

COMMIT TRANSACTION;

PRINT '── Verificación: todo debe decir «presente» ────────────────────────';
SELECT 'Reuniones.OutlookImportadoEventId' AS Control, CASE WHEN COL_LENGTH('Reuniones','OutlookImportadoEventId') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL SELECT 'Reuniones.OutlookICalUId',        CASE WHEN COL_LENGTH('Reuniones','OutlookICalUId')        IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT 'Reuniones.OutlookImportadaPorId', CASE WHEN COL_LENGTH('Reuniones','OutlookImportadaPorId') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT 'Reuniones.OutlookModificadaEl',   CASE WHEN COL_LENGTH('Reuniones','OutlookModificadaEl')   IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT 'Reuniones.CanceladaEnOutlookEl',  CASE WHEN COL_LENGTH('Reuniones','CanceladaEnOutlookEl')  IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT 'Asistentes.DeOutlook',            CASE WHEN COL_LENGTH('Asistentes','DeOutlook')            IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT 'IX_Reuniones_OutlookImportadaPorId', CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name = N'IX_Reuniones_OutlookImportadaPorId') THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT 'IX_Reuniones_OutlookICalUId',     CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name = N'IX_Reuniones_OutlookICalUId') THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT 'Historial CalendarioOutlookAlPortal', CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261029212806_CalendarioOutlookAlPortal') THEN 'presente' ELSE 'FALTA' END;
