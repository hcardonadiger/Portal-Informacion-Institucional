/*
    Actualización de cuatro proyectos con las ayudas memoria compartidas el 2026-08-22.

    Documento                                  Fecha        Proyecto
    -----------------------------------------  -----------  -----------------------------
    Acercamiento IP / SDE / DIGER / PNUD       2026-06-04   PRY-2026-12  SOL — IP
    Revisión de APIs para trámites SOL — IP    (sin fecha)  PRY-2026-12  SOL — IP
    Taller TGR-1 y Timbre Electrónico SENASA   2026-06-11   PRY-2026-09  SOL — SENASA
    Presentación GEXFILE — SOL                 2026-06-25   PRY-2026-14  GEXFILE — SDE
    Registro reunión técnica SRECI-DIGER       2026-07-08   PRY-2026-06  SOL — SRECI

    Cómo se fechó cada entrada: la del acta cuando el documento la trae. El acta de revisión
    de APIs del IP **no tiene fecha**, así que su entrada va con la fecha del reporte (hoy) y
    lo dice en el propio texto — es preferible a inventarle un día.

    El taller de SENASA se fechó al 2026-06-11 por coincidencia exacta de sus cuatro acuerdos
    con los de la reunión 6 ya registrada en el módulo de reuniones, no por el nombre del archivo.

    Las cinco actas quedan adjuntas como evidencia de su entrada de bitácora.

    Los porcentajes son estimaciones de quien redacta a partir de lo que dicen las actas; el
    responsable de cada frente puede corregirlos desde el editor.

    Idempotente: reconoce los avances por su descripción y los hitos por (Proyecto, Nombre).

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/actualizar_proyectos_ayudas_memoria.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor nvarchar(200) = N'Henry Alexis Ortez Banegas';
DECLARE @hoy   datetime2     = SYSUTCDATETIME();

/* Honduras es UTC-6: las horas del acta se guardan en UTC (ver ProyectoAvances.Fecha). */
DECLARE @ip     int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-12' AND IsDeleted = 0);
DECLARE @senasa int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-09' AND IsDeleted = 0);
DECLARE @gexf   int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-14' AND IsDeleted = 0);
DECLARE @sreci  int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-06' AND IsDeleted = 0);

IF @ip IS NULL OR @senasa IS NULL OR @gexf IS NULL OR @sreci IS NULL
BEGIN
    RAISERROR(N'Falta alguno de los proyectos PRY-2026-06/09/12/14.', 16, 1);
    RETURN;
END

BEGIN TRANSACTION;

-- ════════ 1. Estado de los proyectos ════════════════════════════════════════
/* El IP estaba como «Planificado» con la sola reunión de exploración. Las actas muestran un
   frente bastante más avanzado: servicios identificados, el modelo del recibo de pago acordado
   y PNUD-SIGOB ajustando la API. Pasa a ejecución. */
UPDATE Proyectos
SET Estado          = N'EnEjecucion',
    FechaInicioReal = ISNULL(FechaInicioReal, '2026-06-04'),
    UpdatedAt       = @hoy,
    UpdatedBy       = @actor
WHERE Id = @ip AND Estado = N'Planificado';

-- ════════ 2. Hitos ══════════════════════════════════════════════════════════
DECLARE @h TABLE (Proy int, Orden int, Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30), FechaReal date NULL);

INSERT INTO @h VALUES
-- ── SOL — Instituto de la Propiedad ──────────────────────────────────────────
(@ip, 1, N'Identificación de los trámites a digitalizar',
     N'Constancia de Situación Catastral y Constancia SURE identificadas como candidatas, además de los servicios de libertad de gravamen e íntegra de asiento.',
     N'Completado', '2026-06-04'),
(@ip, 2, N'Documentación de respaldo y flujos de proceso',
     N'Flujo de ventanilla, escaneo y número de expediente, mesa técnica, inspección de predio y vectorización de polígonos.',
     N'EnProceso', NULL),
(@ip, 3, N'Fórmulas oficiales de cálculo de costos',
     N'El IP debe validar el costo de cada servicio y entregar las fórmulas oficiales de cálculo.',
     N'Pendiente', NULL),
(@ip, 4, N'Modelo del recibo de pago por servicios en SOL',
     N'El recibo se emite únicamente desde SOL, con cobro mínimo inicial y recibo complementario cuando el funcionario verifica que faltan hojas. Evita que un mismo recibo sirva para varias gestiones.',
     N'EnProceso', NULL),
