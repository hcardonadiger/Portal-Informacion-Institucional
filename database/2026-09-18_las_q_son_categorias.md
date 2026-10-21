# Las «Q» son categorías, no prioridades — script de base de datos
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


**Migración:** `20260918193137_LasQSonCategoriasNoPrioridades`
**Fecha:** 2026-09-18
**Rama:** `dev`
**Requisito:** después de los tres scripts anteriores (prioridades de proyecto, prioridades de
ticket y categorías).

## Qué corrige

El primer script de prioridades sembró **«Q3» como una prioridad más**, junto a Alta, Media y
Baja, porque así se planteó el pedido al inicio. Con el catálogo de categorías ya en pie quedó
claro que **Q1, Q2 y Q3 son categorías** —tomadas de las tres rondas de clasificación de la
Fórmula 1— y no niveles de urgencia.

Este script mueve las Q a donde van:

| Cambio | Detalle |
|---|---|
| Quita | La prioridad `Q3`, si ningún proyecto la usa |
| Agrega | Las categorías `Q3`, `Q2` y `Q1` |

Después, **Prioridades** queda con Alta, Media y Baja —que es lo que significa una prioridad— y
**Categorías** con las tres Q.

## El orden y los colores

Sigue al de la F1: **Q3 es la ronda final, la de los más rápidos**, así que va primero.

| Categoría | Orden | Color |
|---|---|---|
| Q3 | 1 | Verde |
| Q2 | 2 | Azul |
| Q1 | 3 | Gris |

Los colores **no usan el rojo a propósito**. En el listado cada proyecto ya muestra su insignia de
prioridad, y una categoría en rojo al lado haría que toda la fila se leyera como una alarma. Verde,
azul y gris separan las dos escalas de un vistazo. Se cambian en dos clics desde
**Catálogos › Categorías de proyectos**.

## Es inofensivo si ya lo hizo a mano

Todo va con guardas: la baja de la prioridad solo ocurre si nadie la usa, y las altas comprueban
que el nombre no exista. Correrlo sobre una base ya ajustada no hace nada.

Si algún proyecto llegara a tener la prioridad `Q3` asignada, **el script la deja donde está** en
vez de fallar: primero hay que reasignar esos proyectos a una prioridad real.

## El script

```sql
BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918193137_LasQSonCategoriasNoPrioridades'
)
BEGIN

    DELETE FROM PrioridadesProyecto
    WHERE  Nombre = 'Q3'
      AND  NOT EXISTS (SELECT 1 FROM Proyectos p WHERE p.PrioridadId = PrioridadesProyecto.Id);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918193137_LasQSonCategoriasNoPrioridades'
)
BEGIN

    INSERT INTO CategoriasProyecto (Nombre, Orden, Color, Activo, CreatedAt, CreatedBy)
    SELECT v.Nombre, v.Orden, v.Color, 1, SYSUTCDATETIME(), 'migracion'
    FROM   (VALUES
               ('Q3', 1, 'Verde'),
               ('Q2', 2, 'Azul'),
               ('Q1', 3, 'Gris')
           ) AS v(Nombre, Orden, Color)
    WHERE  NOT EXISTS (SELECT 1 FROM CategoriasProyecto c WHERE c.Nombre = v.Nombre);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260918193137_LasQSonCategoriasNoPrioridades'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260918193137_LasQSonCategoriasNoPrioridades', N'9.0.0');
END;

COMMIT;
GO

```

## Después de correrlo

```sql
SELECT 'Prioridad' AS Catalogo, Nombre, Orden FROM PrioridadesProyecto
UNION ALL
SELECT 'Categoría', Nombre, Orden FROM CategoriasProyecto
ORDER BY Catalogo, Orden;
```

Tiene que quedar:

```
Categoría   Q3   1
Categoría   Q2   2
Categoría   Q1   3
Prioridad   Alta   1
Prioridad   Media  2
Prioridad   Baja   3
```

Si todavía apareciera `Prioridad Q3`, es que algún proyecto la tenía asignada. Reasígnelos y
vuelva a correr el script.

## La plantilla de Excel

**Regenérela después de correr esto.** Sus listas desplegables salen de la base, así que la que
tenga de antes ofrece `Q3` como prioridad y no ofrece ninguna categoría:

```powershell
cd database\plantillas
.\Nueva-Plantilla-Proyectos.ps1
```
