/*
    Alta de cuatro frentes que faltaban en el portafolio:

      1. SGSEC — Portal de solicitudes de certificados digitales
      2. Data Center Gubernamental Unificado (BID 4942/BL-HO)
      3. Diagnóstico de Madurez de Gobierno Digital — aplicación del instrumento
      4. Encuesta ILIA — Índice Latinoamericano de Inteligencia Artificial

    Fuentes de los hitos cumplidos: el historial del repositorio de SGSEC (69 commits desde el
    2026-01-09 más el trabajo del 18 al 21 de agosto), los scripts de carga del repositorio de
    encuestas, y la ayuda memoria del Comité Técnico Asesor del ILIA del 2026-05-05.

    Advertencias sobre lo que NO se sabe, para que nadie lo lea como dato:
      - SGSEC entra SIN hitos pendientes: hay evidencia abundante de lo entregado y ninguna de
        lo que falta. El porcentaje es una estimación y hay que completarle el tramo final.
      - El Data Center entra con los dos instrumentos ya cargados; los hitos del estudio y del
        diseño los tiene que dictar quien lleva el proyecto.
      - El diagnóstico de madurez queda en Planificado y sin fechas: arranca cuando se apruebe
        el instrumento (hito 11 de PRY-2026-18).
      - El ILIA es el único con fecha de cierre comprometida: el lanzamiento de la 4.ª edición
        es el 2026-09-29, y esa fecha la fija el índice, no la DIGER.

    Idempotente: reconoce cada proyecto por Nombre y sus hitos por (Proyecto, Nombre).

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/proyectos_frentes_adicionales.sql
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

DECLARE @henry    uniqueidentifier = 'FF56AE4F-AB09-41A6-BCE3-F954E5E7DAAF';
DECLARE @henryNom nvarchar(200)    = N'Henry Ortez';
DECLARE @actor    nvarchar(200)    = N'Henry Alexis Ortez Banegas';
DECLARE @hoy      datetime2        = SYSUTCDATETIME();

-- ── Definición de los cuatro proyectos ──────────────────────────────────────
DECLARE @p TABLE (
    Ord int, Nombre nvarchar(300), Objetivo nvarchar(2000),
    Estado nvarchar(30), Prioridad nvarchar(20),
    Inicio date NULL, FinPlan date NULL, Avance int,
    ConResponsable bit
);

INSERT INTO @p VALUES
(1, N'SGSEC — Portal de solicitudes de certificados digitales',
    N'Sistema que gestiona el ciclo de vida de las solicitudes de emisión de certificados digitales de la PKI de la DIGER: registro de titulares por institución con convenio, validación por un operador, envío a la Autoridad de Registro de Bit4id y seguimiento hasta la emisión, revocación o suspensión.',
    N'EnEjecucion', N'Alta', '2026-01-09', NULL, 85, 1),

(2, N'Data Center Gubernamental Unificado (BID 4942/BL-HO)',
    N'Levantar la capacidad de cómputo instalada en las instituciones del Estado y sustentar el diseño de un data center gubernamental unificado, en el marco de la operación BID 4942/BL-HO.',
    N'EnEjecucion', N'Alta', '2026-08-04', NULL, 20, 1),

(3, N'Diagnóstico de Madurez de Gobierno Digital — aplicación',
    N'Aplicar el instrumento de madurez en las instituciones del Estado una vez aprobado: convocatoria, acompañamiento al llenado, análisis de resultados e informe país con las brechas y recomendaciones por dimensión.',
    N'Planificado', N'Alta', NULL, NULL, 0, 1),

(4, N'Encuesta ILIA — Índice Latinoamericano de Inteligencia Artificial',
    N'Preparar y remitir la información de Honduras para la 4.ª edición del Índice Latinoamericano de Inteligencia Artificial, incluidos los 33 subindicadores nuevos, y definir la hoja de ruta para mejorar la puntuación en los factores habilitantes detectados como críticos.',
    N'EnEjecucion', N'Alta', '2026-05-05', '2026-09-29', 25, 1);

-- ── Hitos ───────────────────────────────────────────────────────────────────
DECLARE @h TABLE (
    ProyOrd int, Orden int, Nombre nvarchar(300), Descripcion nvarchar(2000),
    Estado nvarchar(30), FechaPlan date NULL, FechaReal date NULL
);

INSERT INTO @h VALUES
-- 1. SGSEC — todo lo que hay documentado está entregado; falta definirle el tramo final.
(1,  1, N'Administración de instituciones y catálogos',
        N'Mantenimientos del sistema y administración de las instituciones con convenio vigente.',
        N'Completado', NULL, '2026-02-17'),
(1,  2, N'Integración con la Autoridad de Registro de Bit4id',
        N'Envío de solicitudes a la RA, con aprobación, rechazo y plantilla de la solicitud.',
        N'Completado', NULL, '2026-02-19'),
(1,  3, N'Sitio público y formularios de solicitud',
        N'Ocho páginas públicas en el portal y los formularios de solicitud, con exportación y tablas en español.',
        N'Completado', NULL, '2026-03-05'),
(1,  4, N'Inicio de sesión con identidad o correo (SSO)',
        N'Autenticación integrada, además del branding institucional del portal.',
        N'Completado', NULL, '2026-03-18'),
(1,  5, N'Alcance por institución y endurecimiento de accesos',
        N'Las solicitudes se acotan a la institución del usuario, los documentos salieron de wwwroot y la cuota mensual pasó a aplicarse de verdad.',
        N'Completado', NULL, '2026-08-18'),
(1,  6, N'Cierre del ciclo de emisión',
        N'Los estados que devuelve la RA ahora se persisten: la solicitud avanza hasta certificado emitido o error, sin tocar la base a mano.',
        N'Completado', NULL, '2026-08-19'),
(1,  7, N'Revocación, suspensión y restauración de certificados',
        N'Operaciones sobre el certificado ya emitido, resolviendo su identificador contra la RA en el momento.',
        N'Completado', NULL, '2026-08-20'),
(1,  8, N'Optimización de la bandeja del operador',
        N'La pantalla pasó de 65 consultas a 3, con pruebas de guardia para que no vuelva a degradarse.',
        N'Completado', NULL, '2026-08-20'),
(1,  9, N'Unificación del vocabulario visual',
        N'Cinco vocabularios de CSS consolidados en un solo archivo de tokens y 152 reglas duplicadas eliminadas de las páginas informativas.',
        N'Completado', NULL, '2026-08-21'),
(1, 10, N'Documentación y control de versiones',
        N'README del proyecto, convenciones y gotchas en CLAUDE.md, esquema versionado de solo lectura, configuración fuera del repositorio y versión real en el pie del sitio.',
        N'Completado', NULL, '2026-08-21'),

-- 2. Data Center
(2,  1, N'Instrumento unificado de madurez y capacidad',
        N'Cincuenta campos de capacidad de cómputo —valor actual y proyección a tres años— integrados al instrumento de madurez en siete bloques temáticos.',
        N'Completado', NULL, '2026-08-04'),
(2,  2, N'Instrumento de diagnóstico de capacidad de cómputo',
        N'Instrumento propio de levantamiento cuantitativo a nivel gubernamental, cargado en el portal de encuestas.',
        N'Completado', NULL, '2026-08-05'),
(2,  3, N'Levantamiento de capacidad en las instituciones',
        N'Aplicación del instrumento y consolidación de los datos de capacidad instalada.',
        N'Pendiente', NULL, NULL),
(2,  4, N'Análisis de capacidad y proyección de demanda',
        N'Procesamiento de lo levantado para dimensionar la demanda de cómputo del Estado a tres años.',
        N'Pendiente', NULL, NULL),
(2,  5, N'Diseño del data center gubernamental unificado',
        N'Definición de la arquitectura, el dimensionamiento y el modelo de operación. Los hitos de este tramo están por detallar con quien lleva la operación BID.',
        N'Pendiente', NULL, NULL),

-- 3. Diagnóstico de madurez — arranca cuando se apruebe el instrumento
(3,  1, N'Convocatoria a las instituciones',
        N'Habilitación de accesos por institución y comunicación oficial del ejercicio.',
        N'Pendiente', NULL, NULL),
(3,  2, N'Acompañamiento al llenado',
        N'Soporte a las instituciones durante la autoevaluación y control de avance de los llenados.',
        N'Pendiente', NULL, NULL),
(3,  3, N'Análisis de resultados por dimensión',
        N'Explotación del tablero de madurez y de las exportaciones para leer los resultados por dimensión y subdimensión.',
        N'Pendiente', NULL, NULL),
(3,  4, N'Informe país de madurez de gobierno digital',
        N'Documento con las brechas detectadas y las recomendaciones por dimensión.',
        N'Pendiente', NULL, NULL),
(3,  5, N'Socialización de resultados',
        N'Devolución de resultados a las instituciones evaluadas y a la coordinación.',
        N'Pendiente', NULL, NULL),

-- 4. ILIA
(4,  1, N'Participación en el Comité Técnico Asesor',
        N'Sesión del 5 de mayo de 2026: contenidos de la 4.ª edición del índice, 20 países participantes y 33 subindicadores nuevos.',
        N'Completado', NULL, '2026-05-05'),
(4,  2, N'Solicitud formal de apoyo técnico al Comité',
        N'Honduras solicitó apoyo del Comité para alcanzar los estándares que exige el índice.',
        N'Completado', NULL, '2026-05-05'),
(4,  3, N'Identificación de los factores habilitantes críticos',
        N'Determinar en qué factores habilitantes está más baja la puntuación del país.',
        N'EnProceso', NULL, NULL),
(4,  4, N'Hoja de ruta para mejorar la puntuación',
        N'Plan de acción sobre los factores críticos, acordado con el Comité Técnico Asesor.',
        N'Pendiente', NULL, NULL),
(4,  5, N'Levantamiento de los 33 subindicadores nuevos',
        N'Recolección de la información que exige la edición 2026 del índice.',
        N'Pendiente', NULL, NULL),
(4,  6, N'Envío de la información al índice',
        N'Remisión de los datos de Honduras antes del cierre de la edición.',
        N'Pendiente', '2026-09-15', NULL),
(4,  7, N'Lanzamiento de la 4.ª edición',
        N'Publicación del índice con los 20 países participantes. La fecha la fija el índice, no la DIGER.',
        N'Pendiente', '2026-09-29', NULL);

-- ── Inserción ───────────────────────────────────────────────────────────────
DECLARE @base int = (
    SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(Codigo, 10, 10) AS int)), 0)
    FROM Proyectos WHERE Codigo LIKE N'PRY-2026-%'
);

BEGIN TRANSACTION;

INSERT INTO Proyectos
    (IsDeleted, Codigo, Nombre, Objetivo, AreaId, ResponsableId, Responsable, Prioridad, Estado,
     FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, CreatedAt, CreatedBy)
SELECT 0,
       N'PRY-2026-' + FORMAT(@base + ROW_NUMBER() OVER (ORDER BY p.Ord), '00'),
       p.Nombre, p.Objetivo, NULL,
       CASE WHEN p.ConResponsable = 1 THEN @henry END,
       CASE WHEN p.ConResponsable = 1 THEN @henryNom END,
       p.Prioridad, p.Estado,
       p.Inicio, p.FinPlan,
       CASE WHEN p.Estado = N'EnEjecucion' THEN p.Inicio END,
       NULL, p.Avance, @hoy, @actor
FROM @p p
WHERE NOT EXISTS (SELECT 1 FROM Proyectos x WHERE x.Nombre = p.Nombre AND x.IsDeleted = 0);

/* Del proyecto ya existente solo se refresca el Objetivo. Estado, Prioridad, fechas y
   AvancePct son estado vivo que mueven la app y los avances; pisarlos desde aquí
   revertiría lo que el responsable haya registrado después. */
