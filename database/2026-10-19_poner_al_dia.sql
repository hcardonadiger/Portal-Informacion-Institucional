/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-19
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-14_poner_al_dia.sql. Corra antes 00_diagnostico.sql: él dice si
   esta base está en condiciones de recibir éste.

   QUÉ TRAE, Y POR QUÉ

   Tres tablas nuevas — la migración 20261019171050_AgregarPlantillasDeProyecto:

     PlantillasProyecto     la plantilla: nombre, descripción, si está activa y hasta
                            dónde se ve (institución / área / unidad).
     PlantillaEntregables   sus entregables, con orden.
     PlantillaActividades   las actividades de cada entregable, con orden.

   Son para los proyectos CÍCLICOS: los que se repiten cada año o cada convocatoria con
   los mismos entregables y las mismas actividades, y que hoy obligan a recapturar el
   árbol entero desde cero. La plantilla guarda SOLO el esqueleto —qué entregables, qué
   actividades y en qué orden—. No guarda fechas, ni estados, ni avances, ni
   responsables: eso es del proyecto concreto y cambia en cada edición, así que una
   plantilla que los guardara los traería caducados a la siguiente.

   ESTE SCRIPT ES ADITIVO. No toca ninguna tabla existente: ni agrega columnas, ni
   cambia tipos, ni mueve datos. Si algo sale mal, lo peor que puede pasar es que las
   tres tablas queden a medias, y volver a correrlo las completa.

   La visibilidad reusa el vocabulario de NivelAlcance que el portal ya tiene:
     2 = Institución   la ve toda la institución
     3 = Área          solo quien esté en esa área
     4 = Unidad        solo quien esté en esa unidad
   El 1 (Global) NO se usa acá: una plantilla siempre nace en una institución.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-19_poner_al_dia.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* QUOTED_IDENTIFIER tiene que ir ON y no es opcional: PlantillasProyecto lleva un
   índice FILTRADO —el de unicidad, que solo mira las filas vivas— y SQL Server rechaza
   cualquier INSERT, UPDATE o DELETE sobre una tabla con índice filtrado si la sesión lo
   tiene apagado. Comprobado: sin esta línea, un INSERT de prueba muere con el error
   1934. EF siempre lo manda ON, así que la aplicación nunca lo sufre; quien sí lo
   sufriría es el operador corriendo SQL a mano, que es exactamente para quien se
   escribe este archivo. */
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ── Guarda: ¿es ésta la base correcta? ──────────────────────────────────────────── */
IF OBJECT_ID(N'[Proyectos]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Esta base no tiene Proyectos o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o está incompleta.',
        1;
END
GO

PRINT '════════ Plantillas de estructura de proyecto ════════';
GO

/* ── 1. PlantillasProyecto ───────────────────────────────────────────────────────────
   Idempotente contra el CATÁLOGO y no contra __EFMigrationsHistory: la fila del
   historial se escribe al final, así que una corrida que muera a la mitad la deja sin
   escribir y el historial mentiría sobre lo que sí llegó a crearse. */
IF OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NULL
BEGIN
    CREATE TABLE [PlantillasProyecto] (
        [Id]            int            IDENTITY(1,1) NOT NULL,
        [Nombre]        nvarchar(300)  NOT NULL,
        [Descripcion]   nvarchar(1000) NULL,
        [Activa]        bit            NOT NULL CONSTRAINT [DF_PlantillasProyecto_Activa] DEFAULT (CONVERT(bit, 1)),
        [Nivel]         int            NOT NULL,
        [InstitucionId] nvarchar(60)   NOT NULL,
        [AreaId]        nvarchar(60)   NULL,
        [UnidadId]      nvarchar(60)   NULL,
        [IsDeleted]     bit            NOT NULL CONSTRAINT [DF_PlantillasProyecto_IsDeleted] DEFAULT (CONVERT(bit, 0)),
        [CreatedAt]     datetime2      NOT NULL,
        [CreatedBy]     nvarchar(max)  NULL,
        [UpdatedAt]     datetime2      NULL,
        [UpdatedBy]     nvarchar(max)  NULL,
        CONSTRAINT [PK_PlantillasProyecto] PRIMARY KEY ([Id])
    );
    PRINT '  PlantillasProyecto .............. creada';
END
ELSE PRINT '  PlantillasProyecto .............. ya estaba';
GO

/* ── 2. PlantillaEntregables ─────────────────────────────────────────────────────── */
IF OBJECT_ID(N'[PlantillaEntregables]', N'U') IS NULL
BEGIN
    CREATE TABLE [PlantillaEntregables] (
        [Id]                  int            IDENTITY(1,1) NOT NULL,
        [PlantillaProyectoId] int            NOT NULL,
        [Orden]               int            NOT NULL,
        [Nombre]              nvarchar(300)  NOT NULL,
        [Descripcion]         nvarchar(1000) NULL,
        CONSTRAINT [PK_PlantillaEntregables] PRIMARY KEY ([Id]),
        /* Cascade: un entregable de plantilla no existe fuera de su plantilla. */
        CONSTRAINT [FK_PlantillaEntregables_PlantillasProyecto_PlantillaProyectoId]
            FOREIGN KEY ([PlantillaProyectoId]) REFERENCES [PlantillasProyecto] ([Id]) ON DELETE CASCADE
    );
    PRINT '  PlantillaEntregables ............ creada';
END
ELSE PRINT '  PlantillaEntregables ............ ya estaba';
GO

/* ── 3. PlantillaActividades ─────────────────────────────────────────────────────── */
IF OBJECT_ID(N'[PlantillaActividades]', N'U') IS NULL
BEGIN
    CREATE TABLE [PlantillaActividades] (
        [Id]                    int            IDENTITY(1,1) NOT NULL,
        [PlantillaEntregableId] int            NOT NULL,
        [Orden]                 int            NOT NULL,
        [Nombre]                nvarchar(300)  NOT NULL,
        [Descripcion]           nvarchar(1000) NULL,
        CONSTRAINT [PK_PlantillaActividades] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlantillaActividades_PlantillaEntregables_PlantillaEntregableId]
            FOREIGN KEY ([PlantillaEntregableId]) REFERENCES [PlantillaEntregables] ([Id]) ON DELETE CASCADE
    );
    PRINT '  PlantillaActividades ............ creada';
