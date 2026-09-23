/* ============================================================================
   Carga de 29 instituciones al catálogo — 2026-09-23

   Se puede correr las veces que haga falta: cada institución se busca primero
   por sus siglas y por su nombre completo, y sólo se crea la que no aparezca.

   El Nombre guarda las SIGLAS, que es como se pide ver el catálogo y como ya
   están las que hay. El nombre completo no se pierde: va en Descripcion.
   LogoUrl y Color quedan vacíos a propósito.

       sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-23_catalogo_instituciones.sql
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* ── Las 29 de la lista ────────────────────────────────────────────────────
   Siglas verificadas contra el sitio oficial o el Portal Único de
   Transparencia de cada institución. Donde conviven dos formas se tomó la que
   usa la propia institución: COPECO y no SEGRCN, SEP y no SDP, SEVIAH y no
   SEVAH, y HBCBH porque el nombre de ley es «Heroico y Benemérito Cuerpo de
   Bomberos de Honduras».                                                     */
DECLARE @lista TABLE (Orden int NOT NULL, Siglas nvarchar(30) NULL, NombreCompleto nvarchar(120) NOT NULL);

INSERT INTO @lista (Orden, Siglas, NombreCompleto) VALUES
 ( 1, N'IHSS',      N'Instituto Hondureño de Seguridad Social'),
 ( 2, N'COPECO',    N'Secretaría de Gestión de Riesgos y Contingencias Nacionales'),
 ( 3, N'HBCBH',     N'Cuerpo de Bomberos de Honduras'),
 ( 4, N'UNAH',      N'Universidad Nacional Autónoma de Honduras'),
 ( 5, N'UNAG',      N'Universidad Nacional de Agricultura'),
 ( 6, N'UNACIFOR',  N'Universidad Nacional de Ciencias Forestales'),
 ( 7, N'UPNFM',     N'Universidad Pedagógica Nacional Francisco Morazán'),
 ( 8, N'INFOP',     N'Instituto Nacional de Formación Profesional'),
 ( 9, N'SGJD',      N'Secretaría de Gobernación, Justicia y Descentralización'),
 (10, N'SEP',       N'Secretaría de la Presidencia'),
 (11, N'DNBE',      N'Dirección Nacional de Bienes del Estado'),
 (12, N'ENAG',      N'Empresa Nacional de Artes Gráficas'),
 (13, N'AHAC',      N'Agencia Hondureña de Aeronáutica Civil'),
 (14, N'DGMM',      N'Dirección General de la Marina Mercante'),
 (15, N'IHTT',      N'Instituto Hondureño de Transporte Terrestre'),
 (16, N'BANADESA',  N'Banco Nacional de Desarrollo Agrícola'),
 (17, N'DICTA',     N'Dirección de Ciencia y Tecnología Agropecuaria'),
 (18, N'ICF',       N'Instituto Nacional de Conservación y Desarrollo Forestal'),
 (19, N'IHMA',      N'Instituto Hondureño de Mercadeo Agrícola'),
 (20, N'INA',       N'Instituto Nacional Agrario'),
 (21, N'IPM',       N'Instituto de Previsión Militar'),
 (22, N'INPREUNAH', N'Instituto de Previsión Social de los Empleados de la Universidad Nacional Autónoma de Honduras'),
 (23, N'INJUPEMP',  N'Instituto Nacional de Jubilaciones y Pensiones de los Empleados del Poder Ejecutivo'),
 (24, N'INPREMA',   N'Instituto Nacional de Previsión del Magisterio'),
 (25, N'SEVIAH',    N'Secretaría de Vivienda y Asentamientos Humanos'),
 (26, N'SANAA',     N'Servicio Autónomo Nacional de Acueductos y Alcantarillados'),
 (27, N'SAPP',      N'Superintendencia de Alianza Público y Privada'),
 (28, N'CONSUCOOP', N'Consejo Nacional Supervisor de Cooperativas'),
 (29, N'BANHPROVI', N'Banco Hondureño para la Producción y Vivienda');

/* ── Cómo se compara ───────────────────────────────────────────────────────
   El catálogo trae los nombres escritos de todas las formas imaginables: unos
   son las puras siglas («UNAH»), otros el nombre corrido en mayúsculas y sin
   tildes, y otros el nombre con las siglas pegadas al final («INSTITUTO
   HONDUREÑO DE MERCADEO AGRICOLA IHMA»). Comparar tal cual no serviría.

   Por eso todo se lleva antes a una forma común: fuera espacios, puntos,
   comas, guiones, paréntesis, barras y comillas; la Ñ pasa a N; y la
   comparación va con una intercalación que ignora mayúsculas y tildes, así
   «Secretaría de Gestión…» y «SECRETARIA DE GESTION…» son lo mismo.

   TRANSLATE manda cada separador a una campana —CHAR(7), que no aparece en
   ningún nombre— y un solo REPLACE la borra; sale más legible que once
   REPLACE anidados.                                                          */
DECLARE @sep nvarchar(20) = REPLICATE(NCHAR(7), 11);
DECLARE @car nvarchar(20) = N' .,-_/()''"' + NCHAR(8211);   -- el último es el guion largo –

