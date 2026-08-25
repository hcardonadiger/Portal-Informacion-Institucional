/*
    Dos cosas:

    1. Actualiza PRY-2026-01 «Mesa técnica de simplificación» con las dos ayudas memoria del
       frente de simplificación regulatoria con IA: la reunión con el BID del 2026-07-09 y la
       mesa técnica DIGER–CNI. Pasa a ejecución —tenía cuatro hitos cumplidos y seguía marcada
       como planificada— y suma los hitos que las actas evidencian.

       Los hitos que ya existían NO se tocan: son los que escribió su responsable, con su propia
       redacción y sus fechas. Solo se agregan los nuevos al final. Tampoco se cambia el 10 %
       que fijó el responsable: la entrada de bitácora más reciente se registra con ese mismo
       valor para no contradecirlo.

       Fecha de la mesa DIGER–CNI: el acta viene con la fecha en blanco. Se ubica en el
       2026-07-15 porque el acta del BID convocó a la siguiente coordinación para el miércoles
       15 de julio, y esta acta cita esa reunión como antecedente y convoca a la próxima para
       el miércoles 29. Es una inferencia y queda dicha en el texto de la entrada.

    2. Da de alta el Portal Nacional de Datos Abiertos, con el estado que refleja su ayuda
       memoria del 2026-03-13. **El acta tiene cinco meses**: lo que aquí queda es la foto de
       marzo, no el estado de hoy. El proyecto va a aparecer en el tablero como «sin reportar»,
       que es exactamente lo que corresponde hasta que alguien lo actualice.

    Idempotente: reconoce el proyecto por Nombre, los hitos por (Proyecto, Nombre) y los
    avances por su descripción.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/mesa_simplificacion_y_datos_abiertos.sql
*/
-- QUOTED_IDENTIFIER tiene que ir encendido: Proyectos.Codigo lleva un indice unico filtrado
-- (WHERE IsDeleted = 0) y SQL Server rechaza el INSERT si la opcion viene apagada, que es
-- justo como la deja sqlcmd por omision. SSMS la enciende sola, y por eso el problema solo
-- aparece al correr por linea de comandos. Va en su propio lote a proposito.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor    nvarchar(200)    = N'Henry Alexis Ortez Banegas';
DECLARE @hoy      datetime2        = SYSUTCDATETIME();
DECLARE @henry    uniqueidentifier = 'FF56AE4F-AB09-41A6-BCE3-F954E5E7DAAF';
DECLARE @henryNom nvarchar(200)    = N'Henry Ortez';

DECLARE @mesa int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-01' AND IsDeleted = 0);
IF @mesa IS NULL BEGIN RAISERROR(N'No se encontró PRY-2026-01.', 16, 1); RETURN; END

BEGIN TRANSACTION;

-- ════════ 1. Mesa técnica de simplificación ═════════════════════════════════
/* Tenía cuatro hitos cumplidos y seguía en «Planificado». */
UPDATE Proyectos
SET Estado          = N'EnEjecucion',
    FechaInicioReal = ISNULL(FechaInicioReal, FechaInicioPlan),
    UpdatedAt       = @hoy,
    UpdatedBy       = @actor
WHERE Id = @mesa AND Estado = N'Planificado';

/* Hitos nuevos, al final y sin tocar los existentes. */
DECLARE @hm TABLE (Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30));

INSERT INTO @hm VALUES
(N'Carga de fichas institucionales en el portal consolidado',
 N'Cada institución llena las dos fichas que entregó el CNI —radiografía de infraestructura, y línea base y flujo de trámites— y las sube al portal consolidado de la DIGER.',
 N'EnProceso'),
(N'Definición de los sectores finales por el CNI',
 N'El CNI define la priorización definitiva. Propuesta del CNI: infraestructura, turismo y agricultura. Sectores identificados en la mesa: agroindustria, infraestructura, logística y transporte, y energía.',
 N'EnProceso'),
(N'Integración de las fichas faltantes',
 N'Completar el inventario con las fichas que no se han recibido, para priorizar procesos sobre la base de los sectores ya priorizados.',
 N'Pendiente'),
