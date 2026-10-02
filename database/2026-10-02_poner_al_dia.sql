/* ═══════════════════════════════════════════════════════════════════════════════════════
   Poner al día la base con los cambios al 2026-10-02
   ───────────────────────────────────────────────────────────────────────────────────────
   Va DESPUÉS de 2026-09-24_poner_al_dia.sql. Corra antes 00_diagnostico.sql: él dice si
   hace falta aquél y si esta base está en condiciones de recibir éste.

   Qué trae:

   1. La tabla ReunionAdjuntos — la migración 20261002162236. Es lo único que este script
      crea, y es lo único que «dotnet ef database update» habría aplicado. Al final se
      registra en __EFMigrationsHistory para que EF no la repita.

      Guarda los documentos y enlaces del cierre de una reunión, en las dos tarjetas que
      ahora los capturan:

        · «Documentos y recursos»        → Seccion = 'Documento'
        · «Evidencia fotográfica y de video» → Seccion = 'Evidencia'

      Y distingue si la fila es un archivo que el portal guardó en disco o un enlace a algo
      que vive afuera:

        · Clase = 'Archivo' → Url es una ruta relativa bajo /uploads/reuniones
        · Clase = 'Enlace'  → Url es una dirección http/https (OneDrive, típicamente)

      Esa distinción es la que permite que un video de una capacitación no se coma el disco
      del servidor: el video se deja en OneDrive y acá queda su enlace.

   Qué NO trae, y conviene saberlo:

   · El mismo cambio quitó del portal «Detalle de capacitación» —el objetivo formativo y el
     contenido— porque repetía el objetivo y el desarrollo de la memoria. Las columnas
     Reuniones.ObjetivoCap y Reuniones.Contenido SE QUEDAN donde están, con lo que ya
     tengan: nada las borra y nada las lee. No hay que hacer nada con ellas.

   · Tampoco cambia Reuniones.Foto1Url/Foto2Url. Los dos cupos de foto del acta siguen
     exactamente igual; la tabla nueva los acompaña, no los reemplaza.

   Como el resto de los scripts de esta carpeta, no pregunta por __EFMigrationsHistory para
   decidir qué hacer: cada paso comprueba SU PROPIA condición contra el catálogo de la base
   —¿existe la tabla?, ¿existe el índice?—. La fila del historial se escribe al final, así
   que si la corrida muriera a la mitad, esa fila no sirve para saber qué quedó hecho.

   Se puede correr las veces que haga falta. La segunda no hace nada.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-02_poner_al_dia.sql
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

/* ReunionAdjuntos cuelga de Reuniones con una FK en cascada. Sin esa tabla no hay nada que
   crear, y seguir dejaría la transacción muriendo más abajo con un error de clave foránea
   que no le dice nada a quien corre el script.

   THROW y no RAISERROR: RAISERROR deja el mensaje pero NO corta el lote. */
IF OBJECT_ID(N'[Reuniones]', 'U') IS NULL
    THROW 50000, N'Falta la tabla Reuniones. Corra 00_diagnostico.sql y revise antes de seguir.', 1;

PRINT '   tabla Reuniones presente';

PRINT '── 1. Tabla ReunionAdjuntos ────────────────────────────────────────';

/* Seccion y Clase van como texto y no como número a propósito: estas filas se leen a mano
   cuando algo no cuadra en un despliegue, y un 'Evidencia'/'Enlace' legible vale más que un
   2. Es el mismo criterio que ya sigue Reuniones.Visibilidad.

   Url es nvarchar(1000) y no 600 como Foto1Url: un enlace de OneDrive compartido trae el
   token en el query string y pasa largo de los 600 que alcanzan para una ruta local.

   ON DELETE CASCADE: los adjuntos no tienen vida propia fuera de su reunión. */
