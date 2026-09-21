# Catálogo de prioridades de proyecto — script de base de datos
> ## ⚠ Reemplazado
>
> **No corra este script.** Lo sustituye
> [`2026-09-18_poner_al_dia.md`](2026-09-18_poner_al_dia.md), que hace lo de los cuatro
> documentos de ese día en una sola pasada.
>
> El motivo: éste decidía qué hacer mirando `__EFMigrationsHistory`, y esa fila se escribe al
> final. Si la corrida moría a la mitad quedaban cambios hechos sin marca de que se hicieron, y
> volver a intentarlo fallaba de otra manera. El de reemplazo comprueba el estado real de la base
> en cada paso, así que sirve esté como esté.
>
> Se conserva como referencia de qué hizo cada migración.


**Migración:** `20260918155856_CatalogoDePrioridadesDeProyecto`
**Fecha:** 2026-09-18
**Rama:** `dev`

## Qué hace

La prioridad del proyecto era un texto libre (`Proyectos.Prioridad`, `nvarchar(20)`) que guardaba
el nombre de un enum de C#: `Alta`, `Media` o `Baja`. Agregar una prioridad obligaba a tocar código
y desplegar.

Este script la convierte en una llave foránea al catálogo administrable `PrioridadesProyecto`, que
se edita desde **Catálogos › Prioridades de proyectos**.

Deja sembradas cuatro filas: las tres que existían —con el mismo nombre, para que los proyectos
actuales se reconozcan solos— más **Q3**, que fue la que motivó el cambio.

| Nombre | Orden | Color   | Predeterminada |
|--------|-------|---------|----------------|
| Alta   | 1     | Naranja | no             |
| Media  | 2     | Azul    | **sí**         |
| Baja   | 3     | Gris    | no             |
| Q3     | 4     | Verde   | no             |

> **Ojo:** este script siembra `Q3` como prioridad. Un script posterior la retira y la vuelve a
> crear como categoría — ver [`2026-09-18_las_q_son_categorias.md`](2026-09-18_las_q_son_categorias.md).
> Si va a correr los cuatro seguidos, no haga nada especial: el último deja todo en su lugar.

## El orden de los pasos importa

La columna vieja se borra **al final**, cuando su contenido ya se trasladó. El andamiaje de EF
proponía borrarla primero, lo que habría tirado la prioridad de todos los proyectos y dejado la
llave foránea apuntando a un `Id 0` inexistente.

Un proyecto cuya prioridad no case con ninguna fila —nulo, vacío, o un texto que nadie reconoce—
queda con la predeterminada (`Media`). No hay forma de dejarlo en nulo: la columna queda
obligatoria.

## Antes de correrlo

Conviene saber contra qué se está corriendo:

```sql
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PrioridadesProyecto_Nombre] ON [PrioridadesProyecto] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    CREATE INDEX [IX_PrioridadesProyecto_Orden] ON [PrioridadesProyecto] ([Orden]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN

    EXEC(N'
    INSERT INTO PrioridadesProyecto (Nombre, Orden, Color, EsPredeterminada, Activo, CreatedAt, CreatedBy)
    VALUES (''Alta'',  1, ''Naranja'', 0, 1, SYSUTCDATETIME(), ''migracion''),
           (''Media'', 2, ''Azul'',    1, 1, SYSUTCDATETIME(), ''migracion''),
           (''Baja'',  3, ''Gris'',    0, 1, SYSUTCDATETIME(), ''migracion''),
           (''Q3'',    4, ''Verde'',   0, 1, SYSUTCDATETIME(), ''migracion'');
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    ALTER TABLE [Proyectos] ADD [PrioridadId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN

    EXEC(N'
    UPDATE p
    SET    p.PrioridadId = c.Id
    FROM   Proyectos p
    JOIN   PrioridadesProyecto c ON c.Nombre = p.Prioridad;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN

    EXEC(N'
    UPDATE Proyectos
    SET    PrioridadId = (SELECT TOP 1 Id FROM PrioridadesProyecto WHERE EsPredeterminada = 1)
    WHERE  PrioridadId IS NULL;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Proyectos]') AND [c].[name] = N'PrioridadId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Proyectos] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Proyectos] ALTER COLUMN [PrioridadId] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    CREATE INDEX [IX_Proyectos_PrioridadId] ON [Proyectos] ([PrioridadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    ALTER TABLE [Proyectos] ADD CONSTRAINT [FK_Proyectos_PrioridadesProyecto_PrioridadId] FOREIGN KEY ([PrioridadId]) REFERENCES [PrioridadesProyecto] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Proyectos]') AND [c].[name] = N'Prioridad');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Proyectos] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Proyectos] DROP COLUMN [Prioridad];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918155856_CatalogoDePrioridadesDeProyecto', N'9.0.0');
END;

COMMIT;
GO

```

