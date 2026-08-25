/*
    Actualiza PRY-2026-16 «Componente 3 SENPRENDE — constitución de empresas» con el acta del
    2026-07-16: «Plan de Integración para la Digitalización de la Constitución de Empresas
    (Mi Empresa en Línea) — DIGER / IP / SENPRENDE», reunión legal e interinstitucional,
    firmada digitalmente el 2026-07-28.

    Los cinco acuerdos del acta caen casi uno a uno sobre los hitos que ya tenía el proyecto,
    así que sobre todo se les pone estado y descripción; se agregan dos que el acta descubre:
    el flujo legal-operativo de Mi Empresa en Línea y la apertura de la Cámara de Comercio de
    la zona norte.

    El proyecto **no se renombra**: se enriquece el objetivo para que quede registrado que la
    iniciativa se llama «Mi Empresa en Línea», sin cambiar la referencia con la que se creó.

    Idempotente: hitos por (Proyecto, Nombre), avance por Descripción.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/actualizar_senprende_c3.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @actor nvarchar(200) = N'Henry Alexis Ortez Banegas';
DECLARE @hoy   datetime2     = SYSUTCDATETIME();
DECLARE @p     int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-16' AND IsDeleted = 0);
IF @p IS NULL BEGIN RAISERROR(N'No se encontró PRY-2026-16.', 16, 1); RETURN; END

BEGIN TRANSACTION;

UPDATE Proyectos
SET Objetivo = N'Digitalizar el 100 % del registro mercantil y societario mediante la iniciativa «Mi Empresa en Línea», integrando la firma electrónica avanzada y la Billetera Electrónica Nacional en la inscripción registral y la constitución de empresas, junto con el Instituto de la Propiedad y SENPRENDE.',
    UpdatedAt = @hoy, UpdatedBy = @actor
WHERE Id = @p;

-- ── Hitos ───────────────────────────────────────────────────────────────────
DECLARE @h TABLE (Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30));

INSERT INTO @h VALUES
(N'Firma avanzada y Billetera Nacional en inscripción registral',
 N'Componentes interconectados de la Billetera Electrónica Nacional y el Registro Nacional de las Personas: escaneo, sello de tiempo que da inviolabilidad a las fechas y validación de que el certificado digital sigue vigente y no revocado. Se evalúa restringir el sistema a firmas emitidas bajo la autoridad certificadora de la DIGER.',
 N'EnProceso'),

(N'Convenios interinstitucionales para el uso de la billetera',
 N'Convenios formales entre autoridades para constituir la cadena de confianza digital, dejando establecido que el consumo de estos certificados gubernamentales tiene costo cero entre entidades del Estado.',
 N'EnProceso'),

(N'Reglamentos e instrumentos legales con IP y SENPRENDE',
 N'Adecuar los reglamentos internos que todavía exigen firma manuscrita para que admitan la firma digital avanzada de certificadores reconocidos, con el respaldo de la ley que equipara sus efectos a los del documento en papel. Incluye la mesa con la Dirección de Registro y la Superintendencia de Recursos del IP.',
 N'EnProceso'),

(N'Fórmulas de cobro de tasas registrales y su automatización',
 N'Extraer las estructuras de cobro de tasas registrales y de matrícula mercantil de los recibos vigentes del IP y de las Cámaras de Comercio de Tegucigalpa, San Pedro Sula y Choluteca, y estructurarlas en un formato editable para integrarlas a la pasarela de pagos.',
 N'EnProceso'),

(N'Flujo legal-operativo de Mi Empresa en Línea',
 N'Definición y validación del flujo completo. El 80 % de los registros actuales son unipersonales, lo que permite arrancar por ahí; el 20 % restante requiere un facilitador. El registrador valida la homonimia del nombre, apoyado en la unidad de Registradores Itinerantes que opera en todo el país.',
 N'EnProceso'),

(N'Apertura de la Cámara de Comercio de la zona norte al flujo',
 N'San Pedro Sula maneja sus propias directrices financieras, lo que hoy obliga al usuario a hacer visitas presenciales. Requiere relevar sus tasas registrales y la interoperabilidad de sus sistemas.',
 N'Pendiente');

UPDATE hp
SET hp.Descripcion = h.Descripcion, hp.Estado = h.Estado
FROM ProyectoHitos hp JOIN @h h ON h.Nombre = hp.Nombre
WHERE hp.ProyectoId = @p;

INSERT INTO ProyectoHitos (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT @p,
       (SELECT ISNULL(MAX(Orden),0) FROM ProyectoHitos WHERE ProyectoId = @p)
         + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)),
       h.Nombre, h.Descripcion, NULL, NULL, h.Estado, NULL, NULL
FROM @h h
WHERE NOT EXISTS (SELECT 1 FROM ProyectoHitos x WHERE x.ProyectoId = @p AND x.Nombre = h.Nombre);

-- ── Bitácora ────────────────────────────────────────────────────────────────
DECLARE @desc nvarchar(2000) = N'Reunión legal e interinstitucional entre DIGER, el Instituto de la Propiedad y SENPRENDE para definir y validar el flujo legal-operativo que digitalice el 100 % del registro mercantil y societario. Se repasaron los componentes interconectados de la Billetera Electrónica Nacional y el RNP —escaneo, sello de tiempo para la inviolabilidad de fechas y validación de vigencia de los certificados— y se planteó restringir el sistema a firmas emitidas bajo la autoridad certificadora de la DIGER. Sobre el flujo de Mi Empresa en Línea se estableció que el 80 % de los registros actuales son unipersonales, lo que permite arrancar por ahí, mientras el 20 % restante requiere facilitador; el registrador valida la homonimia del nombre apoyado en la unidad de Registradores Itinerantes. Quedaron cinco compromisos, todos abiertos: redactar los convenios que formalicen la cadena de confianza digital con costo cero entre entidades del Estado; adecuar los reglamentos internos que exigen firma manuscrita; formalizar el marco legal ante la Dirección de Registro y la Superintendencia de Recursos del IP; relevar en San Pedro Sula las tasas registrales y la interoperabilidad de sus sistemas; y extraer las estructuras de cobro de las Cámaras de Comercio de Tegucigalpa, San Pedro Sula y Choluteca para integrarlas a la pasarela de pagos.';

INSERT INTO ProyectoAvances
    (ProyectoId, HitoId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT @p,
       (SELECT TOP 1 Id FROM ProyectoHitos WHERE ProyectoId = @p AND Nombre = N'Flujo legal-operativo de Mi Empresa en Línea'),
       '2026-07-16T20:00:00', @actor, @desc, 20,
       N'Los reglamentos internos del IP y de SENPRENDE todavía exigen firma manuscrita. Hasta adecuarlos no hay sustento normativo para el flujo con firma digital avanzada, aunque la ley ya equipare sus efectos a los del papel.',
       N'Acta_Mi_Empresa_en_Linea_16-jul-2026.pdf', N'/uploads/proyectos/07936590d38ef3745d6fec6f8905c193.pdf', 266469
WHERE NOT EXISTS (SELECT 1 FROM ProyectoAvances WHERE ProyectoId = @p AND Descripcion = @desc);

UPDATE p
SET p.AvancePct = u.Pct, p.UpdatedAt = @hoy, p.UpdatedBy = @actor
FROM Proyectos p
CROSS APPLY (SELECT TOP 1 a.PorcentajeReportado AS Pct FROM ProyectoAvances a
             WHERE a.ProyectoId = p.Id ORDER BY a.Fecha DESC, a.Id DESC) u
WHERE p.Id = @p;

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT Codigo, Estado, AvancePct AS Pct FROM Proyectos WHERE Id = @p;
SELECT h.Orden, LEFT(h.Nombre, 54) AS Hito, h.Estado
FROM ProyectoHitos h WHERE h.ProyectoId = @p ORDER BY h.Orden;