(@ip, 5, N'API IP — SOL e integración con SURE',
     N'Ajustes de la API por parte de PNUD-SIGOB para soportar el recibo de pago, y envío de la solicitud a SURE al iniciar gestión.',
     N'EnProceso', NULL),
(@ip, 6, N'Depuración del material público sobre costos',
     N'Retirar de las plataformas públicas el material que contradice la fórmula real de cálculo, en particular de libertad de gravamen e íntegra de asiento.',
     N'Pendiente', NULL),
(@ip, 7, N'Piloto de los servicios en SOL',
     N'Puesta en marcha del ciclo completo: revisión de la solicitud, inicio de gestión hacia SURE y finalización con entrega digital del documento al ciudadano.',
     N'Pendiente', NULL),

-- ── SOL — SENASA ─────────────────────────────────────────────────────────────
(@senasa, 4, N'Capacitación por grupos funcionales y tipo de trámite',
     N'Metodología acordada: capacitar por trámite y grupo funcional con casos prácticos, empezando por Zoosanitario y siguiendo con Fitosanitario.',
     N'EnProceso', NULL),
(@senasa, 5, N'Integración SOL — SEFIN para la generación del TGR',
     N'Revisión de la lógica de integración con los servicios de SEFIN e incorporación del TGR de los demás servicios al módulo de configuración.',
     N'EnProceso', NULL),
(@senasa, 6, N'Pruebas funcionales con expedientes de prueba',
     N'Validación de los trámites en la plataforma con el RTN de pruebas, previo a simulaciones con escenarios reales de operación.',
     N'EnProceso', NULL),
(@senasa, 7, N'Interoperabilidad con SIECA y Aduanas',
     N'Validación de las APIs de interoperabilidad en los procesos de exportación.',
     N'Pendiente', NULL),
(@senasa, 8, N'Ajustes en los certificados de exportación',
     N'Bloquear la presentación del trámite si el timbre electrónico no se generó; ocultar nombre común y científico cuando la categoría de exportación no es Animal; verificar los formatos de China, COMIECO e internacionales.',
     N'Pendiente', NULL),
(@senasa, 9, N'Migración a la infraestructura tecnológica institucional',
     N'Traslado de los servicios a la infraestructura que corresponde a la institución.',
     N'Pendiente', NULL),

-- ── Expediente Digital GEXFILE — SDE ─────────────────────────────────────────
(@gexf, 1, N'Presentación del expediente digital en producción',
     N'Demostración a la SDE del funcionamiento en producción: consulta interna por RTN y metadatos, registro por el ciudadano, revisión y aprobación del funcionario, histórico de cambios, y depósito automático de los documentos que emite la institución.',
     N'Completado', '2026-06-25'),
(@gexf, 2, N'Reutilización documental en nuevos trámites',
     N'El ciudadano anexa en línea un documento que ya está en su expediente, en lugar de volver a cargarlo. Es el corazón del expediente digital.',
     N'EnProceso', NULL),
(@gexf, 7, N'Trámite de baja o inactivación de expedientes',
     N'Se descartó la eliminación física por su valor de memoria histórica; se implementará la baja lógica mediante un trámite ad-hoc.',
     N'Pendiente', NULL),

-- ── SOL — SRECI ──────────────────────────────────────────────────────────────
(@sreci, 6, N'Implementación del pago TGR y la firma electrónica',
     N'El pago TGR es una incorporación reciente de la plataforma; la firma electrónica va nativa mediante los servicios de la DIGER, sin costo adicional para la institución, y requiere un certificado para el piloto de emisión.',
     N'EnProceso', NULL);

/* Actualiza los que ya existen y agrega los nuevos, sin borrar ni renumerar los que no
   aparecen: los avances ya imputados a un hito no deben perder su referencia. */
UPDATE hp
SET hp.Descripcion = h.Descripcion,
    hp.Estado      = h.Estado,
    hp.FechaReal   = h.FechaReal
FROM ProyectoEntregables hp
JOIN @h h ON h.Proy = hp.ProyectoId AND h.Nombre = hp.Nombre;

INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT h.Proy,
       /* Los hitos nuevos van al final, después del mayor orden que ya tenga el proyecto. */
       (SELECT ISNULL(MAX(x.Orden), 0) FROM ProyectoEntregables x WHERE x.ProyectoId = h.Proy) + h.Orden,
       h.Nombre, h.Descripcion, NULL, h.FechaReal, h.Estado, NULL, NULL
