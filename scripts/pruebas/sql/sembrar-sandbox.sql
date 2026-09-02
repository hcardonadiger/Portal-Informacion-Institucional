/* =========================================================================================
   Siembra el sandbox para que HondurasSimple tenga algo que mostrar.

   POR QUE HACE FALTA
   La base real es un volcado crudo de SIGER: 1057 tramites, ninguno publicado, ninguno con
   categoria, ninguno con modalidad, costo ni tiempo. HondurasSimple solo muestra lo que
   GestionGD publica, asi que contra una copia tal cual el catalogo sale vacio y no hay nada
   que probar: ni filtros, ni ordenamientos, ni fichas.

   QUE INVENTA Y QUE NO
   - NO inventa tramites. Publica los que ya existen, con sus pasos, requisitos, lugares de
     atencion y enlaces reales.
   - SI inventa categoria, modalidad, costo y tiempo, porque en el origen todavia no estan
     capturados. Son valores de relleno para que las pantallas tengan variedad que probar.

   Dicho de otra manera: lo que se prueba con esto es el COMPORTAMIENTO de las pantallas,
   no la exactitud del dato. Un costo o un plazo que se vea en el sandbox no significa nada.

   DONDE SE PUEDE CORRER
   Solo sobre una base cuyo nombre termine en _Sandbox. Si no, se detiene sin tocar nada.

   Ejecutar con:  sqlcmd -S <instancia> -E -d <base>_Sandbox -f 65001 -i sembrar-sandbox.sql
   ========================================================================================= */

-- Obligatorias: TramitesSiger tiene la columna calculada persistida FichaCompleta y varios
-- indices filtrados. Sin esto, cualquier UPDATE sobre la tabla muere con el Msg 1934.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

-- La barrera. Va antes de todo y no hay parametro que la salte.
IF RIGHT(DB_NAME(), 8) <> '_Sandbox'
BEGIN
    RAISERROR('NEGADO: este guion solo corre sobre una base _Sandbox. Actual: %s', 16, 1, @@SERVERNAME);
    SET NOEXEC ON;
END
GO

PRINT '--- Sembrando ' + DB_NAME() + ' ---';
GO

BEGIN TRAN;

/* -----------------------------------------------------------------------------------------
   1. Categoria
   Primero por palabra clave del nombre, que es lo que mas se parece a la clasificacion real.
   Lo que no cae por palabra clave se reparte por institucion, y lo que queda se distribuye
   en circulo para que ninguna de las ocho categorias quede vacia: una categoria sin tramites
   no deja probar el filtro.
   ----------------------------------------------------------------------------------------- */
UPDATE TramitesSiger SET CategoriaId = CASE
    WHEN Nombre LIKE '%salud%'    OR Nombre LIKE '%sanitari%' OR Nombre LIKE '%medic%'
      OR Nombre LIKE '%hospital%' OR Nombre LIKE '%farmac%'   OR Nombre LIKE '%pension%'
      OR Nombre LIKE '%jubilac%'                                                       THEN 1
    WHEN Nombre LIKE '%educa%'   OR Nombre LIKE '%escolar%' OR Nombre LIKE '%universi%'
      OR Nombre LIKE '%titulo%'  OR Nombre LIKE '%docent%'  OR Nombre LIKE '%cultur%'
      OR Nombre LIKE '%beca%'                                                          THEN 2
    WHEN Nombre LIKE '%impuest%' OR Nombre LIKE '%tribut%'  OR Nombre LIKE '%aduan%'
      OR Nombre LIKE '%banc%'    OR Nombre LIKE '%financ%'  OR Nombre LIKE '%credit%'
      OR Nombre LIKE '%valores%'                                                       THEN 3
    WHEN Nombre LIKE '%identidad%'   OR Nombre LIKE '%pasaporte%' OR Nombre LIKE '%cedula%'
      OR Nombre LIKE '%naturaliza%'  OR Nombre LIKE '%migra%'     OR Nombre LIKE '%residen%'
      OR Nombre LIKE '%visa%'        OR Nombre LIKE '%antecedent%'                     THEN 4
    WHEN Nombre LIKE '%empresa%'  OR Nombre LIKE '%comerci%' OR Nombre LIKE '%socied%'
      OR Nombre LIKE '%mercantil%' OR Nombre LIKE '%patente%' OR Nombre LIKE '%marca%'
      OR Nombre LIKE '%cooperativ%'                                                    THEN 5
    WHEN Nombre LIKE '%propiedad%' OR Nombre LIKE '%catastro%' OR Nombre LIKE '%inmueble%'
      OR Nombre LIKE '%terreno%'   OR Nombre LIKE '%vivienda%' OR Nombre LIKE '%hipotec%'
      OR Nombre LIKE '%dominio%'                                                       THEN 6
    WHEN Nombre LIKE '%vehicul%'  OR Nombre LIKE '%transport%' OR Nombre LIKE '%conducir%'
      OR Nombre LIKE '%placa%'    OR Nombre LIKE '%automotor%' OR Nombre LIKE '%aerona%'
      OR Nombre LIKE '%maritim%'                                                       THEN 7
    WHEN Nombre LIKE '%ambient%' OR Nombre LIKE '%forest%'  OR Nombre LIKE '%agua%'
      OR Nombre LIKE '%mineri%'  OR Nombre LIKE '%pesca%'   OR Nombre LIKE '%agricol%'
      OR Nombre LIKE '%animal%'                                                        THEN 8
    ELSE NULL END;

