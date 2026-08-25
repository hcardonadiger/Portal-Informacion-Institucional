-- =============================================================================
-- 16 — Comprobar que la columna FichaCompleta dice lo que dice la regla en C#
--
-- POR QUÉ EXISTE
--   «Ficha completa» está escrita dos veces: en C#
--   (FichaPublicaCompletitud.CamposFaltantes, que le dice al técnico QUÉ falta) y
--   en SQL (la columna calculada, que es lo que lee la API pública). Las dos
--   viven en PortalDigital, pero siguen siendo dos, y dos expresiones de la misma
--   regla divergen si nadie las contrasta.
--
--   Comparar la columna consigo misma no probaría nada. Lo que hace este script
--   es correr contra la columna la MISMA TABLA DE VERDAD que fija
--   FichaPublicaCompletitudTests en C#, con los resultados esperados escritos a
--   mano. Si la expresión de la columna tuviera mal la precedencia de operadores,
--   o exigiera el enlace a SOL cuando no toca, alguna fila discreparía.
--
-- NO CAMBIA NADA
--   Todo ocurre dentro de una transacción que termina en ROLLBACK. Se puede
--   correr contra producción sin pensarlo dos veces.
--
-- CÓMO SE CORRE
--       sqlcmd -S <servidor> -d <base> -f 65001 -i 16-verificar-ficha-completa.sql
--
--   Se espera: DISCREPANCIAS = 0 y el CHECK rechazando el caso imposible.
-- =============================================================================

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;

IF COL_LENGTH('TramitesSiger', 'FichaCompleta') IS NULL
BEGIN
    RAISERROR('No existe la columna FichaCompleta. Aplique primero 15-ficha-completa-columna.sql.', 16, 1);
    RETURN;
END

DECLARE @cat int = (SELECT TOP 1 Id FROM CategoriasTramite ORDER BY Id);

BEGIN TRAN;

DECLARE @casos TABLE (
    N int IDENTITY(1,1), Caso nvarchar(80), Esperado bit,
    CategoriaId int, Modalidad nvarchar(30), TiempoTexto nvarchar(100),
    CostoEsGratuito bit, EstaEnSol bit, SolUrl nvarchar(600), SolTramo nvarchar(300));

INSERT INTO @casos (Caso,Esperado,CategoriaId,Modalidad,TiempoTexto,CostoEsGratuito,EstaEnSol,SolUrl,SolTramo) VALUES
 (N'todo capturado, no esta en SOL',        1, @cat, N'Virtual', N'3 dias', 1, 0, NULL, NULL),
 (N'con costo (gratuito=false) cuenta',     1, @cat, N'Virtual', N'3 dias', 0, 0, NULL, NULL),
 (N'texto vacio cuenta como capturado',     1, @cat, N'Virtual', N'',       1, 0, NULL, NULL),
 (N'falta categoria',                       0, NULL, N'Virtual', N'3 dias', 1, 0, NULL, NULL),
 (N'falta modalidad',                       0, @cat, NULL,       N'3 dias', 1, 0, NULL, NULL),
 (N'falta tiempo',                          0, @cat, N'Virtual', NULL,      1, 0, NULL, NULL),
 (N'falta costo',                           0, @cat, N'Virtual', N'3 dias', NULL, 0, NULL, NULL),
 (N'en SOL con tramo',                      1, @cat, N'Virtual', N'3 dias', 1, 1, NULL, N'licencia'),
 (N'en SOL con URL heredada',               1, @cat, N'Virtual', N'3 dias', 1, 1, N'https://x/y', NULL),
 (N'en SOL con las dos',                    1, @cat, N'Virtual', N'3 dias', 1, 1, N'https://x/y', N'licencia'),
 (N'en SOL, con enlace, sin modalidad',     0, @cat, NULL,       N'3 dias', 1, 1, NULL, N'licencia');

INSERT INTO TramitesSiger
    (Codigo, Nombre, Institucion, Publicado, CategoriaId, Modalidad, TiempoTexto,
     CostoEsGratuito, EstaEnSol, SolUrl, SolTramo, CreatedAt,
     DisponibleEnLinea, EnPlanDigitalizacion, EsPopular)
SELECT N'ZZ-' + CAST(N AS nvarchar(4)), Caso, N'PRUEBA TABLA DE VERDAD', 0,
       CategoriaId, Modalidad, TiempoTexto, CostoEsGratuito, EstaEnSol, SolUrl, SolTramo,
       SYSUTCDATETIME(), 0, 0, 0
FROM @casos;

SELECT c.Caso, c.Esperado AS esperado, t.FichaCompleta AS calculado,
       CASE WHEN c.Esperado = t.FichaCompleta THEN 'ok' ELSE '*** DISCREPA ***' END AS veredicto
FROM   @casos c JOIN TramitesSiger t ON t.Codigo = N'ZZ-' + CAST(c.N AS nvarchar(4))
ORDER BY c.N;

SELECT 'DISCREPANCIAS' AS resumen, COUNT(*) AS n
FROM   @casos c JOIN TramitesSiger t ON t.Codigo = N'ZZ-' + CAST(c.N AS nvarchar(4))
WHERE  c.Esperado <> t.FichaCompleta;

-- El duodécimo caso —en SOL y sin ningún enlace— la columna nunca lo verá: la base
-- lo rechaza antes, por CK_TramitesSiger_Sol. Se comprueba que efectivamente lo
-- rechace: si alguien quitara ese CHECK, esta prueba lo diría.
BEGIN TRY
    INSERT INTO TramitesSiger (Codigo,Nombre,Institucion,Publicado,CategoriaId,Modalidad,
        TiempoTexto,CostoEsGratuito,EstaEnSol,SolUrl,SolTramo,CreatedAt,
        DisponibleEnLinea,EnPlanDigitalizacion,EsPopular)
    VALUES (N'ZZ-99', N'en SOL sin enlace', N'PRUEBA TABLA DE VERDAD', 0, @cat, N'Virtual',
            N'3 dias', 1, 1, NULL, NULL, SYSUTCDATETIME(), 0, 0, 0);
    SELECT 'CK_TramitesSiger_Sol' AS chequeo, '*** NO la rechazo — revisar ***' AS resultado;
END TRY
BEGIN CATCH
    SELECT 'CK_TramitesSiger_Sol' AS chequeo, 'rechaza en-SOL-sin-enlace: ok' AS resultado;
END CATCH;

-- Y el contraste sobre los datos de verdad: la columna contra la expresion escrita
-- aparte. Cubre lo que la tabla de verdad no puede — las combinaciones reales.
SELECT 'FILAS REALES QUE DISCREPAN' AS resumen, COUNT(*) AS n
FROM   TramitesSiger
WHERE  FichaCompleta <> CASE WHEN CategoriaId IS NOT NULL AND Modalidad IS NOT NULL
                              AND TiempoTexto IS NOT NULL AND CostoEsGratuito IS NOT NULL
                              AND (EstaEnSol = 0 OR SolUrl IS NOT NULL OR SolTramo IS NOT NULL)
                        THEN CAST(1 AS bit) ELSE CAST(0 AS bit) END;

ROLLBACK;