FROM @h h
WHERE NOT EXISTS (SELECT 1 FROM ProyectoEntregables x WHERE x.ProyectoId = h.Proy AND x.Nombre = h.Nombre);

/* «Reunión de seguimiento» no es un entregable: entró en la carga inicial derivada de un
   acuerdo suelto y ahora el proyecto tiene hitos de verdad. Se retira solo si nadie le imputó
   un avance. */
DELETE FROM ProyectoEntregables
WHERE ProyectoId = @ip AND Nombre = N'Reunión de seguimiento'
  AND NOT EXISTS (SELECT 1 FROM ProyectoAvances a WHERE a.EntregableId = ProyectoEntregables.Id);

-- ════════ 3. Bitácora ═══════════════════════════════════════════════════════
DECLARE @a TABLE (
    Proy int, Fecha datetime2, Pct int, HitoNombre nvarchar(300),
    Descripcion nvarchar(2000), Bloqueo nvarchar(1000) NULL,
    ArchivoNombre nvarchar(300), ArchivoUrl nvarchar(500), ArchivoTamano bigint
);

INSERT INTO @a VALUES
(@ip, '2026-06-04T15:00:00', 15, N'Identificación de los trámites a digitalizar',
 N'Acercamiento del Instituto de la Propiedad con la SDE, la DIGER y el PNUD para socializar las capacidades de la Plataforma SOL. Se presentaron las características que debe reunir un trámite para digitalizarse y se recorrió el flujo de atención de trámites ya digitalizados por la SDE. El IP expuso el flujo de sus servicios sobre predios —ventanilla, escaneo y número de expediente, mesa técnica, inspección del predio y vectorización de polígonos— con un costo actual de L 200.00 por recibo de la TGR. Se identificó preliminarmente la Constancia de Situación Catastral como candidata, y se mencionó la Constancia SURE para analizar en sesiones posteriores.',
 NULL, N'Ayuda_Memoria_IP_SDE_DIGER_PNUD_04-jun-2026.pdf', N'/uploads/proyectos/08c269769d7753559c8bd85ad3c40339.pdf', 324250),

(@ip, NULL, 30, N'Modelo del recibo de pago por servicios en SOL',
 N'[El acta no trae fecha; se registra con la fecha de este reporte.] Revisión técnica de las APIs del IP para los trámites en SOL. Quedó definido que el recibo de pago por servicios se emite únicamente desde SOL, para tener control integral sobre su emisión y evitar que un mismo recibo sirva para varias gestiones: se emite con el mínimo de hojas del servicio y, si no alcanza, el funcionario genera un recibo complementario y devuelve el trámite al solicitante. Se acordó que SOL es la plataforma receptora y el medio oficial de entrega 100 % digital del documento final, con tres acciones obligatorias del funcionario: revisar la solicitud, iniciar gestión —que la envía a SURE por la API— y finalizar el trámite para asociar el documento y notificar al ciudadano. El IP debe entregar las fórmulas oficiales de cálculo de costos, retirar de sus plataformas públicas el material que las contradice y compartir la versión más reciente de la documentación de la API; PNUD-SIGOB ajusta la modelación del recibo y la API.',
 N'Faltan las fórmulas oficiales de cálculo de costos del IP; sin ellas no se puede cerrar el monto de los servicios.',
 N'Ayuda_Memoria_revision_APIs_SOL-IP.docx', N'/uploads/proyectos/56a1fe05b0eae5861cbc9db10b34259e.docx', 17448),

(@senasa, '2026-06-11T15:00:00', 20, N'Capacitación por grupos funcionales y tipo de trámite',
 N'Taller técnico sobre TGR-1 y Timbre Electrónico impartido por el consultor del PNUD, orientado a los trámites digitalizados de SENASA y al Certificado Fitosanitario de Exportación. Se acordó la metodología de capacitación por trámite y grupo funcional con casos prácticos, el uso inicial de expedientes de prueba y, después, simulaciones cercanas a la operación real. Se revisó el módulo de configuración del TGR y se confirmó la necesidad de validar la interoperabilidad con SIECA y Aduanas. Quedaron identificados ajustes en los certificados de exportación y la revisión, junto al equipo del PNUD, de la lógica de integración con los servicios de SEFIN.',
 NULL, N'Ayuda_Memoria_SENASA_TGR1.pdf', N'/uploads/proyectos/03c907782f2181a00351b0686f4687cf.pdf', 235565),

