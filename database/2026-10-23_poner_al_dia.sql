/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-23 — reuniones a Outlook (fase 1)
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-22_poner_al_dia.sql. Corra antes 00_diagnostico.sql.

   QUÉ TRAE — la migración EF 20261022221928_ReunionesAOutlook:

     ConexionesMicrosoft   una fila por usuario que conectó su cuenta Microsoft desde su
                           perfil. Guarda la caché de tokens de MSAL CIFRADA con Data
                           Protection: sin las llaves del servidor no se puede leer.
     Reuniones.Outlook*    nueve columnas que recuerdan la cita de Outlook de cada reunión:
                           su id, quién la organiza, el enlace de Teams, la huella de lo último
                           enviado y si la última sincronización quedó pendiente.

   ES ADITIVO. No cambia tipos ni mueve datos. OutlookConTeams nace en 1 para las filas
   existentes: Teams es el comportamiento por defecto.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-23_poner_al_dia.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'[Reuniones]', N'U') IS NULL
   OR OBJECT_ID(N'[Usuarios]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Faltan Reuniones, Usuarios o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o está incompleta.',
        1;
END
GO

PRINT '════════ Reuniones a Outlook ════════';
GO

IF OBJECT_ID(N'[ConexionesMicrosoft]', N'U') IS NULL
BEGIN
    CREATE TABLE [ConexionesMicrosoft] (
        [Id]                 uniqueidentifier NOT NULL,
        [Upn]                nvarchar(256)    NOT NULL,
        [HomeAccountId]      nvarchar(200)    NOT NULL,
        [CacheTokens]        varbinary(max)   NOT NULL,
        [ConectadaEl]        datetime2        NOT NULL,
        [RequiereReconexion] bit              NOT NULL,
        [UltimoError]        nvarchar(500)    NULL,
        [CreatedAt]          datetime2        NOT NULL,
        [CreatedBy]          nvarchar(max)    NULL,
        [UpdatedAt]          datetime2        NULL,
        [UpdatedBy]          nvarchar(max)    NULL,
        CONSTRAINT [PK_ConexionesMicrosoft] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ConexionesMicrosoft_Usuarios_Id]
            FOREIGN KEY ([Id]) REFERENCES [Usuarios] ([Id]) ON DELETE CASCADE
    );
    PRINT '  ConexionesMicrosoft ............. creada';
END
ELSE PRINT '  ConexionesMicrosoft ............. ya estaba';
GO

IF COL_LENGTH(N'Reuniones', N'OutlookEventId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookEventId] nvarchar(400) NULL; PRINT '  OutlookEventId .................. agregada'; END
ELSE PRINT '  OutlookEventId .................. ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookTransaccionId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookTransaccionId] uniqueidentifier NULL; PRINT '  OutlookTransaccionId ............ agregada'; END
ELSE PRINT '  OutlookTransaccionId ............ ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookOrganizadorId') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookOrganizadorId] uniqueidentifier NULL; PRINT '  OutlookOrganizadorId ............ agregada'; END
ELSE PRINT '  OutlookOrganizadorId ............ ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookConTeams') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookConTeams] bit NOT NULL CONSTRAINT [DF_Reuniones_OutlookConTeams] DEFAULT (CONVERT(bit, 1)); PRINT '  OutlookConTeams ................. agregada'; END
ELSE PRINT '  OutlookConTeams ................. ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookEnlaceTeams') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookEnlaceTeams] nvarchar(1000) NULL; PRINT '  OutlookEnlaceTeams .............. agregada'; END
ELSE PRINT '  OutlookEnlaceTeams .............. ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookHuella') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookHuella] nchar(64) NULL; PRINT '  OutlookHuella ................... agregada'; END
ELSE PRINT '  OutlookHuella ................... ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookSincronizadaEl') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookSincronizadaEl] datetime2 NULL; PRINT '  OutlookSincronizadaEl ........... agregada'; END
ELSE PRINT '  OutlookSincronizadaEl ........... ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookPendiente') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookPendiente] bit NOT NULL CONSTRAINT [DF_Reuniones_OutlookPendiente] DEFAULT (CONVERT(bit, 0)); PRINT '  OutlookPendiente ................ agregada'; END
ELSE PRINT '  OutlookPendiente ................ ya estaba';
GO
IF COL_LENGTH(N'Reuniones', N'OutlookError') IS NULL
BEGIN ALTER TABLE [Reuniones] ADD [OutlookError] nvarchar(500) NULL; PRINT '  OutlookError .................... agregada'; END
ELSE PRINT '  OutlookError .................... ya estaba';
GO

/* El historial al final y solo si todo está: escribirlo antes haría que `dotnet ef` se
   saltara una base a medias. */
IF OBJECT_ID(N'[ConexionesMicrosoft]', N'U') IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookEventId')        IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookTransaccionId')  IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookOrganizadorId')  IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookConTeams')       IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookEnlaceTeams')    IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookHuella')         IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookSincronizadaEl') IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookPendiente')      IS NOT NULL
   AND COL_LENGTH(N'Reuniones', N'OutlookError')          IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261022221928_ReunionesAOutlook')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'20261022221928_ReunionesAOutlook', N'9.0.0');
    PRINT '  historial ....................... registrado';
END
ELSE PRINT '  historial ....................... ya estaba';
GO

PRINT '';
PRINT '════════ Comprobación ════════';
SELECT N'Tabla ConexionesMicrosoft' AS Control,
       CASE WHEN OBJECT_ID(N'[ConexionesMicrosoft]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL SELECT N'FK ConexionesMicrosoft → Usuarios',
       CASE WHEN OBJECT_ID(N'[FK_ConexionesMicrosoft_Usuarios_Id]', N'F') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Las nueve columnas Outlook de Reuniones',
       CASE WHEN (SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'[Reuniones]') AND name LIKE N'Outlook%') = 9
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'Migración registrada en el historial',
       CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261022221928_ReunionesAOutlook')
            THEN 'presente' ELSE 'FALTA' END;
PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
