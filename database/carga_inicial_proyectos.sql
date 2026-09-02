/*
    Carga inicial del módulo Proyectos, derivada del análisis de las 45 reuniones activas
    registradas entre el 2026-06-04 y el 2026-07-29 (124 acuerdos).

    Criterios:
      - Un proyecto por institución en incorporación a la Plataforma SOL, más los frentes
        transversales que atraviesan varias instituciones.
      - Los hitos son LÍNEAS DE TRABAJO derivadas de los acuerdos, no un hito por acuerdo:
        118 de los 124 acuerdos siguen en «Pendiente» y volcarlos uno a uno daría ruido.
      - FechaInicioPlan = fecha de la primera reunión del frente. FechaInicioReal se llena
        solo para los que van En ejecución, para no fingir un arranque que no ocurrió.
      - FechaFinPlan queda NULA a propósito: no hay fecha comprometida en ninguna reunión.
      - AvancePct queda en 0 en todos. El seguimiento de compromisos no se está actualizando,
        así que cualquier porcentaje calculado sería inventado — lo reporta el responsable.
      - Responsable solo donde los datos lo sostienen (Brizzio Zelaya figura como facilitador
        o responsable de acuerdos en IHADFA, IHT, HBCBH y CONSUCOOP). El resto va sin asignar.

    Idempotente: reconoce los proyectos por Nombre y los hitos por (Proyecto, Orden).
    Se puede correr dos veces sin duplicar nada.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/carga_inicial_proyectos.sql
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

DECLARE @hoy   datetime2      = SYSUTCDATETIME();
DECLARE @autor nvarchar(200)  = N'carga-inicial-reuniones';

/* Único responsable que los datos sostienen. */
DECLARE @brizzio    uniqueidentifier = '9BBB2591-9790-427E-B1E0-792B8E562385';
DECLARE @brizzioNom nvarchar(200)    = N'Brizzio Zelaya';

-- ── Proyectos ────────────────────────────────────────────────────────────────
DECLARE @p TABLE (
    Ord           int,
    Nombre        nvarchar(300),
    Objetivo      nvarchar(2000),
    Estado        nvarchar(30),
    Prioridad     nvarchar(20),
    Inicio        date,
    ResponsableId uniqueidentifier NULL,
    Responsable   nvarchar(200)    NULL
);

INSERT INTO @p (Ord, Nombre, Objetivo, Estado, Prioridad, Inicio, ResponsableId, Responsable) VALUES
( 1, N'SOL — CONSUCOOP',
     N'Incorporar a CONSUCOOP a la Plataforma SOL: migración del hosting institucional, configuración de sus siete trámites y capacitación del personal técnico y legal.',
     N'EnEjecucion', N'Alta', '2026-06-08', @brizzio, @brizzioNom),

( 2, N'SOL — SESAL',
     N'Lanzar los trámites digitales de la Secretaría de Salud en la Plataforma SOL e incorporar la firma electrónica avanzada en sus servicios, con acompañamiento a la mesa de inspectores.',
     N'EnEjecucion', N'Alta', '2026-06-30', NULL, NULL),

( 3, N'SOL — IHADFA',
     N'Ajustar y poner en operación los trámites del IHADFA en la Plataforma SOL: moldes, requisitos, mensajería y reglas de pago, con capacitación al equipo legal y operativo.',
     N'EnEjecucion', N'Alta', '2026-06-04', @brizzio, @brizzioNom),

( 4, N'SOL — SRECI (Cancillería)',
     N'Integrar a la Cancillería con la Plataforma SOL: diagnóstico del modelado actual, arquitectura de interoperabilidad por APIs y piloto de emisión con firma electrónica.',
     N'EnEjecucion', N'Alta', '2026-07-01', NULL, NULL),

( 5, N'SOL — FOSOVI',
     N'Levantar y digitalizar los servicios de FOSOVI en la Plataforma SOL, definiendo el alojamiento de la solución y el sustento legal y reglamentario que lo faculta.',
     N'EnEjecucion', N'Media', '2026-06-17', NULL, NULL),

( 6, N'SOL — IHT / CANATURH',
     N'Estabilizar la operación del IHT en la Plataforma SOL: atención de incidencias en producción, validación institucional de los trámites y capacitación del personal a cargo.',
     N'EnEjecucion', N'Media', '2026-06-17', @brizzio, @brizzioNom),