DECLARE @cat TABLE (NId nvarchar(200), NNombre nvarchar(200), NCorto nvarchar(200));
INSERT INTO @cat (NId, NNombre, NCorto)
SELECT REPLACE(REPLACE(TRANSLATE(i.Id,                      @car, @sep), NCHAR(7), N''), N'Ñ', N'N'),
       REPLACE(REPLACE(TRANSLATE(i.Nombre,                  @car, @sep), NCHAR(7), N''), N'Ñ', N'N'),
       REPLACE(REPLACE(TRANSLATE(ISNULL(i.NombreCorto, N''), @car, @sep), NCHAR(7), N''), N'Ñ', N'N')
FROM   Instituciones i;

DECLARE @pedidas TABLE (Orden int, Siglas nvarchar(30), NombreCompleto nvarchar(120),
                        NSiglas nvarchar(200), NNombre nvarchar(200));
INSERT INTO @pedidas (Orden, Siglas, NombreCompleto, NSiglas, NNombre)
SELECT l.Orden, l.Siglas, l.NombreCompleto,
       REPLACE(REPLACE(TRANSLATE(ISNULL(l.Siglas, N''), @car, @sep), NCHAR(7), N''), N'Ñ', N'N'),
       REPLACE(REPLACE(TRANSLATE(l.NombreCompleto,      @car, @sep), NCHAR(7), N''), N'Ñ', N'N')
FROM   @lista l;

/* ── Las que faltan ────────────────────────────────────────────────────────
   Una institución ya está si cualquiera de sus tres campos —llave, nombre o
   nombre corto— coincide con las siglas pedidas, con el nombre completo, o
   empieza por el nombre completo, que es el caso de las que traen las siglas
   pegadas al final.

   Nombre lleva las siglas; si alguna viniera sin ellas lleva el nombre
   completo, y entonces la llave se arma con ese mismo nombre ya normalizado,
   porque el dominio no admite espacios en la llave.                          */
DECLARE @nuevas TABLE (Id nvarchar(120), Nombre nvarchar(120), Siglas nvarchar(30), NombreCompleto nvarchar(120));

INSERT INTO @nuevas (Id, Nombre, Siglas, NombreCompleto)
SELECT LEFT(COALESCE(NULLIF(p.NSiglas, N''), p.NNombre), 120),
       LEFT(COALESCE(NULLIF(p.Siglas,  N''), p.NombreCompleto), 120),
       p.Siglas,
       p.NombreCompleto
FROM   @pedidas p
WHERE  NOT EXISTS (
           SELECT 1
           FROM   @cat c
           WHERE  (p.NSiglas <> N'' AND (c.NId     COLLATE Latin1_General_CI_AI = p.NSiglas
                                     OR  c.NNombre COLLATE Latin1_General_CI_AI = p.NSiglas
                                     OR  c.NCorto  COLLATE Latin1_General_CI_AI = p.NSiglas))
              OR   c.NId     COLLATE Latin1_General_CI_AI = p.NNombre
              OR   c.NNombre COLLATE Latin1_General_CI_AI = p.NNombre
              OR   c.NId     COLLATE Latin1_General_CI_AI LIKE p.NNombre + N'%'
              OR   c.NNombre COLLATE Latin1_General_CI_AI LIKE p.NNombre + N'%'
       );

INSERT INTO Instituciones (Id, Nombre, NombreCorto, Descripcion, Activo, CreatedAt, CreatedBy)
SELECT n.Id, n.Nombre, n.Siglas, n.NombreCompleto, 1, SYSUTCDATETIME(), N'carga-instituciones-2026-09-23'
FROM   @nuevas n;

DECLARE @creadas int = @@ROWCOUNT;

COMMIT;

/* ── Qué pasó ───────────────────────────────────────────────────────────── */
PRINT N'Creadas en esta corrida: ' + CAST(@creadas AS nvarchar(10)) + N' de 29';

SELECT  Pedida      = p.Siglas,
        Institucion = p.NombreCompleto,
        Resultado   = CASE WHEN n.Id IS NOT NULL THEN N'creada' ELSE N'ya estaba' END,
        EnCatalogo  = COALESCE(n.Id, (
                        SELECT TOP 1 i.Id
                        FROM   Instituciones i
                        CROSS APPLY (VALUES (
                            REPLACE(REPLACE(TRANSLATE(i.Id,                       @car, @sep), NCHAR(7), N''), N'Ñ', N'N'),
                            REPLACE(REPLACE(TRANSLATE(i.Nombre,                   @car, @sep), NCHAR(7), N''), N'Ñ', N'N'),
                            REPLACE(REPLACE(TRANSLATE(ISNULL(i.NombreCorto, N''), @car, @sep), NCHAR(7), N''), N'Ñ', N'N')
                        )) AS x(NId, NNombre, NCorto)
                        WHERE (p.NSiglas <> N'' AND (x.NId     COLLATE Latin1_General_CI_AI = p.NSiglas
                                                 OR  x.NNombre COLLATE Latin1_General_CI_AI = p.NSiglas
                                                 OR  x.NCorto  COLLATE Latin1_General_CI_AI = p.NSiglas))
                           OR  x.NId     COLLATE Latin1_General_CI_AI = p.NNombre
                           OR  x.NNombre COLLATE Latin1_General_CI_AI = p.NNombre
                           OR  x.NId     COLLATE Latin1_General_CI_AI LIKE p.NNombre + N'%'
                           OR  x.NNombre COLLATE Latin1_General_CI_AI LIKE p.NNombre + N'%'))
FROM      @pedidas p
LEFT JOIN @nuevas  n ON n.NombreCompleto = p.NombreCompleto
ORDER BY  p.Orden;
