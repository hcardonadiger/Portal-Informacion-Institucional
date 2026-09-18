/* ═══════════════════════════════════════════════════════════════════════════════════════
   Poner al día la base con los cambios del 2026-09-18
   ───────────────────────────────────────────────────────────────────────────────────────
   Sustituye a los cuatro scripts sueltos de ese día. Los reemplaza porque aquéllos
   dependían de __EFMigrationsHistory para saber qué hacer, y esa fila se escribe AL FINAL:
   si la corrida moría a la mitad, quedaban cambios hechos y ninguna marca de que se
   hicieron, y volver a correr el script fallaba de otra manera.

   Éste no pregunta por el historial. Cada paso comprueba SU PROPIA condición contra el
   catálogo de la base —¿existe la tabla?, ¿existe la columna?— así que converge al estado
   final desde donde sea que esté: sin empezar, a medias o ya completo.

   Se puede correr las veces que haga falta. La segunda no hace nada.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-18_poner_al_dia.sql
   o pegarlo en SSMS y ejecutar.

     -b  detiene en el primer error. IMPORTANTE: sin esa bandera sqlcmd sigue de largo,
         llega al COMMIT y confirma lo que alcanzó a hacer. Así quedó la base a medias.
     -I  QUOTED_IDENTIFIER ON, que exige el índice filtrado de Proyectos.Codigo.

   XACT_ABORT ON hace lo mismo desde adentro: cualquier error deshace TODO. O entra
   completo o no entra nada.
   ═══════════════════════════════════════════════════════════════════════════════════════ */

SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT '── 1. Catálogo de prioridades de proyecto ──────────────────────────';

IF OBJECT_ID('PrioridadesProyecto') IS NULL
BEGIN
    CREATE TABLE [PrioridadesProyecto] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(40) NOT NULL,
        [Orden] int NOT NULL DEFAULT 0,
        [Color] nvarchar(20) NOT NULL,
        [EsPredeterminada] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PrioridadesProyecto] PRIMARY KEY ([Id])
    );
    PRINT '   tabla PrioridadesProyecto creada';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PrioridadesProyecto_Nombre'
               AND object_id = OBJECT_ID('PrioridadesProyecto'))
    CREATE UNIQUE INDEX [IX_PrioridadesProyecto_Nombre] ON [PrioridadesProyecto] ([Nombre]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PrioridadesProyecto_Orden'
               AND object_id = OBJECT_ID('PrioridadesProyecto'))
    CREATE INDEX [IX_PrioridadesProyecto_Orden] ON [PrioridadesProyecto] ([Orden]);

/* Solo las que falten. Si ya están, no se tocan: quien administre pudo haberles cambiado
   color, orden o cuál es la predeterminada, y eso manda sobre la semilla. */
INSERT INTO PrioridadesProyecto (Nombre, Orden, Color, EsPredeterminada, Activo, CreatedAt, CreatedBy)
SELECT v.Nombre, v.Orden, v.Color, v.Predet, 1, SYSUTCDATETIME(), 'migracion'
FROM   (VALUES ('Alta', 1, 'Naranja', CAST(0 AS bit)),
               ('Media', 2, 'Azul',   CAST(1 AS bit)),
               ('Baja', 3, 'Gris',    CAST(0 AS bit))) AS v(Nombre, Orden, Color, Predet)
WHERE  NOT EXISTS (SELECT 1 FROM PrioridadesProyecto p WHERE p.Nombre = v.Nombre);

PRINT '── 2. Proyectos.PrioridadId ────────────────────────────────────────';

IF COL_LENGTH('Proyectos', 'PrioridadId') IS NULL
BEGIN
    ALTER TABLE [Proyectos] ADD [PrioridadId] int NULL;
    PRINT '   columna PrioridadId agregada';
END;

/* De acá en adelante, todo lo que toque PrioridadId va dentro de EXEC. El lote se compila
   entero antes de ejecutarse, así que una columna agregada arriba todavía no existe para
   el compilador: sin EXEC esto muere con «Invalid column name». */

-- Traslado desde la columna vieja, solo si todavía está y solo sobre lo que falta.
IF COL_LENGTH('Proyectos', 'Prioridad') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE p
        SET    p.PrioridadId = c.Id
        FROM   Proyectos p
        JOIN   PrioridadesProyecto c ON c.Nombre = p.Prioridad
        WHERE  p.PrioridadId IS NULL;');
    PRINT '   prioridades trasladadas desde la columna de texto';
END;

