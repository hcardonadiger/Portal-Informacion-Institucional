# Catálogo de categorías de proyecto — script de base de datos
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


**Migración:** `20260918191759_CatalogoDeCategoriasDeProyecto`
**Fecha:** 2026-09-18
**Rama:** `dev`
**Requisito:** después de los dos scripts de prioridades.

## Qué hace

Agrega el catálogo administrable `CategoriasProyecto` —se edita en **Catálogos › Categorías de
proyectos**, solo administradores— y la columna `Proyectos.CategoriaId` que lo apunta.

La categoría responde **«¿de qué trata?»**, que es distinto de la acción —«¿qué ponemos nosotros
acá?»— y de la prioridad —«¿cuánto pesa?»—.

## Es opcional, y eso simplifica todo

`CategoriaId` **acepta nulo**. Los proyectos que ya existen quedan «sin clasificar», que es un
valor legítimo y no un hueco: una categoría puesta de oficio por una migración se lee igual que
una que alguien declaró, y después no hay forma de separarlas. Es el mismo criterio con el que se
agregó la acción del proyecto.

Por eso esta migración, a diferencia de las dos de prioridades, **no traslada nada ni borra
ninguna columna**: solo agrega. No hay riesgo de perder datos.

## Tampoco siembra categorías

El catálogo nace vacío, a propósito. Inventar categorías acá sería exactamente lo que el nulo
evita. Las crea usted en la pantalla, y mientras no haya ninguna los proyectos simplemente quedan
sin clasificar.

## El script

Idempotente y dentro de una transacción.

```sql
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918191759_CatalogoDeCategoriasDeProyecto'
)
BEGIN
    ALTER TABLE [Proyectos] ADD [CategoriaId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918191759_CatalogoDeCategoriasDeProyecto'
)
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
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918191759_CatalogoDeCategoriasDeProyecto'
)
BEGIN
    CREATE INDEX [IX_Proyectos_CategoriaId] ON [Proyectos] ([CategoriaId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918191759_CatalogoDeCategoriasDeProyecto'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CategoriasProyecto_Nombre] ON [CategoriasProyecto] ([Nombre]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918191759_CatalogoDeCategoriasDeProyecto'
)
BEGIN
    CREATE INDEX [IX_CategoriasProyecto_Orden] ON [CategoriasProyecto] ([Orden]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918191759_CatalogoDeCategoriasDeProyecto'
)
BEGIN
    ALTER TABLE [Proyectos] ADD CONSTRAINT [FK_Proyectos_CategoriasProyecto_CategoriaId] FOREIGN KEY ([CategoriaId]) REFERENCES [CategoriasProyecto] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918191759_CatalogoDeCategoriasDeProyecto'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918191759_CatalogoDeCategoriasDeProyecto', N'9.0.0');
END;

COMMIT;
GO

```

## Después de correrlo

```sql
-- La tabla y la columna existen, y ningún proyecto apunta a una categoría inexistente.
SELECT 'Tabla',   CASE WHEN OBJECT_ID('CategoriasProyecto') IS NULL THEN 'FALTA' ELSE 'ok' END
UNION ALL
SELECT 'Columna', CASE WHEN COL_LENGTH('Proyectos','CategoriaId') IS NULL THEN 'FALTA' ELSE 'ok' END
UNION ALL
SELECT 'Huérfanos', CAST((SELECT COUNT(*) FROM Proyectos p
                          LEFT JOIN CategoriasProyecto c ON c.Id = p.CategoriaId
                          WHERE p.CategoriaId IS NOT NULL AND c.Id IS NULL) AS varchar);
```

`Huérfanos` tiene que dar 0. Todos los proyectos quedan con `CategoriaId` en nulo hasta que alguien
los clasifique desde la ficha.

## Permisos

**No hace falta script.** La clave `Categorias.Proyectos.Editar` la registra sola
`PermissionCatalogSyncService` al arrancar. Queda solo para administradores, como se pidió: para
dárselo a otro rol se otorga desde **Administración › Permisos**.

## Para revertir

Esta sí se revierte limpio —solo se pierde lo que se haya clasificado, porque la columna
desaparece—:

```powershell
dotnet ef migrations script CatalogoDeCategoriasDeProyecto CatalogoDePrioridadesDeTicket `
  --project src\Infrastructure --startup-project src\Web --output revertir.sql
```
