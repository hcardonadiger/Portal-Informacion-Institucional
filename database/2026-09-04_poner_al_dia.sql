/* ═══════════════════════════════════════════════════════════════════════════════════════
   Poner al día la base con los cambios al 2026-09-04
   ───────────────────────────────────────────────────────────────────────────────────────
   Es el PRIMERO de la cadena: va ANTES de 2026-09-18_poner_al_dia.sql. Corra antes
   00_diagnostico.sql, que dice cuáles de los cuatro scripts hacen falta.

   Trae las CINCO migraciones del 2 al 4 de septiembre, que ningún script cubría. Son seis
   columnas en cinco tablas, y es exactamente lo que «dotnet ef database update» habría
   aplicado. Al final se registran las cinco en __EFMigrationsHistory.

   ┌─ Roles.EsJefeDeArea, Roles.EsPmo ── 20260902215804_AgregarJefeDeAreaYPmo ────────────
   │
   │  Dos capacidades más del rol, al lado de las cuatro que ya había (EsAdministrador,
   │  EsSoloLectura, EsSupervisor, EsTecnicoSoporte). Siguen el mismo criterio: lo que el
   │  rol PUEDE hacer se declara como capacidad, no se adivina del nombre del rol.
   │
   │  Entran en 0 para todos los roles existentes, que es lo correcto: nadie gana nada por
   │  correr este script. Quien deba ser jefe de área o PMO se marca después en
   │  /Accesos/Roles.
   │
   ├─ Proyectos.Accion ── 20260902215902_AgregarAccionProyecto ───────────────────────────
   ├─ ExpedienteTramites.Accion ── 20260903175629_AgregarAccionTramite ───────────────────
   │
   │  La acción en curso sobre el proyecto y sobre el trámite del expediente. Nulables y
   │  sin valor por omisión: un proyecto sin acción anotada es un estado legítimo, no un
   │  dato faltante. Son de largo distinto a propósito —20 y 60— porque la del trámite
   │  describe el paso y la del proyecto es una etiqueta corta.
   │
   ├─ ProyectoInteresados.Automatico ── 20260902221131_AgregarInteresadoAutomatico ───────
   │
   │  Distingue al interesado que el portal agregó solo —porque es el responsable, el jefe
   │  del área, el PMO— del que alguien puso a mano. Importa porque los automáticos se
   │  recalculan y los manuales no se tocan.
   │
   │  Entra en 0: todos los interesados que ya existen quedan como puestos a mano, que es
   │  lo prudente. Marcarlos como automáticos los expondría a que un recálculo los borre.
   │
   └─ Usuarios.IsDeleted ── 20260904173357_AgregarBorradoLogicoUsuario ───────────────────

      Borrado lógico del usuario: dar de baja a alguien no borra la fila, la marca. Así la
      auditoría conserva quién hizo qué, y los expedientes siguen sabiendo quién fue su
      analista.

      ESTA ES LA MÁS URGENTE DE LAS SEIS. El modelo la mapea, así que si la columna no
      está, EF la pide en cada consulta de Usuarios y falla con «Invalid column name
      'IsDeleted'» — y sin consultar Usuarios no hay inicio de sesión. Una base sin esta
      columna no es una base con una función de menos: es un portal al que nadie entra.

      Entra en 0 para todos: nadie queda dado de baja por correr el script.

   Qué NO trae:

   · No borra ni modifica una sola fila. Las cuatro columnas «bit» entran en 0 y las dos
     de texto entran en NULL, así que el comportamiento de hoy no cambia por aplicarlo.

   · No toca los catálogos de prioridades ni categorías: eso es 2026-09-18_poner_al_dia.sql
     y es el que va justo después de éste.

   Como el resto de los scripts de esta carpeta, no pregunta por __EFMigrationsHistory para
   decidir qué hacer: cada paso comprueba SU PROPIA condición contra el catálogo de la base
   —¿existe la columna?—. Las filas del historial se escriben al final, así que si la
   corrida muriera a la mitad, esas filas no sirven para saber qué quedó hecho.

   Se puede correr las veces que haga falta. La segunda no hace nada.

   CÓMO CORRERLO
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-04_poner_al_dia.sql
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

/* Las cinco tablas tienen que estar: este script agrega columnas, no crea módulos. Si
   falta alguna, la base está mucho más atrasada que lo que un script de columnas arregla.

   THROW y no RAISERROR: RAISERROR deja el mensaje pero NO corta el lote, así que el script
   seguiría y moriría más abajo con un error que no le dice nada a quien lo corre. */