/* Lo que no casó con ninguna fila —nulo, vacío, o un texto que nadie reconoce— toma la
   predeterminada. Si nadie marcó una, la de menor orden. */
EXEC(N'
    DECLARE @pred int = (SELECT TOP 1 Id FROM PrioridadesProyecto
                         WHERE Activo = 1 ORDER BY EsPredeterminada DESC, Orden, Nombre);
    IF @pred IS NOT NULL
        UPDATE Proyectos SET PrioridadId = @pred WHERE PrioridadId IS NULL;');

-- Obligatoria. Se quita antes cualquier restricción de valor por omisión que estorbe.
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Proyectos')
           AND name = 'PrioridadId' AND is_nullable = 1)
BEGIN
    DECLARE @dfPrioProy sysname;
    SELECT @dfPrioProy = d.name
    FROM   sys.default_constraints d
    JOIN   sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
    WHERE  d.parent_object_id = OBJECT_ID('Proyectos') AND c.name = 'PrioridadId';
    IF @dfPrioProy IS NOT NULL EXEC(N'ALTER TABLE [Proyectos] DROP CONSTRAINT [' + @dfPrioProy + '];');

    ALTER TABLE [Proyectos] ALTER COLUMN [PrioridadId] int NOT NULL;
    PRINT '   PrioridadId pasa a obligatoria';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Proyectos_PrioridadId'
               AND object_id = OBJECT_ID('Proyectos'))
    CREATE INDEX [IX_Proyectos_PrioridadId] ON [Proyectos] ([PrioridadId]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Proyectos_PrioridadesProyecto_PrioridadId')
    ALTER TABLE [Proyectos] ADD CONSTRAINT [FK_Proyectos_PrioridadesProyecto_PrioridadId]
        FOREIGN KEY ([PrioridadId]) REFERENCES [PrioridadesProyecto] ([Id]) ON DELETE NO ACTION;

-- La columna vieja se borra al final, cuando su contenido ya viajó.
IF COL_LENGTH('Proyectos', 'Prioridad') IS NOT NULL
BEGIN
    DECLARE @dfProy sysname;
    SELECT @dfProy = d.name
    FROM   sys.default_constraints d
    JOIN   sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
    WHERE  d.parent_object_id = OBJECT_ID('Proyectos') AND c.name = 'Prioridad';
    IF @dfProy IS NOT NULL EXEC(N'ALTER TABLE [Proyectos] DROP CONSTRAINT [' + @dfProy + '];');

    ALTER TABLE [Proyectos] DROP COLUMN [Prioridad];
    PRINT '   columna Prioridad (texto) eliminada';
END;

PRINT '── 3. Catálogo de prioridades de ticket ────────────────────────────';

IF OBJECT_ID('PrioridadesTicket') IS NULL
BEGIN
    CREATE TABLE [PrioridadesTicket] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(40) NOT NULL,
        [Orden] int NOT NULL DEFAULT 0,
        [Color] nvarchar(20) NOT NULL,
        [EsCritica] bit NOT NULL DEFAULT CAST(0 AS bit),
        [EsPredeterminada] bit NOT NULL DEFAULT CAST(0 AS bit),
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_PrioridadesTicket] PRIMARY KEY ([Id])
    );
    PRINT '   tabla PrioridadesTicket creada';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PrioridadesTicket_Nombre'
               AND object_id = OBJECT_ID('PrioridadesTicket'))
    CREATE UNIQUE INDEX [IX_PrioridadesTicket_Nombre] ON [PrioridadesTicket] ([Nombre]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_PrioridadesTicket_Orden'
               AND object_id = OBJECT_ID('PrioridadesTicket'))
    CREATE INDEX [IX_PrioridadesTicket_Orden] ON [PrioridadesTicket] ([Orden]);

/* «Critica» va sin tilde: así se llamaba el miembro del enum y así está guardado en los
   tickets que ya existen, de modo que el traslado los reconozca. Se puede renombrar desde
   la pantalla apenas termine esto. */
INSERT INTO PrioridadesTicket (Nombre, Orden, Color, EsCritica, EsPredeterminada, Activo, CreatedAt, CreatedBy)
SELECT v.Nombre, v.Orden, v.Color, v.Critica, v.Predet, 1, SYSUTCDATETIME(), 'migracion'
FROM   (VALUES ('Critica', 1, 'Rojo',    CAST(1 AS bit), CAST(0 AS bit)),
               ('Alta',    2, 'Naranja', CAST(0 AS bit), CAST(0 AS bit)),
               ('Media',   3, 'Azul',    CAST(0 AS bit), CAST(1 AS bit)),
               ('Baja',    4, 'Gris',    CAST(0 AS bit), CAST(0 AS bit))) AS v(Nombre, Orden, Color, Critica, Predet)
