/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-26 — equipo institucional del expediente
   -------------------------------------------------------------------------------------
   YA NO SE CORRE APARTE: 2026-10-27_poner_al_dia.sql lo trae dentro, junto con lo del
   2026-10-27. Se conserva como antecedente; correrlo no hace daño (es idempotente).

   Va DESPUÉS de 2026-10-26_poner_al_dia.sql (el script único de producción). Corra antes 00_diagnostico.sql.

   QUÉ TRAE — la migración EF 20261026192746_ExpedienteEquipoInstitucional:

     ExpedienteEquipo   el equipo institucional de cada expediente: las personas de la
                        institución que atienden las consultas, con su orden de prioridad
                        (1 = contacto principal; si no está disponible, la 2, y así).
                        Reemplaza en pantalla al «Contacto institucional principal», que era
                        una sola persona.

   Y UN PASO DE DATOS, que la migración también hace: el contacto que ya tenía cada
   expediente (Expedientes.ContactoNombre/Cargo/Correo/Tel) entra a su equipo como
   prioridad 1. Solo en expedientes que todavía no tienen equipo, así que correrlo dos
   veces no duplica a nadie.

   ES ADITIVO. Las columnas Expedientes.Contacto* se quedan: el portal las sigue llenando
   con la prioridad 1, porque el resumen, los recordatorios y los informes leen de ahí.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-26_equipo_institucional.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'[Expedientes]', N'U') IS NULL
   OR COL_LENGTH(N'Expedientes', N'ContactoNombre') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Faltan Expedientes (o su columna ContactoNombre) o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o está incompleta.',
        1;
END
GO

PRINT '════════ Equipo institucional del expediente ════════';
GO

IF OBJECT_ID(N'[ExpedienteEquipo]', N'U') IS NULL
BEGIN
    CREATE TABLE [ExpedienteEquipo] (
        [Id]           int           IDENTITY(1,1) NOT NULL,
        [ExpedienteId] int           NOT NULL,
        [Prioridad]    int           NOT NULL,
        [Nombre]       nvarchar(150) NOT NULL,
        [Cargo]        nvarchar(150) NULL,
        [Correo]       nvarchar(200) NULL,
        [Telefono]     nvarchar(40)  NULL,
        CONSTRAINT [PK_ExpedienteEquipo] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ExpedienteEquipo_Expedientes_ExpedienteId]
            FOREIGN KEY ([ExpedienteId]) REFERENCES [Expedientes] ([Id]) ON DELETE CASCADE
    );
    PRINT '  ExpedienteEquipo ................ creada';
END
ELSE PRINT '  ExpedienteEquipo ................ ya estaba';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[ExpedienteEquipo]') AND name = N'IX_ExpedienteEquipo_ExpedienteId')
BEGIN
    CREATE INDEX [IX_ExpedienteEquipo_ExpedienteId] ON [ExpedienteEquipo] ([ExpedienteId]);
    PRINT '  IX_ExpedienteEquipo_ExpedienteId  creado';
END
ELSE PRINT '  IX_ExpedienteEquipo_ExpedienteId  ya estaba';
GO

/* El contacto de siempre abre el equipo como prioridad 1. NOT EXISTS por expediente: un
   expediente que ya tiene equipo —porque ya se corrió esto o porque alguien lo llenó en el
   portal— no se toca. */
INSERT INTO [ExpedienteEquipo] ([ExpedienteId], [Prioridad], [Nombre], [Cargo], [Correo], [Telefono])
SELECT e.[Id], 1, LTRIM(RTRIM(e.[ContactoNombre])),
       NULLIF(LTRIM(RTRIM(e.[ContactoCargo])), N''),
       NULLIF(LTRIM(RTRIM(e.[ContactoCorreo])), N''),
       NULLIF(LTRIM(RTRIM(e.[ContactoTel])), N'')
FROM [Expedientes] e
WHERE NULLIF(LTRIM(RTRIM(e.[ContactoNombre])), N'') IS NOT NULL
  AND NOT EXISTS (SELECT 1 FROM [ExpedienteEquipo] q WHERE q.[ExpedienteId] = e.[Id]);
PRINT '  contactos → prioridad 1 ......... ' + CAST(@@ROWCOUNT AS varchar(10)) + ' expediente(s)';
GO

/* El historial al final y solo si todo está: escribirlo antes haría que `dotnet ef` se
   saltara una base a medias. */
IF OBJECT_ID(N'[ExpedienteEquipo]', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[ExpedienteEquipo]') AND name = N'IX_ExpedienteEquipo_ExpedienteId')
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261026192746_ExpedienteEquipoInstitucional')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'20261026192746_ExpedienteEquipoInstitucional', N'9.0.0');
    PRINT '  historial ....................... registrado';
END
GO

PRINT '';
PRINT '════════ Comprobación ════════';
SELECT N'Tabla ExpedienteEquipo' AS Control,
       CASE WHEN OBJECT_ID(N'[ExpedienteEquipo]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL SELECT N'FK ExpedienteEquipo → Expedientes',
       CASE WHEN OBJECT_ID(N'[FK_ExpedienteEquipo_Expedientes_ExpedienteId]', N'F') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Índice por expediente',
       CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                         WHERE object_id = OBJECT_ID(N'[ExpedienteEquipo]') AND name = N'IX_ExpedienteEquipo_ExpedienteId')
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'Todo contacto tiene su prioridad 1',
       CASE WHEN EXISTS (SELECT 1 FROM [Expedientes] e
                         WHERE NULLIF(LTRIM(RTRIM(e.[ContactoNombre])), N'') IS NOT NULL
                           AND NOT EXISTS (SELECT 1 FROM [ExpedienteEquipo] q WHERE q.[ExpedienteId] = e.[Id]))
            THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Migración registrada en el historial',
       CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261026192746_ExpedienteEquipoInstitucional')
            THEN 'presente' ELSE 'FALTA' END;
PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