Todo lo que no salga como `Alta`, `Media` o `Baja` va a terminar en `Media`. Si aparece algo más,
avíseme antes de correrlo y le agrego la fila correspondiente al sembrado.

## Por qué hay EXEC en el script

El archivo es **un solo lote** y SQL Server lo compila entero antes de ejecutar nada, así que las
instrucciones que tocan la tabla y la columna que el propio script crea no llegaban a compilar:
fallaba con «Invalid column name» e «Invalid object name». Van dentro de `EXEC(N'...')`, que
difiere la compilación hasta el momento de ejecutarse — es el mismo recurso que usa EF en sus
propias operaciones. Por eso las comillas simples aparecen dobladas.

Corriendo por `dotnet ef database update` esto nunca se notaba: EF manda cada operación por
separado.

## El script

Es **idempotente**: comprueba `__EFMigrationsHistory` antes de cada paso, así que correrlo dos
veces no hace nada la segunda. Va todo dentro de una transacción.

```sql
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PrioridadesProyecto_Nombre] ON [PrioridadesProyecto] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    CREATE INDEX [IX_PrioridadesProyecto_Orden] ON [PrioridadesProyecto] ([Orden]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN

    INSERT INTO PrioridadesProyecto (Nombre, Orden, Color, EsPredeterminada, Activo, CreatedAt, CreatedBy)
    VALUES ('Alta',  1, 'Naranja', 0, 1, SYSUTCDATETIME(), 'migracion'),
           ('Media', 2, 'Azul',    1, 1, SYSUTCDATETIME(), 'migracion'),
           ('Baja',  3, 'Gris',    0, 1, SYSUTCDATETIME(), 'migracion'),
           ('Q3',    4, 'Verde',   0, 1, SYSUTCDATETIME(), 'migracion');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    ALTER TABLE [Proyectos] ADD [PrioridadId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN

    UPDATE p
    SET    p.PrioridadId = c.Id
    FROM   Proyectos p
    JOIN   PrioridadesProyecto c ON c.Nombre = p.Prioridad;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN

    UPDATE Proyectos
    SET    PrioridadId = (SELECT TOP 1 Id FROM PrioridadesProyecto WHERE EsPredeterminada = 1)
    WHERE  PrioridadId IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Proyectos]') AND [c].[name] = N'PrioridadId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Proyectos] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Proyectos] ALTER COLUMN [PrioridadId] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    CREATE INDEX [IX_Proyectos_PrioridadId] ON [Proyectos] ([PrioridadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    ALTER TABLE [Proyectos] ADD CONSTRAINT [FK_Proyectos_PrioridadesProyecto_PrioridadId] FOREIGN KEY ([PrioridadId]) REFERENCES [PrioridadesProyecto] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Proyectos]') AND [c].[name] = N'Prioridad');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Proyectos] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Proyectos] DROP COLUMN [Prioridad];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918155856_CatalogoDePrioridadesDeProyecto'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918155856_CatalogoDePrioridadesDeProyecto', N'9.0.0');
END;

COMMIT;
GO

```

## Después de correrlo

```sql
-- 1. Ningún proyecto debe quedar sin prioridad válida. Tiene que devolver 0 filas.
SELECT p.Id, p.Codigo
FROM   Proyectos p
LEFT   JOIN PrioridadesProyecto c ON c.Id = p.PrioridadId
WHERE  c.Id IS NULL;

-- 2. El reparto, para compararlo con el conteo de antes.
SELECT c.Nombre, COUNT(p.Id) AS Proyectos
FROM   PrioridadesProyecto c
LEFT   JOIN Proyectos p ON p.PrioridadId = c.Id
GROUP  BY c.Nombre, c.Orden
ORDER  BY c.Orden;

-- 3. Debe haber exactamente una predeterminada.
SELECT COUNT(*) AS Predeterminadas FROM PrioridadesProyecto WHERE EsPredeterminada = 1;
```

Los conteos de la consulta 2 tienen que coincidir con los de la consulta previa, salvo por lo que
se haya reasignado a `Media` y por `Q3`, que arranca en cero.

## Permisos

**No hace falta script.** La clave nueva —`Prioridades.Proyectos.Editar`— la registra sola
`PermissionCatalogSyncService` al arrancar la aplicación, en todos los ambientes. Un administrador
entra sin más porque su rol aprueba por código; para dársela a otro rol, se otorga desde
**Administración › Permisos**.

## Para revertir

La migración tiene su `Down`, pero **revertir pierde información**: una prioridad creada después
—`Q3` incluida— no cabe en el enum de vuelta y sus proyectos aterrizan en `Media`. Si hace falta,
el script de reversión se genera con:

```powershell
dotnet ef migrations script CatalogoDePrioridadesDeProyecto AgregarBorradoLogicoUsuario `
  --project src\Infrastructure --startup-project src\Web --output revertir.sql
```
