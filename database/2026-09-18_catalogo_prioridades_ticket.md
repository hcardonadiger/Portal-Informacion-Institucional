# Catálogo de prioridades de ticket — script de base de datos
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


**Migración:** `20260918162251_CatalogoDePrioridadesDeTicket`
**Fecha:** 2026-09-18
**Rama:** `dev`
**Requisito:** correr primero [`2026-09-18_catalogo_prioridades.md`](2026-09-18_catalogo_prioridades.md) — el de proyectos.

## Qué hace

Lo mismo que el de proyectos, pero con los tickets: `Tickets.Prioridad` (texto) pasa a ser una
llave foránea al catálogo administrable `PrioridadesTicket`, que se edita en
**Catálogos › Prioridades de tickets**.

| Nombre  | Orden | Color   | Crítica | Predeterminada |
|---------|-------|---------|---------|----------------|
| Critica | 1     | Rojo    | **sí**  | no             |
| Alta    | 2     | Naranja | no      | no             |
| Media   | 3     | Azul    | no      | **sí**         |
| Baja    | 4     | Gris    | no      | no             |

El orden se invierte respecto del enum (donde `Baja` era 1): acá el 1 es el que va primero en
pantalla, y lo urgente va primero.

`Critica` va **sin tilde** a propósito: así se llamaba el miembro del enum y así está guardado en
los tickets que ya existen, de modo que el traslado los reconozca. Una vez corrido el script,
usted puede renombrarla a «Crítica» desde la pantalla, y nada se rompe —los tickets apuntan al
Id, no al nombre—.

## La marca «cuenta como crítica»

Los tableros muestran un indicador de **críticos abiertos**. Esa cuenta preguntaba
`Prioridad == PrioridadTicket.Critica`; con un catálogo el nombre lo edita quien administra, así
que la condición pasó a mirar la columna `EsCritica`. Puede marcar más de una: si algún día
«Crítica» y «Bloqueante» tienen que pesar igual en el indicador, se marcan las dos.

## Antes de correrlo

```sql
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PrioridadesTicket_Nombre] ON [PrioridadesTicket] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    CREATE INDEX [IX_PrioridadesTicket_Orden] ON [PrioridadesTicket] ([Orden]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN

    EXEC(N'
    INSERT INTO PrioridadesTicket (Nombre, Orden, Color, EsCritica, EsPredeterminada, Activo, CreatedAt, CreatedBy)
    VALUES (''Critica'', 1, ''Rojo'',    1, 0, 1, SYSUTCDATETIME(), ''migracion''),
           (''Alta'',    2, ''Naranja'', 0, 0, 1, SYSUTCDATETIME(), ''migracion''),
           (''Media'',   3, ''Azul'',    0, 1, 1, SYSUTCDATETIME(), ''migracion''),
           (''Baja'',    4, ''Gris'',    0, 0, 1, SYSUTCDATETIME(), ''migracion'');
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    ALTER TABLE [Tickets] ADD [PrioridadId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN

    EXEC(N'
    UPDATE t
    SET    t.PrioridadId = c.Id
    FROM   Tickets t
    JOIN   PrioridadesTicket c ON c.Nombre = t.Prioridad;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN

    EXEC(N'
    UPDATE Tickets
    SET    PrioridadId = (SELECT TOP 1 Id FROM PrioridadesTicket WHERE EsPredeterminada = 1)
    WHERE  PrioridadId IS NULL;
    ');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tickets]') AND [c].[name] = N'PrioridadId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Tickets] ALTER COLUMN [PrioridadId] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    CREATE INDEX [IX_Tickets_PrioridadId] ON [Tickets] ([PrioridadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    ALTER TABLE [Tickets] ADD CONSTRAINT [FK_Tickets_PrioridadesTicket_PrioridadId] FOREIGN KEY ([PrioridadId]) REFERENCES [PrioridadesTicket] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tickets]') AND [c].[name] = N'Prioridad');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Tickets] DROP COLUMN [Prioridad];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918162251_CatalogoDePrioridadesDeTicket', N'9.0.0');
END;

COMMIT;
GO

```

Todo lo que no salga como `Baja`, `Media`, `Alta` o `Critica` va a terminar en `Media`.

## Por qué hay EXEC en el script

