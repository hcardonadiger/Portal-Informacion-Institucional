/* ╔══════════════════════════════════════════════════════════════════╗
   ║  2026-10-12 — Recuperar los expedientes legados                  ║
   ║                                                                  ║
   ║  QUÉ TRAE: los expedientes anteriores al corte de legado         ║
   ║  (2026-04-01) con sus trámites. En la base nueva no existe       ║
   ║  ninguno: se quedaron en el respaldo de producción al crear      ║
   ║  GestionGD_new el 2026-09-22. Son 29 expedientes de 29           ║
   ║  instituciones con 188 trámites — el catálogo del levantamiento  ║
   ║  inicial (ADUANAS, ARSA, CNBS, CONATEL, IHT, INFOP, BANHPROVI…). ║
   ║                                                                  ║
   ║  Se copian TAL CUAL, con su FechaApertura de 2026-01-01. Esa     ║
   ║  fecha es un relleno de la carga masiva del 2026-08-06, no una   ║
   ║  apertura real, y así se decidió conservarla para que la base    ║
   ║  local quede idéntica a producción.                              ║
   ║                                                                  ║
   ║  NO es una migración de EF: no cambia el esquema, solo inserta   ║
   ║  datos. No se escribe fila en __EFMigrationsHistory.             ║
   ║                                                                  ║
   ║  ANTES DE CORRERLO hay que restaurar el respaldo de producción   ║
   ║  como base de apoyo. Ver 2026-10-12_recuperar_legados.md.        ║
   ║                                                                  ║
   ║  VA DESPUÉS DE: database/2026-10-12_poner_al_dia.sql             ║
   ║                                                                  ║
   ║  CÓMO CORRERLO:                                                  ║
   ║  sqlcmd -S <servidor> -d <base> -b -I -f 65001                   ║
   ║         -i 2026-10-12_recuperar_legados.sql                      ║
   ╚══════════════════════════════════════════════════════════════════╝ */

SET NOCOUNT ON;
SET XACT_ABORT ON;

/* ── Base de apoyo ────────────────────────────────────────────────────
   Cambie este nombre si restauró el respaldo con otro. */
DECLARE @Origen sysname = N'TramitesEstado_Recuperacion';

/* El corte vive en código (Application/Expedientes/Seguimiento/CorteLegado.cs).
   Si allí cambia, aquí también. */
DECLARE @Corte date = '2026-04-01';

/* ── Guardas ──────────────────────────────────────────────────────────
   Preguntan por el catálogo, no por __EFMigrationsHistory: una fila de
   historial no prueba que la tabla exista. */
IF OBJECT_ID('dbo.Expedientes', 'U') IS NULL
    OR OBJECT_ID('dbo.ExpedienteTramites', 'U') IS NULL
BEGIN
    THROW 50000, 'Faltan las tablas Expedientes o ExpedienteTramites. Corra primero database/00_diagnostico.sql.', 1;
END

IF DB_ID(@Origen) IS NULL
BEGIN
    DECLARE @msg nvarchar(400) = CONCAT(
        'No existe la base de apoyo "', @Origen,
        '". Restaure el respaldo de produccion con ese nombre antes de correr este script. Ver 2026-10-12_recuperar_legados.md.');
    THROW 50001, @msg, 1;
END

/* ── Columnas en común ────────────────────────────────────────────────
   El respaldo es del 14 de agosto y la base actual lleva migraciones
   posteriores, así que los esquemas no tienen por qué coincidir. Se
   copian solo las columnas presentes en ambos lados; las que sobran en
   destino se quedan con su valor por omisión. Hoy la única diferencia es
   ExpedienteTramites.Accion, que admite nulos.

   Va dinámico justamente por eso: una lista fija de 57 columnas se
   rompería contra un respaldo de otra fecha. Se imprime el conteo para
   que el operador vea qué se copia antes de que pase nada. */
DECLARE @colsExp nvarchar(max), @colsExpS nvarchar(max),
        @colsTram nvarchar(max), @colsTramS nvarchar(max);

