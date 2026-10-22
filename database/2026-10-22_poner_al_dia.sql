/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-22 — catálogo de insumos
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-19_poner_al_dia.sql. Corra antes 00_diagnostico.sql: él dice si
   esta base está en condiciones de recibir éste.

   QUÉ TRAE, Y POR QUÉ

   Tres tablas nuevas — la migración 20261022203532_AgregarCatalogoDeInsumos:

     CategoriasInsumo          las categorías del catálogo: nombre único, orden, activa.
     Insumos                   los insumos: categoría, nombre (único DENTRO de su
                               categoría), descripción opcional, activo.
     ProyectoActividadInsumos  qué insumos necesita cada actividad de un proyecto, con
                               quién y cuándo lo agregó. Llave: actividad + insumo.

   Es para que el Administrador mantenga un catálogo de insumos —materiales, equipo,
   servicios— y en cada actividad se elijan los que necesita. Sin cantidad, costo ni
   inventario. En el portal se llama «Insumos», para no confundirlo con «Recursos y
   plantillas», que no cambia.

   Nada se borra: insumos y categorías se DESACTIVAN. Por eso las llaves foráneas hacia
   el catálogo son sin cascada: quitar una fila del catálogo a mano no puede llevarse lo
   que las actividades ya tenían asignado. La única cascada es la de la actividad: si se
   quita una actividad, se van sus insumos asignados.

   Y siembra las seis categorías iniciales —Materiales, Equipo y maquinaria,
   Herramientas, Equipo informático, Servicios y Otros— SOLO SI LA TABLA ESTÁ VACÍA. Son
   editables: sembrarlas nombre por nombre volvería a crear «Otros» en una segunda
   corrida si alguien ya la renombró a «Varios».

   ESTE SCRIPT ES ADITIVO. No toca ninguna tabla existente: ni agrega columnas, ni
   cambia tipos, ni mueve datos. Correrlo dos veces no cambia nada. Para deshacerlo está
   2026-10-22_revertir_insumos.sql.

   El permiso nuevo (Insumos.Catalogo.Editar) NO va acá: lo registra el portal solo al
   arrancar, sin otorgárselo a ningún rol. El Administrador lo tiene por serlo.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-22_poner_al_dia.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ── Guarda: ¿es ésta la base correcta? ──────────────────────────────────────────── */
IF OBJECT_ID(N'[ProyectoActividades]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Esta base no tiene ProyectoActividades o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o está incompleta.',
        1;
END
GO

PRINT '════════ Catálogo de insumos ════════';
GO

/* ── 1. CategoriasInsumo ─────────────────────────────────────────────────────────────
   Idempotente contra el CATÁLOGO y no contra __EFMigrationsHistory: la fila del
   historial se escribe al final, así que una corrida que muera a la mitad la deja sin
   escribir y el historial no miente sobre lo que llegó a crearse. */
IF OBJECT_ID(N'[CategoriasInsumo]', N'U') IS NULL
BEGIN
    CREATE TABLE [CategoriasInsumo] (
        [Id]        int           IDENTITY(1,1) NOT NULL,
        [Nombre]    nvarchar(60)  NOT NULL,
        [Orden]     int           NOT NULL CONSTRAINT [DF_CategoriasInsumo_Orden]  DEFAULT (0),
        [Activo]    bit           NOT NULL CONSTRAINT [DF_CategoriasInsumo_Activo] DEFAULT (CONVERT(bit, 1)),
        [CreatedAt] datetime2     NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2     NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CategoriasInsumo] PRIMARY KEY ([Id])
    );
    PRINT '  CategoriasInsumo ................ creada';
END
ELSE PRINT '  CategoriasInsumo ................ ya estaba';
GO

