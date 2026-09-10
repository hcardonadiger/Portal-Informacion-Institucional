/*
    Alta de tres unidades del área de Gobierno Digital (GOBDIG) de DIGER:

      CIBSEG  CIBERSEGURIDAD
      PRYDIG  PROYECTOS DIGITALES Y NUEVAS TECNOLOGIAS
      INTCON  INTERCONECTIVIDAD

    Convenciones que se respetan, tomadas de las tres unidades que ya existían
    (DITRA, PRYESP, TALDIG):
      - Id de seis letras, dos sílabas de tres, en mayúsculas.
      - Nombre en MAYÚSCULAS y SIN TILDES. No es un descuido: es como están cargadas las otras
        tres, y mezclar criterios rompe el orden alfabético de los selectores y obliga a
        normalizar al comparar.

    Ambas cuelgan de GOBDIG, que es la única área de DIGER.

    Efecto en el portal: aparecen en el selector de unidad de la ficha de proyecto, y por lo
    tanto pasan a poder acotar el alcance de un proyecto. También entran en la plantilla de
    carga masiva la próxima vez que se genere (database/plantillas/).

    Idempotente: reconoce las unidades por Id, así que se puede correr más de una vez.
*/

-- QUOTED_IDENTIFIER tiene que ir encendido: varias tablas del esquema llevan índices filtrados
-- y SQL Server rechaza la escritura si la opción viene apagada, que es como la deja sqlcmd por
-- omisión. Va en su propio lote a propósito: el lote se compila entero antes de ejecutarse.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor nvarchar(200) = N'Henry Alexis Ortez Banegas';
DECLARE @hoy   datetime2     = SYSUTCDATETIME();
DECLARE @area  nvarchar(240) = N'GOBDIG';

BEGIN TRANSACTION;

-- Falla temprano y con un mensaje legible si el área no está: sin esto el INSERT moriría con
-- una violación de clave foránea, que no le dice nada a quien corre el script.
IF NOT EXISTS (SELECT 1 FROM Areas WHERE Id = @area AND InstitucionId = N'DIGER')
BEGIN
    ROLLBACK TRANSACTION;
    THROW 50000, N'No existe el área GOBDIG en DIGER: revise el catálogo de áreas antes de correr esto.', 1;
END

DECLARE @nuevas TABLE (Id nvarchar(240), Nombre nvarchar(240));
INSERT INTO @nuevas (Id, Nombre) VALUES
    (N'CIBSEG', N'CIBERSEGURIDAD'),
    (N'PRYDIG', N'PROYECTOS DIGITALES Y NUEVAS TECNOLOGIAS'),
    (N'INTCON', N'INTERCONECTIVIDAD');

INSERT INTO Unidades (Id, AreaId, Nombre, Activo, CreatedAt, CreatedBy)
SELECT n.Id, @area, n.Nombre, 1, @hoy, @actor
FROM @nuevas n
WHERE NOT EXISTS (SELECT 1 FROM Unidades u WHERE u.Id = n.Id);

-- Si ya existían, se corrige el nombre pero no se toca Activo: alguien pudo haberlas dado de
-- baja a propósito y este script no es quien para revivirlas.
UPDATE u SET
    u.Nombre    = n.Nombre,
    u.AreaId    = @area,
    u.UpdatedAt = @hoy,
    u.UpdatedBy = @actor
FROM Unidades u
JOIN @nuevas n ON n.Id = u.Id
WHERE u.Nombre <> n.Nombre OR u.AreaId <> @area;

-- ── Verificación ────────────────────────────────────────────────────────────
SELECT u.Id, u.Nombre, u.AreaId, u.Activo
FROM Unidades u
JOIN Areas a ON a.Id = u.AreaId
WHERE a.InstitucionId = N'DIGER'
ORDER BY u.Nombre;

COMMIT TRANSACTION;
PRINT 'Unidades CIBSEG, PRYDIG e INTCON listas.';
