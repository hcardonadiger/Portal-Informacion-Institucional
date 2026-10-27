/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-27 — clasificación de plantillas
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-26_poner_al_dia.sql. Corra antes 00_diagnostico.sql.

   QUÉ TRAE — la migración EF 20261027182217_PlantillaCategoriaYAccion:

     PlantillasProyecto.CategoriaId   la categoría de proyecto para la que sirve la plantilla
                                      (FK a CategoriasProyecto, ON DELETE NO ACTION: quitar
                                      una categoría no se lleva por delante las plantillas).
     PlantillasProyecto.Accion        la acción de DIGER para la que sirve (Acompanamiento,
                                      Digitalizacion, Soporte, Desarrollo), como texto.

   Con las dos, el alta de un proyecto ofrece solo las plantillas que encajan con la
   categoría y la acción que se le eligen. NULL = «sirve para cualquiera».

   ES ADITIVO. Dos columnas nulables, un índice y una llave foránea. No mueve datos: las
   plantillas existentes quedan sin clasificar y siguen apareciendo para todos.

   No trae nada fuera de EF.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-27_poner_al_dia.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NULL
   OR OBJECT_ID(N'[CategoriasProyecto]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Faltan PlantillasProyecto, CategoriasProyecto o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o le faltan scripts anteriores.',
        1;
END
GO

PRINT '════════ Clasificación de plantillas de proyecto ════════';
GO

IF COL_LENGTH(N'PlantillasProyecto', N'Accion') IS NULL
BEGIN ALTER TABLE [PlantillasProyecto] ADD [Accion] nvarchar(20) NULL; PRINT '  Accion .......................... agregada'; END
ELSE PRINT '  Accion .......................... ya estaba';
GO
IF COL_LENGTH(N'PlantillasProyecto', N'CategoriaId') IS NULL
BEGIN ALTER TABLE [PlantillasProyecto] ADD [CategoriaId] int NULL; PRINT '  CategoriaId ..................... agregada'; END
ELSE PRINT '  CategoriaId ..................... ya estaba';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[PlantillasProyecto]') AND name = N'IX_PlantillasProyecto_CategoriaId')
BEGIN
    CREATE INDEX [IX_PlantillasProyecto_CategoriaId] ON [PlantillasProyecto] ([CategoriaId]);
    PRINT '  índice CategoriaId .............. creado';
END
ELSE PRINT '  índice CategoriaId .............. ya estaba';
GO

IF OBJECT_ID(N'[FK_PlantillasProyecto_CategoriasProyecto_CategoriaId]', N'F') IS NULL
BEGIN
    ALTER TABLE [PlantillasProyecto] ADD CONSTRAINT [FK_PlantillasProyecto_CategoriasProyecto_CategoriaId]
        FOREIGN KEY ([CategoriaId]) REFERENCES [CategoriasProyecto] ([Id]);
    PRINT '  FK a CategoriasProyecto ......... creada';
END
ELSE PRINT '  FK a CategoriasProyecto ......... ya estaba';
GO

/* El historial al final y solo si todo está: escribirlo antes haría que `dotnet ef` se
   saltara una base a medias. */
IF COL_LENGTH(N'PlantillasProyecto', N'Accion')      IS NOT NULL
   AND COL_LENGTH(N'PlantillasProyecto', N'CategoriaId') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[PlantillasProyecto]') AND name = N'IX_PlantillasProyecto_CategoriaId')
   AND OBJECT_ID(N'[FK_PlantillasProyecto_CategoriasProyecto_CategoriaId]', N'F') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261027182217_PlantillaCategoriaYAccion')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'20261027182217_PlantillaCategoriaYAccion', N'9.0.0');
    PRINT '  historial ....................... registrado';
END
ELSE PRINT '  historial ....................... ya estaba';
GO

PRINT '';
PRINT '════════ Comprobación ════════';
SELECT N'Columna PlantillasProyecto.Accion' AS Control,
       CASE WHEN COL_LENGTH(N'PlantillasProyecto', N'Accion') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL SELECT N'Columna PlantillasProyecto.CategoriaId',
       CASE WHEN COL_LENGTH(N'PlantillasProyecto', N'CategoriaId') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Índice IX_PlantillasProyecto_CategoriaId',
       CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                         WHERE object_id = OBJECT_ID(N'[PlantillasProyecto]') AND name = N'IX_PlantillasProyecto_CategoriaId')
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'FK PlantillasProyecto → CategoriasProyecto',
       CASE WHEN OBJECT_ID(N'[FK_PlantillasProyecto_CategoriasProyecto_CategoriaId]', N'F') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Migración registrada en el historial',
       CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261027182217_PlantillaCategoriaYAccion')
            THEN 'presente' ELSE 'FALTA' END;
PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
