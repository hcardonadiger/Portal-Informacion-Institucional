/*
    Proyecto DEMOSTRATIVO para la etapa de validación con usuarios.

    Qué es
    ------
    Un proyecto completo, de principio a fin, que ejercita TODAS las capacidades del módulo con
    datos coherentes entre sí. Sirve para mostrarle a un usuario cómo se ve un proyecto bien
    llevado, en vez de explicarle campos vacíos sobre el portafolio de línea base —que se cargó
    justamente para eso, para tener línea base, y al que todavía le falta el proceso de asignación
    de responsables y la actualización de fechas.

    Está marcado como DEMO en el nombre y en el objetivo, y usa una institución inventada
    («Instituto Modelo»), para que nadie lo confunda con trabajo real ni lo cuente en el tablero
    como un frente activo de verdad.

    Qué ejercita
    ------------
      · EDT real de 3 niveles: 4 entregables que son PRODUCTOS verificables, no tareas.
      · 12 actividades con ventana (inicio y fin), responsable y avance coherente.
      · 11 dependencias Fin → Comienzo, incluida una actividad bloqueada de verdad.
      · 4 interesados con rol e influencia — que es lo que habilita asignar responsables.
      · 3 riesgos: uno materializado, uno abierto con mitigación y uno cerrado.
      · 9 entradas de bitácora repartidas en el tiempo, con evidencia y con un bloqueo vigente.
      · 3 documentos, uno de ellos con dos versiones (muestra la corrección de un acta).
      · Estados y fechas reales consistentes con los porcentajes.

    Avance: se calcula, no se inventa. E1 100 %, E2 100 %, E3 53 %, E4 0 % → proyecto 63 %.
    (E3 = promedio de 100, 60 y 0 = 53,33 → 53. Proyecto = promedio de 100, 100, 53 y 0 = 63,25 → 63.)

    Requisito previo
    ----------------
    Los 4 PDF de los documentos tienen que existir bajo App_Data/uploads/proyectos/documentos/.
    Los genera `node database/demo/generar-documentos.js <ruta>`; si faltan, el portal muestra el
    documento pero la descarga avisa que el archivo ya no está disponible. Los nombres GUID del
    script tienen que coincidir con los que quedaron en disco.

    Idempotente: reconoce el proyecto por su código y no hace nada si ya está cargado.
    Para recargarlo desde cero, borrar antes con el bloque comentado del final.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @codigo nvarchar(30) = N'PRY-2026-99';
DECLARE @actor  nvarchar(200) = N'Carga demostrativa';

IF EXISTS (SELECT 1 FROM Proyectos WHERE Codigo = @codigo)
BEGIN
    SELECT N'El proyecto demostrativo ya está cargado. No se hizo nada.' AS Resultado;
    RETURN;
END

/* Las personas del demo salen de usuarios reales del portal: un interesado sin cuenta no podría
   abrir el proyecto, y el módulo exige que el responsable de una actividad sea interesado. */
DECLARE @patrocinador uniqueidentifier = (SELECT TOP 1 Id FROM Usuarios WHERE Correo = N'hortez@diger.gob.hn');
DECLARE @lider        uniqueidentifier = (SELECT TOP 1 Id FROM Usuarios WHERE Correo = N'bzelaya@diger.gob.hn');
DECLARE @tecnico      uniqueidentifier = (SELECT TOP 1 Id FROM Usuarios WHERE Correo = N'cmaldonado@diger.gob.hn');
DECLARE @soporte      uniqueidentifier = (SELECT TOP 1 Id FROM Usuarios WHERE Correo = N'soporte.plataforma@diger.gob.hn');

IF @patrocinador IS NULL OR @lider IS NULL OR @tecnico IS NULL OR @soporte IS NULL
BEGIN
    SELECT N'Faltan usuarios base en esta instancia. Revise los correos del script.' AS Resultado;
    RETURN;
END

DECLARE @nombrePatro nvarchar(200) = (SELECT Nombre FROM Usuarios WHERE Id = @patrocinador);
DECLARE @nombreLider nvarchar(200) = (SELECT Nombre FROM Usuarios WHERE Id = @lider);
DECLARE @nombreTec   nvarchar(200) = (SELECT Nombre FROM Usuarios WHERE Id = @tecnico);
DECLARE @nombreSop   nvarchar(200) = (SELECT Nombre FROM Usuarios WHERE Id = @soporte);