IF OBJECT_ID(N'[ReunionAdjuntos]', 'U') IS NULL
BEGIN
    CREATE TABLE [ReunionAdjuntos] (
        [Id]            int            IDENTITY(1,1) NOT NULL,
        [ReunionId]     int            NOT NULL,
        [Orden]         int            NOT NULL,
        [Seccion]       nvarchar(20)   NOT NULL,
        [Clase]         nvarchar(20)   NOT NULL,
        [Titulo]        nvarchar(250)  NOT NULL,
        [Descripcion]   nvarchar(500)  NULL,
        [NombreArchivo] nvarchar(260)  NULL,
        [Url]           nvarchar(1000) NOT NULL,
        [Tamano]        bigint         NOT NULL,
        CONSTRAINT [PK_ReunionAdjuntos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ReunionAdjuntos_Reuniones_ReunionId] FOREIGN KEY ([ReunionId])
            REFERENCES [Reuniones] ([Id]) ON DELETE CASCADE
    );
    PRINT '   tabla ReunionAdjuntos creada';
END
ELSE
    PRINT '   tabla ReunionAdjuntos ya estaba';

PRINT '── 2. Índice IX_ReunionAdjuntos_ReunionId_Seccion_Orden ────────────';

/* Es por el que se listan: todo lo de una reunión, agrupado por tarjeta y en el orden en que
   se capturó. Dentro de EXEC por lo mismo de siempre: el lote se compila entero antes de
   ejecutarse, así que en la primera corrida la tabla de arriba todavía no existe para el
   compilador. */
IF OBJECT_ID(N'[ReunionAdjuntos]', 'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes
                   WHERE name = N'IX_ReunionAdjuntos_ReunionId_Seccion_Orden'
                     AND object_id = OBJECT_ID(N'[ReunionAdjuntos]'))
BEGIN
    EXEC(N'CREATE INDEX [IX_ReunionAdjuntos_ReunionId_Seccion_Orden]
             ON [ReunionAdjuntos] ([ReunionId], [Seccion], [Orden]);');
    PRINT '   índice creado';
END
ELSE
    PRINT '   índice ya estaba';

PRINT '── 3. Historial de migraciones ─────────────────────────────────────';

/* La tabla de arriba sí corresponde a una migración que EF reconoce, así que se registra. */
IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT N'20261002162236_AddReunionAdjuntos', N'9.0.0'
    WHERE  NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory
                       WHERE MigrationId = N'20261002162236_AddReunionAdjuntos');
    PRINT '   historial al día';
END
ELSE
    PRINT '   *** no existe __EFMigrationsHistory: no se registró nada';

COMMIT;
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT  N'Tabla ReunionAdjuntos' AS Control,
        CASE WHEN OBJECT_ID(N'[ReunionAdjuntos]', 'U') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor

UNION ALL
SELECT  v.Control,
        CASE WHEN COL_LENGTH(N'ReunionAdjuntos', v.Columna) IS NULL THEN 'FALTA' ELSE 'presente' END
FROM (VALUES
        (N'ReunionAdjuntos.ReunionId',     N'ReunionId'),
        (N'ReunionAdjuntos.Orden',         N'Orden'),
        (N'ReunionAdjuntos.Seccion',       N'Seccion'),
        (N'ReunionAdjuntos.Clase',         N'Clase'),
        (N'ReunionAdjuntos.Titulo',        N'Titulo'),
        (N'ReunionAdjuntos.Descripcion',   N'Descripcion'),
        (N'ReunionAdjuntos.NombreArchivo', N'NombreArchivo'),
        (N'ReunionAdjuntos.Url',           N'Url'),
        (N'ReunionAdjuntos.Tamano',        N'Tamano')
     ) v(Control, Columna)

UNION ALL
SELECT  N'FK a Reuniones (en cascada)',
        CASE WHEN EXISTS (SELECT 1 FROM sys.foreign_keys
                          WHERE name = N'FK_ReunionAdjuntos_Reuniones_ReunionId')
             THEN 'presente' ELSE 'FALTA' END

UNION ALL
SELECT  N'IX_ReunionAdjuntos_ReunionId_Seccion_Orden',
        CASE WHEN EXISTS (SELECT 1 FROM sys.indexes
                          WHERE name = N'IX_ReunionAdjuntos_ReunionId_Seccion_Orden'
                            AND object_id = OBJECT_ID(N'[ReunionAdjuntos]'))
             THEN 'presente' ELSE 'FALTA' END

UNION ALL
SELECT  N'Migración registrada',
        CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory
                          WHERE MigrationId = N'20261002162236_AddReunionAdjuntos')
             THEN 'presente' ELSE 'FALTA' END;

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
