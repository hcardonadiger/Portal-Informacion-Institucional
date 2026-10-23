/* =====================================================================================
   Poner al día la base con los cambios al 2026-10-22 — lo que trajo la rama Jamil
   -------------------------------------------------------------------------------------
   Va DESPUÉS de 2026-10-19_poner_al_dia.sql. Corra antes 00_diagnostico.sql: él dice si
   esta base está en condiciones de recibir éste.

   EL FALLO QUE ARREGLA

   La fusión de Jamil en ike_opt (commit b1385e7, 2026-10-20) trajo diez migraciones de
   EF fechadas entre el 21 y el 27 de agosto. Sus fechas son ANTERIORES a migraciones que
   las bases ya tienen, así que ningún «poner al día» posterior las incluyó, y las bases
   que no venían de Jamil se quedaron sin ellas. El modelo sí las mapea: la primera
   consulta que lee Instituciones revienta con

       Invalid column name 'RutaSol'.

   y con ella caen «Nueva reunión», el editor de instituciones y cualquier pantalla que
   cargue el catálogo de instituciones.

   QUÉ TRAE — diez migraciones de EF, todas reales:

     20260821210115_SigerIdOpcional                 ┐
     20260824173317_ArchivoSigerOriginal            ├ scripts/sql/11-esquema-siger-fase1-a-fase3.sql
     20260824192857_ConciliacionConClaveEstable     ┘
     20260824222016_ColaLlenadoAsistido             ─ scripts/sql/12-cola-llenado-asistido.sql
     20260825160050_UrlSolCompuesta                 ─ scripts/sql/13-url-sol-compuesta.sql
                                                      (Instituciones.RutaSol: el error de arriba)
     20260825165843_FichaCompletaEnTramiteExpediente┐
     20260825170007_ConvertirModalidadesExistentes  ├ scripts/sql/14-ficha-completa-en-expediente.sql
     20260825170158_SembrarEntregablesYLugares      ┘
     20260825190000_ColumnaFichaCompleta            ─ scripts/sql/15-ficha-completa-columna.sql
     20260827161837_FusionRamaDev                   ─ vacía a propósito; solo se registra aquí

   Este archivo NO reescribe esas migraciones: las encadena con :r. Los scripts 11 a 15
   los escribió y los midió la rama Jamil contra Ensayo, y copiarlos aquí sería tener dos
   versiones del mismo cambio esperando a divergir.

   ESTE SÍ TOCA DATOS (los toca el 11 y el 14, no este archivo):
     · 11 rellena la clave estable de las conciliaciones y BORRA las que apunten a un
       trámite de expediente que ya no existe.
     · 14 copia cada Modalidad de texto libre a ModalidadDetalle, la reduce al catálogo
       Virtual / Presencial / Hibrido, y siembra entregables y lugares del expediente.
   Ninguna otra fila se borra. RESPALDE ANTES.

   POR QUÉ HAY UNA GUARDA DE COHERENCIA, Y NO SOLO EL «YA ESTABA»

   Los scripts 11 a 15 son los idempotentes que genera EF: deciden por la fila de
   __EFMigrationsHistory, no por el catálogo. En una base sin nada de Jamil eso es
   correcto. En una base donde alguien aplicó un pedazo a mano, sin escribir el
   historial, volverían a crear lo que ya existe y morirían con «objeto duplicado» a
   mitad de camino. Por eso el paso 1 mira el CATÁLOGO de cada migración contra su fila
   del historial y, si no cuadran, se niega a empezar y dice cuál. Esa base hay que
   conciliarla a mano: adivinar qué parte de una migración llegó no es trabajo de un
   script.

   CÓMO CORRERLO — desde la RAÍZ del repositorio, porque :r resuelve las rutas contra la
   carpeta desde la que se llama a sqlcmd, no contra la de este archivo:
     sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i database\2026-10-22_poner_al_dia.sql
   El -b no es opcional: es lo que hace que un fallo en el 11 no deje correr el 12.
   ===================================================================================== */

SET NOCOUNT ON;
SET XACT_ABORT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

/* ── Guarda: ¿es ésta la base correcta? ──────────────────────────────────────────── */
IF OBJECT_ID(N'[Instituciones]', N'U') IS NULL
   OR OBJECT_ID(N'[TramitesSiger]', N'U') IS NULL
   OR OBJECT_ID(N'[ExpedienteTramites]', N'U') IS NULL
   OR OBJECT_ID(N'[ConciliacionesSiger]', N'U') IS NULL
   OR OBJECT_ID(N'[__EFMigrationsHistory]', N'U') IS NULL