DECLARE @faltan nvarchar(400) = N'';

SELECT @faltan = @faltan + CASE WHEN OBJECT_ID(QUOTENAME(v.Tabla), 'U') IS NULL
                                THEN v.Tabla + N' ' ELSE N'' END
FROM (VALUES (N'Roles'), (N'Proyectos'), (N'ProyectoInteresados'),
             (N'ExpedienteTramites'), (N'Usuarios')) v(Tabla);

IF LEN(@faltan) > 0
    THROW 50000, N'Faltan tablas base. Corra 00_diagnostico.sql y revise antes de seguir.', 1;

PRINT '   las cinco tablas base están presentes';

PRINT '── 1. Roles.EsJefeDeArea y Roles.EsPmo ─────────────────────────────';

/* DEFAULT CONVERT(bit, 0) y no DEFAULT 0: es la forma exacta en que EF declara un
   «defaultValue: false», y así la columna queda idéntica a la que crea la migración.
   Sobre una tabla con filas, NOT NULL con valor por omisión las rellena con 0. */
IF COL_LENGTH(N'Roles', N'EsJefeDeArea') IS NULL
BEGIN
    ALTER TABLE [Roles] ADD [EsJefeDeArea] bit NOT NULL DEFAULT CONVERT(bit, 0);
    PRINT '   Roles.EsJefeDeArea agregada';
END
ELSE
    PRINT '   Roles.EsJefeDeArea ya estaba';

IF COL_LENGTH(N'Roles', N'EsPmo') IS NULL
BEGIN
    ALTER TABLE [Roles] ADD [EsPmo] bit NOT NULL DEFAULT CONVERT(bit, 0);
    PRINT '   Roles.EsPmo agregada';
END
ELSE
    PRINT '   Roles.EsPmo ya estaba';

PRINT '── 2. Proyectos.Accion ─────────────────────────────────────────────';

/* Nulable y sin valor por omisión: un proyecto sin acción anotada es un estado legítimo. */
IF COL_LENGTH(N'Proyectos', N'Accion') IS NULL
BEGIN
    ALTER TABLE [Proyectos] ADD [Accion] nvarchar(20) NULL;
    PRINT '   Proyectos.Accion agregada';
END
ELSE
    PRINT '   Proyectos.Accion ya estaba';

PRINT '── 3. ProyectoInteresados.Automatico ───────────────────────────────';

/* En 0: los interesados que ya existen quedan como puestos a mano. Marcarlos como
   automáticos los expondría a que un recálculo los borre. */
IF COL_LENGTH(N'ProyectoInteresados', N'Automatico') IS NULL
BEGIN
    ALTER TABLE [ProyectoInteresados] ADD [Automatico] bit NOT NULL DEFAULT CONVERT(bit, 0);
    PRINT '   ProyectoInteresados.Automatico agregada';
END
ELSE
    PRINT '   ProyectoInteresados.Automatico ya estaba';

PRINT '── 4. ExpedienteTramites.Accion ────────────────────────────────────';

IF COL_LENGTH(N'ExpedienteTramites', N'Accion') IS NULL
BEGIN
    ALTER TABLE [ExpedienteTramites] ADD [Accion] nvarchar(60) NULL;
    PRINT '   ExpedienteTramites.Accion agregada';
END
ELSE
    PRINT '   ExpedienteTramites.Accion ya estaba';

PRINT '── 5. Usuarios.IsDeleted ───────────────────────────────────────────';

/* La más urgente: sin ella, EF falla en cada consulta de Usuarios y nadie inicia sesión.
   En 0 para todos — nadie queda dado de baja por correr el script. */
