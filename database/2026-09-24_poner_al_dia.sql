/* ═══════════════════════════════════════════════════════════════════════════════════════
   Poner al día la base con los cambios al 2026-09-24
   ───────────────────────────────────────────────────────────────────────────────────────
   Va DESPUÉS de 2026-09-18_poner_al_dia.sql. Corra antes 00_diagnostico.sql: él dice si
   hace falta aquél y si esta base está en condiciones de recibir éste.

   Qué trae, y por qué son dos cosas distintas:

   1. Reuniones.EncuestaActiva — la migración 20260923223127. Es la única migración de EF
      posterior al 2026-09-18, así que es lo único que «dotnet ef database update» habría
      aplicado. Al final se registra en __EFMigrationsHistory para que EF no la repita.

   2. Asistentes.EsPreregistro, Asistentes.Confirmado, Instituciones.Color — éstas NO las
      crea ninguna migración. Sus dos archivos (AddPreregistroAsistente, AddInstitucionBranding)
      quedaron sin el atributo [Migration] y sin Designer, así que EF no los ve y nunca los
      va a aplicar, por mucho que se corra «database update». El modelo sí usa las tres
      columnas, y Instituciones.Color es la que lee IInstitucionBrandingService.
      Las bases en uso ya las tienen porque se agregaron a mano en su momento; una base
      creada desde cero con las migraciones NO. Por eso van acá y NO se registran en el
      historial: registrar un MigrationId que el código no reconoce solo confunde.

   Como el resto de los scripts de esta carpeta, no pregunta por __EFMigrationsHistory para
   decidir qué hacer: cada paso comprueba SU PROPIA condición contra el catálogo de la base
   —¿existe la columna?, ¿existe el índice?—. La fila del historial se escribe al final, así
   que si la corrida muriera a la mitad, esa fila no sirve para saber qué quedó hecho.

   Se puede correr las veces que haga falta. La segunda no hace nada.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-24_poner_al_dia.sql
   o pegarlo en SSMS y ejecutar.

     -b  detiene en el primer error. IMPORTANTE: sin esa bandera sqlcmd sigue de largo,
         llega al COMMIT y confirma lo que alcanzó a hacer.
     -I  QUOTED_IDENTIFIER ON.

   XACT_ABORT ON hace lo mismo desde adentro: cualquier error deshace TODO. O entra
   completo o no entra nada.
   ═══════════════════════════════════════════════════════════════════════════════════════ */

SET QUOTED_IDENTIFIER ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

PRINT '── 0. Comprobación previa ──────────────────────────────────────────';

/* Si falta alguna de estas tablas, esta base está mucho más atrasada de lo que este script
   arregla, y seguir dejaría un arreglo a medias. Mejor parar con un mensaje que diga qué
   pasa: agregar columnas no reconstruye módulos.

   THROW y no RAISERROR: RAISERROR deja el mensaje pero NO corta el lote, así que el script
   seguía de largo y moría más abajo con «Cannot find the object "Reuniones"», que no le
   dice nada a quien lo corre. THROW termina el lote, y con XACT_ABORT ON deshace la
   transacción. */
IF OBJECT_ID(N'[Reuniones]', 'U') IS NULL
    OR OBJECT_ID(N'[Asistentes]', 'U') IS NULL
    OR OBJECT_ID(N'[Instituciones]', 'U') IS NULL
    THROW 50000, N'Faltan tablas base (Reuniones, Asistentes o Instituciones). Corra 00_diagnostico.sql y revise antes de seguir.', 1;

PRINT '   tablas base presentes';

PRINT '── 1. Reuniones.EncuestaActiva ─────────────────────────────────────';

/* Decide si el cierre de la reunión pide la segunda firma. Obligatoria y en falso: una
   reunión que ya existe no tenía encuesta, así que ese es el valor correcto para todas. */
IF COL_LENGTH('Reuniones', 'EncuestaActiva') IS NULL
BEGIN
    ALTER TABLE [Reuniones] ADD [EncuestaActiva] bit NOT NULL DEFAULT CAST(0 AS bit);
    PRINT '   columna EncuestaActiva agregada';
