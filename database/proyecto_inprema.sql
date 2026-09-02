/*
    Alta del proyecto «SOL — INPREMA», a partir del informe de situación institucional
    «INPREMA — Plataforma SOL», elaborado por Henry Alejandro Cardona Hércules el 2026-08-24,
    que cubre del 29 de junio al 24 de agosto de 2026.

    Situación al alta: los seis trámites están digitalizados y en producción, pero el beneficio
    de la digitalización se diluye por tres causas —inconsistencias entre pantalla e impresión,
    retorno al papel por exigencias de firma y huella, y comprensión limitada del alcance de los
    servicios— más una pérdida de capacidad instalada por rotación de personal. En la práctica la
    institución opera cinco de seis trámites: el de denuncias está detenido porque el departamento
    no tiene personal, no por causas técnicas.

    Responsable: Henry Cardona, que es quien gestiona la relación y firma el informe.

    Ojo con dos cosas al leer el portafolio:
      · Los tres compromisos de DIGER vencen el 26/08/2026 — dos días después de este informe.
      · La plataforma NO está migrada a la infraestructura del INPREMA, así que los tiempos de
        atención de las incidencias no dependen de la institución.

    Idempotente: reconoce el proyecto por Nombre, los hitos por (Proyecto, Nombre) y el avance
    por Descripción.

    Ejecución:
      sqlcmd -S localhost -U sa -P '...' -d DigerTramitesEstado -C -I -f 65001 \
             -i database/proyecto_inprema.sql
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

DECLARE @nombre    nvarchar(300)    = N'SOL — INPREMA';
DECLARE @cardona   uniqueidentifier = 'F49D4A33-267A-4E84-B7B1-364E7FF0AB4A';
DECLARE @cardonaN  nvarchar(200)    = N'Henry Cardona';
DECLARE @actor     nvarchar(200)    = N'Henry Alejandro Cardona Hércules';
DECLARE @hoy       datetime2        = SYSUTCDATETIME();
DECLARE @avancePct int              = 70;

BEGIN TRANSACTION;

-- ── Proyecto ────────────────────────────────────────────────────────────────
DECLARE @base int = (
    SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(Codigo, 10, 10) AS int)), 0)
    FROM Proyectos WHERE Codigo LIKE N'PRY-2026-%'
);

IF NOT EXISTS (SELECT 1 FROM Proyectos WHERE Nombre = @nombre AND IsDeleted = 0)
BEGIN
    INSERT INTO Proyectos
        (IsDeleted, Codigo, Nombre, Objetivo, InstitucionId, AreaId, UnidadId,
         ResponsableId, Responsable, Prioridad, Estado,
         FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, CreatedAt, CreatedBy)
    VALUES
        (0,
         N'PRY-2026-' + FORMAT(@base + 1, '00'),
         @nombre,
         N'Estabilizar y sostener la operación de los seis trámites del INPREMA en la Plataforma SOL: cerrar las incidencias de acceso y carga, corregir las inconsistencias entre la plataforma y el documento impreso, reducir el retorno al papel, reactivar el trámite de denuncias y transferir capacidades al equipo institucional.',
         -- El ancla es DIGER, que es quien ejecuta; área y unidad quedan transversales.
         N'DIGER', NULL, NULL,
         @cardona, @cardonaN, N'Alta', N'EnEjecucion',
         NULL,   -- la implementación es anterior al informe; no hay fecha de arranque registrada
         NULL,   -- sin fecha de cierre comprometida
         NULL,
         NULL, @avancePct, @hoy, @actor);
END

DECLARE @proy int = (SELECT Id FROM Proyectos WHERE Nombre = @nombre AND IsDeleted = 0);

-- ── Hitos ───────────────────────────────────────────────────────────────────
DECLARE @h TABLE (Orden int, Nombre nvarchar(300), Descripcion nvarchar(2000), Estado nvarchar(30));

INSERT INTO @h (Orden, Nombre, Descripcion, Estado) VALUES
( 1, N'Digitalización y puesta en producción de los seis trámites',
     N'Seis trámites digitalizados y activos en producción, respaldados por un acta suscrita durante la implementación: actualización de datos docentes, cuatro preanálisis de préstamos (Rapibono, hipotecario, personal y consolidación de deudas) y registro y atención de denuncias.',
     N'Completado'),

( 2, N'Corrección de incidencias de acceso y carga de archivos',
     N'El equipo de infraestructura aplicó dos correcciones: excepción para la ruta /sigob, que resolvía el error al extraer las librerías JS, y regla de excepción para /wsapi/api/ acotada a solicitudes POST de carga de archivos, manteniendo el resto de validaciones de seguridad.',
     N'Completado'),

( 3, N'Validación de los cuatro escenarios de acceso',
     N'Pendiente desde el 2 de julio y sin respuesta registrada: acceso nacional desde distintas ubicaciones, acceso internacional, integración PDI–SOLPDI de extremo a extremo, y carga de archivos. Si persisten fallas hay que adjuntar log del navegador (consola y Network), hora de la prueba, usuario y URL.',
     N'EnProceso'),

( 4, N'Corrección de la impresión del formulario de actualización de datos',
     N'El colegio magisterial y la fotografía del docente están en la Plataforma SOL pero no salen en el formulario impreso que usa Afiliaciones para actualizar el sistema BYTE, así que el personal vuelve a la plataforma a consultarlos. Es el hallazgo con la corrección más acotada y el de mayor impacto inmediato en la carga de trabajo del área.',
     N'Pendiente'),

( 5, N'Consolidación documental y expediente digital del docente',
     N'Compromiso de DIGER con plazo al 26/08/2026: trasladar a revisión interna las mejoras del trámite de actualización de datos. Hoy los requisitos se remiten por separado y en formatos dispares —PDF, JPG, fotos de celular—, con calidad desigual. Consolidarlos en un solo documento digital reduciría trabajo manual, pero debe analizarse contra las obligaciones internas de firma, huella y conservación.',
     N'Pendiente'),

( 6, N'Instructivos, videos y socialización al docente',
     N'Compromiso de DIGER con plazo al 26/08/2026: verificar disponibilidad y vigencia de los materiales de orientación. Se confirmó que existe material del preanálisis de préstamo personal, pero no hay certeza sobre el resto. Cuatro de los seis trámites son preanálisis afectados por la misma causa, así que una sola acción de comunicación incide sobre dos tercios del portafolio; conviene anticiparla al ciclo de Rapibono, no ejecutarla durante.',
     N'EnProceso'),

( 7, N'Reactivación del trámite de atención de denuncias',
     N'El Departamento de Derechos Humanos no tiene personal para operar el servicio y está en proceso de contratación. El trámite es funcional: está inactivo por ausencia de personal, no por causas técnicas. Una vez incorporado el equipo hace falta capacitarlo para reactivarlo.',
     N'Pendiente'),

( 8, N'Transferencia de capacidades y mesa técnica institucional',
     N'La rotación dejó a la institución sin parte del conocimiento operativo original de la plataforma. DIGER ofrece talleres de administración, soporte y uso, con acompañamiento sobre el marco legal, preferentemente presenciales cuando se trate de transferencia de capacidades. Incluye actualizar la mesa técnica con los funcionarios vigentes y fijar un punto de contacto estable con Infotecnología del INPREMA.',
     N'Pendiente'),

( 9, N'Integraciones y reducción del retorno al papel',
     N'Compromiso de DIGER con plazo al 26/08/2026: revisar las posibilidades de integración y automatización. Consultar las bases institucionales por DNI evitaría digitación repetida, pero la formalización de préstamos exige validación biométrica y acreditación segura de identidad y voluntad, por lo que los préstamos se mantienen a nivel de preanálisis. Requiere una sesión con acompañamiento legal para delimitar qué exigencias de firma y huella son ineludibles.',
     N'Pendiente'),

(10, N'Migración de la Plataforma SOL a la infraestructura del INPREMA',
     N'La plataforma no está migrada a la institución, así que las incidencias se resuelven fuera del INPREMA y sus tiempos de atención no dependen de él. El personal técnico está disponible solo parcialmente y la institución pide acompañamiento expreso.',
     N'Pendiente');

UPDATE hp
SET hp.Orden       = h.Orden,
    hp.Descripcion = h.Descripcion,
    hp.Estado      = h.Estado
FROM ProyectoEntregables hp
JOIN @h h ON h.Nombre = hp.Nombre
WHERE hp.ProyectoId = @proy;

INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable)
SELECT @proy, h.Orden, h.Nombre, h.Descripcion, NULL, NULL, h.Estado, NULL, NULL
FROM @h h
WHERE NOT EXISTS (SELECT 1 FROM ProyectoEntregables x WHERE x.ProyectoId = @proy AND x.Nombre = h.Nombre);

-- ── Bitácora ────────────────────────────────────────────────────────────────
DECLARE @desc nvarchar(2000) = N'Informe de situación institucional del 24 de agosto, que cubre del 29 de junio al 24 de agosto. Los seis trámites están digitalizados y en producción, pero en la práctica la institución opera cinco: el de atención de denuncias está detenido porque el Departamento de Derechos Humanos no tiene personal, y está en proceso de contratación. El diagnóstico es que la plataforma funciona y el beneficio de la digitalización se diluye por tres causas: inconsistencias entre lo que muestra la plataforma y lo que sale impreso —el colegio magisterial y la fotografía del docente no aparecen en el formulario—, un retorno sistemático al papel por exigencias de firma, huella y resguardo documental, y comprensión limitada del alcance del preanálisis de préstamos, que cuatro de los seis trámites comparten. En lo técnico, infraestructura aplicó dos correcciones —excepción para /sigob y regla para la carga por POST en /wsapi/api/— y quedaron sin cerrar desde el 2 de julio los cuatro escenarios de validación: acceso nacional, acceso internacional, integración PDI–SOLPDI y carga de archivos. La reunión de levantamiento se acordó para el 4 de agosto y se ejecutó el 18, con dos semanas de desfase. De ella salieron tres compromisos de DIGER con plazo al 26 de agosto: verificar la vigencia de instructivos y videos, trasladar a revisión interna las mejoras de actualización de datos, y revisar las posibilidades de integración y automatización.';

INSERT INTO ProyectoAvances
    (ProyectoId, EntregableId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo,
     ArchivoNombre, ArchivoUrl, ArchivoTamano)
SELECT @proy,
       (SELECT TOP 1 Id FROM ProyectoEntregables
        WHERE ProyectoId = @proy AND Nombre = N'Validación de los cuatro escenarios de acceso'),
       '2026-08-24T09:00:00', @actor, @desc, @avancePct,
       N'Dos frentes quedan fuera del control de DIGER: la reactivación del trámite de denuncias depende de las contrataciones del INPREMA, y la plataforma no está migrada a la institución, así que los tiempos de atención de incidencias los fija un tercero. Además, la rotación dejó a la institución sin parte de los administradores originales, y las jornadas de transferencia de capacidades dependen de que designe equipos permanentes.',
       N'Informe_INPREMA_PlataformaSOL.docx',
       N'/uploads/proyectos/5c3c22726e29aa0962c958a780a58e63.docx', 20179
WHERE NOT EXISTS (SELECT 1 FROM ProyectoAvances WHERE ProyectoId = @proy AND Descripcion = @desc);

UPDATE p
SET p.AvancePct = u.Pct, p.UpdatedAt = @hoy, p.UpdatedBy = @actor
FROM Proyectos p
CROSS APPLY (SELECT TOP 1 a.PorcentajeReportado AS Pct FROM ProyectoAvances a
             WHERE a.ProyectoId = p.Id ORDER BY a.Fecha DESC, a.Id DESC) u
WHERE p.Id = @proy;

/* La auditoría del proyecto no la escriben los scripts —se alimenta desde los comandos de la
   app— así que el alta por esta vía queda registrada acá a mano, para que el historial no
   arranque en blanco. */
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT @proy, N'ModificacionFicha',
       N'Alta del proyecto desde el informe de situación institucional del 24/08/2026, por script.',
       N'script-proyecto-inprema', @hoy
WHERE NOT EXISTS (SELECT 1 FROM BitacoraProyecto WHERE ProyectoId = @proy);

COMMIT;

-- ── Resultado ───────────────────────────────────────────────────────────────
SELECT Codigo, Estado, Prioridad, AvancePct AS Pct, Responsable,
       ISNULL(InstitucionId, '(sin ancla)') AS Institucion
FROM Proyectos WHERE Id = @proy;

SELECT h.Orden, LEFT(h.Nombre, 56) AS Hito, h.Estado
FROM ProyectoEntregables h WHERE h.ProyectoId = @proy ORDER BY h.Orden;
