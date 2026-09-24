/* ============================================================================
   Carga de la segunda lista de instituciones — 2026-09-24

   Se puede correr las veces que haga falta: cada institución se busca primero
   por sus siglas y por su nombre completo, y sólo se crea la que no aparezca.

   El Nombre guarda las SIGLAS, que es como se pide ver el catálogo y como ya
   están las que hay. El nombre completo no se pierde: va en Descripcion.
   LogoUrl y Color quedan vacíos a propósito.

       sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-24_catalogo_instituciones_segunda_lista.sql
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

/* ── Las 29 de la lista ────────────────────────────────────────────────────
   Ninguna de estas estaba en la lista del 23; son otras treinta.

   Siglas verificadas contra el sitio oficial o el Portal Único de
   Transparencia de cada institución. INJUVE y no INJ, SETUR para la Secretaría
   de Turismo —que comparte sitio con el IHT pero es otra entidad—, y PNPRRS
   como la escribe el propio programa, aunque el portal lo liste como PNRRS.

   Dos van sin siglas porque no las tienen: el Centro de la Cultura Garinagu de
   Honduras y el Programa Ciudad Mujer. A esas el Nombre les queda completo, y
   la llave se arma con ese mismo nombre sin espacios.                        */
DECLARE @lista TABLE (Orden int NOT NULL, Siglas nvarchar(30) NULL, NombreCompleto nvarchar(120) NOT NULL);

INSERT INTO @lista (Orden, Siglas, NombreCompleto) VALUES
 ( 1, NULL,          N'Centro de la Cultura Garinagu de Honduras'),
 ( 2, N'IHAH',       N'Instituto Hondureño de Antropología e Historia'),
 ( 3, N'SECAPPH',    N'Secretaría de las Culturas, las Artes y los Patrimonios de los Pueblos de Honduras'),
 ( 4, N'CICESCT',    N'Comisión Interinstitucional contra la Explotación Sexual Comercial y la Trata de Personas'),
 ( 5, N'CONAPREV',   N'Comité Nacional de Prevención contra la Tortura, Tratos Crueles, Inhumanos y Degradantes'),
 ( 6, N'INAMI',      N'Instituto Nacional para la Atención a Menores Infractores'),
 ( 7, N'SEDH',       N'Secretaría de Derechos Humanos'),
 ( 8, N'CDPC',       N'Comisión para la Defensa y la Promoción de la Competencia'),
 ( 9, N'DIDADPOL',   N'Dirección de Asuntos Disciplinarios Policiales'),
 (10, N'HONDUCOR',   N'Empresa de Correos de Honduras'),
 (11, N'INP',        N'Instituto Nacional Penitenciario'),
 (12, N'PNPRRS',     N'Programa Nacional de Prevención, Rehabilitación y Reinserción Social'),
 (13, N'INHGEOMIN',  N'Instituto Hondureño de Geología y Minas'),
 (14, N'IHADFA',     N'Instituto Hondureño para la Prevención del Alcoholismo, Drogadicción y Farmacodependencia'),
 (15, N'SENAF',      N'Secretaría de Estado en los Despachos de Niñez, Adolescencia y Familia'),
 (16, N'INJUVE',     N'Instituto Nacional de la Juventud'),
 (17, N'CENET',      N'Centro Nacional de Educación para el Trabajo'),
 (18, N'ZOLITUR',    N'Comisión Administradora Zona Libre Turística Islas de la Bahía'),
 (19, N'IHT',        N'Instituto Hondureño de Turismo'),
 (20, N'SETUR',      N'Secretaría de Turismo'),
 (21, N'CONDEPOR',   N'Comisión Nacional del Deporte, Educación Física y Recreación'),
 (22, N'CONDEPAH',   N'Confederación Deportiva Autónoma de Honduras'),
 (23, N'PANI',       N'Patronato Nacional de la Infancia'),
 (24, N'STSS',       N'Secretaría de Trabajo y Seguridad Social'),
 (25, NULL,          N'Programa Ciudad Mujer'),
 (26, N'SEMUJER',    N'Secretaría de Asuntos de la Mujer'),
 (27, N'PRONADERS',  N'Programa Nacional de Desarrollo Rural y Urbano Sostenible'),
 (28, N'BANASUPRO',  N'Suplidora Nacional de Productos Básicos'),
 (29, N'EDUCREDITO', N'Instituto de Crédito Educativo'),
 (30, N'ERSAPS',     N'Ente Regulador de Servicios de Agua Potable y Saneamiento');

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

   La llave va en mayúsculas porque el dominio no admite otra cosa: sólo letras
   mayúsculas, dígitos, guion y guion bajo. Sin el UPPER, «Programa Ciudad Mujer»
   entraba como «ProgramaCiudadMujer» y no pasaba esa validación al editarla.

   Nombre lleva las siglas; si alguna viniera sin ellas lleva el nombre
   completo, y entonces la llave se arma con ese mismo nombre ya normalizado,
   porque el dominio no admite espacios en la llave.                          */
DECLARE @nuevas TABLE (Id nvarchar(120), Nombre nvarchar(120), Siglas nvarchar(30), NombreCompleto nvarchar(120));

INSERT INTO @nuevas (Id, Nombre, Siglas, NombreCompleto)
SELECT LEFT(UPPER(COALESCE(NULLIF(p.NSiglas, N''), p.NNombre)), 120),
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
SELECT n.Id, n.Nombre, n.Siglas, n.NombreCompleto, 1, SYSUTCDATETIME(), N'carga-instituciones-2026-09-24'
FROM   @nuevas n;

DECLARE @creadas int = @@ROWCOUNT;

COMMIT;

/* ── Qué pasó ───────────────────────────────────────────────────────────── */
PRINT N'Creadas en esta corrida: ' + CAST(@creadas AS nvarchar(10)) + N' de 30';

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