WHERE  NOT EXISTS (SELECT 1 FROM PrioridadesTicket p WHERE p.Nombre = v.Nombre);

PRINT '── 4. Tickets.PrioridadId ──────────────────────────────────────────';

IF COL_LENGTH('Tickets', 'PrioridadId') IS NULL
BEGIN
    ALTER TABLE [Tickets] ADD [PrioridadId] int NULL;
    PRINT '   columna PrioridadId agregada';
END;

IF COL_LENGTH('Tickets', 'Prioridad') IS NOT NULL
BEGIN
    EXEC(N'
        UPDATE t
        SET    t.PrioridadId = c.Id
        FROM   Tickets t
        JOIN   PrioridadesTicket c ON c.Nombre = t.Prioridad
        WHERE  t.PrioridadId IS NULL;');
    PRINT '   prioridades trasladadas desde la columna de texto';
END;

EXEC(N'
    DECLARE @predTk int = (SELECT TOP 1 Id FROM PrioridadesTicket
                           WHERE Activo = 1 ORDER BY EsPredeterminada DESC, Orden, Nombre);
    IF @predTk IS NOT NULL
        UPDATE Tickets SET PrioridadId = @predTk WHERE PrioridadId IS NULL;');

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Tickets')
           AND name = 'PrioridadId' AND is_nullable = 1)
BEGIN
    DECLARE @dfPrioTk sysname;
    SELECT @dfPrioTk = d.name
    FROM   sys.default_constraints d
    JOIN   sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
    WHERE  d.parent_object_id = OBJECT_ID('Tickets') AND c.name = 'PrioridadId';
    IF @dfPrioTk IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT [' + @dfPrioTk + '];');

    ALTER TABLE [Tickets] ALTER COLUMN [PrioridadId] int NOT NULL;
    PRINT '   PrioridadId pasa a obligatoria';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Tickets_PrioridadId'
               AND object_id = OBJECT_ID('Tickets'))
    CREATE INDEX [IX_Tickets_PrioridadId] ON [Tickets] ([PrioridadId]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Tickets_PrioridadesTicket_PrioridadId')
    ALTER TABLE [Tickets] ADD CONSTRAINT [FK_Tickets_PrioridadesTicket_PrioridadId]
        FOREIGN KEY ([PrioridadId]) REFERENCES [PrioridadesTicket] ([Id]) ON DELETE NO ACTION;

IF COL_LENGTH('Tickets', 'Prioridad') IS NOT NULL
BEGIN
    DECLARE @dfTk sysname;
    SELECT @dfTk = d.name
    FROM   sys.default_constraints d
    JOIN   sys.columns c ON c.object_id = d.parent_object_id AND c.column_id = d.parent_column_id
    WHERE  d.parent_object_id = OBJECT_ID('Tickets') AND c.name = 'Prioridad';
    IF @dfTk IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT [' + @dfTk + '];');

    ALTER TABLE [Tickets] DROP COLUMN [Prioridad];
    PRINT '   columna Prioridad (texto) eliminada';
END;

PRINT '── 5. Catálogo de categorías de proyecto ───────────────────────────';

IF OBJECT_ID('CategoriasProyecto') IS NULL
BEGIN
    CREATE TABLE [CategoriasProyecto] (
        [Id] int NOT NULL IDENTITY,
        [Nombre] nvarchar(60) NOT NULL,
        [Orden] int NOT NULL DEFAULT 0,
        [Color] nvarchar(20) NOT NULL,
        [Activo] bit NOT NULL DEFAULT CAST(1 AS bit),
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        [UpdatedAt] datetime2 NULL,
        [UpdatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_CategoriasProyecto] PRIMARY KEY ([Id])
    );
    PRINT '   tabla CategoriasProyecto creada';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CategoriasProyecto_Nombre'
               AND object_id = OBJECT_ID('CategoriasProyecto'))
    CREATE UNIQUE INDEX [IX_CategoriasProyecto_Nombre] ON [CategoriasProyecto] ([Nombre]);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CategoriasProyecto_Orden'
               AND object_id = OBJECT_ID('CategoriasProyecto'))
    CREATE INDEX [IX_CategoriasProyecto_Orden] ON [CategoriasProyecto] ([Orden]);

/* La categoría es OPCIONAL: la columna acepta nulo y los proyectos que ya existen quedan
   «sin clasificar», que es un dato y no un hueco. */