-- Los que no cayeron por palabra clave: por institucion, que es la siguiente mejor pista.
UPDATE TramitesSiger SET CategoriaId = CASE InstitucionId
    WHEN 'ARSA'      THEN 1  WHEN 'SESAL'   THEN 1  WHEN 'INPREMA'   THEN 1
    WHEN 'CNBS'      THEN 3  WHEN 'SEFIN'   THEN 3
    WHEN 'SRECI'     THEN 4
    WHEN 'CONSUCOOP' THEN 5  WHEN 'CONATEL' THEN 5
    WHEN 'IP'        THEN 6
    WHEN 'IHTT'      THEN 7
    WHEN 'SENASA'    THEN 8  WHEN 'ICF'     THEN 8
    ELSE NULL END
WHERE CategoriaId IS NULL;

-- El resto, en circulo. Con Id como semilla es estable: dos siembras dan lo mismo.
UPDATE TramitesSiger SET CategoriaId = (Id % 8) + 1 WHERE CategoriaId IS NULL;

/* -----------------------------------------------------------------------------------------
   2. Modalidad, costo y tiempo
   Repartidos con el Id como semilla, asi que dos siembras dan exactamente lo mismo y una
   prueba se puede repetir.

   Se deja un resto A PROPOSITO sin modalidad, sin costo y sin tiempo. No es descuido: la
   pantalla de inicio tiene una nota que explica los que no se cuentan ni como digitales ni
   como presenciales, y las fichas muestran un guion donde el dato falta. Si se sembrara todo
   completo, esos dos comportamientos no se podrian probar nunca.
   ----------------------------------------------------------------------------------------- */
UPDATE TramitesSiger SET Modalidad = CASE (Id % 10)
    WHEN 0 THEN 'Virtual'    WHEN 1 THEN 'Virtual'
    WHEN 2 THEN 'Presencial' WHEN 3 THEN 'Presencial' WHEN 4 THEN 'Presencial'
    WHEN 5 THEN 'Presencial' WHEN 6 THEN 'Hibrido'    WHEN 7 THEN 'Hibrido'
    ELSE NULL END;

UPDATE TramitesSiger SET
    CostoEsGratuito = CASE WHEN (Id % 4) = 0 THEN CAST(1 AS bit)
                           WHEN (Id % 11) = 0 THEN NULL
                           ELSE CAST(0 AS bit) END,
    CostoTexto      = CASE WHEN (Id % 4) = 0 THEN NULL
                           WHEN (Id % 11) = 0 THEN NULL
                           ELSE CASE (Id % 7)
                                WHEN 0 THEN N'L. 50.00'    WHEN 1 THEN N'L. 100.00'
                                WHEN 2 THEN N'L. 250.00'   WHEN 3 THEN N'L. 375.00'
                                WHEN 4 THEN N'L. 600.00'   WHEN 5 THEN N'L. 1,200.00'
                                ELSE N'L. 1,875.00' END END;

