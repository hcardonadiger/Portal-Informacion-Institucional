/* =====================================================================================
   Revertir el catálogo de insumos (2026-10-22_poner_al_dia.sql)
   -------------------------------------------------------------------------------------
   Deshace 2026-10-22_poner_al_dia.sql: borra las tres tablas del catálogo de insumos y la
   fila de la migración 20261022203532_AgregarCatalogoDeInsumos en el historial.

   ⚠ BORRA DATOS. Se pierden el catálogo entero —categorías e insumos, incluidos los que
   el administrador haya creado o renombrado— y los insumos asignados a las actividades.
   No toca ninguna otra tabla: proyectos, entregables y actividades quedan como estaban.
   Saque un respaldo de la base antes de correrlo.

   CUÁNDO: solo junto con volver a una versión del portal ANTERIOR a esta entrega. Con
   el portal nuevo corriendo, las páginas de insumos y la ficha del proyecto fallarían
   al no encontrar las tablas. Pare el portal, corra esto, y despliegue la versión
   anterior.

   La clave de permiso Insumos.Catalogo.Editar no se toca acá: al arrancar la versión
   anterior, el portal la marca como inactiva solo, porque su código ya no la declara.

   Correrlo dos veces no cambia nada: cada paso mira si lo que va a borrar existe.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-22_revertir_insumos.sql
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Esta base no tiene __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal.',
        1;
END
GO

PRINT '════════ Revertir el catálogo de insumos ════════';
GO

/* Lo que se va a perder, para que quede en la salida de la corrida. */
IF OBJECT_ID(N'[ProyectoActividadInsumos]', N'U') IS NOT NULL
    EXEC(N'DECLARE @n int = (SELECT COUNT(*) FROM [ProyectoActividadInsumos]);
           PRINT ''  insumos asignados a actividades .. '' + CAST(@n AS varchar(10)) + '' se borran'';');
IF OBJECT_ID(N'[Insumos]', N'U') IS NOT NULL
    EXEC(N'DECLARE @n int = (SELECT COUNT(*) FROM [Insumos]);
           PRINT ''  insumos del catálogo ............ '' + CAST(@n AS varchar(10)) + '' se borran'';');
IF OBJECT_ID(N'[CategoriasInsumo]', N'U') IS NOT NULL
    EXEC(N'DECLARE @n int = (SELECT COUNT(*) FROM [CategoriasInsumo]);
           PRINT ''  categorías de insumo ............ '' + CAST(@n AS varchar(10)) + '' se borran'';');
GO

/* En orden inverso al de creación: primero lo que apunta, después lo apuntado. */
IF OBJECT_ID(N'[ProyectoActividadInsumos]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [ProyectoActividadInsumos];
    PRINT '  ProyectoActividadInsumos ........ borrada';
END
ELSE PRINT '  ProyectoActividadInsumos ........ no estaba';
GO

IF OBJECT_ID(N'[Insumos]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [Insumos];
    PRINT '  Insumos ......................... borrada';
END
ELSE PRINT '  Insumos ......................... no estaba';
GO

IF OBJECT_ID(N'[CategoriasInsumo]', N'U') IS NOT NULL
BEGIN
    DROP TABLE [CategoriasInsumo];
    PRINT '  CategoriasInsumo ................ borrada';
END
ELSE PRINT '  CategoriasInsumo ................ no estaba';
GO

/* El historial, al final: si alguna tabla no se pudo borrar, la corrida se cortó antes
   y la fila sigue diciendo —con razón— que la migración está aplicada. */
IF EXISTS (SELECT 1 FROM __EFMigrationsHistory
           WHERE MigrationId = N'20261022203532_AgregarCatalogoDeInsumos')
BEGIN
    DELETE FROM __EFMigrationsHistory
    WHERE MigrationId = N'20261022203532_AgregarCatalogoDeInsumos';
    PRINT '  historial ....................... quitado';
END
ELSE PRINT '  historial ....................... no estaba';
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT  N'Tabla CategoriasInsumo' AS Control,
        CASE WHEN OBJECT_ID(N'[CategoriasInsumo]', N'U') IS NULL THEN 'retirada' ELSE 'SIGUE' END AS Valor
UNION ALL
SELECT  N'Tabla Insumos',
        CASE WHEN OBJECT_ID(N'[Insumos]', N'U') IS NULL THEN 'retirada' ELSE 'SIGUE' END
UNION ALL
SELECT  N'Tabla ProyectoActividadInsumos',
        CASE WHEN OBJECT_ID(N'[ProyectoActividadInsumos]', N'U') IS NULL THEN 'retirada' ELSE 'SIGUE' END
UNION ALL
SELECT  N'Migración en el historial',
        CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory
                          WHERE MigrationId = N'20261022203532_AgregarCatalogoDeInsumos')
             THEN 'SIGUE' ELSE 'retirada' END
UNION ALL
SELECT  N'ProyectoActividades (no se toca)',
        CASE WHEN OBJECT_ID(N'[ProyectoActividades]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END;

PRINT '';
PRINT 'Las tres tablas y la migración deben decir «retirada»; ProyectoActividades, «presente».';
GO