BEGIN TRANSACTION;

/* ── Proyecto ─────────────────────────────────────────────────────────────── */
INSERT INTO Proyectos
    (IsDeleted, Codigo, Nombre, Objetivo, InstitucionId, AreaId, UnidadId,
     ResponsableId, Responsable, Prioridad, Estado,
     FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, CreatedAt, CreatedBy)
VALUES
    (0, @codigo,
     N'DEMO — Incorporación del Instituto Modelo a SOL',
     N'PROYECTO DEMOSTRATIVO, no corresponde a un frente real. Existe para mostrar cómo se ve un '
   + N'proyecto completo en el portal: EDT con entregables verificables, cronograma con fechas, '
   + N'dependencias, riesgos, interesados, bitácora con evidencia y repositorio documental. '
   + N'Objetivo simulado: incorporar 6 trámites del Instituto Modelo a la plataforma SOL y dejar '
   + N'capacitado a su personal operativo.',
     N'DIGER', N'GOBDIG', N'DITRA',
     @lider, @nombreLider, N'Alta', N'EnEjecucion',
     '2026-05-04', '2026-10-30', '2026-05-06', NULL, 63, SYSUTCDATETIME(), @actor);

DECLARE @p int = SCOPE_IDENTITY();

/* ── Interesados ──────────────────────────────────────────────────────────────
   Van primero: el módulo solo deja poner como responsable de un entregable o de una actividad
   a alguien que ya esté registrado acá. Es el paso que en el portafolio real todavía falta. */
INSERT INTO ProyectoInteresados
    (ProyectoId, UsuarioId, Nombre, Correo, Institucion, Cargo, Rol, Influencia, Notas, RegistradoPor, RegistradoEn)
VALUES
    (@p, @patrocinador, @nombrePatro, N'hortez@diger.gob.hn', N'DIGER',
     N'Dirección de Gobierno Digital', N'Patrocinador', N'Alta',
     N'Aprueba alcance y cambios de fecha.', @actor, SYSUTCDATETIME()),
    (@p, @lider, @nombreLider, N'bzelaya@diger.gob.hn', N'DIGER',
     N'Líder del frente de incorporación', N'Ejecutor', N'Alta',
     N'Responsable del proyecto y de la relación con la institución.', @actor, SYSUTCDATETIME()),
    (@p, @tecnico, @nombreTec, N'cmaldonado@diger.gob.hn', N'Instituto Modelo',
     N'Contraparte técnica', N'ContraparteTecnica', N'Alta',
     N'Valida las fichas y coordina al personal operativo.', @actor, SYSUTCDATETIME()),
    (@p, @soporte, @nombreSop, N'soporte.plataforma@diger.gob.hn', N'DIGER',
     N'Soporte de plataforma', N'Ejecutor', N'Media',
     N'Configura los trámites y acompaña la puesta en producción.', @actor, SYSUTCDATETIME());

/* ── Entregables ──────────────────────────────────────────────────────────────
   PRODUCTOS verificables, no tareas: es contra esto que se acepta y se cierra. Es exactamente el
   nivel que en el portafolio de línea base todavía está sin desglosar. */
DECLARE @e1 int, @e2 int, @e3 int, @e4 int;

INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
VALUES (@p, 1, N'Diagnóstico de trámites firmado por la institución',
        N'Inventario, priorización y acta firmada. Define el alcance de la primera fase.',
        '2026-05-29', '2026-05-29', N'Completado', @lider, @nombreLider, SYSUTCDATETIME(), @actor);
SET @e1 = SCOPE_IDENTITY();

INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
VALUES (@p, 2, N'Seis trámites configurados y validados en SOL',
        N'Fichas técnicas elaboradas, trámites configurados en ambiente de pruebas y validados por la contraparte.',
        '2026-07-31', '2026-08-07', N'Completado', @soporte, @nombreSop, SYSUTCDATETIME(), @actor);