El archivo es **un solo lote** y SQL Server lo compila entero antes de ejecutar nada, así que las
instrucciones que tocan la tabla y la columna que el propio script crea no llegaban a compilar:
fallaba con «Invalid column name» e «Invalid object name». Van dentro de `EXEC(N'...')`, que
difiere la compilación hasta el momento de ejecutarse — es el mismo recurso que usa EF en sus
propias operaciones. Por eso las comillas simples aparecen dobladas.

Corriendo por `dotnet ef database update` esto nunca se notaba: EF manda cada operación por
separado.

## El script

Idempotente y dentro de una transacción, igual que el anterior.

```sql
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PrioridadesTicket_Nombre] ON [PrioridadesTicket] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    CREATE INDEX [IX_PrioridadesTicket_Orden] ON [PrioridadesTicket] ([Orden]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN

    INSERT INTO PrioridadesTicket (Nombre, Orden, Color, EsCritica, EsPredeterminada, Activo, CreatedAt, CreatedBy)
    VALUES ('Critica', 1, 'Rojo',    1, 0, 1, SYSUTCDATETIME(), 'migracion'),
           ('Alta',    2, 'Naranja', 0, 0, 1, SYSUTCDATETIME(), 'migracion'),
           ('Media',   3, 'Azul',    0, 1, 1, SYSUTCDATETIME(), 'migracion'),
           ('Baja',    4, 'Gris',    0, 0, 1, SYSUTCDATETIME(), 'migracion');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    ALTER TABLE [Tickets] ADD [PrioridadId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN

    UPDATE t
    SET    t.PrioridadId = c.Id
    FROM   Tickets t
    JOIN   PrioridadesTicket c ON c.Nombre = t.Prioridad;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN

    UPDATE Tickets
    SET    PrioridadId = (SELECT TOP 1 Id FROM PrioridadesTicket WHERE EsPredeterminada = 1)
    WHERE  PrioridadId IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tickets]') AND [c].[name] = N'PrioridadId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [Tickets] ALTER COLUMN [PrioridadId] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    CREATE INDEX [IX_Tickets_PrioridadId] ON [Tickets] ([PrioridadId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    ALTER TABLE [Tickets] ADD CONSTRAINT [FK_Tickets_PrioridadesTicket_PrioridadId] FOREIGN KEY ([PrioridadId]) REFERENCES [PrioridadesTicket] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Tickets]') AND [c].[name] = N'Prioridad');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Tickets] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [Tickets] DROP COLUMN [Prioridad];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918162251_CatalogoDePrioridadesDeTicket'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918162251_CatalogoDePrioridadesDeTicket', N'9.0.0');
END;

COMMIT;
GO

```

## Después de correrlo

```sql
-- 1. Ningún ticket debe quedar sin prioridad válida. Tiene que devolver 0 filas.
SELECT t.Id, t.Numero
FROM   Tickets t
LEFT   JOIN PrioridadesTicket c ON c.Id = t.PrioridadId
WHERE  c.Id IS NULL;

-- 2. El reparto, para compararlo con el conteo de antes.
SELECT c.Nombre, COUNT(t.Id) AS Tickets
FROM   PrioridadesTicket c
LEFT   JOIN Tickets t ON t.PrioridadId = c.Id
GROUP  BY c.Nombre, c.Orden
ORDER  BY c.Orden;

-- 3. Una sola predeterminada, y al menos una marcada como crítica.
SELECT
    (SELECT COUNT(*) FROM PrioridadesTicket WHERE EsPredeterminada = 1) AS Predeterminadas,
    (SELECT COUNT(*) FROM PrioridadesTicket WHERE EsCritica = 1)        AS Criticas;
```

La consulta 3 debe dar `Predeterminadas = 1` y `Criticas = 1`. Si `Criticas` diera 0, el indicador
de «críticos abiertos» de los tableros mostraría cero siempre.

## Permisos

**No hace falta script.** La clave `Prioridades.Tickets.Editar` la registra sola
`PermissionCatalogSyncService` al arrancar. El administrador entra sin más; para otro rol, se
otorga desde **Administración › Permisos**.

## Para revertir

Pierde información igual que el anterior: una prioridad creada después no cabe en el enum de
vuelta y sus tickets aterrizan en `Media`.

```powershell
dotnet ef migrations script CatalogoDePrioridadesDeTicket CatalogoDePrioridadesDeProyecto `
  --project src\Infrastructure --startup-project src\Web --output revertir.sql
```