(N'Modelo de acceso a la herramienta del BID',
 N'Definir con el BID si el acceso a la herramienta de inteligencia regulatoria se otorga mediante un préstamo o si el Banco la provee directamente al Gobierno. Quedó explícitamente pendiente de consulta.',
 N'Pendiente'),
(N'Definición de la lista de temas y subsectores de Honduras',
 N'Acotar, en la reunión de ministros, los temas y subsectores con los que el país participa del ejercicio regional de análisis del stock regulatorio.',
 N'EnProceso');

/* Los hitos que ya existen se actualizan; sin este UPDATE el script solo servía la
   primera vez y toda corrección posterior de descripción o estado quedaba sin aplicar. */
UPDATE hp
SET hp.Descripcion = h.Descripcion,
    hp.Estado      = h.Estado
FROM ProyectoHitos hp
JOIN @hm h ON h.Nombre = hp.Nombre
WHERE hp.ProyectoId = @mesa;

INSERT INTO ProyectoHitos (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT @mesa,
       (SELECT ISNULL(MAX(Orden), 0) FROM ProyectoHitos WHERE ProyectoId = @mesa)
         + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       h.Nombre, h.Descripcion, NULL, NULL, h.Estado, NULL, NULL
FROM @hm h
WHERE NOT EXISTS (SELECT 1 FROM ProyectoHitos x WHERE x.ProyectoId = @mesa AND x.Nombre = h.Nombre);

-- ════════ 2. Portal Nacional de Datos Abiertos ══════════════════════════════
DECLARE @nombreDA nvarchar(300) = N'Portal Nacional de Datos Abiertos';

DECLARE @base int = (
    SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(Codigo, 10, 10) AS int)), 0)
    FROM Proyectos WHERE Codigo LIKE N'PRY-2026-%'
);

IF NOT EXISTS (SELECT 1 FROM Proyectos WHERE Nombre = @nombreDA AND IsDeleted = 0)
BEGIN
    INSERT INTO Proyectos
        (IsDeleted, Codigo, Nombre, Objetivo, AreaId, ResponsableId, Responsable, Prioridad, Estado,
         FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, CreatedAt, CreatedBy)
    VALUES
        (0, N'PRY-2026-' + FORMAT(@base + 1, '00'), @nombreDA,
         N'Sostener y expandir el Portal Nacional de Datos Abiertos: publicación de conjuntos de datos bajo estándares de interoperabilidad, formalización y capacitación de los equipos institucionales, y aprobación de la Política Nacional de Datos Abiertos que le da sustento normativo.',
         NULL, @henry, @henryNom, N'Alta', N'EnEjecucion',
         NULL,   -- el portal es anterior al acta; no hay fecha de inicio registrada
         NULL,
         NULL,
         NULL, 55, @hoy, @actor);
END

DECLARE @da int = (SELECT Id FROM Proyectos WHERE Nombre = @nombreDA AND IsDeleted = 0);

DECLARE @hd TABLE (Orden int, Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30));

INSERT INTO @hd VALUES
(1, N'Desarrollo del Portal Nacional de Datos Abiertos',
    N'Portal conceptualizado y desarrollado por la DIGER, con acompañamiento técnico y consultivo de la Organización de los Estados Americanos, aplicando estándares internacionales de interoperabilidad y usabilidad.',
    N'Completado'),
(2, N'Infraestructura y dominio institucional',
    N'Data Center centralizado consolidado, con el equipamiento de cómputo y almacenamiento desplegado en las instalaciones de la ex-STLCC, y dominio institucional asegurado por seis años.',
    N'Completado'),
(3, N'Formalización de los equipos de datos abiertos',
    N'57 equipos formalizados a nivel interinstitucional. Persiste una asimetría de madurez digital entre entidades que hay que cerrar con estandarización.',
    N'Completado'),
(4, N'Ecosistema normativo y protocolos de calidad',
    N'Guías e instructivos de curación y limpieza de datasets, políticas de privacidad y términos de uso, y manuales de metadatos y estándares operativos para el Data Center.',
    N'Completado'),
(5, N'Publicación y curación de conjuntos de datos',
    N'38 datasets publicados, con datos de ONCAE, Poder Judicial, INE, CONADE, IHM, SERNA, INHGEOMIN, ICF y la Secretaría de la Mujer. SEFIN y SRECI actualizaron en el primer bimestre de 2026; SRECI se comprometió a curación periódica.',
    N'EnProceso'),
