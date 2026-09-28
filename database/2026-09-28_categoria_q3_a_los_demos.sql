/*
    Clasifica los proyectos DEMO en la categoría Q3, para que el filtro por categoría del tablero
    de institución y su desglose tengan algo que mostrar.

    Contexto: el catálogo `CategoriasProyecto` ya trae las tres rondas de clasificación de la F1
    —Q3, Q2, Q1— desde la migración LasQSonCategoriasNoPrioridades, pero el portafolio estaba casi
    entero sin clasificar: 45 de 46 proyectos con CategoriaId nulo. Sin al menos un grupo
    clasificado, el filtro nuevo no se puede ni demostrar ni probar contra datos de verdad.

    **Solo toca los DEMO.** Los 26 proyectos reales quedan en NULL a propósito, y eso no es un
    descuido: el diseño del módulo dice que la categoría es opcional y que el nulo significa
    «todavía nadie lo clasificó». Una categoría puesta de oficio por un script se lee después igual
    que una que alguien declaró, y no hay forma de separarlas. Los DEMO son distintos porque su
    contenido es simulado desde que se crearon: ponerles Q3 no inventa una clasificación
    institucional, solo puebla el escenario de demostración.

    Idempotente por los dos lados:
      · solo toca filas con CategoriaId IS NULL, así que no sobrescribe ninguna clasificación
        existente —PRY-2026-28 ya venía en Q3 y este script lo deja como está—;
      · corriéndolo dos veces, la segunda no actualiza nada y no duplica la bitácora.

    Se aborta si Q3 no está en el catálogo, en vez de crearla: si falta, lo que corresponde es
    aplicar la migración, no que un script de datos siembre catálogo por su cuenta.

    Ejecución (Windows auth, que es como corre esa instancia):
      sqlcmd -S "localhost\SQL2025" -E -d GestionGD_new -C -I -f 65001 ^
             -i database/2026-09-28_categoria_q3_a_los_demos.sql

    Para verlo antes de aplicarlo, poner @soloSimular = 1: informa a cuántos tocaría y no escribe.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor      nvarchar(200) = N'script-categoria-q3-demos';
DECLARE @categoria  nvarchar(120) = N'Q3';
DECLARE @soloSimular bit          = 0;

/* El patrón va sin la raya larga a propósito: los nombres son «DEMO — …» con guion em, y hacerlo
   depender de ese carácter deja el script a merced de la codificación con que se lo abra. */
DECLARE @patron nvarchar(50) = N'DEMO %';

DECLARE @categoriaId int =
    (SELECT TOP 1 Id FROM CategoriasProyecto WHERE Nombre = @categoria);

IF @categoriaId IS NULL
BEGIN
    RAISERROR(N'No existe la categoría Q3 en CategoriasProyecto. Aplicar la migración LasQSonCategoriasNoPrioridades antes de correr este script.', 16, 1);
    RETURN;
END

DECLARE @candidatos int =
    (SELECT COUNT(*) FROM Proyectos
     WHERE IsDeleted = 0 AND CategoriaId IS NULL AND Nombre LIKE @patron);

IF @soloSimular = 1
BEGIN
    SELECT Codigo, Nombre
    FROM   Proyectos
    WHERE  IsDeleted = 0 AND CategoriaId IS NULL AND Nombre LIKE @patron
    ORDER  BY Codigo;

    PRINT CONCAT(N'Simulación: se clasificarían ', @candidatos, N' proyectos DEMO como ', @categoria, N'.');
    RETURN;
END

IF @candidatos = 0
BEGIN
    PRINT N'No hay proyectos DEMO sin clasificar. Nada que hacer.';
    RETURN;
END

BEGIN TRANSACTION;

DECLARE @tocados TABLE (Id int, Codigo nvarchar(30), Nombre nvarchar(300));

UPDATE p
SET p.CategoriaId = @categoriaId,
    p.UpdatedAt   = SYSUTCDATETIME(),
    p.UpdatedBy   = @actor
OUTPUT inserted.Id, inserted.Codigo, inserted.Nombre INTO @tocados
FROM   Proyectos p
WHERE  p.IsDeleted = 0
  AND  p.CategoriaId IS NULL
  AND  p.Nombre LIKE @patron;

/* La bitácora del proyecto es el registro que no se puede tocar, así que el cambio queda ahí
   aunque lo haya hecho un script y no una persona —y diciendo que fue para la demostración, que es
   el dato que alguien va a necesitar dentro de seis meses. */
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT t.Id, N'ModificacionFicha',
       CONCAT(N'Clasificado en la categoría ', @categoria,
              N' para poblar el escenario de demostración del filtro por categoría del tablero de proyectos.'),
       @actor, SYSUTCDATETIME()
FROM   @tocados t;

COMMIT;

SELECT Codigo, Nombre FROM @tocados ORDER BY Codigo;
PRINT CONCAT(N'Clasificados ', @candidatos, N' proyectos DEMO como ', @categoria, N'.');