END
ELSE
    PRINT '   columna EncuestaActiva ya estaba';

PRINT '── 2. Asistentes.EsPreregistro ─────────────────────────────────────';

/* Distingue al invitado que se pre-registró del que llegó y se anotó con el QR. Las filas
   que ya existen son del segundo tipo, de ahí el falso. */
IF COL_LENGTH('Asistentes', 'EsPreregistro') IS NULL
BEGIN
    ALTER TABLE [Asistentes] ADD [EsPreregistro] bit NOT NULL DEFAULT CAST(0 AS bit);
    PRINT '   columna EsPreregistro agregada';
END
ELSE
    PRINT '   columna EsPreregistro ya estaba';

PRINT '── 3. Asistentes.Confirmado ────────────────────────────────────────';

/* Tres estados, por eso admite nulo: sí asistió, no asistió, y todavía no se sabe. */
IF COL_LENGTH('Asistentes', 'Confirmado') IS NULL
BEGIN
    ALTER TABLE [Asistentes] ADD [Confirmado] bit NULL;
    PRINT '   columna Confirmado agregada';
END
ELSE
    PRINT '   columna Confirmado ya estaba';

PRINT '── 4. Índice IX_Asistentes_EsPreregistro ───────────────────────────';

/* Dentro de EXEC a propósito. El lote se compila entero antes de ejecutarse, así que una
   columna agregada arriba todavía no existe para el compilador: sin EXEC, la primera
   corrida sobre una base que no tenía EsPreregistro moriría con «Invalid column name». */
IF NOT EXISTS (SELECT 1 FROM sys.indexes
               WHERE name = N'IX_Asistentes_EsPreregistro'
                 AND object_id = OBJECT_ID(N'[Asistentes]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_Asistentes_EsPreregistro] ON [Asistentes] ([EsPreregistro]);');
    PRINT '   índice creado';
END
ELSE
    PRINT '   índice ya estaba';

PRINT '── 5. Instituciones.Color ──────────────────────────────────────────';

/* El color de marca de la institución. nvarchar(max) porque el modelo no le pone largo;
   no confundir con Roles.Color, que sí es nvarchar(20). */
IF COL_LENGTH('Instituciones', 'Color') IS NULL
BEGIN
    ALTER TABLE [Instituciones] ADD [Color] nvarchar(max) NULL;
    PRINT '   columna Color agregada';
END
ELSE
    PRINT '   columna Color ya estaba';

PRINT '── 6. Historial de migraciones ─────────────────────────────────────';

/* Solo la de la encuesta. Las otras tres columnas no corresponden a ninguna migración que
   EF reconozca —ver la cabecera—, así que meterlas acá le mentiría al historial. */
IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT N'20260923223127_AddReunionEncuestaActiva', N'9.0.0'
    WHERE  NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory
                       WHERE MigrationId = N'20260923223127_AddReunionEncuestaActiva');
    PRINT '   historial al día';
END
ELSE
    PRINT '   *** no existe __EFMigrationsHistory: no se registró nada';

COMMIT;
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT  v.Control,
        CASE WHEN COL_LENGTH(v.Tabla, v.Columna) IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
FROM (VALUES
        (N'Reuniones.EncuestaActiva',    N'Reuniones',     N'EncuestaActiva'),
        (N'Asistentes.EsPreregistro',    N'Asistentes',    N'EsPreregistro'),
        (N'Asistentes.Confirmado',       N'Asistentes',    N'Confirmado'),
        (N'Instituciones.Color',         N'Instituciones', N'Color')
     ) v(Control, Tabla, Columna)

UNION ALL
SELECT  N'IX_Asistentes_EsPreregistro',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = N'IX_Asistentes_EsPreregistro'
                            AND object_id = OBJECT_ID(N'[Asistentes]'))
             THEN 'presente' ELSE 'FALTA' END

UNION ALL
SELECT  N'Migración de la encuesta registrada',
        CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory
                          WHERE MigrationId = N'20260923223127_AddReunionEncuestaActiva')
             THEN 'presente' ELSE 'FALTA' END;

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
