/*
    Completa la institución de las fichas que todavía no tienen una válida.

    Por qué hace falta
    ------------------
    Una ficha sin institución válida (TramitesSiger.InstitucionId vacía o que no existe en
    Instituciones) no llega nunca a Honduras Fácil, aunque se publique. El script 20 ya enlazó
    las que tenían por sigla el código exacto de una institución. Quedaban dos casos:

      A. La institución SÍ está registrada, pero con otro código. Las que trajo la importación se
         registraron con un código armado con el nombre (SECRETARIADEDESARROLLOECONOMICO para
         "SDE"), o con la sigla sin tilde (EDUCREDITO para "EDUCRÉDITO"). Se enlazan solas,
         comparando sin tildes ni mayúsculas por código, nombre corto o nombre.

      B. La institución NO está registrada (ADUANAS, SAR, HONDUTEL...). Se da de alta con:
           Id          = la sigla en mayúsculas, sin tildes ni espacios (MiAmbiente -> MIAMBIENTE)
           NombreCorto = la sigla tal como está en las fichas
           Nombre      = el nombre de institución que traen las fichas
         y se enlaza. Dirección, teléfono, sitio web y demás quedan vacíos: se completan después en
         Gestión Digital > Instituciones.

    No inventa nada que las fichas no digan. Lo que no se puede resolver con seguridad (fichas sin
    sigla, o con una sigla que coincide con más de una institución) se lista al final, para
    corregirlo a mano en el Editor.

    Sella UpdatedAt de cada ficha que cambia: así Honduras Fácil ve las publicadas en su ciclo
    rápido (10 s) y no espera al completo (24 h).

    ⚠  EMPIEZA EN MODO «SOLO MOSTRAR» (@SoloMostrar = 1): ejecútelo así primero y revise las dos
       listas. Para aplicar, cambie a 0 y vuelva a ejecutar.

    Cómo correrlo: en SSMS, con la base de Gestión Digital elegida en la lista desplegable de
    arriba a la izquierda (NO master). Es idempotente: la segunda vez no encuentra nada que hacer.

    Para deshacer la última corrida:
        UPDATE t SET InstitucionId = r.InstitucionIdAnterior
        FROM TramitesSiger t JOIN dbo._RespaldoInstitucionFichas r ON r.TramiteSigerId = t.Id
        WHERE r.Corrida = (SELECT MAX(Corrida) FROM dbo._RespaldoInstitucionFichas);
        DELETE i FROM Instituciones i JOIN dbo._RespaldoInstitucionesCreadas c ON c.Id = i.Id
        WHERE c.Corrida = (SELECT MAX(Corrida) FROM dbo._RespaldoInstitucionesCreadas);
*/

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SoloMostrar bit = 1;      -- 1 = solo informa (empieza así); 0 = aplica

DECLARE @BaseActual sysname = DB_NAME();
IF OBJECT_ID(N'dbo.TramitesSiger') IS NULL OR OBJECT_ID(N'dbo.Instituciones') IS NULL
BEGIN
    RAISERROR(N'Está conectado a la base «%s», que no es la de Gestión Digital (le faltan TramitesSiger o Instituciones). En SSMS, cambie la base en la lista desplegable de arriba a la izquierda y vuelva a ejecutar. No se hizo nada.', 16, 1, @BaseActual);
    RETURN;
END

DECLARE @Actor nvarchar(150) = N'script-completar-instituciones';
DECLARE @Ahora datetime2(7)  = SYSUTCDATETIME();

-- ── Las fichas pendientes, con su sigla normalizada ──────────────────────────────────────
-- Normalizar: mayúsculas, sin tildes, sin espacios, puntos ni guiones. Es la forma que tienen
-- los códigos de institución (solo A-Z y 0-9).
SELECT  t.Id,
        t.Codigo,
        t.Publicado,
        Sigla       = LTRIM(RTRIM(ISNULL(t.Sigla, N''))),
        Nombre      = LTRIM(RTRIM(ISNULL(t.Institucion, N''))),
        InstitucionIdAnterior = t.InstitucionId,
        SiglaNorm   = REPLACE(REPLACE(REPLACE(REPLACE(
                        TRANSLATE(UPPER(LTRIM(RTRIM(ISNULL(t.Sigla, N'')))), N'ÁÉÍÓÚÜÑÀÈÌÒÙ', N'AEIOUUNAEIOU'),
                        N' ', N''), N'.', N''), N'-', N''), N'_', N''),
        NombreJunto = REPLACE(REPLACE(REPLACE(LTRIM(RTRIM(ISNULL(t.Institucion, N''))), N' ', N''), N',', N''), N'.', N'')