SET @e2 = SCOPE_IDENTITY();

INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
VALUES (@p, 3, N'Personal operativo capacitado y evaluado',
        N'Doce operadores del Instituto Modelo en condiciones de atender los trámites en SOL.',
        '2026-09-18', NULL, N'EnProceso', @tecnico, @nombreTec, SYSUTCDATETIME(), @actor);
SET @e3 = SCOPE_IDENTITY();

INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
VALUES (@p, 4, N'Trámites en producción con acompañamiento cerrado',
        N'Puesta en producción, dos semanas de acompañamiento y acta de cierre del frente.',
        '2026-10-30', NULL, N'Pendiente', @lider, @nombreLider, SYSUTCDATETIME(), @actor);
SET @e4 = SCOPE_IDENTITY();

/* ── Actividades ──────────────────────────────────────────────────────────────
   El estado se deriva del porcentaje (0 Pendiente, 1-99 EnProceso, 100 Completada) y las fechas
   reales las sella el avance: pasar de 0 marca el inicio, llegar a 100 marca el cierre. Se
   respeta acá para que el portal no muestre combinaciones que él mismo nunca produciría. */
DECLARE @a table (Clave nvarchar(40), Id int);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'inventario', inserted.Id INTO @a
VALUES (@e1, 1, N'Inventario de trámites de la institución', N'Levantamiento de los 14 trámites vigentes.',
        '2026-05-06', '2026-05-15', '2026-05-06', '2026-05-14', 100, N'Completada', @lider, @nombreLider, SYSUTCDATETIME(), @actor);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'priorizacion', inserted.Id INTO @a
VALUES (@e1, 2, N'Priorización con la institución', N'Selección de los 6 trámites de la primera fase.',
        '2026-05-18', '2026-05-25', '2026-05-18', '2026-05-22', 100, N'Completada', @tecnico, @nombreTec, SYSUTCDATETIME(), @actor);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'acta', inserted.Id INTO @a
VALUES (@e1, 3, N'Firma del acta de diagnóstico', N'Acta con el alcance acordado.',
        '2026-05-26', '2026-05-29', '2026-05-26', '2026-05-29', 100, N'Completada', @lider, @nombreLider, SYSUTCDATETIME(), @actor);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'fichas', inserted.Id INTO @a
VALUES (@e2, 1, N'Elaboración de las fichas técnicas', N'Requisitos, plazos y dependencias de cada trámite.',
        '2026-06-01', '2026-06-26', '2026-06-01', '2026-06-30', 100, N'Completada', @soporte, @nombreSop, SYSUTCDATETIME(), @actor);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'configuracion', inserted.Id INTO @a
VALUES (@e2, 2, N'Configuración en ambiente de pruebas', N'Carga de los 6 trámites en SOL.',
        '2026-06-29', '2026-07-24', '2026-07-01', '2026-07-29', 100, N'Completada', @soporte, @nombreSop, SYSUTCDATETIME(), @actor);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'validacion', inserted.Id INTO @a
VALUES (@e2, 3, N'Validación con la contraparte técnica', N'Pruebas de aceptación con la institución.',
        '2026-07-27', '2026-07-31', '2026-07-30', '2026-08-07', 100, N'Completada', @tecnico, @nombreTec, SYSUTCDATETIME(), @actor);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'material', inserted.Id INTO @a
VALUES (@e3, 1, N'Elaboración del material de capacitación', N'Guía del operador, casos de práctica y evaluación.',
        '2026-08-10', '2026-08-14', '2026-08-10', '2026-08-13', 100, N'Completada', @soporte, @nombreSop, SYSUTCDATETIME(), @actor);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'capacitacion', inserted.Id INTO @a
VALUES (@e3, 2, N'Capacitación a los 12 operadores', N'Dos jornadas presenciales en la institución.',
        '2026-08-17', '2026-09-04', '2026-08-18', NULL, 60, N'EnProceso', @tecnico, @nombreTec, SYSUTCDATETIME(), @actor);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'evaluacion', inserted.Id INTO @a