( 7, N'SOL — SENASA',
     N'Habilitar los trámites de SENASA en la Plataforma SOL con validación de TGR-1 y timbre, formatos oficiales de certificados y firma electrónica avanzada para sus funcionarios.',
     N'EnEjecucion', N'Media', '2026-06-11', NULL, NULL),

( 8, N'SOL — Cuerpo de Bomberos (HBCBH)',
     N'Levantar el trámite del Cuerpo de Bomberos para su digitalización en la Plataforma SOL, incluida la revisión de la carga de planos en formato AutoCAD.',
     N'Planificado', N'Media', '2026-07-22', @brizzio, @brizzioNom),

( 9, N'SOL — ARSA',
     N'Acercamiento con la Agencia de Regulación Sanitaria para identificar los trámites candidatos a digitalizarse en la Plataforma SOL.',
     N'Planificado', N'Baja', '2026-07-02', NULL, NULL),

(10, N'SOL — Instituto de la Propiedad',
     N'Exploración con el Instituto de la Propiedad para seleccionar dos trámites digitalizables y reunir sus flujos y documentación de respaldo.',
     N'Planificado', N'Baja', '2026-06-04', NULL, NULL),

(11, N'Firma electrónica avanzada y certificados PKI',
     N'Habilitar la firma electrónica avanzada como servicio transversal del Estado: emisión y enrolamiento de certificados, y su incorporación en los trámites de SESAL, SENASA, FOSOVI, Cancillería e Instituto de la Propiedad.',
     N'EnEjecucion', N'Alta', '2026-06-11', NULL, NULL),

(12, N'Expediente Digital GEXFILE — SDE',
     N'Poner en operación el expediente digital GEXFILE en la Secretaría de Desarrollo Económico, integrado con SOL, y definir sus políticas de conservación y de cobro.',
     N'EnEjecucion', N'Alta', '2026-06-25', NULL, NULL),

(13, N'Talent Up Honduras',
     N'Ejercer la Secretaría Técnica del programa Talent Up Honduras: convenio con el CNI, ajustes técnicos y de imagen de la plataforma, y estrategia de lanzamiento.',
     N'EnEjecucion', N'Alta', '2026-06-23', NULL, NULL),

(14, N'Componente 3 SENPRENDE — constitución de empresas',
     N'Integrar la firma electrónica avanzada y la Billetera Electrónica Nacional en la inscripción registral y la constitución de empresas, junto con IP y SENPRENDE.',
     N'EnEjecucion', N'Media', '2026-06-18', NULL, NULL);

-- ── Hitos (líneas de trabajo derivadas de los acuerdos) ──────────────────────
DECLARE @h TABLE (
    ProyOrd   int,
    Orden     int,
    Nombre    nvarchar(300),
    Estado    nvarchar(30),
    FechaReal date NULL
);