UPDATE TramitesSiger SET TiempoTexto = CASE
    WHEN (Id % 9) = 0 THEN NULL
    ELSE CASE (Id % 6)
         WHEN 0 THEN N'1 día hábil'    WHEN 1 THEN N'3 días hábiles'
         WHEN 2 THEN N'5 días hábiles' WHEN 3 THEN N'10 días hábiles'
         WHEN 4 THEN N'15 días hábiles' ELSE N'1 mes' END END;

UPDATE TramitesSiger SET DisponibleEnLinea = CASE WHEN Modalidad IN ('Virtual','Hibrido') THEN 1 ELSE 0 END;

/* -----------------------------------------------------------------------------------------
   3. Publicacion
   Se publica solo lo que tiene con que llenar una ficha: institucion, pasos y requisitos. Un
   tramite publicado con la ficha vacia no prueba nada y ensucia el catalogo.

   Los lugares de atencion no se exigen: hay tramites enteramente en linea que legitimamente
   no tienen donde presentarse, y dejarlos fuera escondería justo ese caso.
   ----------------------------------------------------------------------------------------- */
UPDATE t SET t.Publicado = 1
FROM TramitesSiger t
WHERE t.InstitucionId IS NOT NULL
  AND EXISTS (SELECT 1 FROM PasosSiger      p WHERE p.TramiteSigerId = t.Id)
  AND EXISTS (SELECT 1 FROM RequisitosSiger r WHERE r.TramiteSigerId = t.Id);

/* -----------------------------------------------------------------------------------------
   4. Destacados
   La pantalla de inicio tiene dos titulos distintos: «Los mas consultados este mes» cuando
   hay medicion real, y «Tramites destacados» cuando la lista viene marcada a mano. En un
   sandbox recien sembrado no hay consultas todavia, asi que sin estas marcas la seccion no
   aparece y no se puede probar.
   ----------------------------------------------------------------------------------------- */
UPDATE TramitesSiger SET EsPopular = 0;

UPDATE t SET t.EsPopular = 1
FROM TramitesSiger t
WHERE t.Publicado = 1
  AND t.Id IN (SELECT TOP 8 Id FROM TramitesSiger
               WHERE Publicado = 1 AND TiempoTexto IS NOT NULL AND Modalidad IS NOT NULL
               ORDER BY Id);

COMMIT TRAN;
GO

PRINT '';
PRINT '--- Resultado ---';
SELECT 'Publicados'        AS Medida, COUNT(*) AS Valor FROM TramitesSiger WHERE Publicado = 1
UNION ALL SELECT 'Con categoria',  COUNT(*) FROM TramitesSiger WHERE Publicado = 1 AND CategoriaId IS NOT NULL
UNION ALL SELECT 'Virtuales',      COUNT(*) FROM TramitesSiger WHERE Publicado = 1 AND Modalidad = 'Virtual'
UNION ALL SELECT 'Presenciales',   COUNT(*) FROM TramitesSiger WHERE Publicado = 1 AND Modalidad = 'Presencial'
UNION ALL SELECT 'Hibridos',       COUNT(*) FROM TramitesSiger WHERE Publicado = 1 AND Modalidad = 'Hibrido'
UNION ALL SELECT 'Sin modalidad',  COUNT(*) FROM TramitesSiger WHERE Publicado = 1 AND Modalidad IS NULL
UNION ALL SELECT 'Gratuitos',      COUNT(*) FROM TramitesSiger WHERE Publicado = 1 AND CostoEsGratuito = 1
UNION ALL SELECT 'Destacados',     COUNT(*) FROM TramitesSiger WHERE Publicado = 1 AND EsPopular = 1
UNION ALL SELECT 'Instituciones',  COUNT(DISTINCT InstitucionId) FROM TramitesSiger WHERE Publicado = 1;
GO

SET NOEXEC OFF;
GO