IF COL_LENGTH('Proyectos', 'CategoriaId') IS NULL
BEGIN
    ALTER TABLE [Proyectos] ADD [CategoriaId] int NULL;
    PRINT '   columna CategoriaId agregada';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Proyectos_CategoriaId'
               AND object_id = OBJECT_ID('Proyectos'))
    CREATE INDEX [IX_Proyectos_CategoriaId] ON [Proyectos] ([CategoriaId]);

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Proyectos_CategoriasProyecto_CategoriaId')
    ALTER TABLE [Proyectos] ADD CONSTRAINT [FK_Proyectos_CategoriasProyecto_CategoriaId]
        FOREIGN KEY ([CategoriaId]) REFERENCES [CategoriasProyecto] ([Id]) ON DELETE NO ACTION;

PRINT '── 6. Las Q son categorías, no prioridades ─────────────────────────';

/* Si una corrida vieja alcanzó a sembrar «Q3» como prioridad, sale de ahí — siempre que
   ningún proyecto la esté usando. Si alguno la usara, se deja donde está: primero hay que
   reasignar esos proyectos. */
IF COL_LENGTH('Proyectos', 'PrioridadId') IS NOT NULL
    EXEC(N'
        DELETE FROM PrioridadesProyecto
        WHERE  Nombre = ''Q3''
          AND  NOT EXISTS (SELECT 1 FROM Proyectos p WHERE p.PrioridadId = PrioridadesProyecto.Id);');

/* El orden sigue al de la Fórmula 1: Q3 es la ronda final, la de los más rápidos. */
INSERT INTO CategoriasProyecto (Nombre, Orden, Color, Activo, CreatedAt, CreatedBy)
SELECT v.Nombre, v.Orden, v.Color, 1, SYSUTCDATETIME(), 'migracion'
FROM   (VALUES ('Q3', 1, 'Verde'),
               ('Q2', 2, 'Azul'),
               ('Q1', 3, 'Gris')) AS v(Nombre, Orden, Color)
WHERE  NOT EXISTS (SELECT 1 FROM CategoriasProyecto c WHERE c.Nombre = v.Nombre);

PRINT '── 7. Historial de migraciones ─────────────────────────────────────';

/* Para que «dotnet ef» sepa que estas cuatro ya están y no intente aplicarlas de nuevo. */
INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
SELECT v.Id, N'9.0.0'
FROM   (VALUES (N'20260918155856_CatalogoDePrioridadesDeProyecto'),
               (N'20260918162251_CatalogoDePrioridadesDeTicket'),
               (N'20260918191759_CatalogoDeCategoriasDeProyecto'),
               (N'20260918193137_LasQSonCategoriasNoPrioridades')) AS v(Id)
WHERE  NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory h WHERE h.MigrationId = v.Id);

COMMIT;
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT 'Proyectos sin prioridad válida (debe ser 0)' AS Control,
       CAST(COUNT(*) AS varchar(10)) AS Valor
FROM   Proyectos p LEFT JOIN PrioridadesProyecto c ON c.Id = p.PrioridadId
WHERE  c.Id IS NULL
UNION ALL
SELECT 'Tickets sin prioridad válida (debe ser 0)',
       CAST(COUNT(*) AS varchar(10))
FROM   Tickets t LEFT JOIN PrioridadesTicket c ON c.Id = t.PrioridadId
WHERE  c.Id IS NULL
UNION ALL
SELECT 'Columna Proyectos.Prioridad (debe decir eliminada)',
       CASE WHEN COL_LENGTH('Proyectos', 'Prioridad') IS NULL THEN 'eliminada' ELSE 'TODAVIA EXISTE' END
UNION ALL
SELECT 'Columna Tickets.Prioridad (debe decir eliminada)',
       CASE WHEN COL_LENGTH('Tickets', 'Prioridad') IS NULL THEN 'eliminada' ELSE 'TODAVIA EXISTE' END
UNION ALL
SELECT 'Migraciones registradas (deben ser 4)',
       CAST(COUNT(*) AS varchar(10))
FROM   __EFMigrationsHistory
WHERE  MigrationId LIKE '2026091%';

PRINT '';
PRINT 'Catálogos resultantes:';
SELECT 'Prioridad proyecto' AS Catalogo, Nombre, Orden FROM PrioridadesProyecto
UNION ALL SELECT 'Prioridad ticket', Nombre, Orden FROM PrioridadesTicket
UNION ALL SELECT 'Categoría', Nombre, Orden FROM CategoriasProyecto
ORDER BY Catalogo, Orden;
GO