INTO    #Pendientes
FROM    TramitesSiger t
WHERE   NOT EXISTS (SELECT 1 FROM Instituciones i WHERE i.Id = t.InstitucionId);

-- ── A. Enlazar con una institución que ya existe ────────────────────────────────────────
-- Se compara sin tildes ni mayúsculas. Solo se enlaza si hay UNA coincidencia: con dos, no se
-- adivina.
SELECT  p.Id, i.Id AS InstitucionId
INTO    #CasoA
FROM    #Pendientes p
CROSS APPLY (
    SELECT i.Id
    FROM   Instituciones i
    WHERE  p.SiglaNorm <> N'' AND (
              i.Id          COLLATE Latin1_General_CI_AI = p.SiglaNorm COLLATE Latin1_General_CI_AI
           OR i.NombreCorto COLLATE Latin1_General_CI_AI = p.Sigla     COLLATE Latin1_General_CI_AI)
       OR  p.Nombre <> N'' AND (
              i.Nombre      COLLATE Latin1_General_CI_AI = p.Nombre      COLLATE Latin1_General_CI_AI
           OR i.Id          COLLATE Latin1_General_CI_AI = p.NombreJunto COLLATE Latin1_General_CI_AI
           OR REPLACE(REPLACE(REPLACE(i.Nombre, N' ', N''), N',', N''), N'.', N'')
                            COLLATE Latin1_General_CI_AI = p.NombreJunto COLLATE Latin1_General_CI_AI)
) i
WHERE   (SELECT COUNT(DISTINCT i2.Id) FROM Instituciones i2
         WHERE  p.SiglaNorm <> N'' AND (
                   i2.Id          COLLATE Latin1_General_CI_AI = p.SiglaNorm COLLATE Latin1_General_CI_AI
                OR i2.NombreCorto COLLATE Latin1_General_CI_AI = p.Sigla     COLLATE Latin1_General_CI_AI)
            OR  p.Nombre <> N'' AND (
                   i2.Nombre      COLLATE Latin1_General_CI_AI = p.Nombre      COLLATE Latin1_General_CI_AI
                OR i2.Id          COLLATE Latin1_General_CI_AI = p.NombreJunto COLLATE Latin1_General_CI_AI
                OR REPLACE(REPLACE(REPLACE(i2.Nombre, N' ', N''), N',', N''), N'.', N'')
                                 COLLATE Latin1_General_CI_AI = p.NombreJunto COLLATE Latin1_General_CI_AI)) = 1;