UPDATE pr
SET pr.Objetivo  = p.Objetivo,
    pr.UpdatedAt = @hoy,
    pr.UpdatedBy = @actor
FROM Proyectos pr
JOIN @p p ON p.Nombre = pr.Nombre
WHERE pr.IsDeleted = 0 AND ISNULL(pr.Objetivo, N'') <> p.Objetivo;

/* Los hitos que ya existen se actualizan; sin este UPDATE el script solo servía la
   primera vez y toda corrección posterior de descripción o estado quedaba sin aplicar. */
UPDATE hp
SET hp.Orden       = h.Orden,
    hp.Descripcion = h.Descripcion,
    hp.Estado      = h.Estado,
    hp.FechaPlan   = h.FechaPlan,
    hp.FechaReal   = h.FechaReal
FROM ProyectoEntregables hp
JOIN Proyectos pr ON pr.Id     = hp.ProyectoId AND pr.IsDeleted = 0
JOIN @p p         ON p.Nombre  = pr.Nombre
JOIN @h h         ON h.ProyOrd = p.Ord AND h.Nombre = hp.Nombre;

INSERT INTO ProyectoEntregables
    (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT pr.Id, h.Orden, h.Nombre, h.Descripcion, h.FechaPlan, h.FechaReal, h.Estado, NULL, NULL
FROM @h h
JOIN @p p         ON p.Ord     = h.ProyOrd
JOIN Proyectos pr ON pr.Nombre = p.Nombre AND pr.IsDeleted = 0
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoEntregables x WHERE x.ProyectoId = pr.Id AND x.Nombre = h.Nombre
);