BEGIN
    THROW 50000,
        'Faltan Instituciones, TramitesSiger, ExpedienteTramites, ConciliacionesSiger o __EFMigrationsHistory. Corra 00_diagnostico.sql: no es la base del portal o está incompleta.',
        1;
END
GO

PRINT '════════ 1. Coherencia entre catálogo e historial ════════';
GO

/* Cada migración con su huella en el catálogo: el último objeto que deja, que solo
   existe si la migración llegó hasta el final. SembrarEntregablesYLugares no tiene
   huella de esquema —solo inserta filas— y se juzga junto con la 14 que la contiene. */
DECLARE @Huellas TABLE (MigrationId nvarchar(150), Presente bit);
INSERT INTO @Huellas VALUES
 (N'20260821210115_SigerIdOpcional',
    CASE WHEN COLUMNPROPERTY(OBJECT_ID(N'[TramitesSiger]'), N'IdSiger', 'AllowsNull') = 1 THEN 1 ELSE 0 END),
 (N'20260824173317_ArchivoSigerOriginal',
    CASE WHEN OBJECT_ID(N'[FotosTramiteSiger]', N'U') IS NOT NULL THEN 1 ELSE 0 END),
 (N'20260824192857_ConciliacionConClaveEstable',
    CASE WHEN COL_LENGTH(N'ExpedienteTramites', N'ClaveEstable') IS NOT NULL
          AND COL_LENGTH(N'ConciliacionesSiger', N'ExpedienteTramiteId') IS NULL THEN 1 ELSE 0 END),
 (N'20260824222016_ColaLlenadoAsistido',
    CASE WHEN OBJECT_ID(N'[PropuestasLlenado]', N'U') IS NOT NULL THEN 1 ELSE 0 END),
 (N'20260825160050_UrlSolCompuesta',
    CASE WHEN COL_LENGTH(N'Instituciones', N'RutaSol') IS NOT NULL
          AND COL_LENGTH(N'TramitesSiger', N'SolTramo') IS NOT NULL THEN 1 ELSE 0 END),
 (N'20260825165843_FichaCompletaEnTramiteExpediente',
    CASE WHEN OBJECT_ID(N'[ExpedienteTramiteLugares]', N'U') IS NOT NULL
          AND COL_LENGTH(N'ExpedienteTramites', N'ModalidadDetalle') IS NOT NULL THEN 1 ELSE 0 END),
 (N'20260825170007_ConvertirModalidadesExistentes',
    CASE WHEN OBJECT_ID(N'[CK_ExpedienteTramites_Modalidad]', N'C') IS NOT NULL THEN 1 ELSE 0 END),
 (N'20260825190000_ColumnaFichaCompleta',
    CASE WHEN COL_LENGTH(N'TramitesSiger', N'FichaCompleta') IS NOT NULL THEN 1 ELSE 0 END);

DECLARE @Descuadres nvarchar(max) =
    (SELECT STRING_AGG(
                h.MigrationId + CASE WHEN h.Presente = 1 THEN N' (está en la base y no en el historial)'
                                                         ELSE N' (está en el historial y no en la base)' END,
                N'; ')
       FROM @Huellas h
      WHERE h.Presente <> CASE WHEN EXISTS (SELECT 1 FROM __EFMigrationsHistory m
                                            WHERE m.MigrationId = h.MigrationId) THEN 1 ELSE 0 END);

IF @Descuadres IS NOT NULL
BEGIN
    DECLARE @Msg nvarchar(2048) =
        N'Catálogo e historial no cuadran — concilie a mano antes de seguir: ' + @Descuadres;
    THROW 50001, @Msg, 1;
END

PRINT '  catálogo e historial .......... cuadran';
GO

PRINT '';
PRINT '════════ 2. Esquema SIGER, fases 1 a 3 (script 11) ════════';
GO
:r scripts\sql\11-esquema-siger-fase1-a-fase3.sql
GO

PRINT '';
PRINT '════════ 3. Cola de llenado asistido (script 12) ════════';
GO
:r scripts\sql\12-cola-llenado-asistido.sql
GO

PRINT '';
PRINT '════════ 4. URL de SOL compuesta — Instituciones.RutaSol (script 13) ════════';
GO
:r scripts\sql\13-url-sol-compuesta.sql
GO