END
ELSE PRINT '  PlantillaActividades ............ ya estaba';
GO

/* ── 4. Índices ──────────────────────────────────────────────────────────────────────
   El único con filtro es el de unicidad: el nombre es único POR INSTITUCIÓN y solo
   entre las vivas. Dos instituciones pueden tener cada una su "Plan Operativo Anual"
   sin pisarse, y una plantilla borrada no puede impedir que se cree otra con su
   nombre. */
IF OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_PlantillasProyecto_InstitucionId_Nombre'
                     AND object_id = OBJECT_ID(N'[PlantillasProyecto]'))
BEGIN
    CREATE UNIQUE INDEX [IX_PlantillasProyecto_InstitucionId_Nombre]
        ON [PlantillasProyecto] ([InstitucionId], [Nombre])
        WHERE [IsDeleted] = 0;
    PRINT '  IX ..._InstitucionId_Nombre ..... creado';
END
ELSE PRINT '  IX ..._InstitucionId_Nombre ..... ya estaba';
GO

IF OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_PlantillasProyecto_InstitucionId_Activa'
                     AND object_id = OBJECT_ID(N'[PlantillasProyecto]'))
BEGIN
    CREATE INDEX [IX_PlantillasProyecto_InstitucionId_Activa]
        ON [PlantillasProyecto] ([InstitucionId], [Activa]);
    PRINT '  IX ..._InstitucionId_Activa ..... creado';
END
ELSE PRINT '  IX ..._InstitucionId_Activa ..... ya estaba';
GO

IF OBJECT_ID(N'[PlantillaEntregables]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_PlantillaEntregables_PlantillaProyectoId'
                     AND object_id = OBJECT_ID(N'[PlantillaEntregables]'))