VALUES (@e3, 3, N'Evaluación de la capacitación', N'Prueba de conocimientos y refuerzo donde haga falta.',
        '2026-09-07', '2026-09-18', NULL, NULL, 0, N'Pendiente', @tecnico, @nombreTec, SYSUTCDATETIME(), @actor);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'produccion', inserted.Id INTO @a
VALUES (@e4, 1, N'Puesta en producción de los 6 trámites', N'Publicación en SOL y aviso a la ciudadanía.',
        '2026-09-21', '2026-09-30', NULL, NULL, 0, N'Pendiente', @soporte, @nombreSop, SYSUTCDATETIME(), @actor);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'acompanamiento', inserted.Id INTO @a
VALUES (@e4, 2, N'Acompañamiento las dos primeras semanas', N'Soporte en sitio y corrección de incidencias.',
        '2026-10-01', '2026-10-16', NULL, NULL, 0, N'Pendiente', @soporte, @nombreSop, SYSUTCDATETIME(), @actor);

INSERT INTO ProyectoActividades (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
OUTPUT N'cierre', inserted.Id INTO @a
VALUES (@e4, 3, N'Acta de cierre del frente', N'Cierre formal con la institución y traspaso a operación.',
        '2026-10-19', '2026-10-30', NULL, NULL, 0, N'Pendiente', @lider, @nombreLider, SYSUTCDATETIME(), @actor);

DECLARE @inventario int = (SELECT Id FROM @a WHERE Clave = N'inventario');
DECLARE @prior      int = (SELECT Id FROM @a WHERE Clave = N'priorizacion');
DECLARE @acta       int = (SELECT Id FROM @a WHERE Clave = N'acta');
DECLARE @fichas     int = (SELECT Id FROM @a WHERE Clave = N'fichas');
DECLARE @config     int = (SELECT Id FROM @a WHERE Clave = N'configuracion');
DECLARE @valid      int = (SELECT Id FROM @a WHERE Clave = N'validacion');
DECLARE @material   int = (SELECT Id FROM @a WHERE Clave = N'material');
DECLARE @capacit    int = (SELECT Id FROM @a WHERE Clave = N'capacitacion');
DECLARE @evalua     int = (SELECT Id FROM @a WHERE Clave = N'evaluacion');
DECLARE @produccion int = (SELECT Id FROM @a WHERE Clave = N'produccion');
DECLARE @acompana   int = (SELECT Id FROM @a WHERE Clave = N'acompanamiento');
DECLARE @cierre     int = (SELECT Id FROM @a WHERE Clave = N'cierre');

/* ── Dependencias Fin → Comienzo ──────────────────────────────────────────────
   La cadena real del frente. «Puesta en producción» espera a dos cosas a la vez, y una de ellas
   —la capacitación— sigue abierta: por eso el portal la marca BLOQUEADA y aparece en el tablero.
   Es la situación que conviene mostrarle al usuario. */
INSERT INTO ProyectoDependenciasActividad (SucesoraId, PredecesoraId, Tipo) VALUES
    (@prior,      @inventario, N'FinComienzo'),
    (@acta,       @prior,      N'FinComienzo'),
    (@fichas,     @acta,       N'FinComienzo'),
    (@config,     @fichas,     N'FinComienzo'),
    (@valid,      @config,     N'FinComienzo'),
    (@capacit,    @material,   N'FinComienzo'),
    (@evalua,     @capacit,    N'FinComienzo'),
    (@produccion, @valid,      N'FinComienzo'),
    (@produccion, @capacit,    N'FinComienzo'),
    (@acompana,   @produccion, N'FinComienzo'),
    (@cierre,     @acompana,   N'FinComienzo');

/* ── Riesgos ──────────────────────────────────────────────────────────────────
   Uno de cada estado, para que se vea el ciclo completo del registro. */
DECLARE @r table (Clave nvarchar(20), Id int);

INSERT INTO ProyectoRiesgos (ProyectoId, Descripcion, Categoria, Probabilidad, Impacto, Estrategia, Estado, Mitigacion, ResponsableId, Responsable, FechaDeteccion, FechaRevision, FechaCierre, RegistradoPor, RegistradoEn)
OUTPUT N'rotacion', inserted.Id INTO @r
VALUES (@p, N'Rotación del personal capacitado antes de la puesta en producción',
        N'Operativo', N'Media', N'Alta', N'Mitigar', N'Abierto',
        N'Capacitar a 12 en vez de a los 8 mínimos y dejar el material publicado en el repositorio.',
        @tecnico, @nombreTec, '2026-08-12', '2026-09-25', NULL, @actor, SYSUTCDATETIME());

INSERT INTO ProyectoRiesgos (ProyectoId, Descripcion, Categoria, Probabilidad, Impacto, Estrategia, Estado, Mitigacion, ResponsableId, Responsable, FechaDeteccion, FechaRevision, FechaCierre, RegistradoPor, RegistradoEn)
OUTPUT N'agenda', inserted.Id INTO @r
VALUES (@p, N'La agenda de la institución retrasa las jornadas de capacitación',
        N'Externo', N'Alta', N'Media', N'Mitigar', N'Materializado',
        N'Se reprograma la segunda jornada y se habilita una sesión virtual de refuerzo.',
        @lider, @nombreLider, '2026-08-19', '2026-09-15', NULL, @actor, SYSUTCDATETIME());

INSERT INTO ProyectoRiesgos (ProyectoId, Descripcion, Categoria, Probabilidad, Impacto, Estrategia, Estado, Mitigacion, ResponsableId, Responsable, FechaDeteccion, FechaRevision, FechaCierre, RegistradoPor, RegistradoEn)
VALUES (@p, N'Las fichas técnicas no reflejan los requisitos vigentes',
        N'Tecnico', N'Media', N'Alta', N'Mitigar', N'Cerrado',
        N'Validación cruzada con la contraparte antes de configurar. Cerrado tras las pruebas de aceptación.',
        @soporte, @nombreSop, '2026-06-10', '2026-07-20', '2026-08-07', @actor, SYSUTCDATETIME());

DECLARE @riesgoAgenda int = (SELECT Id FROM @r WHERE Clave = N'agenda');

/* ── Bitácora de ejecución ────────────────────────────────────────────────────
   OJO CON LA HORA: la columna se guarda en UTC y la vista hace ToLocalTime(). Honduras es UTC-6
   todo el año, así que a la hora local hay que SUMARLE 6 al insertar, o el portal la muestra
   seis horas antes. */
INSERT INTO ProyectoAvances (ProyectoId, EntregableId, ActividadId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo, ArchivoNombre, ArchivoUrl, ArchivoTamano, RiesgoId)
VALUES
    (@p, @e1, @inventario,  '2026-05-14 21:20:00', @nombreLider,
     N'Se completó el inventario: 14 trámites vigentes, con su volumen anual y su unidad responsable.',
     100, NULL, NULL, NULL, NULL, NULL),

    (@p, @e1, @prior,       '2026-05-22 20:10:00', @nombreTec,
     N'Mesa de priorización con la institución. Quedan 6 trámites para la primera fase; el resto pasa a una segunda etapa sin fecha.',
     100, NULL, NULL, NULL, NULL, NULL),

    (@p, @e1, @acta,        '2026-05-29 22:45:00', @nombreLider,
     N'Acta de diagnóstico firmada por ambas partes. Queda cerrado el alcance de la fase.',
     100, NULL, N'acta-diagnostico-v1.pdf', N'/uploads/proyectos/documentos/5f25ccb98c3359d7cb37518f03671069.pdf', 1029, NULL),

    (@p, @e2, @fichas,      '2026-06-30 23:15:00', @nombreSop,
     N'Las 6 fichas técnicas quedaron elaboradas. Se atrasó cuatro días respecto del plan por la corrección del trámite 4 por el 9.',
     100, NULL, N'fichas-tecnicas.pdf', N'/uploads/proyectos/documentos/e6ae894634fbeb8cf2b71ff1c6be4ed3.pdf', 838, NULL),

    (@p, @e2, @config,      '2026-07-29 21:40:00', @nombreSop,
     N'Los 6 trámites están configurados en el ambiente de pruebas y listos para validación.',
     100, NULL, NULL, NULL, NULL, NULL),

    (@p, @e2, @valid,       '2026-08-07 22:05:00', @nombreTec,
     N'Pruebas de aceptación superadas. La contraparte da por validados los 6 trámites; el entregable queda cerrado.',
     100, NULL, NULL, NULL, NULL, NULL),

    (@p, @e3, @material,    '2026-08-13 20:30:00', @nombreSop,
     N'Material de capacitación terminado y publicado en el repositorio del proyecto.',
     100, NULL, N'material-capacitacion.pdf', N'/uploads/proyectos/documentos/b90a467a8fc8779b2279961886e056e3.pdf', 841, NULL),

    (@p, @e3, @capacit,     '2026-08-20 21:00:00', @nombreTec,
     N'Primera jornada realizada con 12 operadores. La segunda se reprograma: la institución tiene cierre trimestral esa semana.',
     40, N'La segunda jornada depende de que la institución confirme fecha. Sin ella no se puede evaluar ni salir a producción.',
     NULL, NULL, NULL, @riesgoAgenda),

    (@p, @e3, @capacit,     '2026-08-25 22:20:00', @nombreTec,
     N'Sesión virtual de refuerzo con 9 de los 12 operadores. Sigue pendiente la jornada presencial de cierre.',
     60, N'Falta que la institución confirme la fecha de la segunda jornada presencial.',
     NULL, NULL, NULL, NULL);

/* ── Repositorio documental ───────────────────────────────────────────────────
   El acta va con DOS versiones: es lo que muestra que corregir un documento no pisa al anterior. */
DECLARE @catActa     int = (SELECT TOP 1 Id FROM CategoriasDocumento WHERE Nombre = N'Acta');
DECLARE @catInforme  int = (SELECT TOP 1 Id FROM CategoriasDocumento WHERE Nombre = N'Informe');
DECLARE @catPlan     int = (SELECT TOP 1 Id FROM CategoriasDocumento WHERE Nombre = N'Plan');

DECLARE @d1 int, @d2 int, @d3 int;

INSERT INTO ProyectoDocumentos (IsDeleted, ProyectoId, CategoriaId, Titulo, Descripcion, CreatedAt, CreatedBy)
VALUES (0, @p, @catActa, N'Acta de diagnóstico de trámites',
        N'Alcance acordado con la institución: 6 trámites para la primera fase.', SYSUTCDATETIME(), @actor);
SET @d1 = SCOPE_IDENTITY();

INSERT INTO ProyectoDocumentoVersiones (DocumentoId, Numero, ArchivoNombre, ArchivoUrl, ArchivoTamano, Sha256, Notas, SubidoPor, SubidoEn)
VALUES
    (@d1, 1, N'acta-diagnostico-v1.pdf', N'/uploads/proyectos/documentos/5f25ccb98c3359d7cb37518f03671069.pdf',
     1029, N'ccee5ad2845a11af69ddaf6752446f39fcd871ace4981148cba45d1c9ceea032',
     NULL, @nombreLider, '2026-05-29 22:45:00'),
    (@d1, 2, N'acta-diagnostico-v2.pdf', N'/uploads/proyectos/documentos/e1f0e74568510d59c33cf94b4a9a3e47.pdf',
     1074, N'8dfa89941dd1369bcc5052bc58fb179ce9dce03de77e0a0ad271e1d2db0bdf01',
     N'A solicitud de la institución, el trámite 4 se reemplaza por el 9 por mayor volumen de demanda.',
     @nombreLider, '2026-06-04 20:15:00');

INSERT INTO ProyectoDocumentos (IsDeleted, ProyectoId, CategoriaId, Titulo, Descripcion, CreatedAt, CreatedBy)
VALUES (0, @p, @catInforme, N'Fichas técnicas de los 6 trámites',
        N'Requisitos, plazos, dependencias y responsables de cada trámite.', SYSUTCDATETIME(), @actor);
SET @d2 = SCOPE_IDENTITY();

INSERT INTO ProyectoDocumentoVersiones (DocumentoId, Numero, ArchivoNombre, ArchivoUrl, ArchivoTamano, Sha256, Notas, SubidoPor, SubidoEn)
VALUES (@d2, 1, N'fichas-tecnicas.pdf', N'/uploads/proyectos/documentos/e6ae894634fbeb8cf2b71ff1c6be4ed3.pdf',
        838, N'57218a0d19b9bec9d0cb90fa7d672c105447fb0213ff06fcea54163fba2bfcc8',
        NULL, @nombreSop, '2026-06-30 23:15:00');

INSERT INTO ProyectoDocumentos (IsDeleted, ProyectoId, CategoriaId, Titulo, Descripcion, CreatedAt, CreatedBy)
VALUES (0, @p, @catPlan, N'Material de capacitación a operadores',
        N'Guía del operador, casos de práctica y evaluación.', SYSUTCDATETIME(), @actor);
SET @d3 = SCOPE_IDENTITY();

INSERT INTO ProyectoDocumentoVersiones (DocumentoId, Numero, ArchivoNombre, ArchivoUrl, ArchivoTamano, Sha256, Notas, SubidoPor, SubidoEn)
VALUES (@d3, 1, N'material-capacitacion.pdf', N'/uploads/proyectos/documentos/b90a467a8fc8779b2279961886e056e3.pdf',
        841, N'f8176e2709cec543d715ac2981fc3562877d6102e1a9827f047a38b387a17125',
        NULL, @nombreSop, '2026-08-13 20:30:00');

/* ── Auditoría ────────────────────────────────────────────────────────────────
   Deja constancia de que este proyecto lo cargó un script y no una persona. */
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
VALUES (@p, N'ModificacionFicha',
        N'Proyecto demostrativo cargado por script para la etapa de validación con usuarios.',
        @actor, SYSUTCDATETIME());

COMMIT;

SELECT N'Proyecto demostrativo cargado.' AS Resultado, @codigo AS Codigo, @p AS ProyectoId;

SELECT N'Entregables' AS Elemento, COUNT(*) AS Cantidad FROM ProyectoEntregables WHERE ProyectoId = @p
UNION ALL SELECT N'Actividades',  COUNT(*) FROM ProyectoActividades a JOIN ProyectoEntregables e ON e.Id = a.EntregableId WHERE e.ProyectoId = @p
UNION ALL SELECT N'Dependencias', COUNT(*) FROM ProyectoDependenciasActividad d JOIN ProyectoActividades a ON a.Id = d.SucesoraId JOIN ProyectoEntregables e ON e.Id = a.EntregableId WHERE e.ProyectoId = @p
UNION ALL SELECT N'Interesados',  COUNT(*) FROM ProyectoInteresados WHERE ProyectoId = @p
UNION ALL SELECT N'Riesgos',      COUNT(*) FROM ProyectoRiesgos     WHERE ProyectoId = @p
UNION ALL SELECT N'Avances',      COUNT(*) FROM ProyectoAvances     WHERE ProyectoId = @p
UNION ALL SELECT N'Documentos',   COUNT(*) FROM ProyectoDocumentos  WHERE ProyectoId = @p AND IsDeleted = 0;

/*
    Para recargarlo desde cero:

    DECLARE @p int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-99');
    DELETE v FROM ProyectoDocumentoVersiones v JOIN ProyectoDocumentos d ON d.Id = v.DocumentoId WHERE d.ProyectoId = @p;
    DELETE FROM ProyectoDocumentos WHERE ProyectoId = @p;
    DELETE FROM ProyectoAvances    WHERE ProyectoId = @p;
    DELETE d FROM ProyectoDependenciasActividad d JOIN ProyectoActividades a ON a.Id = d.SucesoraId JOIN ProyectoEntregables e ON e.Id = a.EntregableId WHERE e.ProyectoId = @p;
    DELETE FROM ProyectoRiesgos      WHERE ProyectoId = @p;
    DELETE FROM ProyectoInteresados  WHERE ProyectoId = @p;
    DELETE FROM BitacoraProyecto     WHERE ProyectoId = @p;
    DELETE a FROM ProyectoActividades a JOIN ProyectoEntregables e ON e.Id = a.EntregableId WHERE e.ProyectoId = @p;
    DELETE FROM ProyectoEntregables  WHERE ProyectoId = @p;
    DELETE FROM Proyectos            WHERE Id = @p;
*/