PRINT '';
PRINT '════════ 5. Ficha completa en el expediente (script 14) ════════';
GO
:r scripts\sql\14-ficha-completa-en-expediente.sql
GO

PRINT '';
PRINT '════════ 6. Columna FichaCompleta (script 15) ════════';
GO
:r scripts\sql\15-ficha-completa-columna.sql
GO

/* ── 7. FusionRamaDev ────────────────────────────────────────────────────────────────
   Su Up está vacío: existe solo para guardar el snapshot unificado de dev + Jamil. Se
   registra al final y solo si las nueve anteriores están, porque registrarla con la
   base a medias haría creer a `dotnet ef` que no queda nada pendiente. */
PRINT '';
PRINT '════════ 7. Historial ════════';
IF (SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId IN (
        N'20260821210115_SigerIdOpcional',
        N'20260824173317_ArchivoSigerOriginal',
        N'20260824192857_ConciliacionConClaveEstable',
        N'20260824222016_ColaLlenadoAsistido',
        N'20260825160050_UrlSolCompuesta',
        N'20260825165843_FichaCompletaEnTramiteExpediente',
        N'20260825170007_ConvertirModalidadesExistentes',
        N'20260825170158_SembrarEntregablesYLugares',
        N'20260825190000_ColumnaFichaCompleta')) = 9
   AND NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory
                   WHERE MigrationId = N'20260827161837_FusionRamaDev')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES (N'20260827161837_FusionRamaDev', N'9.0.0');
    PRINT '  FusionRamaDev ................. registrada';
END
ELSE PRINT '  FusionRamaDev ................. ya estaba (o faltan las anteriores: ver abajo)';
GO

PRINT '';
PRINT '════════ Comprobación ════════';

SELECT  N'Instituciones.RutaSol (el error de «Nueva reunión»)' AS Control,
        CASE WHEN COL_LENGTH(N'Instituciones', N'RutaSol') IS NULL THEN 'FALTA' ELSE 'presente' END AS Valor
UNION ALL
SELECT  N'TramitesSiger.SolTramo',
        CASE WHEN COL_LENGTH(N'TramitesSiger', N'SolTramo') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'TramitesSiger.IdSiger admite nulos',
        CASE WHEN COLUMNPROPERTY(OBJECT_ID(N'[TramitesSiger]'), N'IdSiger', 'AllowsNull') = 1
             THEN 'presente' ELSE 'FALTA' END
UNION ALL
SELECT  N'Tabla FotosTramiteSiger',
        CASE WHEN OBJECT_ID(N'[FotosTramiteSiger]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'ExpedienteTramites.ClaveEstable',
        CASE WHEN COL_LENGTH(N'ExpedienteTramites', N'ClaveEstable') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'Tabla PropuestasLlenado',
        CASE WHEN OBJECT_ID(N'[PropuestasLlenado]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'Tabla ExpedienteTramiteEntregables',
        CASE WHEN OBJECT_ID(N'[ExpedienteTramiteEntregables]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'Tabla ExpedienteTramiteLugares',
        CASE WHEN OBJECT_ID(N'[ExpedienteTramiteLugares]', N'U') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'CK_ExpedienteTramites_Modalidad',
        CASE WHEN OBJECT_ID(N'[CK_ExpedienteTramites_Modalidad]', N'C') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'TramitesSiger.FichaCompleta',
        CASE WHEN COL_LENGTH(N'TramitesSiger', N'FichaCompleta') IS NULL THEN 'FALTA' ELSE 'presente' END
UNION ALL
SELECT  N'Las diez migraciones en el historial',
        CASE WHEN (SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId IN (
                     N'20260821210115_SigerIdOpcional',
                     N'20260824173317_ArchivoSigerOriginal',
                     N'20260824192857_ConciliacionConClaveEstable',
                     N'20260824222016_ColaLlenadoAsistido',
                     N'20260825160050_UrlSolCompuesta',
                     N'20260825165843_FichaCompletaEnTramiteExpediente',
                     N'20260825170007_ConvertirModalidadesExistentes',
                     N'20260825170158_SembrarEntregablesYLugares',
                     N'20260825190000_ColumnaFichaCompleta',
                     N'20260827161837_FusionRamaDev')) = 10
             THEN 'presente' ELSE 'FALTA' END;

PRINT '';
PRINT 'Todo debe decir «presente». Si algo dice FALTA, la corrida no terminó.';
GO
