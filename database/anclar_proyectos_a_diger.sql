/*
    Ancla los proyectos existentes a DIGER, que es lo que activa el filtro de alcance nuevo.

    Contexto: hasta el 2026-08-23 la entidad Proyecto no tenía filtro de alcance. Cualquiera con
    la clave `Proyectos.Ver` veía el portafolio completo, y eso incluía a los usuarios de
    instituciones externas con rol Empleado —cuatro de CONSUCOOP— que leían objetivos, bloqueos,
    bitácora y evidencia adjunta de todos los proyectos internos de DIGER.

    Se agregaron `InstitucionId` y `UnidadId` a la tabla (migración AgregarAlcanceProyecto) y el
    filtro RLS con el mismo anclaje que Expediente y Contacto. Este script pone el ancla en los
    proyectos que ya existían; sin él quedarían con InstitucionId nulo y no los vería nadie salvo
    un rol global o su propio responsable.

    Por qué DIGER y no la institución que atienden: son el portafolio interno de DIGER, que es
    quien los ejecuta. «SOL — CONSUCOOP» es un proyecto de DIGER sobre CONSUCOOP, no de CONSUCOOP;
    anclarlo allá se lo entregaría justamente a quien no debe verlo.

    Área y unidad quedan nulas a propósito: eso los hace transversales, visibles para toda la
    institución, que es como se ven hoy. Acotarlos es una decisión posterior y por proyecto, desde
    los selectores de la ficha.

    Idempotente: solo toca las filas que todavía no tienen ancla.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/anclar_proyectos_a_diger.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor nvarchar(200) = N'script-anclar-proyectos';
DECLARE @inst  nvarchar(120) = N'DIGER';

IF NOT EXISTS (SELECT 1 FROM Instituciones WHERE Id = @inst)
BEGIN
    RAISERROR(N'No existe la institución DIGER; revisar el catálogo antes de anclar.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

DECLARE @tocados TABLE (Id int, Codigo nvarchar(30));

UPDATE p
SET p.InstitucionId = @inst,
    p.UpdatedAt     = SYSUTCDATETIME(),
    p.UpdatedBy     = @actor
OUTPUT inserted.Id, inserted.Codigo INTO @tocados
FROM Proyectos p
WHERE p.InstitucionId IS NULL;

/* La auditoría del proyecto es la que no se puede tocar, así que el cambio de alcance queda
   registrado ahí aunque lo haya hecho un script y no una persona. */
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT t.Id, N'ModificacionFicha',
       N'Anclado a la institución DIGER para activar el filtro de alcance. Área y unidad quedan sin asignar: el proyecto es transversal y lo ve toda la institución.',
       @actor, SYSUTCDATETIME()
FROM @tocados t;

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT CONCAT(N'Proyectos anclados en esta corrida: ', (SELECT COUNT(*) FROM @tocados)) AS Resultado;

SELECT ISNULL(InstitucionId, N'(sin ancla)') AS Institucion,
       ISNULL(AreaId,   N'(transversal)')    AS Area,
       ISNULL(UnidadId, N'(transversal)')    AS Unidad,
       COUNT(*)                              AS Proyectos
FROM Proyectos WHERE IsDeleted = 0
GROUP BY InstitucionId, AreaId, UnidadId
ORDER BY 1, 2, 3;