INSERT INTO @h (ProyOrd, Orden, Nombre, Estado, FechaReal) VALUES
-- 1. CONSUCOOP
( 1, 1, N'Migración de hosting y configuración de ambientes',              N'Completado', '2026-06-19'),
( 1, 2, N'Fichas y configuración de los siete trámites',                   N'EnProceso',  NULL),
( 1, 3, N'Pruebas funcionales y depuración de trámites de prueba',         N'EnProceso',  NULL),
( 1, 4, N'Ajustes de firma, código QR y notificaciones por correo',        N'EnProceso',  NULL),
( 1, 5, N'Capacitación del personal técnico y legal',                      N'EnProceso',  NULL),
( 1, 6, N'Instructivos y materiales de apoyo al ciudadano',                N'Pendiente',  NULL),
-- 2. SESAL
( 2, 1, N'Lanzamiento de los trámites digitales',                          N'EnProceso',  NULL),
( 2, 2, N'Revisión de procesos y puntos de integración',                   N'EnProceso',  NULL),
( 2, 3, N'Validación con el equipo de inspectores',                        N'Pendiente',  NULL),
( 2, 4, N'Enrolamiento y emisión de certificados de firma',                N'Pendiente',  NULL),
-- 3. IHADFA
( 3, 1, N'Adecuación de moldes, requisitos y elementos gráficos',          N'EnProceso',  NULL),
( 3, 2, N'Capacitación del equipo legal y operativo',                      N'EnProceso',  NULL),
( 3, 3, N'Separación de las fases de dictamen y resolución',               N'Pendiente',  NULL),
( 3, 4, N'Regla de un pago por establecimiento e integración bancaria',    N'Pendiente',  NULL),
( 3, 5, N'Usuarios y correos institucionales del nuevo personal',          N'Pendiente',  NULL),
( 3, 6, N'Simulación operativa previa a la puesta en marcha',              N'Pendiente',  NULL),
-- 4. SRECI
( 4, 1, N'Análisis y diagnóstico del modelado actual',                     N'EnProceso',  NULL),
( 4, 2, N'Designación de contrapartes y mesas de trabajo',                 N'EnProceso',  NULL),
( 4, 3, N'Viabilidad técnica de conexiones e infraestructura',             N'Pendiente',  NULL),
( 4, 4, N'Propuesta de arquitectura de interoperabilidad (APIs)',          N'Pendiente',  NULL),
( 4, 5, N'Piloto de emisión con firma electrónica',                        N'Pendiente',  NULL),
-- 5. FOSOVI
( 5, 1, N'Levantamiento de servicios y diagramas de flujo',                N'EnProceso',  NULL),
( 5, 2, N'Análisis legal y reglamentario que faculta a FOSOVI',            N'EnProceso',  NULL),
( 5, 3, N'Alojamiento temporal en servidores de la DIGER',                 N'Pendiente',  NULL),
( 5, 4, N'Solicitud de firma electrónica avanzada',                        N'Pendiente',  NULL),
( 5, 5, N'Análisis de factibilidad social y económica',                    N'Pendiente',  NULL),
( 5, 6, N'Pruebas de plataforma',                                          N'Pendiente',  NULL),
-- 6. IHT / CANATURH
( 6, 1, N'Atención de incidencias en producción',                          N'EnProceso',  NULL),
( 6, 2, N'Ambiente de pruebas y validación institucional',                 N'EnProceso',  NULL),
( 6, 3, N'Capacitación de la administradora y el personal operativo',      N'EnProceso',  NULL),
( 6, 4, N'Escalamiento del caso con el CNI',                               N'EnProceso',  NULL),
( 6, 5, N'Actualización de logos institucionales en la plataforma',        N'Pendiente',  NULL),
-- 7. SENASA
( 7, 1, N'Validación de TGR-1 y timbre en los trámites',                   N'EnProceso',  NULL),
( 7, 2, N'Validación de los formatos oficiales de certificados',           N'Pendiente',  NULL),
( 7, 3, N'Asignación institucional de firmas electrónicas avanzadas',      N'Pendiente',  NULL),
( 7, 4, N'Capacitación por grupos funcionales y tipo de trámite',          N'Pendiente',  NULL),
-- 8. HBCBH
( 8, 1, N'Mesa técnica de levantamiento',                                  N'EnProceso',  NULL),
( 8, 2, N'Flujo, requisitos y formularios del trámite',                    N'Pendiente',  NULL),
( 8, 3, N'Soporte de archivos AutoCAD en la plataforma',                   N'Pendiente',  NULL),
( 8, 4, N'Ajustes visuales e institucionales',                             N'Pendiente',  NULL),
-- 9. ARSA
( 9, 1, N'Reunión de acercamiento institucional',                          N'Completado', '2026-07-02'),
( 9, 2, N'Identificación de los trámites a digitalizar',                   N'Pendiente',  NULL),
-- 10. Instituto de la Propiedad
(10, 1, N'Identificación de dos trámites digitalizables',                  N'EnProceso',  NULL),
(10, 2, N'Documentación de respaldo y flujos de proceso',                  N'Pendiente',  NULL),
(10, 3, N'Reunión de seguimiento',                                         N'Pendiente',  NULL),
-- 11. PKI
(11, 1, N'Taller técnico de firma avanzada con la SESAL',                  N'Completado', '2026-07-28'),
(11, 2, N'Requisitos de emisión y enrolamiento de certificados',           N'EnProceso',  NULL),
(11, 3, N'Proceso institucional de asignación de firmas (SENASA)',         N'Pendiente',  NULL),
(11, 4, N'Integración de la firma en el piloto de Cancillería',            N'Pendiente',  NULL),
(11, 5, N'Firma avanzada en la constitución de empresas (IP/SENPRENDE)',   N'Pendiente',  NULL),
-- 12. GEXFILE
(12, 1, N'Puesta en operación del expediente digital',                     N'EnProceso',  NULL),
(12, 2, N'Capacitación de las funcionarias que tramitan solicitudes',      N'Pendiente',  NULL),
(12, 3, N'Convocatoria a personas jurídicas y publicación en la web',      N'Pendiente',  NULL),
(12, 4, N'Política de conservación e inactivación de expedientes',         N'Pendiente',  NULL),
(12, 5, N'Política de cobro por servicio',                                 N'Pendiente',  NULL),
-- 13. Talent Up
(13, 1, N'Convenio DIGER — CNI',                                           N'EnProceso',  NULL),
(13, 2, N'Reuniones de seguimiento quincenales',                           N'EnProceso',  NULL),
(13, 3, N'Cambios técnicos en la plataforma',                              N'Pendiente',  NULL),
(13, 4, N'Nueva línea gráfica y logos del programa',                       N'Pendiente',  NULL),
(13, 5, N'Estrategia de lanzamiento del programa',                         N'Pendiente',  NULL),
-- 14. SENPRENDE C3
(14, 1, N'Taller de lanzamiento del componente 3',                         N'Completado', '2026-06-18'),
(14, 2, N'Firma avanzada y Billetera Nacional en inscripción registral',   N'Pendiente',  NULL),
(14, 3, N'Convenios interinstitucionales para el uso de la billetera',     N'Pendiente',  NULL),
(14, 4, N'Reglamentos e instrumentos legales con IP y SENPRENDE',          N'Pendiente',  NULL),
(14, 5, N'Fórmulas de cobro de tasas registrales y su automatización',     N'Pendiente',  NULL);

