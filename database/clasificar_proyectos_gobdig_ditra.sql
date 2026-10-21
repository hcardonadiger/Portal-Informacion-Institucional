/*
    Clasifica el portafolio dentro del alcance de DIGER y completa la asignación de los jefes de
    área que quedaron sin área.

    Contexto: hasta hoy 23 de los 25 proyectos tenían AreaId y UnidadId nulos, es decir eran
    «transversales» y los veía cualquiera de DIGER. Como todos son de gobierno digital y
    digitalización de trámites, pasan a GOBDIG / DITRA.

      1. Proyectos       -> AreaId = GOBDIG, UnidadId = DITRA  (los 25)
      2. Jefes de área   -> AreaId = GOBDIG  (César Maldonado, Dennis Vasquez, Mario Castejon)

    **Esto cambia quién ve qué, no es cosmético.** Con el filtro de alcance vigente, a partir de
    acá el portafolio lo ven: quienes estén en la unidad DITRA, quienes tengan nivel Área sobre
    GOBDIG, el alcance global, y —proyecto por proyecto— su responsable y sus interesados.

    Quedan FUERA del portafolio por ámbito los usuarios de DIGER de nivel Unidad que no están en
    DITRA ni tienen unidad asignada: Alejandra Elvir, Ana Maradiaga, Carlos Zelaya, Consultor
    DIGER, Jefe DIGER, Jefe TIC y Empleado DEV (que está en PRYESP). Siguen viendo los proyectos
    donde sean responsables o interesados. Para devolverles el portafolio hay que asignarles la
    unidad DITRA desde /Accesos, que es una decisión de organización, no de este script.

    Los usuarios de CONSUCOOP ya no veían proyectos de DIGER desde el anclaje del 2026-08-23: los
    corta la institución, no la unidad.

    Idempotente: solo toca las filas que difieren, y solo audita esas.
*/

-- QUOTED_IDENTIFIER tiene que ir encendido: Proyectos.Codigo lleva un índice único filtrado y
-- SQL Server rechaza la escritura si viene apagada, que es como la deja sqlcmd por omisión.
-- Va en su propio lote a propósito: el lote se compila entero antes de ejecutarse.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor  nvarchar(200) = N'Henry Alexis Ortez Banegas';
DECLARE @ahora  datetime2     = SYSUTCDATETIME();
DECLARE @area   nvarchar(240) = N'GOBDIG';
DECLARE @unidad nvarchar(240) = N'DITRA';

BEGIN TRANSACTION;

-- Falla temprano y con un mensaje legible si el catálogo no está como se espera.
IF NOT EXISTS (SELECT 1 FROM Areas WHERE Id = @area AND InstitucionId = N'DIGER')
   OR NOT EXISTS (SELECT 1 FROM Unidades WHERE Id = @unidad AND AreaId = @area)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50000, N'Falta el área GOBDIG o la unidad DITRA en el catálogo.', 1;
END

-- ── 1. Proyectos ────────────────────────────────────────────────────────────
-- Se apuntan primero los que cambian, para poder auditar exactamente esos.
DECLARE @cambiados TABLE (Id int PRIMARY KEY, Codigo nvarchar(60), AreaVieja nvarchar(240), UnidadVieja nvarchar(240));

INSERT INTO @cambiados (Id, Codigo, AreaVieja, UnidadVieja)
SELECT p.Id, p.Codigo, p.AreaId, p.UnidadId
FROM Proyectos p
WHERE p.IsDeleted = 0
  AND (ISNULL(p.AreaId, N'') <> @area OR ISNULL(p.UnidadId, N'') <> @unidad);

UPDATE p SET
    p.AreaId    = @area,
    p.UnidadId  = @unidad,
    p.UpdatedAt = @ahora,
    p.UpdatedBy = @actor
FROM Proyectos p
JOIN @cambiados c ON c.Id = p.Id;

-- El cambio de ámbito es un cambio de acceso: tiene que quedar en la bitácora del proyecto.
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT c.Id,
       N'ModificacionFicha',
       N'Alcance: área ' + ISNULL(c.AreaVieja, N'(ninguna)') + N' → ' + @area
         + N', unidad ' + ISNULL(c.UnidadVieja, N'(ninguna)') + N' → ' + @unidad
         + N'. Deja de ser transversal a la institución: ahora lo ven la unidad DITRA, el nivel '
         + N'área sobre GOBDIG, el alcance global, y su responsable e interesados.',
       @actor,
       @ahora
FROM @cambiados c;

-- ── 2. Jefes de área sin área ───────────────────────────────────────────────
-- Su rol es de nivel Área pero la asignación quedó a medias al crearlos, así que sin esto no
-- verían ningún proyecto en cuanto los proyectos dejaran de ser transversales.
DECLARE @jefes TABLE (UsuarioId uniqueidentifier, Nombre nvarchar(400));

INSERT INTO @jefes (UsuarioId, Nombre)
SELECT u.Id, u.Nombre
FROM Usuarios u
JOIN AsignacionesUsuario a ON a.UsuarioId = u.Id
JOIN Roles r ON r.Id = a.Rol
WHERE u.Activo = 1
  AND a.InstitucionId = N'DIGER'
  AND r.NivelAlcance = N'Area'
  AND a.AreaId IS NULL;

UPDATE a SET a.AreaId = @area
FROM AsignacionesUsuario a
JOIN @jefes j ON j.UsuarioId = a.UsuarioId
WHERE a.InstitucionId = N'DIGER' AND a.AreaId IS NULL;

-- ── Verificación ────────────────────────────────────────────────────────────
SELECT N'proyectos reclasificados' AS Concepto, COUNT(*) AS Cantidad FROM @cambiados
UNION ALL
SELECT N'jefes de área completados', COUNT(*) FROM @jefes;

SELECT p.AreaId, p.UnidadId, COUNT(*) AS Proyectos
FROM Proyectos p WHERE p.IsDeleted = 0
GROUP BY p.AreaId, p.UnidadId;

COMMIT TRANSACTION;
PRINT 'Clasificación aplicada.';