DECLARE @qOrigen nvarchar(300) = QUOTENAME(@Origen);
DECLARE @sOrigen nvarchar(300) = REPLACE(@Origen, '''', '''''');

DECLARE @selExp nvarchar(max);

DECLARE @sqlCols nvarchar(max) = N'
SELECT @ce  = STRING_AGG(QUOTENAME(c.name), '', '') WITHIN GROUP (ORDER BY c.column_id),
       @ces = STRING_AGG(''s.'' + QUOTENAME(c.name), '', '') WITHIN GROUP (ORDER BY c.column_id),
       /* InstitucionId se traduce por el mapa de alias; el resto va tal cual. */
       @se  = STRING_AGG(CASE WHEN c.name = ''InstitucionId''
                              THEN ''COALESCE(a.IdLocal, e.'' + QUOTENAME(c.name) + '')''
                              ELSE ''e.'' + QUOTENAME(c.name) END, '', '')
              WITHIN GROUP (ORDER BY c.column_id)
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(''dbo.Expedientes'')
  AND c.name <> ''Id''
  AND EXISTS (SELECT 1 FROM ' + @qOrigen + N'.sys.columns o
              WHERE o.object_id = OBJECT_ID(''' + @sOrigen + N'.dbo.Expedientes'')
                AND o.name = c.name);