(@gexf, '2026-06-25T15:00:00', 40, N'Presentación del expediente digital en producción',
 N'Presentación a la SDE del expediente digital GEXFILE en ambiente de producción, con foco en personas jurídicas. Se mostró la consulta interna por RTN y metadatos, la estructura del expediente —datos principales, documentos aprobados por la institución y anexos del ciudadano—, el registro por el ciudadano con acreditación de su calidad de actuación, la revisión y aprobación del funcionario con histórico de cambios, la actualización mediante trámite específico y el depósito automático de los documentos que emite la SDE. El punto central quedó demostrado: un documento que ya está en el expediente se anexa en línea al trámite en curso, sin volver a cargarlo. Quedaron abiertas tres definiciones institucionales y jurídicas de la SDE: el eventual cobro por el servicio, el límite de almacenamiento y la política de baja de expedientes, para la que se prefirió la inactivación lógica sobre la eliminación, por el valor de memoria histórica del expediente.',
 NULL, N'Ayuda_Memoria_GEXFILE-SOL_25-jun-2026.docx', N'/uploads/proyectos/f473f2ee4b488d701ace8dd26fedcb23.docx', 29099),

(@sreci, '2026-07-08T15:00:00', 10, N'Análisis y diagnóstico del modelado actual',
 N'Reunión técnica presencial de seguimiento a la integración de la Cancillería con SOL. La institución usa hoy la plataforma sobre todo para solicitudes en línea, mientras el trámite de fondo —apostillas y auténticas— se procesa en su propio sistema. El punto central fue la arquitectura: la Cancillería opera 24/7 para consulados en varios países sobre microservicios y no quiere depender de un sistema monolítico ni perder propiedad sobre sus procesos; pidió conectarse a SOL por APIs de doble vía. La DIGER aclaró que no busca imponer un esquema monolítico sino garantizar servicios e integrar instituciones, y reconoció la validez de la postura. Se revisó el pago TGR —incorporación reciente, con SENASA como primera institución en implementarlo— y la firma electrónica, que SOL incorpora de forma nativa sin costo adicional y que requiere un certificado para el piloto. Se acordó iniciar una fase de análisis y diagnóstico con mesas de trabajo para revisar el modelado completo e identificar puntos de dolor.',
 NULL, N'Registro_SRECI_2026-07-08.pdf', N'/uploads/proyectos/2efdf6030e83e5fe5e7564ff79d8a0f1.pdf', 155711);

INSERT INTO ProyectoAvances
    (ProyectoId, EntregableId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT a.Proy,
       (SELECT TOP 1 h.Id FROM ProyectoEntregables h WHERE h.ProyectoId = a.Proy AND h.Nombre = a.HitoNombre),
       ISNULL(a.Fecha, @hoy), @actor, a.Descripcion, a.Pct, a.Bloqueo,
       a.ArchivoNombre, a.ArchivoUrl, a.ArchivoTamano
FROM @a a
WHERE NOT EXISTS (SELECT 1 FROM ProyectoAvances x WHERE x.ProyectoId = a.Proy AND x.Descripcion = a.Descripcion);

-- ════════ 4. Snapshot del avance ════════════════════════════════════════════
/* Se recalcula desde el ÚLTIMO reporte de cada proyecto, no desde el que se acaba de
   insertar: la entrada de SRECI es del 8 de julio y no debe pisar el 25 % del 19 de agosto. */
UPDATE p
SET p.AvancePct = u.Pct, p.UpdatedAt = @hoy, p.UpdatedBy = @actor
FROM Proyectos p
CROSS APPLY (
    SELECT TOP 1 a.PorcentajeReportado AS Pct
    FROM ProyectoAvances a WHERE a.ProyectoId = p.Id
    ORDER BY a.Fecha DESC, a.Id DESC
) u
WHERE p.Id IN (@ip, @senasa, @gexf, @sreci);

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT p.Codigo, LEFT(p.Nombre, 34) AS Proyecto, p.Estado, p.AvancePct AS Pct,
       COUNT(h.Id) AS Hitos,
       SUM(CASE WHEN h.Estado = N'Completado' THEN 1 ELSE 0 END) AS Cumplidos,
       (SELECT COUNT(*) FROM ProyectoAvances a WHERE a.ProyectoId = p.Id) AS Reportes
FROM Proyectos p
LEFT JOIN ProyectoEntregables h ON h.ProyectoId = p.Id
WHERE p.Id IN (@ip, @senasa, @gexf, @sreci)
GROUP BY p.Id, p.Codigo, p.Nombre, p.Estado, p.AvancePct
ORDER BY p.Codigo;