(6, N'Análisis de impacto fiscal',
    N'Proyección de costos operativos y sostenibilidad presupuestaria de la estrategia a mediano y largo plazo.',
    N'EnProceso'),
(7, N'Aprobación de la Política Nacional de Datos Abiertos',
    N'El instrumento alcanzó su fase de finalización técnica, pero la formalización jurídica y administrativa se estancó en dictamen en la Secretaría de Planificación, pendiente de validación final para su publicación y entrada en vigor.',
    N'EnProceso'),
(8, N'Incorporación de la Secretaría de Desarrollo Social',
    N'Identificada como actor prioritario para expandir el portal por su ecosistema de 22 sistemas de información, de alto impacto para la inteligencia de datos del sector social.',
    N'Pendiente'),
(9, N'Visita de la Comisión Evaluadora',
    N'Presentación oficial del nuevo equipo de trabajo y reuniones de alto nivel con las contrapartes para evaluar el estado del proyecto. Estaba programada para el 26 de marzo de 2026; la ayuda memoria no registra su resultado.',
    N'Pendiente');

UPDATE hp
SET hp.Orden       = h.Orden,
    hp.Descripcion = h.Descripcion,
    hp.Estado      = h.Estado
FROM ProyectoHitos hp
JOIN @hd h ON h.Nombre = hp.Nombre
WHERE hp.ProyectoId = @da;

