/* =====================================================================================
   Poner al día la base con los cambios del 2026-10-26 y el 2026-10-27
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-26_poner_al_dia.sql (el script único de producción). Corra antes
   00_diagnostico.sql. Es el ÚNICO que hace falta después de ese: ya trae dentro lo de
   2026-10-26_equipo_institucional.sql, así que ese no se corre aparte (si ya se corrió,
   no pasa nada: aquí todo se salta lo que ya está).

   QUÉ TRAE — dos migraciones EF, nada fuera de EF:

   1. 20261026192746_ExpedienteEquipoInstitucional (2026-10-26)

     ExpedienteEquipo   el equipo institucional de cada expediente: las personas de la
                        institución que atienden las consultas, con su orden de prioridad
                        (1 = contacto principal; si no está disponible, la 2, y así).
                        Reemplaza en pantalla al «Contacto institucional principal», que era
                        una sola persona.

     Y UN PASO DE DATOS, que la migración también hace: el contacto que ya tenía cada
     expediente (Expedientes.ContactoNombre/Cargo/Correo/Tel) entra a su equipo como
     prioridad 1. Solo en expedientes que todavía no tienen equipo, así que correrlo dos
     veces no duplica a nadie. Las columnas Expedientes.Contacto* se quedan: el portal las
     sigue llenando con la prioridad 1, porque el resumen, los recordatorios y los informes
     leen de ahí.

   2. 20261027182217_PlantillaCategoriaYAccion (2026-10-27)

     PlantillasProyecto.CategoriaId   la categoría de proyecto para la que sirve la plantilla
                                      (FK a CategoriasProyecto, ON DELETE NO ACTION: quitar
                                      una categoría no se lleva por delante las plantillas).
     PlantillasProyecto.Accion        la acción de DIGER para la que sirve (Acompanamiento,
                                      Digitalizacion, Soporte, Desarrollo), como texto.

     Con las dos, el alta de un proyecto ofrece solo las plantillas que encajan con la
     categoría y la acción que se le eligen. NULL = «sirve para cualquiera». Las plantillas
     existentes quedan sin clasificar y siguen apareciendo para todos.

   ES ADITIVO. Una tabla nueva, dos columnas nulables, índices y llaves foráneas. Lo único
   que mueve datos es el paso 1: copia, no borra.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-27_poner_al_dia.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'[Expedientes]', N'U') IS NULL
   OR COL_LENGTH(N'Expedientes', N'ContactoNombre') IS NULL
   OR OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NULL
   OR OBJECT_ID(N'[CategoriasProyecto]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Faltan Expedientes (o su columna ContactoNombre), PlantillasProyecto, CategoriasProyecto o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o le falta 2026-10-26_poner_al_dia.sql.',
        1;
END
GO

/* ─────────────────────────────────────────────────────────────────────────────────────
   1. Equipo institucional del expediente (2026-10-26)
   ───────────────────────────────────────────────────────────────────────────────────── */
PRINT '════════ 1. Equipo institucional del expediente ════════';
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
PRINT '  contactos a prioridad 1 ......... ' + CAST(@@ROWCOUNT AS varchar(10)) + ' expediente(s)';
GO

/* ─────────────────────────────────────────────────────────────────────────────────────
   2. Clasificación de plantillas de proyecto (2026-10-27)
   ───────────────────────────────────────────────────────────────────────────────────── */
PRINT '';
PRINT '════════ 2. Clasificación de plantillas de proyecto ════════';
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

/* ─────────────────────────────────────────────────────────────────────────────────────
   Historial de migraciones: al final y cada fila solo si su parte está completa. Escribirlo
   antes haría que `dotnet ef` se saltara una base a medias.
   ───────────────────────────────────────────────────────────────────────────────────── */
PRINT '';
PRINT '════════ Historial de migraciones ════════';
GO

IF OBJECT_ID(N'[ExpedienteEquipo]', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[ExpedienteEquipo]') AND name = N'IX_ExpedienteEquipo_ExpedienteId')
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261026192746_ExpedienteEquipoInstitucional')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'20261026192746_ExpedienteEquipoInstitucional', N'9.0.0');
    PRINT '  ExpedienteEquipoInstitucional ... registrada';
END
ELSE PRINT '  ExpedienteEquipoInstitucional ... ya estaba';
GO

IF COL_LENGTH(N'PlantillasProyecto', N'Accion')      IS NOT NULL
   AND COL_LENGTH(N'PlantillasProyecto', N'CategoriaId') IS NOT NULL
   AND EXISTS (SELECT 1 FROM sys.indexes
               WHERE object_id = OBJECT_ID(N'[PlantillasProyecto]') AND name = N'IX_PlantillasProyecto_CategoriaId')
   AND OBJECT_ID(N'[FK_PlantillasProyecto_CategoriasProyecto_CategoriaId]', N'F') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261027182217_PlantillaCategoriaYAccion')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (N'20261027182217_PlantillaCategoriaYAccion', N'9.0.0');
    PRINT '  PlantillaCategoriaYAccion ....... registrada';
END
ELSE PRINT '  PlantillaCategoriaYAccion ....... ya estaba';
GO

PRINT '';
PRINT '════════ Comprobación ════════';
SELECT N'Tabla ExpedienteEquipo' AS Control,
       CASE WHEN OBJECT_ID(N'[ExpedienteEquipo]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL SELECT N'FK ExpedienteEquipo a Expedientes',
       CASE WHEN OBJECT_ID(N'[FK_ExpedienteEquipo_Expedientes_ExpedienteId]', N'F') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Índice IX_ExpedienteEquipo_ExpedienteId',
       CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                         WHERE object_id = OBJECT_ID(N'[ExpedienteEquipo]') AND name = N'IX_ExpedienteEquipo_ExpedienteId')
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'Todo contacto tiene su prioridad 1',
       CASE WHEN EXISTS (SELECT 1 FROM [Expedientes] e
                         WHERE NULLIF(LTRIM(RTRIM(e.[ContactoNombre])), N'') IS NOT NULL
                           AND NOT EXISTS (SELECT 1 FROM [ExpedienteEquipo] q WHERE q.[ExpedienteId] = e.[Id]))
            THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Columna PlantillasProyecto.Accion',
       CASE WHEN COL_LENGTH(N'PlantillasProyecto', N'Accion') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Columna PlantillasProyecto.CategoriaId',
       CASE WHEN COL_LENGTH(N'PlantillasProyecto', N'CategoriaId') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Índice IX_PlantillasProyecto_CategoriaId',
       CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                         WHERE object_id = OBJECT_ID(N'[PlantillasProyecto]') AND name = N'IX_PlantillasProyecto_CategoriaId')
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'FK PlantillasProyecto a CategoriasProyecto',
       CASE WHEN OBJECT_ID(N'[FK_PlantillasProyecto_CategoriasProyecto_CategoriaId]', N'F') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL SELECT N'Historial: ExpedienteEquipoInstitucional',
       CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261026192746_ExpedienteEquipoInstitucional')
            THEN 'presente' ELSE 'FALTA' END
UNION ALL SELECT N'Historial: PlantillaCategoriaYAccion',
       CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = N'20261027182217_PlantillaCategoriaYAccion')
            THEN 'presente' ELSE 'FALTA' END;
PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
