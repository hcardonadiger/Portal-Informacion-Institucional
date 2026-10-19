/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-14
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-12_poner_al_dia.sql. Corra antes 00_diagnostico.sql: él dice si
   esta base está en condiciones de recibir éste.

   QUÉ TRAE, Y POR QUÉ

   Reuniones.InstitucionDuenaId — la migración 20261014172324_AgregarInstitucionDuenaAReunion.
   Es la institución que CONVOCÓ la reunión. No confundir con Reuniones.InstitucionId, que
   es la CONTRAPARTE invitada y vale null cuando no se eligió ninguna.

   El filtro de alcance de Reunion necesitaba un ancla institucional y solo tenía la
   contraparte, porque AreaId y UnidadId llegan nulas en 80 de las 81 reuniones de la base.
   Eso repartía mal en las dos direcciones:

     · Hacia adentro: un usuario de DIGER veía únicamente las 14 reuniones donde DIGER
       quedó anotada como invitada. De ahí salían los 3 acuerdos vencidos y las 27 personas
       capacitadas —de 21 instituciones ajenas— que aparecían en su Inicio sin ser suyos.

     · Hacia afuera, que es lo grave: los 3 usuarios de CONSUCOOP veían 17 reuniones
       internas de DIGER, con sus acuerdos y la lista completa de asistentes, solo por
       figurar como contraparte. Eso es una fuga de aislamiento, no una molestia de tablero.

   AGREGAR Y RELLENAR SON UN SOLO ACTO. La columna nueva es el ancla del respaldo del
   filtro: una base con la columna agregada y vacía deja a toda reunión sin área ni unidad
   invisible para todos salvo su creador y un administrador global. Por eso el paso 3 no es
   opcional y por eso va dentro de la misma transacción.

   La regla de relleno es: la institución de quien creó la reunión; y si no hay creador —las
   78 importadas del portal legado no lo tienen— la casa, que se deduce de dónde está
   asignada la mayoría de los usuarios en vez de escribir una sigla aquí dentro.

   Y UNA SEGUNDA COSA, QUE NO ES MIGRACIÓN (paso 5): atribuir dueño a las reuniones que no lo
   tenían. La regla es la que ya aplica el portal al crear —la reunión es de la unidad de quien
   la convocó— y las 78 migradas del portal legado, que no traen creador, van a Digitalización
   de Trámites (unidad DITRA, área GOBDIG). Sin eso, 80 de 81 reuniones quedaban sin dueño y el
   respaldo del filtro se las repartía a toda la institución: un Jefe de Unidad de Desarrollo de
   Proyectos veía las 78 de otra unidad. No cambia esquema, así que no va al historial.

   Como el resto de los scripts de esta carpeta, no pregunta por __EFMigrationsHistory para
   decidir qué hacer: cada paso comprueba SU PROPIA condición contra el catálogo de la base.
   La fila del historial se escribe al final, así que si la corrida muriera a la mitad, esa
   fila no sirve para saber qué quedó hecho.

   Se puede correr las veces que haga falta. La segunda no hace nada: el paso 3 solo toca
   filas en null.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-14_poner_al_dia.sql
   o pegarlo en SSMS y ejecutar.

     -b  detiene en el primer error. IMPORTANTE: sin esa bandera sqlcmd sigue de largo,
         llega al COMMIT y confirma lo que alcanzó a hacer.
     -I  QUOTED_IDENTIFIER ON.

   XACT_ABORT ON hace lo mismo desde adentro: cualquier error deshace TODO. O entra
   completo o no entra nada.
   ===================================================================================== */

SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT '── 0. Comprobación previa ──────────────────────────────────────────';

/* AsignacionesUsuario no es decorativa acá: es de donde sale el relleno del paso 3. Sin
   ella el script agregaría la columna y la dejaría vacía, que es justo el estado que no
   debe quedar. THROW y no RAISERROR: RAISERROR deja el mensaje pero NO corta el lote. */
IF OBJECT_ID(N'[Reuniones]', 'U') IS NULL
    OR OBJECT_ID(N'[AsignacionesUsuario]', 'U') IS NULL
    THROW 50000, N'Faltan tablas base (Reuniones o AsignacionesUsuario). Corra 00_diagnostico.sql y revise antes de seguir.', 1;

PRINT '   tablas base presentes';

PRINT '── 1. Reuniones.InstitucionDuenaId ─────────────────────────────────';

/* nvarchar(120) como Instituciones.Id. Admite nulo: una reunión puede existir antes de que
   se sepa la casa, y el paso 3 la rellena. */
IF COL_LENGTH('Reuniones', 'InstitucionDuenaId') IS NULL
BEGIN
    ALTER TABLE [Reuniones] ADD [InstitucionDuenaId] nvarchar(120) NULL;
    PRINT '   columna InstitucionDuenaId agregada';
END
ELSE
    PRINT '   columna InstitucionDuenaId ya estaba';

PRINT '── 2. Índice IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId ───────';