BEGIN
    CREATE INDEX [IX_PlantillaEntregables_PlantillaProyectoId]
        ON [PlantillaEntregables] ([PlantillaProyectoId]);
    PRINT '  IX PlantillaEntregables ......... creado';
END
ELSE PRINT '  IX PlantillaEntregables ......... ya estaba';
GO

IF OBJECT_ID(N'[PlantillaActividades]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_PlantillaActividades_PlantillaEntregableId'
                     AND object_id = OBJECT_ID(N'[PlantillaActividades]'))
BEGIN
    CREATE INDEX [IX_PlantillaActividades_PlantillaEntregableId]
        ON [PlantillaActividades] ([PlantillaEntregableId]);
    PRINT '  IX PlantillaActividades ......... creado';
END
ELSE PRINT '  IX PlantillaActividades ......... ya estaba';
GO

/* ── 5. El historial ─────────────────────────────────────────────────────────────────
   Al final y solo si las tres tablas están: la fila dice "esta migración ya corrió",
   y escribirla antes de que lo esté haría que `dotnet ef database update` se la saltara
   dejando la base a medias sin que nadie se entere. */
IF OBJECT_ID(N'[PlantillasProyecto]',   N'U') IS NOT NULL
   AND OBJECT_ID(N'[PlantillaEntregables]', N'U') IS NOT NULL
   AND OBJECT_ID(N'[PlantillaActividades]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory
                   WHERE MigrationId = N'20261019171050_AgregarPlantillasDeProyecto')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261019171050_AgregarPlantillasDeProyecto', N'9.0.0');
    PRINT '  historial ....................... registrado';
END
ELSE PRINT '  historial ....................... ya estaba';
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT  N'Tabla PlantillasProyecto' AS Control,
        CASE WHEN OBJECT_ID(N'[PlantillasProyecto]', N'U') IS NULL
             THEN 'FALTA' ELSE 'presente' END AS Valor

UNION ALL
SELECT  N'Tabla PlantillaEntregables',
        CASE WHEN OBJECT_ID(N'[PlantillaEntregables]', N'U') IS NULL
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
SELECT  N'Tabla PlantillaActividades',
        CASE WHEN OBJECT_ID(N'[PlantillaActividades]', N'U') IS NULL
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
/* La unicidad por institución: sin ella, dos plantillas con el mismo nombre en la
   misma casa y nadie sabría cuál trae qué. */
SELECT  N'IX_PlantillasProyecto_InstitucionId_Nombre',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = N'IX_PlantillasProyecto_InstitucionId_Nombre'
                            AND object_id = OBJECT_ID(N'[PlantillasProyecto]'))
             THEN 'presente' ELSE 'FALTA' END

UNION ALL
SELECT  N'IX_PlantillaEntregables_PlantillaProyectoId',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = N'IX_PlantillaEntregables_PlantillaProyectoId'
                            AND object_id = OBJECT_ID(N'[PlantillaEntregables]'))
             THEN 'presente' ELSE 'FALTA' END

UNION ALL
SELECT  N'IX_PlantillaActividades_PlantillaEntregableId',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = N'IX_PlantillaActividades_PlantillaEntregableId'
                            AND object_id = OBJECT_ID(N'[PlantillaActividades]'))
             THEN 'presente' ELSE 'FALTA' END

UNION ALL
/* Las dos claves foráneas en cascada: si faltaran, borrar una plantilla dejaría sus
   entregables huérfanos en la tabla. */
SELECT  N'FK PlantillaEntregables → PlantillasProyecto',
        CASE WHEN OBJECT_ID(N'[FK_PlantillaEntregables_PlantillasProyecto_PlantillaProyectoId]', N'F') IS NULL
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
SELECT  N'FK PlantillaActividades → PlantillaEntregables',
        CASE WHEN OBJECT_ID(N'[FK_PlantillaActividades_PlantillaEntregables_PlantillaEntregableId]', N'F') IS NULL
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
SELECT  N'Migración registrada en el historial',
        CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory
                          WHERE MigrationId = N'20261019171050_AgregarPlantillasDeProyecto')
             THEN 'presente' ELSE 'FALTA' END;

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
