/*
    Corrige la institución de las fichas de trámite (TramitesSiger.InstitucionId).

    Por qué hace falta
    ------------------
    La API pública reparte el catálogo por InstitucionId, y Honduras Fácil pide su corte
    (INPREMA, IHTT, CONSUCOOP) por esa misma columna. Hasta el arreglo del 14-10-2026 esa
    columna quedaba mal por dos caminos, y en ninguno se veía nada raro en pantalla:

      A. Vacía. Las 1.057 fichas importadas de SIGER llegaron así, y el Editor nunca la llenaba:
         guardaba la institución solo como texto. La ficha decía «INPREMA» y para la API no era
         de nadie. Es lo que ya cubría el script 18.

      B. Con la institución de QUIEN LA CREÓ. Una regla general de la base le pone a toda fila
         nueva la institución activa del usuario. Para un trámite eso está mal: cada ficha que
         creó alguien de DIGER en el Editor quedó como trámite de DIGER. El script 18 no la
         toca, porque solo llena las vacías.

    En los dos casos, publicar no servía: la ficha quedaba publicada y no llegaba nunca.

    Qué hace exactamente
    --------------------
    Pone InstitucionId = Sigla cuando existe una institución con ese Id y:
      A. InstitucionId está vacía, o
      B. InstitucionId es otra Y la ficha no está enlazada a un expediente. Una ficha enlazada
         recibió la llave del expediente al pasarse a SIGER, y esa es buena: no se toca.

    No inventa nada: una sigla que no es de ninguna institución registrada se queda como está,
    y se lista al final para corregirla a mano en el Editor.

    Sella UpdatedAt, a propósito: así el ciclo rápido de Honduras Fácil (cada 10 s) ve las
    fichas corregidas sin esperar al ciclo completo, que va cada 24 horas.

    Guarda el valor anterior de cada fila que cambia en dbo._RespaldoInstitucionFichas, con la
    fecha de la corrida. Para deshacer la última corrida:

        UPDATE t SET InstitucionId = r.InstitucionIdAnterior
        FROM TramitesSiger t JOIN dbo._RespaldoInstitucionFichas r ON r.TramiteSigerId = t.Id
        WHERE r.Corrida = (SELECT MAX(Corrida) FROM dbo._RespaldoInstitucionFichas);

    Es idempotente: la segunda corrida no encuentra nada que cambiar.

    Cómo correrlo
    -------------
        sqlcmd -S <servidor> -d <base> -E -C -b -f 65001 -i 20-corregir-institucion-de-fichas.sql
    Primero puede verse qué haría sin cambiar nada: cambie @SoloMostrar a 1 abajo.
*/

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SoloMostrar bit = 0;      -- 1 = solo informa, no cambia nada

IF OBJECT_ID(N'dbo.TramitesSiger') IS NULL OR OBJECT_ID(N'dbo.Instituciones') IS NULL
BEGIN
    RAISERROR(N'Esta base no parece de Gestión Digital: le faltan TramitesSiger o Instituciones. No se hizo nada.', 16, 1);
    RETURN;
END

DECLARE @Actor nvarchar(150) = N'script-corregir-institucion';
DECLARE @Ahora datetime2(7)  = SYSUTCDATETIME();

-- Qué cambiaría, fila por fila. Se calcula una vez y de ahí sale todo lo demás.
SELECT  t.Id,
        t.Codigo,
        t.Sigla,
        InstitucionIdAnterior = t.InstitucionId,
        InstitucionIdNueva    = i.Id,
        Caso = CASE WHEN t.InstitucionId IS NULL THEN 'A' ELSE 'B' END,
        t.Publicado
INTO    #Cambios
FROM    TramitesSiger t
JOIN    Instituciones i ON i.Id = LTRIM(RTRIM(t.Sigla))
WHERE   (t.InstitucionId IS NULL OR t.InstitucionId <> i.Id)
  AND   (t.InstitucionId IS NULL
         OR NOT EXISTS (SELECT 1 FROM ExpedienteTramites e WHERE e.TramiteSigerId = t.Id));

PRINT N'--- Qué se corrige ---';
SELECT  caso        = CASE Caso WHEN 'A' THEN N'A: estaba vacía' ELSE N'B: tenía la institución de quien la creó' END,
        fichas      = COUNT(*),
        publicadas  = SUM(CASE WHEN Publicado = 1 THEN 1 ELSE 0 END),
        del_corte   = SUM(CASE WHEN InstitucionIdNueva IN (N'INPREMA', N'IHTT', N'CONSUCOOP') THEN 1 ELSE 0 END)
FROM    #Cambios
GROUP BY Caso;

PRINT N'--- Publicadas del corte que van a empezar a llegar a Honduras Fácil ---';
SELECT  Codigo, Sigla, antes = ISNULL(InstitucionIdAnterior, N'(vacía)'), despues = InstitucionIdNueva
FROM    #Cambios
WHERE   Publicado = 1 AND InstitucionIdNueva IN (N'INPREMA', N'IHTT', N'CONSUCOOP')
ORDER BY Codigo;

IF @SoloMostrar = 0
BEGIN
    IF OBJECT_ID(N'dbo._RespaldoInstitucionFichas') IS NULL
        CREATE TABLE dbo._RespaldoInstitucionFichas (
            Corrida               datetime2(7)  NOT NULL,
            TramiteSigerId        int           NOT NULL,
            Codigo                nvarchar(100) NULL,
            InstitucionIdAnterior nvarchar(450) NULL,
            InstitucionIdNueva    nvarchar(450) NOT NULL
        );

    BEGIN TRAN;

    INSERT dbo._RespaldoInstitucionFichas (Corrida, TramiteSigerId, Codigo, InstitucionIdAnterior, InstitucionIdNueva)
    SELECT @Ahora, Id, Codigo, InstitucionIdAnterior, InstitucionIdNueva FROM #Cambios;

    UPDATE t
       SET InstitucionId = c.InstitucionIdNueva,
           UpdatedAt     = @Ahora,
           UpdatedBy     = @Actor
    FROM TramitesSiger t
    JOIN #Cambios c ON c.Id = t.Id;

    DECLARE @Afectadas int = @@ROWCOUNT;
    COMMIT TRAN;

    PRINT CONCAT(N'Corregidas: ', @Afectadas, N' fichas. Respaldo en dbo._RespaldoInstitucionFichas, corrida ',
                 CONVERT(nvarchar(30), @Ahora, 126), N'.');
END
ELSE
    PRINT N'@SoloMostrar = 1: no se cambió nada.';

-- Las que este script NO puede arreglar: su sigla no es de ninguna institución registrada. Se
-- excluyen las de #Cambios para que la lista diga lo mismo con @SoloMostrar en 1 que en 0.
PRINT N'--- Publicadas que se quedan sin institución: corregir la sigla en el Editor ---';
SELECT  t.Codigo, t.Nombre, sigla = ISNULL(t.Sigla, N'(vacía)')
FROM    TramitesSiger t
WHERE   t.Publicado = 1
  AND   (t.InstitucionId IS NULL OR NOT EXISTS (SELECT 1 FROM Instituciones i WHERE i.Id = t.InstitucionId))
  AND   NOT EXISTS (SELECT 1 FROM #Cambios c WHERE c.Id = t.Id)
ORDER BY t.Codigo;

DROP TABLE #Cambios;
