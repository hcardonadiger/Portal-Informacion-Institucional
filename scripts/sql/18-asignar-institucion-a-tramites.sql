/*
    Asigna la institución a los trámites de SIGER copiando la sigla.

    Por qué hace falta
    ------------------
    En la base unificada las 1.057 filas de TramitesSiger llegaron con
    InstitucionId en NULL. La API pública filtra y cuenta por InstitucionId, y
    HondurasÁgil pide el corte piloto (INPREMA, IHTT, CONSUCOOP) por esa misma
    columna. Mientras esté vacía, la API no devuelve nada y el portal ciudadano
    se queda sin catálogo: no está roto, está en blanco.

    Qué hace exactamente
    --------------------
    Copia TramitesSiger.Sigla a TramitesSiger.InstitucionId SOLO cuando:
      - InstitucionId está en NULL (no pisa nada ya asignado), y
      - existe una fila en Instituciones cuyo Id sea igual a esa sigla.

    Las siglas sin institución registrada se quedan en NULL a propósito: no se
    inventa un dato que la fuente no tiene. Medido el 31 de agosto de 2026:
    1.057 filas, 68 siglas distintas, 38 instituciones -> emparejan 473 filas,
    entre ellas las 57 del corte piloto (INPREMA 24, IHTT 21, CONSUCOOP 12).

    Es idempotente: correrlo dos veces no cambia nada la segunda vez.

    Para deshacerlo: UPDATE TramitesSiger SET InstitucionId = NULL
    WHERE UpdatedBy = N'script-asignar-institucion';
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Actor nvarchar(150) = N'script-asignar-institucion';
DECLARE @Ahora datetime2(7)  = SYSUTCDATETIME();

PRINT N'--- Antes ---';
SELECT
    total          = COUNT(*),
    sin_institucion= SUM(CASE WHEN t.InstitucionId IS NULL THEN 1 ELSE 0 END),
    emparejables   = SUM(CASE WHEN t.InstitucionId IS NULL AND i.Id IS NOT NULL THEN 1 ELSE 0 END)
FROM TramitesSiger t
LEFT JOIN Instituciones i ON i.Id = t.Sigla;

BEGIN TRAN;

UPDATE t
   SET InstitucionId = t.Sigla,
       UpdatedAt     = @Ahora,
       UpdatedBy     = @Actor
FROM TramitesSiger t
WHERE t.InstitucionId IS NULL
  AND EXISTS (SELECT 1 FROM Instituciones i WHERE i.Id = t.Sigla);

DECLARE @Afectadas int = @@ROWCOUNT;

COMMIT TRAN;

PRINT N'--- Después ---';
SELECT
    filas_asignadas = @Afectadas,
    total           = COUNT(*),
    con_institucion = SUM(CASE WHEN InstitucionId IS NOT NULL THEN 1 ELSE 0 END),
    sin_institucion = SUM(CASE WHEN InstitucionId IS NULL THEN 1 ELSE 0 END)
FROM TramitesSiger;

PRINT N'--- Corte piloto ---';
SELECT Sigla,
       trámites        = COUNT(*),
       con_institucion = SUM(CASE WHEN InstitucionId IS NOT NULL THEN 1 ELSE 0 END),
       publicados      = SUM(CASE WHEN Publicado = 1 THEN 1 ELSE 0 END)
FROM TramitesSiger
WHERE Sigla IN (N'INPREMA', N'IHTT', N'CONSUCOOP')
GROUP BY Sigla
ORDER BY Sigla;

PRINT N'--- Siglas que quedaron sin institución registrada ---';
SELECT Sigla, trámites = COUNT(*)
FROM TramitesSiger t
WHERE t.InstitucionId IS NULL
GROUP BY Sigla
ORDER BY COUNT(*) DESC;