IF COL_LENGTH(N'Usuarios', N'IsDeleted') IS NULL
BEGIN
    ALTER TABLE [Usuarios] ADD [IsDeleted] bit NOT NULL DEFAULT CONVERT(bit, 0);
    PRINT '   Usuarios.IsDeleted agregada';
END
ELSE
    PRINT '   Usuarios.IsDeleted ya estaba';

PRINT '── 6. Historial de migraciones ─────────────────────────────────────';

/* Las seis columnas corresponden a cinco migraciones que EF reconoce, así que se
   registran. */
IF OBJECT_ID(N'[__EFMigrationsHistory]', 'U') IS NOT NULL
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT v.Id, N'9.0.0'
    FROM   (VALUES
              (N'20260902215804_AgregarJefeDeAreaYPmo'),
              (N'20260902215902_AgregarAccionProyecto'),
              (N'20260902221131_AgregarInteresadoAutomatico'),
              (N'20260903175629_AgregarAccionTramite'),
              (N'20260904173357_AgregarBorradoLogicoUsuario')
           ) v(Id)
    WHERE  NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory h WHERE h.MigrationId = v.Id);
    PRINT '   historial al día';
END
ELSE
    PRINT '   *** no existe __EFMigrationsHistory: no se registró nada';

COMMIT;
GO

PRINT '';
PRINT '════════ Comprobación ════════';

/* Las seis columnas, con su tipo y su nulabilidad: una columna que exista pero con el tipo
   equivocado es peor que una que falte, porque el diagnóstico la daría por buena. */
SELECT  v.Control,
        CASE WHEN OBJECT_ID(QUOTENAME(v.Tabla), 'U') IS NULL     THEN 'NO EXISTE LA TABLA'
             WHEN COL_LENGTH(v.Tabla, v.Columna) IS NULL         THEN 'FALTA'
             WHEN EXISTS (SELECT 1
                          FROM sys.columns c
                          JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                          WHERE c.object_id = OBJECT_ID(QUOTENAME(v.Tabla))
                            AND c.name = v.Columna
                            AND ty.name = v.Tipo
                            AND c.max_length = v.Bytes
                            AND c.is_nullable = v.Nulable)       THEN 'presente'
             ELSE 'PRESENTE PERO DISTINTA' END AS Valor
FROM (VALUES
        (N'Roles.EsJefeDeArea',              N'Roles',               N'EsJefeDeArea', N'bit',      CAST(1 AS smallint),   CAST(0 AS bit)),
        (N'Roles.EsPmo',                     N'Roles',               N'EsPmo',        N'bit',      CAST(1 AS smallint),   CAST(0 AS bit)),
        (N'Proyectos.Accion',                N'Proyectos',           N'Accion',       N'nvarchar', CAST(40 AS smallint),  CAST(1 AS bit)),
        (N'ProyectoInteresados.Automatico',  N'ProyectoInteresados', N'Automatico',   N'bit',      CAST(1 AS smallint),   CAST(0 AS bit)),
        (N'ExpedienteTramites.Accion',       N'ExpedienteTramites',  N'Accion',       N'nvarchar', CAST(120 AS smallint), CAST(1 AS bit)),
        (N'Usuarios.IsDeleted',              N'Usuarios',            N'IsDeleted',    N'bit',      CAST(1 AS smallint),   CAST(0 AS bit))
     ) v(Control, Tabla, Columna, Tipo, Bytes, Nulable)

UNION ALL
SELECT  N'Migración ' + v.Id,
        CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = v.Id)
             THEN 'presente' ELSE 'FALTA' END
FROM (VALUES
        (N'20260902215804_AgregarJefeDeAreaYPmo'),
        (N'20260902215902_AgregarAccionProyecto'),
        (N'20260902221131_AgregarInteresadoAutomatico'),
        (N'20260903175629_AgregarAccionTramite'),
        (N'20260904173357_AgregarBorradoLogicoUsuario')
     ) v(Id);

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
PRINT 'Si algo dice PRESENTE PERO DISTINTA, esa columna existe con otro tipo: pare y avise.';
PRINT '';
PRINT 'Sigue: database\2026-09-18_poner_al_dia.sql';
GO