INSERT INTO ProyectoHitos (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT @da, h.Orden, h.Nombre, h.Descripcion, NULL, NULL, h.Estado, NULL, NULL
FROM @hd h
WHERE NOT EXISTS (SELECT 1 FROM ProyectoHitos x WHERE x.ProyectoId = @da AND x.Nombre = h.Nombre);

-- ════════ 3. Bitácora ═══════════════════════════════════════════════════════
DECLARE @a TABLE (
    Proy int, Fecha datetime2, Pct int, HitoNombre nvarchar(300),
    Descripcion nvarchar(2000), Bloqueo nvarchar(1000) NULL,
    ArchivoNombre nvarchar(300), ArchivoUrl nvarchar(500), ArchivoTamano bigint
);

INSERT INTO @a VALUES
(@mesa, '2026-07-09T15:00:00', 5, N'Definición de la lista de temas y subsectores de Honduras',
 N'Reunión en Casa Presidencial con el Banco Interamericano de Desarrollo. El BID presentó una herramienta de inteligencia regulatoria: un sistema agéntico de 12 agentes especializados sobre Gemini, Claude, NotebookLM y Antigravity, que mapea el stock regulatorio, detecta barreras y trámites asociados, aplica un checklist estandarizado —fundamento legal, estructura, plazos, costos, procesos, canales y limitaciones— y prioriza hallazgos por sector, impacto y severidad, con 3 a 5 días por corrida. El diagnóstico clasifica cada trámite en obsoleto, duplicado o mejorable; como referencia de escala se citó el ejercicio sobre Argentina, del orden de 280 mil regulaciones. Se acordó anclar el ejercicio en la Ley de Simplificación Administrativa y distinguir los procedimientos que no deberían existir de los que deben simplificarse. El BID compartirá los primeros resultados separando sector público y privado; Honduras debe definir su lista de temas y subsectores en la reunión de ministros.',
 N'Falta definir con el BID si el acceso a la herramienta se otorga mediante préstamo o si el Banco la provee directamente al Gobierno.',
 N'Ayuda_Memoria_BID_IA_Tramites_09-jul-2026.docx', N'/uploads/proyectos/304dbe909631acdd45cca81794c6d863.docx', 13149),

(@mesa, '2026-07-15T15:00:00', 10, N'Carga de fichas institucionales en el portal consolidado',
 N'[El acta no trae fecha; se ubica en el 15 de julio porque el acta anterior convocó a esa fecha y esta cita aquella como antecedente.] Mesa técnica DIGER–CNI para definir el equipo de trabajo y las instituciones participantes, acordar la metodología de levantamiento y avanzar en la priorización de sectores, con el liderazgo del proceso en la DIGER. El CNI entregó dos fichas: la radiografía de infraestructura —si las instituciones pueden adueñarse de estos servicios y tienen capacidad para hacerlo— y la línea base y flujo de cada trámite, para conocer cómo interoperan entre instituciones. La DIGER mostró su portal consolidado para apoyar la carga, clasificación y priorización de fichas y procesos. El CNI definirá los sectores finales; la propuesta fue infraestructura, turismo y agricultura, sobre los sectores identificados de agroindustria, infraestructura, logística y transporte, y energía. El tema más problemático es el de las municipalidades, porque cada una opera a su manera y dificulta la estandarización. Desde Casa Presidencial se reconoció el trabajo de la DIGER en la consolidación de la PDI y los trámites.',
 NULL, N'Ayuda_Memoria_mesa_tecnica_DIGER-CNI.docx', N'/uploads/proyectos/6ec713274dfbe4bfd415e10711601ac6.docx', 713348),

(@da, '2026-03-13T20:30:00', 55, N'Aprobación de la Política Nacional de Datos Abiertos',
 N'Reunión de estado de avance en oficinas de la DIGER con la STLCC. El portal consolida 38 datasets publicados bajo estándares de interoperabilidad, con datos de ONCAE, Poder Judicial, INE, CONADE, IHM, SERNA, INHGEOMIN, ICF y la Secretaría de la Mujer; SEFIN y SRECI actualizaron en el primer bimestre y SRECI formalizó su compromiso de curación periódica. Hay 57 equipos de datos abiertos formalizados, con asimetría de madurez digital entre entidades. El Data Center centralizado está consolidado con el equipamiento desplegado en la ex-STLCC, y el dominio institucional asegurado por seis años. Está avanzado el análisis de impacto fiscal para la sostenibilidad presupuestaria. La cooperación internacional sostiene buena parte del avance: PIDA aportó la hoja de ruta, CAF tiene fondos disponibles para el Open Data Chart y el BCIE facilita infraestructura para capacitaciones. Se identificó a la Secretaría de Desarrollo Social como actor prioritario para expandir el portal, por sus 22 sistemas de información.',
 N'La Política Nacional de Datos Abiertos está terminada técnicamente pero estancada en fase de dictamen en la Secretaría de Planificación, pendiente de validación final para su publicación y entrada en vigor.',
 N'Ayuda_Memoria_Datos_Abiertos_13-mar-2026.pdf', N'/uploads/proyectos/f7dc7a665690cf948af3aa9a5fa33f92.pdf', 164703);

INSERT INTO ProyectoAvances
    (ProyectoId, HitoId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT a.Proy,
       (SELECT TOP 1 h.Id FROM ProyectoHitos h WHERE h.ProyectoId = a.Proy AND h.Nombre = a.HitoNombre),
       a.Fecha, @actor, a.Descripcion, a.Pct, a.Bloqueo,
       a.ArchivoNombre, a.ArchivoUrl, a.ArchivoTamano
FROM @a a
WHERE NOT EXISTS (SELECT 1 FROM ProyectoAvances x WHERE x.ProyectoId = a.Proy AND x.Descripcion = a.Descripcion);

/* Snapshot desde el último reporte. En la mesa técnica esto deja el 10 % que ya tenía. */
UPDATE p
SET p.AvancePct = u.Pct, p.UpdatedAt = @hoy, p.UpdatedBy = @actor
FROM Proyectos p
CROSS APPLY (
    SELECT TOP 1 a.PorcentajeReportado AS Pct
    FROM ProyectoAvances a WHERE a.ProyectoId = p.Id
    ORDER BY a.Fecha DESC, a.Id DESC
) u
WHERE p.Id IN (@mesa, @da);

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT p.Codigo, LEFT(p.Nombre, 36) AS Proyecto, p.Estado, p.AvancePct AS Pct,
       COUNT(h.Id) AS Hitos,
       SUM(CASE WHEN h.Estado = N'Completado' THEN 1 ELSE 0 END) AS Cumplidos,
       (SELECT COUNT(*) FROM ProyectoAvances a WHERE a.ProyectoId = p.Id) AS Reportes
FROM Proyectos p
LEFT JOIN ProyectoHitos h ON h.ProyectoId = p.Id
WHERE p.Id IN (@mesa, @da)
GROUP BY p.Id, p.Codigo, p.Nombre, p.Estado, p.AvancePct;