-- Quitar duplicados del CROSS APPLY (la misma institución puede coincidir por dos reglas).
WITH d AS (SELECT *, ROW_NUMBER() OVER (PARTITION BY Id ORDER BY InstitucionId) AS n FROM #CasoA)
DELETE FROM d WHERE n > 1;

-- ── B. Instituciones a dar de alta ──────────────────────────────────────────────────────
-- Las pendientes que no resolvió A, con una sigla que normalizada quede en solo A-Z y 0-9.
SELECT  p.SiglaNorm AS Id,
        MAX(p.Sigla) AS NombreCorto,
        -- El nombre más repetido entre sus fichas.
        (SELECT TOP 1 p2.Nombre FROM #Pendientes p2
         WHERE p2.SiglaNorm = p.SiglaNorm AND p2.Nombre <> N''
         GROUP BY p2.Nombre ORDER BY COUNT(*) DESC, p2.Nombre) AS Nombre,
        COUNT(*) AS Fichas,
        SUM(CASE WHEN p.Publicado = 1 THEN 1 ELSE 0 END) AS Publicadas
INTO    #CasoB
FROM    #Pendientes p
WHERE   NOT EXISTS (SELECT 1 FROM #CasoA a WHERE a.Id = p.Id)
  AND   p.SiglaNorm <> N''
  AND   p.SiglaNorm NOT LIKE N'%[^A-Z0-9]%' COLLATE Latin1_General_BIN
  AND   NOT EXISTS (SELECT 1 FROM Instituciones i WHERE i.Id = p.SiglaNorm)
GROUP BY p.SiglaNorm;

DELETE FROM #CasoB WHERE Nombre IS NULL;   -- sin nombre no se registra: va a la lista final

PRINT N'--- A. Fichas que se enlazan con una institución que ya existe ---';
SELECT  sigla_en_la_ficha = p.Sigla, se_enlaza_con = a.InstitucionId,
        fichas = COUNT(*), publicadas = SUM(CASE WHEN p.Publicado = 1 THEN 1 ELSE 0 END)
FROM    #CasoA a JOIN #Pendientes p ON p.Id = a.Id
GROUP BY p.Sigla, a.InstitucionId
ORDER BY COUNT(*) DESC;

PRINT N'--- B. Instituciones que se dan de alta (revise los nombres) ---';
SELECT  codigo = Id, nombre_corto = NombreCorto, nombre = Nombre, fichas = Fichas, publicadas = Publicadas
FROM    #CasoB
ORDER BY Fichas DESC;

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
    IF OBJECT_ID(N'dbo._RespaldoInstitucionesCreadas') IS NULL
        CREATE TABLE dbo._RespaldoInstitucionesCreadas (
            Corrida datetime2(7)  NOT NULL,
            Id      nvarchar(450) NOT NULL
        );

    BEGIN TRAN;

    INSERT Instituciones (Id, Nombre, NombreCorto, Activo, CreatedAt, CreatedBy)
    SELECT Id, LEFT(Nombre, 120), LEFT(NombreCorto, 30), 1, @Ahora, @Actor
    FROM   #CasoB;

    INSERT dbo._RespaldoInstitucionesCreadas (Corrida, Id)
    SELECT @Ahora, Id FROM #CasoB;

    -- Todas las asignaciones juntas: las de A y las de B (ahora que B ya existe).
    SELECT p.Id, p.Codigo, p.InstitucionIdAnterior, n.InstitucionId
    INTO   #Asignar
    FROM   #Pendientes p
    JOIN  (SELECT Id, InstitucionId FROM #CasoA
           UNION ALL
           SELECT p.Id, b.Id FROM #Pendientes p JOIN #CasoB b ON b.Id = p.SiglaNorm
           WHERE NOT EXISTS (SELECT 1 FROM #CasoA a WHERE a.Id = p.Id)) n ON n.Id = p.Id;

    INSERT dbo._RespaldoInstitucionFichas (Corrida, TramiteSigerId, Codigo, InstitucionIdAnterior, InstitucionIdNueva)
    SELECT @Ahora, Id, Codigo, InstitucionIdAnterior, InstitucionId FROM #Asignar;

    UPDATE t
       SET InstitucionId = a.InstitucionId,
           UpdatedAt     = @Ahora,
           UpdatedBy     = @Actor
    FROM TramitesSiger t
    JOIN #Asignar a ON a.Id = t.Id;

    DECLARE @Fichas int = @@ROWCOUNT;
    DECLARE @Creadas int = (SELECT COUNT(*) FROM #CasoB);
    COMMIT TRAN;

    PRINT CONCAT(N'Hecho: ', @Creadas, N' instituciones dadas de alta y ', @Fichas,
                 N' fichas enlazadas. Respaldo: corrida ', CONVERT(nvarchar(30), @Ahora, 126), N'.');
    DROP TABLE #Asignar;
END
ELSE
    PRINT N'@SoloMostrar = 1: no se cambió nada. Si las dos listas están bien, cambie a 0 y ejecute otra vez.';

PRINT N'--- Lo que queda para corregir a mano en el Editor ---';
SELECT  t.Codigo, t.Nombre, sigla = ISNULL(NULLIF(LTRIM(RTRIM(t.Sigla)), N''), N'(vacía)'),
        institucion = t.Institucion, publicado = t.Publicado
FROM    TramitesSiger t
JOIN    #Pendientes p ON p.Id = t.Id
WHERE   NOT EXISTS (SELECT 1 FROM #CasoA a WHERE a.Id = p.Id)
  AND   NOT EXISTS (SELECT 1 FROM #CasoB b WHERE b.Id = p.SiglaNorm)
  AND   NOT EXISTS (SELECT 1 FROM Instituciones i WHERE i.Id = t.InstitucionId)
ORDER BY t.Codigo;

DROP TABLE #Pendientes; DROP TABLE #CasoA; DROP TABLE #CasoB;