/* ── 2. Insumos ──────────────────────────────────────────────────────────────────── */
IF OBJECT_ID(N'[Insumos]', N'U') IS NULL
BEGIN
    CREATE TABLE [Insumos] (
        [Id]          int           IDENTITY(1,1) NOT NULL,
        [CategoriaId] int           NOT NULL,
        [Nombre]      nvarchar(120) NOT NULL,
        [Descripcion] nvarchar(500) NULL,
        [Activo]      bit           NOT NULL CONSTRAINT [DF_Insumos_Activo] DEFAULT (CONVERT(bit, 1)),
        [CreatedAt]   datetime2     NOT NULL,
        [CreatedBy]   nvarchar(max) NULL,
        [UpdatedAt]   datetime2     NULL,
        [UpdatedBy]   nvarchar(max) NULL,
        CONSTRAINT [PK_Insumos] PRIMARY KEY ([Id]),
        /* Sin cascada: quitar una categoría no se lleva sus insumos. */
        CONSTRAINT [FK_Insumos_CategoriasInsumo_CategoriaId]
            FOREIGN KEY ([CategoriaId]) REFERENCES [CategoriasInsumo] ([Id])
    );
    PRINT '  Insumos ......................... creada';
END
ELSE PRINT '  Insumos ......................... ya estaba';
GO

/* ── 3. ProyectoActividadInsumos ─────────────────────────────────────────────────── */
IF OBJECT_ID(N'[ProyectoActividadInsumos]', N'U') IS NULL
BEGIN
    CREATE TABLE [ProyectoActividadInsumos] (
        [ActividadId] int           NOT NULL,
        [InsumoId]    int           NOT NULL,
        [AgregadoEl]  datetime2     NOT NULL,
        [AgregadoPor] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_ProyectoActividadInsumos] PRIMARY KEY ([ActividadId], [InsumoId]),
        /* Sin cascada desde el catálogo: lo asignado no se pierde por tocar el catálogo. */
        CONSTRAINT [FK_ProyectoActividadInsumos_Insumos_InsumoId]
            FOREIGN KEY ([InsumoId]) REFERENCES [Insumos] ([Id]),
        /* Con cascada desde la actividad: es la única ruta desde Proyectos, así que no
           choca con el error 1785. */
        CONSTRAINT [FK_ProyectoActividadInsumos_ProyectoActividades_ActividadId]
            FOREIGN KEY ([ActividadId]) REFERENCES [ProyectoActividades] ([Id]) ON DELETE CASCADE
    );
    PRINT '  ProyectoActividadInsumos ........ creada';
END
ELSE PRINT '  ProyectoActividadInsumos ........ ya estaba';
GO

/* ── 4. Índices ──────────────────────────────────────────────────────────────────── */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CategoriasInsumo_Nombre'
                 AND object_id = OBJECT_ID(N'[CategoriasInsumo]'))
BEGIN
    CREATE UNIQUE INDEX [IX_CategoriasInsumo_Nombre] ON [CategoriasInsumo] ([Nombre]);
    PRINT '  IX_CategoriasInsumo_Nombre ...... creado';
END
ELSE PRINT '  IX_CategoriasInsumo_Nombre ...... ya estaba';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CategoriasInsumo_Orden'
                 AND object_id = OBJECT_ID(N'[CategoriasInsumo]'))
BEGIN
    CREATE INDEX [IX_CategoriasInsumo_Orden] ON [CategoriasInsumo] ([Orden]);
    PRINT '  IX_CategoriasInsumo_Orden ....... creado';
END
ELSE PRINT '  IX_CategoriasInsumo_Orden ....... ya estaba';
GO

/* El nombre se repite entre categorías pero no dentro de una. */
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Insumos_CategoriaId_Nombre'
                 AND object_id = OBJECT_ID(N'[Insumos]'))
BEGIN
    CREATE UNIQUE INDEX [IX_Insumos_CategoriaId_Nombre] ON [Insumos] ([CategoriaId], [Nombre]);
    PRINT '  IX_Insumos_CategoriaId_Nombre ... creado';
END
ELSE PRINT '  IX_Insumos_CategoriaId_Nombre ... ya estaba';
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProyectoActividadInsumos_InsumoId'
                 AND object_id = OBJECT_ID(N'[ProyectoActividadInsumos]'))