/* Las tres juntas porque el respaldo del filtro pregunta por las tres a la vez.
   Dentro de EXEC a propósito: el lote se compila entero antes de ejecutarse, así que la
   columna agregada arriba todavía no existe para el compilador. Sin EXEC, la primera
   corrida sobre una base que no la tenía moriría con «Invalid column name». */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId'
                 AND object_id = OBJECT_ID(N'[Reuniones]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId]
             ON [Reuniones] ([InstitucionDuenaId], [AreaId], [UnidadId]);');
    PRINT '   índice creado';
END
ELSE
    PRINT '   índice ya estaba';

PRINT '── 3. Relleno de la institución dueña ──────────────────────────────';

/* También dentro de EXEC, y por el mismo motivo que el índice.
   Solo filas en null: ni pisa lo que ya esté puesto ni hace nada en la segunda corrida. */
EXEC(N'
DECLARE @casa nvarchar(120) = (
    SELECT TOP 1 InstitucionId FROM AsignacionesUsuario
    GROUP BY InstitucionId ORDER BY COUNT(DISTINCT UsuarioId) DESC);

IF @casa IS NULL
    PRINT ''   *** no hay asignaciones de usuario: no se pudo deducir la casa'';

UPDATE r SET r.InstitucionDuenaId = COALESCE(
        (SELECT TOP 1 a.InstitucionId FROM AsignacionesUsuario a WHERE a.UsuarioId = r.CreadoPorId),
        @casa)
FROM Reuniones r
WHERE r.InstitucionDuenaId IS NULL;

PRINT ''   reuniones rellenadas: '' + CAST(@@ROWCOUNT AS varchar(10));
');

PRINT '── 4. Historial de migraciones ─────────────────────────────────────';

/* Esta sí corresponde a una migración que EF reconoce, así que se registra para que
   «dotnet ef database update» no intente repetirla. */
IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT N'20261014172324_AgregarInstitucionDuenaAReunion', N'9.0.0'
    WHERE  NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory
                       WHERE MigrationId = N'20261014172324_AgregarInstitucionDuenaAReunion');
    PRINT '   historial al día';
END
ELSE
    PRINT '   *** no existe __EFMigrationsHistory: no se registró nada';

PRINT '── 5. Unidad dueña de las reuniones ───────────────────────────────';

/* Esto NO es una migración de EF: no cambia el esquema, solo atribuye dueño a filas que no lo
   tenían. Por eso no se registra en el historial.

   La regla es la que ya aplica el portal al crear: la reunión es de la unidad de quien la
   convocó. Las 78 migradas del portal legado no traen creador, y por decisión del área se
   atribuyen a Digitalización de Trámites —unidad DITRA, del área GOBDIG—, que es quien hizo
   ese trabajo.

   Sin esto, 80 de las 81 reuniones quedaban sin dueño y el respaldo del filtro se las repartía
   a toda la institución: un Jefe de Unidad de Desarrollo de Proyectos veía las 78 de otra
   unidad. Dentro de EXEC por lo mismo que los pasos anteriores. */
IF NOT EXISTS (SELECT 1 FROM Unidades WHERE Id = N'DITRA')
    PRINT '   *** no existe la unidad DITRA: las migradas quedan sin dueño';
ELSE
EXEC(N'
UPDATE r
SET r.AreaId   = COALESCE(r.AreaId,   a.AreaId),
    r.UnidadId = COALESCE(r.UnidadId, a.UnidadId)
FROM Reuniones r
CROSS APPLY (SELECT TOP 1 x.AreaId, x.UnidadId FROM AsignacionesUsuario x
             WHERE x.UsuarioId = r.CreadoPorId ORDER BY x.Id) a
WHERE r.CreadoPorId IS NOT NULL AND (r.AreaId IS NULL OR r.UnidadId IS NULL)
  AND (a.AreaId IS NOT NULL OR a.UnidadId IS NOT NULL);

PRINT ''   atribuidas a la unidad de su creador: '' + CAST(@@ROWCOUNT AS varchar(10));

UPDATE Reuniones
SET AreaId = ''GOBDIG'', UnidadId = ''DITRA''
WHERE OrigenExternoId IS NOT NULL AND AreaId IS NULL AND UnidadId IS NULL;

PRINT ''   migradas atribuidas a Digitalización de Trámites: '' + CAST(@@ROWCOUNT AS varchar(10));
');
COMMIT;
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT  N'Reuniones.InstitucionDuenaId' AS Control,
        CASE WHEN COL_LENGTH('Reuniones', 'InstitucionDuenaId') IS NULL
             THEN 'FALTA' ELSE 'presente' END AS Valor

UNION ALL
SELECT  N'IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = N'IX_Reuniones_InstitucionDuenaId_AreaId_UnidadId'
                            AND object_id = OBJECT_ID(N'[Reuniones]'))
             THEN 'presente' ELSE 'FALTA' END

UNION ALL
/* La que de verdad importa: una reunión sin dueña no la ve nadie. */
SELECT  N'Ninguna reunión sin institución dueña',
        CASE WHEN EXISTS (SELECT 1 FROM Reuniones WHERE InstitucionDuenaId IS NULL AND IsDeleted = 0)
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
/* Las migradas tienen que haber quedado con unidad; si no, siguen repartiéndose solas. */
SELECT  N'Ninguna reunión migrada sin unidad dueña',
        CASE WHEN EXISTS (SELECT 1 FROM Reuniones
                          WHERE OrigenExternoId IS NOT NULL AND IsDeleted = 0 AND UnidadId IS NULL)
             THEN 'FALTA' ELSE 'presente' END

UNION ALL
SELECT  N'Migración registrada en el historial',
        CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory
                          WHERE MigrationId = N'20261014172324_AgregarInstitucionDuenaAReunion')
             THEN 'presente' ELSE 'FALTA' END;

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
