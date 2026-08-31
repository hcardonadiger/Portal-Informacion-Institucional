/*
================================================================================
  Pone las cinco columnas descolgadas en la intercalación de la base.
================================================================================

  EL FALLO QUE ARREGLA
  --------------------
  Tras unificar las bases quedaron cinco columnas en Modern_Spanish_CI_AI
  mientras el resto de la base está en Modern_Spanish_CI_AS. Cualquier consulta
  que compare o combine una de ellas con una columna normal revienta con:

      Cannot resolve collation conflict between "Modern_Spanish_CI_AS" and
      "Modern_Spanish_CI_AI" in CASE operator occurring in SELECT statement.

  Medido el 31-08-2026 contra una copia de la base unificada, con sesión de
  administrador. Caen tres pantallas del portal:

      /Areas                    (listado de áreas)
      /Contactos/Editor/{id}    (ficha de contacto)
      /Unidades/Editor/{id}     (ficha de unidad)

  La consulta concreta es GetAreasQuery: el `?? a.InstitucionId` de C# se
  traduce a un CASE que mezcla Instituciones.Nombre (CI_AI) con
  Areas.InstitucionId (CI_AS). No es culpa del C#: el mismo patrón funciona en
  cualquier base bien formada. El defecto está en los datos.

  POR QUÉ SE ARREGLA EL ESQUEMA Y NO LA CONSULTA
  ----------------------------------------------
  Poner un COLLATE explícito en esa consulta taparía esas tres pantallas y
  dejaría la mina puesta para la siguiente que junte esas columnas con otras.
  Son cinco columnas contra cincuenta y tantas consultas: se arregla el origen.

  ES SEGURO EN CUANTO A DATOS
  ---------------------------
  Solo cambia la intercalación, no el contenido: ni una letra de texto se toca.
  IX_Instituciones_Nombre es ÚNICO, y pasar de CI_AI (que ignora tildes) a
  CI_AS (que las distingue) solo puede volver la comparación MÁS estricta, así
  que no pueden aparecer duplicados nuevos. Aun así el guion lo comprueba antes.

  CÓMO SE USA
  -----------
      sqlcmd -S <servidor> -d <base> -E -I -i scripts\sql\19-unificar-intercalacion.sql

  El -I es obligatorio. Idempotente: si ya está aplicado, lo dice y sale.
================================================================================
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Objetivo sysname = CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS sysname);

PRINT N'--- Antes ---';
SELECT tabla = t.name, columna = c.name, intercalacion = c.collation_name
FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
WHERE c.collation_name IS NOT NULL AND c.collation_name <> @Objetivo
ORDER BY t.name, c.name;

IF NOT EXISTS (
    SELECT 1 FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
    WHERE c.collation_name = N'Modern_Spanish_CI_AI')
BEGIN
    SELECT N'No hay columnas en Modern_Spanish_CI_AI. Nada que hacer.' AS Resultado;
    RETURN;
END

/* Comprobación previa: que el índice único de Instituciones.Nombre siga siendo
   posible con la intercalación nueva. Si esto devuelve filas, PARE. */
IF EXISTS (
    SELECT 1 FROM Instituciones
    GROUP BY Nombre COLLATE Modern_Spanish_CI_AS
    HAVING COUNT(*) > 1)
BEGIN
    SELECT N'ABORTA: habría nombres de institución duplicados con la intercalación nueva.' AS Resultado;
    SELECT Nombre = Nombre COLLATE Modern_Spanish_CI_AS, repetidos = COUNT(*) FROM Instituciones
    GROUP BY Nombre COLLATE Modern_Spanish_CI_AS HAVING COUNT(*) > 1;
    RETURN;
END

BEGIN TRAN;

/* 1. Quitar los índices que dependen de esas columnas. */
DROP INDEX IF EXISTS [IX_Instituciones_Nombre]      ON [Instituciones];
DROP INDEX IF EXISTS [IX_TramitesSiger_Catalogo]    ON [TramitesSiger];
DROP INDEX IF EXISTS [IX_TramitesSiger_Institucion] ON [TramitesSiger];

/* 2. Alinear las cinco columnas. Tipo y nulabilidad se repiten tal cual estaban:
      un ALTER COLUMN que los omita los cambiaría sin avisar. */
ALTER TABLE [Instituciones] ALTER COLUMN [Nombre]      nvarchar(120)  COLLATE Modern_Spanish_CI_AS NOT NULL;
ALTER TABLE [TramitesSiger] ALTER COLUMN [Nombre]      nvarchar(600)  COLLATE Modern_Spanish_CI_AS NOT NULL;
ALTER TABLE [TramitesSiger] ALTER COLUMN [Institucion] nvarchar(200)  COLLATE Modern_Spanish_CI_AS NOT NULL;
ALTER TABLE [TramitesSiger] ALTER COLUMN [Descripcion] nvarchar(4000) COLLATE Modern_Spanish_CI_AS NULL;
ALTER TABLE [TramitesSiger] ALTER COLUMN [Objetivo]    nvarchar(4000) COLLATE Modern_Spanish_CI_AS NULL;

/* 3. Rehacer los índices exactamente como estaban. */
CREATE UNIQUE INDEX [IX_Instituciones_Nombre] ON [Instituciones] ([Nombre]);

CREATE INDEX [IX_TramitesSiger_Catalogo] ON [TramitesSiger] ([Publicado], [CategoriaId], [InstitucionId])
    INCLUDE ([Codigo], [Nombre], [Modalidad], [EsPopular], [CostoEsGratuito]);

CREATE INDEX [IX_TramitesSiger_Institucion] ON [TramitesSiger] ([Institucion]);

COMMIT TRAN;

PRINT N'--- Después ---';
SELECT descolgadas = COUNT(*)
FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
WHERE c.collation_name = N'Modern_Spanish_CI_AI';

SELECT indice = i.name, tabla = t.name, unico = i.is_unique
FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
WHERE i.name IN (N'IX_Instituciones_Nombre', N'IX_TramitesSiger_Catalogo', N'IX_TramitesSiger_Institucion')
ORDER BY i.name;

/* Prueba de humo: reproduce el CASE que reventaba. Si esto pasa, /Areas pasa. */
SELECT ensayo_case_areas = COUNT(*)
FROM Areas a
CROSS APPLY (SELECT nombre = ISNULL(
        (SELECT TOP 1 i.Nombre FROM Instituciones i WHERE i.Id = a.InstitucionId), a.InstitucionId)) x;

SELECT N'Intercalación unificada.' AS Resultado;