SELECT @ct  = STRING_AGG(QUOTENAME(c.name), '', '') WITHIN GROUP (ORDER BY c.column_id),
       @cts = STRING_AGG(''s.'' + QUOTENAME(c.name), '', '') WITHIN GROUP (ORDER BY c.column_id)
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(''dbo.ExpedienteTramites'')
  AND c.name NOT IN (''Id'', ''ExpedienteId'')
  AND EXISTS (SELECT 1 FROM ' + @qOrigen + N'.sys.columns o
              WHERE o.object_id = OBJECT_ID(''' + @sOrigen + N'.dbo.ExpedienteTramites'')
                AND o.name = c.name);';

EXEC sp_executesql @sqlCols,
     N'@ce nvarchar(max) OUTPUT, @ces nvarchar(max) OUTPUT, @se nvarchar(max) OUTPUT, @ct nvarchar(max) OUTPUT, @cts nvarchar(max) OUTPUT',
     @ce = @colsExp OUTPUT, @ces = @colsExpS OUTPUT, @se = @selExp OUTPUT,
     @ct = @colsTram OUTPUT, @cts = @colsTramS OUTPUT;

PRINT CONCAT('columnas de Expedientes a copiar       : ', LEN(@colsExp)  - LEN(REPLACE(@colsExp,  ',', '')) + 1);
PRINT CONCAT('columnas de ExpedienteTramites a copiar: ', LEN(@colsTram) - LEN(REPLACE(@colsTram, ',', '')) + 1);

/* El mapa va en tabla temporal y no en variable de tabla: una variable de
   tabla no es visible dentro de sp_executesql, y la lista de columnas
   obliga a construir el MERGE dinámicamente. */
IF OBJECT_ID('tempdb..#MapaLegados') IS NOT NULL DROP TABLE #MapaLegados;
CREATE TABLE #MapaLegados (IdOrigen int PRIMARY KEY, IdNuevo int NOT NULL);

/* ── Mapa de alias de institución ─────────────────────────────────────
   La misma institución puede existir en ambas bases con distinto Id: el
   respaldo la llama INM y la base nueva INSTITUTONACIONALDEM, con el
   mismo Nombre. Y el índice IX_Instituciones_Nombre es único, así que
   insertarla otra vez revienta.

   Se empareja por Nombre y se guarda la traducción. Los expedientes que
   apunten al Id viejo se reenrutan al que la base ya usa, en vez de
   duplicar la institución. Es el mismo criterio del importador de
   Supabase, que también desduplica instituciones por nombre. */
IF OBJECT_ID('tempdb..#AliasInst') IS NOT NULL DROP TABLE #AliasInst;
CREATE TABLE #AliasInst (IdRespaldo nvarchar(240) PRIMARY KEY, IdLocal nvarchar(240) NOT NULL);

DECLARE @sqlAlias nvarchar(max) = N'
INSERT INTO #AliasInst (IdRespaldo, IdLocal)
SELECT s.Id, x.Id
FROM ' + @qOrigen + N'.dbo.Instituciones s
JOIN dbo.Instituciones x ON x.Nombre = s.Nombre AND x.Id <> s.Id
WHERE NOT EXISTS (SELECT 1 FROM dbo.Instituciones y WHERE y.Id = s.Id);';

EXEC sp_executesql @sqlAlias;

IF EXISTS (SELECT 1 FROM #AliasInst)
    SELECT Aviso = 'institucion reenrutada por nombre', IdRespaldo, IdLocal FROM #AliasInst;

/* Las instituciones van aparte: su Id es clave natural de texto, no
   identidad, así que se copian con el mismo identificador y no hace falta
   mapear nada. */
DECLARE @colsInst nvarchar(max), @colsInstS nvarchar(max);

DECLARE @sqlColsInst nvarchar(max) = N'
SELECT @ci  = STRING_AGG(QUOTENAME(c.name), '', '') WITHIN GROUP (ORDER BY c.column_id),
       @cis = STRING_AGG(''s.'' + QUOTENAME(c.name), '', '') WITHIN GROUP (ORDER BY c.column_id)
FROM sys.columns c
WHERE c.object_id = OBJECT_ID(''dbo.Instituciones'')
  AND EXISTS (SELECT 1 FROM ' + @qOrigen + N'.sys.columns o
              WHERE o.object_id = OBJECT_ID(''' + @sOrigen + N'.dbo.Instituciones'')
                AND o.name = c.name);';

EXEC sp_executesql @sqlColsInst,
     N'@ci nvarchar(max) OUTPUT, @cis nvarchar(max) OUTPUT',
     @ci = @colsInst OUTPUT, @cis = @colsInstS OUTPUT;

BEGIN TRY
    BEGIN TRANSACTION;

    /* ── 0. Instituciones que falten ──────────────────────────────────
       Los expedientes legados apuntan a instituciones que la base nueva
       no tiene —ADUANAS, SAR, INM, STSS y otras—, y sin ellas la clave
       foránea rechaza la inserción. Se traen solo las que falten; las
       que ya existen no se tocan, para no pisar datos que alguien haya
       editado desde el portal. */
    DECLARE @sqlInst nvarchar(max) = N'
    INSERT INTO dbo.Instituciones (' + @colsInst + N')
    SELECT ' + @colsInstS + N'
    FROM ' + @qOrigen + N'.dbo.Instituciones s
    WHERE EXISTS (
        SELECT 1 FROM ' + @qOrigen + N'.dbo.Expedientes e
        WHERE e.InstitucionId = s.Id
          AND (e.FechaApertura IS NULL OR e.FechaApertura < @Corte))
      AND NOT EXISTS (SELECT 1 FROM dbo.Instituciones x WHERE x.Id = s.Id)
      /* ni por Id ni por Nombre: el indice de nombre tambien es unico */
      AND NOT EXISTS (SELECT 1 FROM dbo.Instituciones x WHERE x.Nombre = s.Nombre);';

    EXEC sp_executesql @sqlInst, N'@Corte date', @Corte = @Corte;
    DECLARE @nInst int = @@ROWCOUNT;

    /* ── 1. Expedientes ───────────────────────────────────────────────
       Idempotente por Codigo: la segunda corrida no encuentra nada que
       insertar. Esa misma regla resuelve de paso la única colisión
       conocida —EXP-IHTT-2026-01 existe en ambos lados como dos
       expedientes distintos que comparten código— sin lógica especial:
       el código ya está en destino, así que ese no entra. La
       verificación final lo deja anotado.

       MERGE y no INSERT porque hace falta OUTPUT de una columna del
       ORIGEN (el Id viejo) para poder enlazar los trámites, y eso INSERT
       no lo permite. El ON 1 = 0 nunca casa: siempre va por INSERT. */
    DECLARE @sqlExp nvarchar(max) = N'
    MERGE INTO dbo.Expedientes AS d
    USING (
        SELECT ' + @selExp + N', IdOrigen = e.Id
        FROM ' + @qOrigen + N'.dbo.Expedientes e
        LEFT JOIN #AliasInst a ON a.IdRespaldo = e.InstitucionId
        WHERE (e.FechaApertura IS NULL OR e.FechaApertura < @Corte)
          AND NOT EXISTS (SELECT 1 FROM dbo.Expedientes x WHERE x.Codigo = e.Codigo)
    ) AS s (' + @colsExp + N', IdOrigen)
    ON 1 = 0
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (' + @colsExp + N') VALUES (' + @colsExpS + N')
    OUTPUT s.IdOrigen, inserted.Id INTO #MapaLegados (IdOrigen, IdNuevo);';

    EXEC sp_executesql @sqlExp, N'@Corte date', @Corte = @Corte;

    DECLARE @nExp int = (SELECT COUNT(*) FROM #MapaLegados);

    /* ── 2. Sus trámites ──────────────────────────────────────────────
       Van por el mapa, nunca por el Id viejo: las dos tablas usan
       identidad y los números no se conservan entre bases. */
    DECLARE @sqlTram nvarchar(max) = N'
    INSERT INTO dbo.ExpedienteTramites (ExpedienteId, ' + @colsTram + N')
    SELECT m.IdNuevo, ' + @colsTramS + N'
    FROM ' + @qOrigen + N'.dbo.ExpedienteTramites s
    JOIN #MapaLegados m ON m.IdOrigen = s.ExpedienteId;';

    EXEC sp_executesql @sqlTram;

    DECLARE @nTram int = @@ROWCOUNT;

    COMMIT TRANSACTION;

    IF @nExp > 0 OR @nInst > 0
        PRINT CONCAT('recuperados: ', @nInst, ' institucion(es), ', @nExp,
                     ' expediente(s) y ', @nTram, ' tramite(s)');
    ELSE
        PRINT 'ya estaban recuperados: no se inserto ninguna fila';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH

/* ── Verificación: todo control debe leer 'presente' ──────────────────
   Un 'FALTA' significa que ese expediente del respaldo no quedó en
   destino. Para EXP-IHTT-2026-01 se espera 'omitido': su código ya
   existía en la base con otro expediente, y no se pisa nada. */
DECLARE @sqlVer nvarchar(max) = N'
SELECT
    o.Codigo,
    Tramites_origen  = (SELECT COUNT(*) FROM ' + @qOrigen + N'.dbo.ExpedienteTramites t WHERE t.ExpedienteId = o.Id),
    Tramites_destino = (SELECT COUNT(*) FROM dbo.ExpedienteTramites t JOIN dbo.Expedientes d2 ON d2.Id = t.ExpedienteId WHERE d2.Codigo = o.Codigo),
    Control = CASE
        WHEN o.Codigo = ''EXP-IHTT-2026-01'' AND EXISTS (SELECT 1 FROM dbo.Expedientes x WHERE x.Codigo = o.Codigo)
             THEN ''omitido (codigo ya usado)''
        WHEN EXISTS (SELECT 1 FROM dbo.Expedientes x WHERE x.Codigo = o.Codigo)
             THEN ''presente''
        ELSE ''FALTA'' END
FROM ' + @qOrigen + N'.dbo.Expedientes o
WHERE (o.FechaApertura IS NULL OR o.FechaApertura < @Corte)
ORDER BY o.Codigo;';

EXEC sp_executesql @sqlVer, N'@Corte date', @Corte = @Corte;

DROP TABLE #MapaLegados;
DROP TABLE #AliasInst;
