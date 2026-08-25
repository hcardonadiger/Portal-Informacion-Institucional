-- =============================================================================
-- 15 — «Ficha completa» pasa a ser una columna de la base
--      (separación de la API pública)
--
-- POR QUÉ HACE FALTA
--   Hasta ahora la API pública decidía por su cuenta si una ficha estaba
--   completa: llevaba una copia en C# de la regla de PortalDigital y otra en
--   SQL dentro de su consulta. Eso ataba los dos sistemas — agregar un campo
--   obligatorio en PortalDigital obligaba a tocar la API y a desplegarla.
--
--   Con esta columna se invierte: PORTALDIGITAL DECIDE, LA API SOLO LEE.
--
-- POR QUÉ LA CALCULA LA BASE Y NO LA APLICACIÓN
--   Una columna que alguien tiene que acordarse de recalcular al guardar es una
--   columna que tarde o temprano miente: basta un camino de escritura que la
--   olvide —una carga masiva, un UPDATE directo, una importación— para que
--   quede desfasada, y nadie lo nota hasta que el ciudadano ve una ficha a
--   medias. Calculada por SQL Server no existe ese camino: no hay forma de
--   escribirla mal porque no hay forma de escribirla.
--
-- REQUISITO: VA DESPUÉS DEL 13
--   La expresión menciona SolTramo, que agrega el script 13. Si esa columna no
--   está, este script se detiene y lo dice; no deja la base a medias.
--
-- CÓMO SE CORRE
--   El archivo va en UTF-8 con BOM y se ejecuta con -f 65001. Y hace falta
--   QUOTED_IDENTIFIER ON: SQL Server no crea columnas calculadas persistidas sin
--   él, y sqlcmd arranca con la opción APAGADA.
--
--       sqlcmd -S <servidor> -d <base> -f 65001 -i 15-ficha-completa-columna.sql
--
--   Es idempotente: correrlo dos veces no hace nada la segunda.
--
-- QUÉ NO HACE
--   No cambia ningún dato y no cambia lo que ve nadie. La columna dice
--   exactamente lo mismo que ya decía el filtro de la API; se comprobó fila por
--   fila sobre las 1 057 fichas de Ensayo antes de escribir esto.
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF COL_LENGTH('TramitesSiger', 'SolTramo') IS NULL
BEGIN
    RAISERROR(
        'Falta la columna SolTramo. Aplique primero 13-url-sol-compuesta.sql: la expresion de FichaCompleta la necesita.',
        16, 1);
    RETURN;
END

IF COL_LENGTH('TramitesSiger', 'FichaCompleta') IS NOT NULL
BEGIN
    PRINT 'FichaCompleta ya existe. Nada que hacer.';
    RETURN;
END

-- La comparación es contra NULL y no contra cadena vacía, igual que su gemela en
-- C# (FichaPublicaCompletitud.CamposFaltantes): un texto en blanco cuenta como
-- capturado. Apretar el criterio solo de un lado haría que la alerta del editor y
-- el catálogo público discreparan en silencio.
--
-- El costo se decide por CostoEsGratuito y no por CostoTexto: «es gratuito» ya es
-- una respuesta completa aunque no haya monto que escribir.
--
-- El enlace a SOL solo se exige cuando EstaEnSol, y vale cualquiera de los dos:
-- el tramo nuevo o la URL heredada de antes de la Fase 7.
ALTER TABLE TramitesSiger ADD FichaCompleta AS (
    CASE WHEN CategoriaId      IS NOT NULL
          AND Modalidad        IS NOT NULL
          AND TiempoTexto      IS NOT NULL
          AND CostoEsGratuito  IS NOT NULL
          AND (EstaEnSol = 0 OR SolUrl IS NOT NULL OR SolTramo IS NOT NULL)
         THEN CAST(1 AS bit)
         ELSE CAST(0 AS bit)
    END
) PERSISTED;

PRINT 'FichaCompleta creada.';

-- Registra la migración para que EF no intente aplicarla de nuevo.
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260825190000_ColumnaFichaCompleta')
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    SELECT '20260825190000_ColumnaFichaCompleta', MAX(ProductVersion) FROM __EFMigrationsHistory;

-- Qué quedó, para que quien lo corra lo vea sin tener que preguntar.
SELECT  'publicadas'            = COUNT(*),
        'de esas, completas'    = SUM(CASE WHEN FichaCompleta = 1 THEN 1 ELSE 0 END),
        'de esas, incompletas'  = SUM(CASE WHEN FichaCompleta = 0 THEN 1 ELSE 0 END)
FROM    TramitesSiger
WHERE   Publicado = 1;