-- ── Inserción ───────────────────────────────────────────────────────────────
/* El correlativo continúa después del último PRY-2026-NN existente, contando también los
   borrados: reciclar un código ya citado en un informe confunde a quien lo leyó. */
DECLARE @base int = (
    SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(Codigo, 10, 10) AS int)), 0)
    FROM Proyectos WHERE Codigo LIKE N'PRY-2026-%'
);

BEGIN TRANSACTION;

INSERT INTO Proyectos
    (IsDeleted, Codigo, Nombre, Objetivo, AreaId, ResponsableId, Responsable, Prioridad, Estado,
     FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, CreatedAt, CreatedBy)
SELECT
    0,
    N'PRY-2026-' + FORMAT(@base + ROW_NUMBER() OVER (ORDER BY p.Ord), '00'),
    p.Nombre, p.Objetivo, NULL, p.ResponsableId, p.Responsable, p.Prioridad, p.Estado,
    p.Inicio, NULL,
    CASE WHEN p.Estado = N'EnEjecucion' THEN p.Inicio END,
    NULL, 0, @hoy, @autor
FROM @p p
WHERE NOT EXISTS (SELECT 1 FROM Proyectos x WHERE x.Nombre = p.Nombre AND x.IsDeleted = 0);

INSERT INTO ProyectoEntregables
    (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT pr.Id, h.Orden, h.Nombre, NULL, NULL, h.FechaReal, h.Estado, NULL, NULL
FROM @h h
JOIN @p p         ON p.Ord    = h.ProyOrd
JOIN Proyectos pr ON pr.Nombre = p.Nombre AND pr.IsDeleted = 0
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoEntregables x WHERE x.ProyectoId = pr.Id AND x.Orden = h.Orden
);

/* El proyecto de prueba que dejó la verificación del módulo no pertenece al catálogo. */
UPDATE Proyectos
SET IsDeleted = 1, UpdatedAt = @hoy, UpdatedBy = @autor
WHERE Nombre = N'Prueba de humo — módulo de proyectos' AND IsDeleted = 0;

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT p.Codigo, LEFT(p.Nombre, 46) AS Proyecto, p.Estado, p.Prioridad,
       ISNULL(p.Responsable, N'— por asignar —') AS Responsable,
       CONVERT(varchar(10), p.FechaInicioPlan, 103) AS Inicio,
       COUNT(h.Id) AS Hitos
FROM Proyectos p
LEFT JOIN ProyectoEntregables h ON h.ProyectoId = p.Id
WHERE p.IsDeleted = 0
GROUP BY p.Id, p.Codigo, p.Nombre, p.Estado, p.Prioridad, p.Responsable, p.FechaInicioPlan
ORDER BY p.Codigo;
