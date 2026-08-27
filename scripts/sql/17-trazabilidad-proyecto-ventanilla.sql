/*  Trazabilidad del proyecto PRY-2026-25 «Ventanilla Única - Honduras Ágil».

    Carga la estructura de desglose real del proyecto —entregables y actividades— a partir
    del historial de los dos repositorios (honduras-agil y Portal-Informacion-Institucional)
    y de la tabla de trazabilidad medida del plan por fases.

    Reglas del dominio que este guion respeta a mano, porque no pasa por la aplicación:
      · El estado de una actividad lo deriva su porcentaje: 0 Pendiente, 1-99 EnProceso, 100 Completada.
      · El avance de un entregable es el promedio simple de sus actividades vigentes.
      · El avance del proyecto es el promedio simple de sus entregables vigentes.
      · Un entregable Completado lleva FechaReal; uno que no lo está, no.

    Idempotente por negativa: si ya se corrió, aborta en vez de duplicar.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Actor  nvarchar(150) = N'script-trazabilidad-ventanilla';
DECLARE @Ahora  datetime2(7)  = SYSUTCDATETIME();
DECLARE @Proy   int           = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-25');

IF @Proy IS NULL
BEGIN
    RAISERROR(N'No existe el proyecto PRY-2026-25. Nada que actualizar.', 16, 1);
    RETURN;
END

IF EXISTS (SELECT 1 FROM BitacoraProyecto WHERE ProyectoId = @Proy AND Actor = @Actor)
BEGIN
    RAISERROR(N'La trazabilidad ya se cargó antes en este proyecto. Aborta para no duplicar.', 16, 1);
    RETURN;
END

BEGIN TRAN;

-- Los cinco entregables que ya existen se conservan con su Id: la bitácora los referencia
-- y borrarlos desimputaría los avances ya registrados.
DECLARE @E1 int = (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = @Proy AND Orden = 1);
DECLARE @E2 int = (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = @Proy AND Orden = 2);
DECLARE @E3 int = (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = @Proy AND Orden = 3);
DECLARE @E4 int = (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = @Proy AND Orden = 4);
DECLARE @E5 int = (SELECT Id FROM ProyectoEntregables WHERE ProyectoId = @Proy AND Orden = 5);

IF @E1 IS NULL OR @E2 IS NULL OR @E3 IS NULL OR @E4 IS NULL OR @E5 IS NULL
BEGIN
    ROLLBACK TRAN;
    RAISERROR(N'Faltan los entregables 1 a 5 que se esperaban en el proyecto.', 16, 1);
    RETURN;
END

-- ── 1. Los cinco entregables que ya estaban: se les pone descripción y fecha de entrega ──
UPDATE ProyectoEntregables SET
    Descripcion = N'Qué tiene que resolver la ventanilla y contra qué se mide. Cierra con el análisis de brechas entre PortalDigital, SOL y la ventanilla, y con la auditoría de experiencia de uso del portal ciudadano.',
    FechaPlan = '2026-08-12', FechaReal = '2026-08-12', Estado = N'Completado',
    UpdatedAt = @Ahora, UpdatedBy = @Actor
WHERE Id = @E1;

UPDATE ProyectoEntregables SET
    Descripcion = N'Cómo viajan los datos desde PortalDigital hasta el ciudadano: plan por fases, diseño de la frescura del catálogo y contrato de la API congelado antes de escribir código.',
    FechaPlan = '2026-08-24', FechaReal = '2026-08-24', Estado = N'Completado',
    UpdatedAt = @Ahora, UpdatedBy = @Actor
WHERE Id = @E2;

UPDATE ProyectoEntregables SET
    Descripcion = N'De dónde sale la información que ve el ciudadano. Inventario del origen y los cambios de esquema que PortalDigital necesitaba para poder servirla, verificados sobre una copia fiel de producción.',
    FechaPlan = '2026-08-17', FechaReal = '2026-08-17', Estado = N'Completado',
    UpdatedAt = @Ahora, UpdatedBy = @Actor
WHERE Id = @E3;

UPDATE ProyectoEntregables SET
    Descripcion = N'La base sobre la que se construye: solución con arquitectura limpia, sistema de diseño e identidad institucional, y un entorno de ensayo para no ejercer nada contra producción.',
    FechaPlan = '2026-08-17', FechaReal = '2026-08-17', Estado = N'Completado',
    UpdatedAt = @Ahora, UpdatedBy = @Actor
WHERE Id = @E4;

UPDATE ProyectoEntregables SET
    Descripcion = N'La API v1 que PortalDigital expone y de la que se alimenta la ventanilla. El código está terminado y ejercido contra datos reales; falta desplegarla con su clave fuera del repositorio.',
    FechaPlan = NULL, FechaReal = NULL, Estado = N'EnProceso',
    UpdatedAt = @Ahora, UpdatedBy = @Actor
WHERE Id = @E5;

-- ── 2. Entregables nuevos: el trabajo que no tenía dónde registrarse ──
INSERT INTO ProyectoEntregables (ProyectoId, Orden, Nombre, Descripcion, FechaPlan, FechaReal, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
VALUES
 (@Proy,  6, N'Réplica del catálogo en HondurasÁgil',
  N'La ventanilla no lee la API al vuelo: mantiene su propia copia del catálogo y la sincroniza. Cliente de las siete rutas, esquema replicado, motor de dos ciclos y disparador automático.',
  '2026-08-18', '2026-08-18', N'Completado', NULL, NULL, @Ahora, @Actor),

 (@Proy,  7, N'Sustitución del catálogo sembrado por el real',
  N'El corte: el ciudadano deja de ver trámites de muestra y pasa a ver los replicados de PortalDigital. Incluye cambiar la identidad del trámite a su código y retirar el esquema heredado.',
  '2026-08-19', '2026-08-19', N'Completado', NULL, NULL, @Ahora, @Actor),

 (@Proy,  8, N'Deudas de datos del catálogo',
  N'Lo que falta o está sucio en el origen y que el ciudadano vería tal cual. Cada bloque se midió antes y después; lo que queda abierto depende de decisiones de DIGER, no de código.',
  NULL, NULL, N'EnProceso', NULL, NULL, @Ahora, @Actor),

 (@Proy,  9, N'Herramientas de captura de fichas en PortalDigital',
  N'Con qué se llenan las fichas que la ventanilla publica: editor, captura por lotes, tablero de completitud y la pantalla de publicación. Las herramientas están; el llenado humano es el cuello de botella.',
  NULL, NULL, N'EnProceso', NULL, NULL, @Ahora, @Actor),

 (@Proy, 10, N'Base de datos unificada para producción',
  N'Una sola base que junte lo que hay en producción con lo que construyó el equipo, verificada de punta a punta y lista para subir.',
  NULL, NULL, N'EnProceso', NULL, NULL, @Ahora, @Actor),

 (@Proy, 11, N'Verificación funcional por el usuario',
  N'Que el usuario pueda comprobar por su cuenta que lo entregado funciona, sin depender de quien lo construyó.',
  NULL, NULL, N'EnProceso', NULL, NULL, @Ahora, @Actor),

 (@Proy, 12, N'Publicación del piloto y puesta en operación',
  N'Lo que falta para que esto sea un servicio y no un entorno de pruebas: publicar los trámites del corte, cerrar las decisiones abiertas y desplegar en el servidor.',
  NULL, NULL, N'Pendiente', NULL, NULL, @Ahora, @Actor);

-- ── 3. Actividades ──────────────────────────────────────────────────────────
-- Las ventanas de fecha son las medidas en el historial de los dos repositorios, no una línea
-- base pactada de antemano: este proyecto se está registrando después de ejecutarse. Lo que no
-- tiene fecha es lo que todavía no está programado, y sale aparte en el cronograma.
DECLARE @Act TABLE (
    OrdenEnt int, Orden int, Nombre nvarchar(300), Descripcion nvarchar(2000),
    Ini date NULL, Fin date NULL, Avance int);

INSERT INTO @Act (OrdenEnt, Orden, Nombre, Descripcion, Ini, Fin, Avance) VALUES
-- E1 · Análisis de requerimientos
(1, 1, N'Análisis de brechas entre PortalDigital, SOL y la ventanilla única',
    N'Qué información necesita la ventanilla y cuánta de ella existe hoy en PortalDigital. Documento brechas-portaldigital-sol-ventanilla-analisis.md, con el addendum del 12-08 (commit a13822e) que recoge lo que cambiaron las fases 2 y 3.',
    '2026-08-03', '2026-08-12', 100),
(1, 2, N'Auditoría de experiencia de uso del portal ciudadano (fases 0 a 3)',
    N'Cuatro entregas: cierre de los cuatro bloqueantes (c53b49f), sistema de diseño e identidad institucional con contraste AA (8319823), catálogo, detalle e instituciones (e9d5b9a), y ayuda, accesibilidad y servicio (a1092c2). Documentos en docs/auditoria-ux/.',
    '2026-08-12', '2026-08-12', 100),

-- E2 · Definición del flujo
(2, 1, N'Plan por fases de la integración PortalDigital → API → HondurasÁgil',
    N'Ocho fases (F0 a F7) con criterios de salida medibles, decisiones D-01 a D-23 y preguntas P-01 a P-08. Commit ddbf45d. Es el documento contra el que se mide el avance de este proyecto.',
    '2026-08-13', '2026-08-13', 100),
(2, 2, N'Diseño de la frescura del catálogo y la carga por partes',
    N'Cómo se mantiene al día el catálogo replicado sin releerlo entero: un ciclo ligero que solo pide los cambios y un ciclo completo de reconciliación. Documento de diseño incorporado en el commit ad6df88.',
    '2026-08-13', '2026-08-24', 100),
(2, 3, N'Congelar el contrato de la API v1',
    N'Siete rutas especificadas en OpenAPI (docs/superpowers/plans/api-v1/openapi-v1.yaml) y encargo autocontenido para poder delegar la construcción sin depender del entorno local. Commits 9724a0e y 63bc55e.',
    '2026-08-13', '2026-08-13', 100),

-- E3 · Identificación del origen de datos
(3, 1, N'Inventario del origen: 1.057 trámites y 45 instituciones',
    N'Medido sobre TramitesEstado_Ensayo, copia fiel de producción restaurada el 17-08. Es la base de todas las cifras de avance del plan: lo que se dice del origen se dice contra este inventario.',
    '2026-08-13', '2026-08-17', 100),
(3, 2, N'Scripts de esquema sobre PortalDigital (A, B, C, D y F)',
    N'Campos nuevos en TramitesSiger y PasosSiger, tabla CategoriasTramite con su semilla, contacto institucional en Instituciones y búsqueda insensible a tildes. 5 de 5. El script E (trámites relacionados) queda deliberadamente fuera de esta fase.',
    '2026-08-17', '2026-08-17', 100),
(3, 3, N'Verificación del esquema contra datos reales',
    N'24 de 24 comprobaciones: 9 columnas, 3 colaciones, 8 categorías, 3 índices filtrados y la búsqueda de «migracion» encontrando «migración».',
    '2026-08-17', '2026-08-17', 100),

-- E4 · Establecimiento del proyecto base
(4, 1, N'Proyecto base de HondurasÁgil con arquitectura limpia',
    N'Solución .NET con Domain, Application, Infrastructure y Web, y el módulo de trámites decidido en docs/superpowers/specs/2026-08-03-modulo-tramites-decision.md. Commits 540e4eb, 0bc3a2b y fda7485.',
    '2026-08-03', '2026-08-06', 100),
(4, 2, N'Sistema de diseño e identidad institucional',
    N'Tipografía embebida, paleta institucional y contraste AA verificado. Cierra los cuatro bloqueantes de la fase 0 de la auditoría (c53b49f) y la fase 1 (8319823).',
    '2026-08-12', '2026-08-12', 100),
(4, 3, N'Entorno de ensayo con copia fiel de producción',
    N'TramitesEstado_Ensayo, restaurada desde respaldo el 17-08, para poder ejercer la API y los guiones de datos sin tocar producción.',
    '2026-08-17', '2026-08-17', 100),

-- E5 · API intermediario
(5, 1, N'Implementar las siete rutas de la API v1',
    N'Las siete responden conforme al contrato congelado: salud, catálogo, ficha por código, cambios, instituciones, categorías y tipos de institución. 7 de 7.',
    '2026-08-14', '2026-08-18', 100),
(5, 2, N'Ejercer la API contra datos reales',
    N'27 de 27 casos sobre TramitesEstado_Ensayo: autenticación, acotado de la paginación, 404, 400, búsqueda, orden y sincronización. Ejercida ruta por ruta, no por muestreo.',
    '2026-08-18', '2026-08-18', 100),
(5, 3, N'Documentación de la API generada por el propio código',
    N'Commit 4dd7e38. Antes era un documento escrito a mano que se desincronizaba del contrato; ahora sale del código. Manual en docs/api/2026-08-17-manual-api-v1-portaldigital.html.',
    '2026-08-25', '2026-08-25', 100),
(5, 4, N'Separar la API pública en su propio proyecto',
    N'Commit fb1453b: la API sale de src/Presentation a src/Api, para poder desplegarla sola sin exponer con ella el portal interno.',
    '2026-08-25', '2026-08-25', 100),
(5, 5, N'Desplegar la API con su clave fuera del repositorio',
    N'La clave vive hoy en user-secrets, que no viaja al servidor. Falta decidir dónde vive en producción —variable de entorno del grupo de aplicaciones de IIS— y publicar. Depende de la decisión P-03, que sigue abierta.',
    NULL, NULL, 0);

INSERT INTO @Act (OrdenEnt, Orden, Nombre, Descripcion, Ini, Fin, Avance) VALUES
-- E6 · Réplica del catálogo en HondurasÁgil
(6, 1, N'Cliente de las siete rutas, con degradación ante fallo',
    N'Arnés contra la API real: 13 de 13, incluidas las dos degradaciones —sin URL configurada, y con la API caída devolviendo null sin lanzar excepción—. La ventanilla no se cae porque la API no responda.',
    '2026-08-14', '2026-08-18', 100),
(6, 2, N'Esquema replicado del catálogo (9 tablas Portal*)',
    N'Colación insensible a tildes e índices por lo que realmente se filtra. Migración SincronizacionCatalogoPortal. 9 de 9 tablas.',
    '2026-08-14', '2026-08-18', 100),
(6, 3, N'Motor de sincronización de dos ciclos',
    N'Ciclo ligero que pide solo los cambios y ciclo completo de reconciliación. Medido el 18-08: 49 trámites en 1,7 s, con 380 pasos, 216 requisitos, 65 entregables, 202 lugares de atención y 68 enlaces.',
    '2026-08-18', '2026-08-18', 100),
(6, 4, N'Disparador automático de la sincronización',
    N'SincronizacionPortalHostedService, con intervalo configurable, arranque opcional y ciclo completo periódico. La copia se mantiene al día sola.',
    '2026-08-18', '2026-08-18', 100),
(6, 5, N'Prueba de punta a punta de la sincronización',
    N'4 de 4: ciclo completo, incremental, retirada —la baja propaga y borra los hijos sin dejar huérfanos— y recuperación por ciclo completo.',
    '2026-08-18', '2026-08-18', 100),

-- E7 · Sustitución del catálogo sembrado
(7, 1, N'Identidad del trámite por su código, con redirección desde las rutas viejas',
    N'Fase F1b, hecha el 18-08. Rutas por código y 301 desde las anteriores. Los 23 votos y las 230 consultas sobrevivieron al cambio, comprobado aplicando, revirtiendo y volviendo a aplicar.',
    '2026-08-18', '2026-08-18', 100),
(7, 2, N'El ciudadano pasa a ver el catálogo real',
    N'Fase F6a, hecha el 19-08. 49 trámites replicados con sus nombres, requisitos, pasos y lugares de atención; sembradores retirados y el interruptor de origen eliminado por quedarse con una sola posición útil. 0 desbordes en 150 combinaciones y 0 fallos AA en 6.072 textos, medido en navegador.',
    '2026-08-19', '2026-08-19', 100),
(7, 3, N'Retirar el esquema heredado',
    N'Fase F6b, hecha el 19-08. Las 8 tablas del catálogo sembrado se eliminaron con una migración probada de ida y vuelta. Los 8 contactos institucionales verificados se rescataron antes a docs/api/contactos/.',
    '2026-08-19', '2026-08-19', 100),

-- E8 · Deudas de datos del catálogo
(8, 1, N'Normalizar el nombre corto de las instituciones (G-1)',
    N'43 de 43, que era la meta del plan. Sin esto la ventanilla mostraba el nombre largo completo en las tarjetas del catálogo.',
    '2026-08-17', '2026-08-17', 100),
(8, 2, N'Emparejar cada trámite con su institución (G-2)',
    N'Guion probado sobre el entorno de ensayo: 751 de 1.057, por sigla (593) y por nombre (158). Cuidado: la copia de producción del 27-08 trae InstitucionId vacío en los 1.057, así que el guion hay que volver a aplicarlo sobre la base que va a subir.',
    '2026-08-17', '2026-08-18', 71),
(8, 3, N'Dar de alta las 41 instituciones que faltan en el catálogo',
    N'Ensayado, no aplicado: completaría el emparejamiento a 1.057 de 1.057. Requiere 41 decisiones de DIGER, porque esas instituciones sencillamente no existen todavía en la tabla Instituciones.',
    NULL, NULL, 0),
(8, 4, N'Limpiar enlaces, fechas centinela y nombres en mayúsculas (G-4, G-5 y G-6)',
    N'14 enlaces sucios, 12 fechas centinela y 38 nombres escritos en mayúsculas. Listados y medidos, no corregidos: 0 de 64.',
    NULL, NULL, 0),

-- E9 · Herramientas de captura
(9, 1, N'Editor de ficha, captura por lotes y tablero de completitud',
    N'Las tres herramientas con las que se llena una ficha en PortalDigital. 3 de 3 construidas y revisadas en código; el tablero es el que le dice a cada institución qué le falta.',
    '2026-08-18', '2026-08-19', 100),
(9, 2, N'Publicación manual hacia HondurasÁgil, con su propia pantalla',
    N'Commit 352c1ab. La publicación deja de ser automática: alguien decide qué sale al ciudadano y lo hace desde una pantalla hecha para eso.',
    '2026-08-24', '2026-08-24', 100),
(9, 3, N'Poner en cola lo que el sistema propone para las fichas incompletas',
    N'Commit 1393fe2. Las propuestas de llenado no se aplican solas: quedan en cola para que alguien las apruebe.',
    '2026-08-24', '2026-08-24', 100),
(9, 4, N'Captura humana de las fichas del piloto',
    N'49 fichas × 5 campos = 245 datos que llenan los técnicos de las instituciones. Es el cuello de botella del proyecto. Medido el 19-08 y sin cambio: en producción ninguna de las 49 publicadas tiene categoría, modalidad, tiempo ni costo, y el ciudadano ve un guion donde falta el dato.',
    NULL, NULL, 0);

INSERT INTO @Act (OrdenEnt, Orden, Nombre, Descripcion, Ini, Fin, Avance) VALUES
-- E10 · Base de datos unificada
(10, 1, N'Traer a la rama de trabajo lo que estaba en dev',
    N'Commit 9db434a. Fusión de origin/dev con reparación de los conflictos en Enums, IRepositories, AppDbContext, el snapshot del modelo —que git había entrelazado— y una ambigüedad de tipos que ninguna de las dos ramas veía por separado. Rama de respaldo: respaldo/Jamil-antes-de-dev-27ago.',
    '2026-08-27', '2026-08-27', 100),
(10, 2, N'Reconstruir la base unificada sobre la copia de producción',
    N'Copia de producción del 27-08 más las 70 migraciones del equipo: 79 tablas y 39.123 filas. El respaldo recibido traía dos conjuntos de copia dentro del mismo archivo y había que restaurar el segundo; restaurarlo sin más devolvía los datos viejos.',
    '2026-08-26', '2026-08-27', 100),
(10, 3, N'Apuntar el portal y la API a la base unificada',
    N'Commit efd1003. Las dos aplicaciones dejan de mirar a la base de ensayo.',
    '2026-08-27', '2026-08-27', 100),
(10, 4, N'Verificar la base unificada de punta a punta',
    N'871 columnas idénticas entre las dos instancias, las 78 tablas de datos comparadas fila por fila, restricciones revalidadas contra los datos y DBCC CHECKDB limpio. Comprobado además que la API responde y autentica, que HondurasÁgil sincroniza y que el portal interno sirve sin excepciones.',
    '2026-08-27', '2026-08-27', 100),
(10, 5, N'Subir la base unificada a producción',
    N'Respaldo listo y verificado en C:\Respaldos\DigerTramitesEstado_Unificada_20260827_v2.bak. La restauración en el servidor la ejecuta DIGER.',
    NULL, NULL, 0),

-- E11 · Verificación funcional por el usuario
(11, 1, N'Terminar el renombrado a Honduras Ágil',
    N'Commit aa87b45. Quedaba «Ventanilla Digital» en el ícono del sitio, que es justamente lo que anuncia un lector de pantalla.',
    '2026-08-27', '2026-08-27', 100),
(11, 2, N'Plan de pruebas manual para el usuario',
    N'Commit 92f04b4. Diez pruebas, cada una con dónde ir, qué tocar y qué debe pasar, con seguimiento del progreso. docs/plan-de-pruebas-manual.html.',
    '2026-08-27', '2026-08-27', 100),
(11, 3, N'Ejecución de las pruebas por el usuario',
    N'Pendiente de que el usuario recorra las diez pruebas y confirme el resultado.',
    NULL, NULL, 0),

-- E12 · Publicación del piloto y operación
(12, 1, N'Publicar los trámites del corte piloto',
    N'INPREMA, IHTT y CONSUCOOP. En la base unificada del 27-08 hay 1.057 trámites y ninguno publicado; los 57 del corte están identificados y 49 de ellos ya están en estado Aprobado o Completo.',
    NULL, NULL, 0),
(12, 2, N'Cerrar las decisiones abiertas P-03, P-04 y P-06',
    N'Dónde vive la clave de la API en el servidor, quién autoriza la publicación y con qué calendario se hace el corte. 5 de las 8 preguntas del contrato están cerradas; estas tres bloquean el despliegue.',
    NULL, NULL, 0),
(12, 3, N'Desplegar la API y el portal ciudadano en el servidor',
    N'Publicar las dos aplicaciones fuera de la máquina de desarrollo, con sus secretos en variables de entorno y no en archivos versionados.',
    NULL, NULL, 0);

-- ── 4. Volcar las actividades ───────────────────────────────────────────────
-- Las cinco actividades espejo que creó la migración ActividadEspejoPorEntregable se reescriben
-- en vez de borrarse: una de ellas tiene un avance imputado y borrarla lo desimputaría.
UPDATE pa SET
    Nombre          = a.Nombre,
    Descripcion     = a.Descripcion,
    FechaInicioPlan = a.Ini,
    FechaFinPlan    = a.Fin,
    FechaInicioReal = CASE WHEN a.Avance > 0   THEN a.Ini END,
    FechaFinReal    = CASE WHEN a.Avance = 100 THEN a.Fin END,
    AvancePct       = a.Avance,
    Estado          = CASE a.Avance WHEN 0 THEN N'Pendiente' WHEN 100 THEN N'Completada' ELSE N'EnProceso' END,
    UpdatedAt       = @Ahora,
    UpdatedBy       = @Actor
FROM ProyectoActividades pa
JOIN ProyectoEntregables e ON e.Id = pa.EntregableId AND e.ProyectoId = @Proy
JOIN @Act a ON a.OrdenEnt = e.Orden AND a.Orden = pa.Orden;

INSERT INTO ProyectoActividades
    (EntregableId, Orden, Nombre, Descripcion, FechaInicioPlan, FechaFinPlan,
     FechaInicioReal, FechaFinReal, AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
SELECT
    e.Id, a.Orden, a.Nombre, a.Descripcion, a.Ini, a.Fin,
    CASE WHEN a.Avance > 0   THEN a.Ini END,
    CASE WHEN a.Avance = 100 THEN a.Fin END,
    a.Avance,
    CASE a.Avance WHEN 0 THEN N'Pendiente' WHEN 100 THEN N'Completada' ELSE N'EnProceso' END,
    NULL, NULL, @Ahora, @Actor
FROM @Act a
JOIN ProyectoEntregables e ON e.ProyectoId = @Proy AND e.Orden = a.OrdenEnt
WHERE NOT EXISTS (SELECT 1 FROM ProyectoActividades pa
                  WHERE pa.EntregableId = e.Id AND pa.Orden = a.Orden);

-- ── 5. Recalcular hacia arriba, con las mismas reglas del dominio ───────────
-- Entregable = promedio simple de sus actividades vigentes; proyecto = promedio simple de sus
-- entregables vigentes. Un entregable con todas sus actividades al 100 % queda Completado.
;WITH AvanceEnt AS (
    SELECT e.Id,
           Prom = CAST(ROUND(AVG(CAST(a.AvancePct AS float)), 0) AS int)
    FROM ProyectoEntregables e
    JOIN ProyectoActividades a ON a.EntregableId = e.Id AND a.Estado <> N'Cancelada'
    WHERE e.ProyectoId = @Proy
    GROUP BY e.Id)
UPDATE e SET
    Estado    = CASE WHEN v.Prom = 100 THEN N'Completado'
                     WHEN v.Prom = 0   THEN N'Pendiente'
                     ELSE N'EnProceso' END,
    FechaReal = CASE WHEN v.Prom = 100 THEN ISNULL(e.FechaReal, e.FechaPlan) END,
    UpdatedAt = @Ahora,
    UpdatedBy = @Actor
FROM ProyectoEntregables e
JOIN AvanceEnt v ON v.Id = e.Id
WHERE e.ProyectoId = @Proy;

DECLARE @AvanceProy int;
;WITH AvanceEnt AS (
    SELECT e.Id,
           Prom = CAST(ROUND(AVG(CAST(a.AvancePct AS float)), 0) AS int)
    FROM ProyectoEntregables e
    JOIN ProyectoActividades a ON a.EntregableId = e.Id AND a.Estado <> N'Cancelada'
    WHERE e.ProyectoId = @Proy AND e.Estado <> N'Cancelado'
    GROUP BY e.Id)
SELECT @AvanceProy = CAST(ROUND(AVG(CAST(Prom AS float)), 0) AS int) FROM AvanceEnt;

UPDATE Proyectos SET
    AvancePct       = @AvanceProy,
    FechaInicioReal = ISNULL(FechaInicioReal, '2026-08-03'),
    UpdatedAt       = @Ahora,
    UpdatedBy       = @Actor
WHERE Id = @Proy;

-- ── 6. Dejar constancia de por qué cambió la estructura ─────────────────────
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
VALUES (@Proy, N'ModificacionEstructura',
    N'Carga de la trazabilidad del proyecto. La estructura pasa de 5 entregables con una actividad espejo cada uno a 12 entregables y 43 actividades, reconstruidos desde el historial de los repositorios honduras-agil y Portal-Informacion-Institucional y desde la tabla de trazabilidad medida del plan por fases. Cada actividad cita su evidencia: commit, documento o cifra medida. Las ventanas de fecha son las medidas en el historial, no una línea base pactada de antemano —el proyecto se registra después de ejecutarse—, y lo que no tiene fecha es lo que todavía no está programado. El avance del proyecto pasa de 90 % a un valor calculado desde la estructura nueva: baja porque ahora incluye el trabajo pendiente que antes no estaba representado, no porque se haya deshecho nada.',
    @Actor, @Ahora);

INSERT INTO ProyectoAvances (ProyectoId, EntregableId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo, ActividadId)
VALUES (@Proy, NULL, @Ahora, N'Jamil Garcia',
    N'Al 27 de agosto la tubería completa funciona: PortalDigital expone la API v1 con sus siete rutas, HondurasÁgil mantiene su copia del catálogo y la sincroniza sola, y el ciudadano ve los 49 trámites replicados con nombres, requisitos, pasos y lugares de atención. La base de datos unificada quedó reconstruida sobre la copia de producción del 27-08 y verificada de punta a punta, con respaldo listo para subir. Lo que falta ya casi no es de código: llenar las fichas del piloto (245 datos, por los técnicos de las instituciones), aplicar el guion que empareja cada trámite con su institución sobre la base que va a producción, cerrar las decisiones P-03, P-04 y P-06, y desplegar. Esta entrada acompaña la carga de la estructura de desglose reconstruida desde el historial de los dos repositorios.',
    NULL,
    N'La captura humana de las 49 fichas del piloto es el cuello de botella. Además, el despliegue de la API depende de la decisión P-03 (dónde vive su clave en el servidor).',
    NULL);

COMMIT TRAN;

PRINT N'--- Resultado ---';
SELECT Codigo, Nombre, Estado, AvancePct, FechaInicioReal FROM Proyectos WHERE Id = @Proy;
SELECT e.Orden, e.Nombre, e.Estado, e.FechaReal,
       Actividades = COUNT(a.Id),
       Avance      = CAST(ROUND(AVG(CAST(a.AvancePct AS float)), 0) AS int)
FROM ProyectoEntregables e
LEFT JOIN ProyectoActividades a ON a.EntregableId = e.Id
WHERE e.ProyectoId = @Proy
GROUP BY e.Orden, e.Nombre, e.Estado, e.FechaReal
ORDER BY e.Orden;