BEGIN
    CREATE INDEX [IX_ProyectoActividadInsumos_InsumoId] ON [ProyectoActividadInsumos] ([InsumoId]);
    PRINT '  IX ..._InsumoId ................. creado';
END
ELSE PRINT '  IX ..._InsumoId ................. ya estaba';
GO

/* ── 5. Las categorías iniciales ─────────────────────────────────────────────────────
   Solo si no hay NINGUNA: ver el encabezado. */
IF NOT EXISTS (SELECT 1 FROM [CategoriasInsumo])
BEGIN
    INSERT INTO [CategoriasInsumo] ([Nombre], [Orden], [Activo], [CreatedAt], [CreatedBy])
    VALUES (N'Materiales',          1, 1, SYSUTCDATETIME(), N'migracion'),
           (N'Equipo y maquinaria', 2, 1, SYSUTCDATETIME(), N'migracion'),
           (N'Herramientas',        3, 1, SYSUTCDATETIME(), N'migracion'),
           (N'Equipo informático',  4, 1, SYSUTCDATETIME(), N'migracion'),
           (N'Servicios',           5, 1, SYSUTCDATETIME(), N'migracion'),
           (N'Otros',               6, 1, SYSUTCDATETIME(), N'migracion');
    PRINT '  categorías iniciales ............ sembradas (6)';
END
ELSE PRINT '  categorías iniciales ............ ya había categorías, no se tocan';
GO

/* ── 6. El historial ─────────────────────────────────────────────────────────────────
   Al final y solo si las tres tablas están: escribirla antes haría que `dotnet ef
   database update` se saltara la migración dejando la base a medias. */
IF OBJECT_ID(N'[CategoriasInsumo]',         N'U') IS NOT NULL
   AND OBJECT_ID(N'[Insumos]',                  N'U') IS NOT NULL
   AND OBJECT_ID(N'[ProyectoActividadInsumos]', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory
                   WHERE MigrationId = N'20261022203532_AgregarCatalogoDeInsumos')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20261022203532_AgregarCatalogoDeInsumos', N'9.0.0');
    PRINT '  historial ....................... registrado';
END
ELSE PRINT '  historial ....................... ya estaba';
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT  N'Tabla CategoriasInsumo' AS Control,
        CASE WHEN OBJECT_ID(N'[CategoriasInsumo]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT  N'Tabla Insumos',
        CASE WHEN OBJECT_ID(N'[Insumos]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'Tabla ProyectoActividadInsumos',
        CASE WHEN OBJECT_ID(N'[ProyectoActividadInsumos]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'IX_CategoriasInsumo_Nombre (único)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CategoriasInsumo_Nombre' AND is_unique = 1)
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'IX_Insumos_CategoriaId_Nombre (único)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Insumos_CategoriaId_Nombre' AND is_unique = 1)
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'IX_ProyectoActividadInsumos_InsumoId',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProyectoActividadInsumos_InsumoId')
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
/* Que la categoría NO borre en cascada: es lo que protege lo ya asignado. */
SELECT  N'FK Insumos → CategoriasInsumo, sin cascada',
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys
                          WHERE name = N'FK_Insumos_CategoriasInsumo_CategoriaId'
                            AND delete_referential_action = 0)
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'FK asignación → Insumos, sin cascada',
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys
                          WHERE name = N'FK_ProyectoActividadInsumos_Insumos_InsumoId'
                            AND delete_referential_action = 0)
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'FK asignación → ProyectoActividades, en cascada',
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys
                          WHERE name = N'FK_ProyectoActividadInsumos_ProyectoActividades_ActividadId'
                            AND delete_referential_action = 1)
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'Al menos una categoría de insumo',
        CASE WHEN EXISTS (SELECT 1 FROM [CategoriasInsumo]) THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'Migración registrada en el historial',
        CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory
                          WHERE MigrationId = N'20261022203532_AgregarCatalogoDeInsumos')
             THEN 'presente' ELSE 'FALTA' END;

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