-- ── Bitácora inicial de los tres frentes que ya están en marcha ─────────────
DECLARE @a TABLE (ProyOrd int, Pct int, Descripcion nvarchar(2000));

INSERT INTO @a VALUES
(1, 85, N'El portal de solicitudes de certificados está construido y con su ciclo completo operando: solicitudes acotadas a la institución del usuario, envío a la Autoridad de Registro de Bit4id, persistencia de los estados que devuelve la RA hasta la emisión, y revocación, suspensión y restauración del certificado ya emitido. En la semana del 18 al 21 de agosto se hizo el endurecimiento de accesos, se optimizó la bandeja del operador de 65 consultas a 3 con pruebas de guardia, se unificó el vocabulario visual en un solo archivo de tokens y se dejó documentado el proyecto con su esquema versionado. Falta definir el tramo de cierre del proyecto.'),
(2, 20, N'Los dos instrumentos de levantamiento están cargados en el portal de encuestas: el diagnóstico de capacidad de cómputo a nivel gubernamental y los cincuenta campos de capacidad integrados al instrumento de madurez, con valor actual y proyección a tres años en siete bloques temáticos. Lo que sigue es aplicarlos en las instituciones, consolidar la capacidad instalada y proyectar la demanda, insumos del diseño del data center unificado.'),
(4, 25, N'En la sesión del Comité Técnico Asesor del 5 de mayo se expusieron los contenidos de la 4.ª edición del índice: 20 países participantes y 33 subindicadores nuevos. Honduras solicitó formalmente apoyo técnico al Comité para alcanzar los estándares exigidos, y quedó pendiente definir la hoja de ruta sobre los factores habilitantes detectados como críticos. El lanzamiento de la edición es el 29 de septiembre, fecha que fija el índice.');

INSERT INTO ProyectoAvances
    (ProyectoId, EntregableId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT pr.Id, NULL, @hoy, @actor, a.Descripcion, a.Pct, NULL, NULL, NULL, NULL
FROM @a a
JOIN @p p         ON p.Ord     = a.ProyOrd
JOIN Proyectos pr ON pr.Nombre = p.Nombre AND pr.IsDeleted = 0
WHERE NOT EXISTS (
    SELECT 1 FROM ProyectoAvances x WHERE x.ProyectoId = pr.Id AND x.Descripcion = a.Descripcion
);

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT p.Codigo, LEFT(p.Nombre, 48) AS Proyecto, p.Estado, p.AvancePct AS Pct,
       CONVERT(varchar(10), p.FechaFinPlan, 103) AS CierrePlan,
       COUNT(h.Id) AS Hitos,
       SUM(CASE WHEN h.Estado = N'Completado' THEN 1 ELSE 0 END) AS Cumplidos
FROM Proyectos p
LEFT JOIN ProyectoEntregables h ON h.ProyectoId = p.Id
WHERE p.IsDeleted = 0 AND p.Nombre IN (SELECT Nombre FROM @p)
GROUP BY p.Id, p.Codigo, p.Nombre, p.Estado, p.AvancePct, p.FechaFinPlan
ORDER BY p.Codigo;
