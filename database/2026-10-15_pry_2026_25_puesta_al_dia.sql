/* ============================================================================
   PRY-2026-25 · Ventanilla Única — Honduras Ágil
   Puesta al día de la ficha con lo hecho entre el 22 de septiembre y el 15 de octubre
   ----------------------------------------------------------------------------
   Qué hace, en orden:

     1. Registra los diez frentes cerrados del período: el catálogo de
        instituciones, los scripts de esquema, el renombre a Honduras Fácil, los
        ajustes de Gestión Digital, la preparación del despliegue, el despliegue
        en Test, la apertura a todas las instituciones, la institución
        obligatoria en las fichas, la identidad visual oficial y las mejoras que
        salieron de las observaciones de los usuarios en Test.
     2. Parte en dos la actividad que quedaba abierta, «Pruebas de aceptación y
        puesta en producción»: las pruebas de aceptación arrancaron el 14 de
        octubre en Test y van a mitad; la puesta en producción no ha empezado
        porque espera autorización. Las dos quedan al final de la lista.
     3. Pone al día los riesgos: cierra el del catálogo vacío y el de «nunca se
        ha desplegado», pasa a tratamiento el de las pruebas con personas y
        registra tres nuevos.
     4. Recalcula el avance con la regla del dominio: queda en 94 %.
     5. Deja constancia en avances y en bitácora.

   Sobre el 94 %: NO se escribe a mano. «Proyecto.RecalcularAvance» se dispara
   sola cada vez que alguien guarda la estructura o reporta un avance, así que
   un porcentaje puesto a dedo dura hasta la primera edición desde el portal.
   Acá sale del árbol: veinticinco actividades, veintitrés cerradas, la de
   aceptación al 50 y la de producción en 0, dan 2350/25 = 94 %. Mientras la
   puesta en producción siga en 0, el techo es 96 %: aunque la aceptación
   llegue a 100, faltaría la actividad que no se puede hacer sin autorización.

   Se puede correr las veces que haga falta: cada actividad y cada riesgo se
   busca antes por su nombre y sólo se crea o cambia lo que falte.

   ARRANCA EN MODO SIMULACIÓN. Revise el reporte y después cambie @soloSimular
   a 0 para aplicar.

       sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-10-15_pry_2026_25_puesta_al_dia.sql
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ── Parámetros ─────────────────────────────────────────────────────────────
DECLARE @soloSimular    bit           = 1;              -- <<< cambie a 0 para aplicar
DECLARE @actor          nvarchar(200) = N'Jamil Garcia';
DECLARE @avanceEsperado int           = 94;             -- lo que debe dar el cálculo
DECLARE @pctAceptacion  int           = 50;             -- avance de las pruebas de aceptación

DECLARE @ahora datetime2 = SYSUTCDATETIME();
DECLARE @hoy   date      = CAST(SYSUTCDATETIME() AS date);

BEGIN TRANSACTION;

-- ── Guardas de esquema ─────────────────────────────────────────────────────
IF OBJECT_ID('Proyectos')           IS NULL OR OBJECT_ID('ProyectoEntregables') IS NULL
OR OBJECT_ID('ProyectoActividades') IS NULL OR OBJECT_ID('BitacoraProyecto')    IS NULL
OR OBJECT_ID('ProyectoAvances')     IS NULL OR OBJECT_ID('ProyectoRiesgos')     IS NULL
BEGIN
    ROLLBACK;
    RAISERROR('Esta base no tiene el esquema de proyectos completo. No se hizo nada.', 16, 1);
    RETURN;
END

-- ── Resolver el proyecto y su entregable ───────────────────────────────────
DECLARE @pry int = (SELECT Id FROM Proyectos WHERE Codigo = N'PRY-2026-25' AND IsDeleted = 0);

IF @pry IS NULL
BEGIN
    ROLLBACK;
    RAISERROR('No se encontró el proyecto PRY-2026-25 activo. No se hizo nada.', 16, 1);
    RETURN;
END

/* Mismo criterio que la puesta al día del 28 de septiembre: el entregable es el
   que tiene la actividad del API intermediario. */
DECLARE @ent int = (
    SELECT TOP 1 e.Id
      FROM ProyectoEntregables e
     WHERE e.ProyectoId = @pry
       AND EXISTS (SELECT 1 FROM ProyectoActividades a
                    WHERE a.EntregableId = e.Id
                      AND a.Nombre = N'API intermediario con el portal de digitalización')
     ORDER BY e.Orden);

IF @ent IS NULL
BEGIN
    ROLLBACK;
    RAISERROR('PRY-2026-25 no tiene el entregable con el API intermediario. No se hizo nada.', 16, 1);
    RETURN;
END

/* Esta puesta al día va encima de la del 28 de septiembre. Sin ella, las fechas,
   los riesgos y el porcentaje esperado no cuadran. */
IF NOT EXISTS (SELECT 1 FROM ProyectoAvances
                WHERE ProyectoId = @pry
                  AND Descripcion LIKE N'Puesta al día del 2 al 21 de septiembre.%')
BEGIN
    ROLLBACK;
    RAISERROR('Falta la puesta al día del 28 de septiembre (2026-09-28_pry_2026_25_puesta_al_dia.sql). Córrala antes. No se hizo nada.', 16, 1);
    RETURN;
END

-- ── Responsables ───────────────────────────────────────────────────────────
DECLARE @idJamil  uniqueidentifier = (SELECT Id FROM Usuarios WHERE Correo = N'jgarcia@diger.gob.hn');
DECLARE @nomJamil nvarchar(400)    = (SELECT Nombre FROM Usuarios WHERE Id = @idJamil);

IF @idJamil IS NULL
BEGIN
    ROLLBACK;
    RAISERROR('No existe el usuario jgarcia@diger.gob.hn en esta base. No se hizo nada.', 16, 1);
    RETURN;
END

DECLARE @avancePrevio int = (SELECT AvancePct FROM Proyectos WHERE Id = @pry);

-- ══ PASO 1 · Los frentes cerrados del período ══════════════════════════════
DECLARE @nuevas TABLE (
    Nombre      nvarchar(300),
    Descripcion nvarchar(2000),
    IniReal     date,
    FinReal     date);

INSERT INTO @nuevas VALUES

 (N'Catálogo de instituciones completo',
  N'Dos listas de instituciones que faltaban en el catálogo de Gestión Digital: veintinueve el 23 de septiembre y treinta más el 24. Muchas ya estaban escritas de otra forma —solo siglas, el nombre en mayúsculas sin tildes, o las siglas pegadas a una llave importada—, así que el script compara normalizando y reconoce las existentes en vez de duplicarlas. Siglas verificadas contra el sitio de cada institución o el Portal Único de Transparencia. Test quedó con 96 instituciones. Es la base para que cada trámite tenga a quién pertenecer.',
  '2026-09-23', '2026-09-24'),

 (N'Scripts de esquema para llevar Test y Producción a la versión nueva',
  N'Dos scripts probados sobre los respaldos reales: el primero lleva Producción al esquema de Test sin tocar sus datos —Producción resultó ser un prefijo exacto de Test—, y el segundo lleva Test al esquema de la rama de Honduras Fácil, con las diez migraciones de SIGER. Cada uno se corrió dos veces y se comprobó abriendo el modelo real de cada rama contra su base. Incluye el sandbox de desarrollo en SQL Server 2025.',
  '2026-09-21', '2026-09-28'),

 (N'Renombre a Honduras Fácil',
  N'El portal ciudadano pasa de Honduras Simple a Honduras Fácil, y Gestión Digital muestra el mismo nombre en el menú, el catálogo de módulos, la pantalla Publicado y los permisos. Las claves de permisos no cambian, así que los permisos ya concedidos se conservan. El manual se puso al día con el nombre nuevo y con un recorrido de prueba.',
  '2026-09-28', '2026-09-28'),

 (N'Ajustes de Gestión Digital para operar el portal',
  N'Captura por lotes ofrece las instituciones de las fichas y no solo la propia: un técnico de DIGER no podía acotar el lote a INPREMA. Al entrar, cada quien va a la primera pantalla que puede abrir, en vez de a Tableros. Los avisos de ficha incompleta dicen de qué portal hablan. En Honduras Fácil, el selector de orden ofrece Relevancia al buscar.',
  '2026-09-28', '2026-09-28'),

 (N'Preparación del despliegue',
  N'Script de despliegue de Honduras Fácil en IIS —grupo de aplicaciones, variables, permisos y esquema—, esquema SQL que se puede correr las veces que haga falta, guía de despliegue paso a paso y respaldo limpio de la base. Se reparó también el script de despliegue de Gestión Digital y se alineó con el de Honduras Fácil. El portal reintenta la sincronización al arrancar si la API todavía no responde.',
  '2026-09-28', '2026-09-28'),

 (N'Despliegue en el ambiente de pruebas (Test)',
  N'Gestión Digital, su API y Honduras Fácil corriendo en el servidor de Test (192.168.0.52, IIS), con datos reales que viajan de Gestión Digital al portal ciudadano por la API. La API se expone a Honduras Fácil por un enlace local, sin salir del servidor. Se escribió un diagnóstico de solo lectura que recorre la cadena entera —Gestión Digital, API, sincronización y portal— y dice en qué eslabón se corta un trámite que no aparece; valida certificados y no expone la clave. Con él se resolvieron los fallos de configuración del primer despliegue.',
  '2026-10-14', '2026-10-15'),

 (N'Honduras Fácil abierto a todas las instituciones',
  N'Se retira el corte piloto de tres instituciones: el portal replica las fichas publicadas de todas. La sincronización se hizo más segura: solo un «no existe» retira un trámite —cualquier otro fallo de la API ya no borra nada—, guarda ficha por ficha para que una mala no tumbe el resto, y va a un ritmo que la API aguanta. Ensayo: 714 de 719 trámites en 188 segundos, sin un solo rechazo por exceso de peticiones. Los largos de las columnas quedaron iguales a los de Gestión Digital.',
  '2026-10-14', '2026-10-14'),

 (N'Institución obligatoria en las fichas',
  N'La ficha se publicaba y no aparecía nunca en el portal, sin un error en ninguna parte: el Editor guardaba la institución solo como texto. Ahora se elige de una lista de las instituciones registradas y sin una no se guarda. Un script completó las 343 fichas de Test que no tenían institución: enlazó 172 a instituciones existentes y dio de alta 17 instituciones para las 171 restantes. Al publicar, la pantalla avisa con su código si una ficha no llegará al portal.',
  '2026-10-14', '2026-10-14'),

 (N'Identidad visual oficial del portal',
  N'El escudo nacional y el logo oficial de DIGER, los mismos de Gestión Digital, reemplazan los espacios que estaban marcados como pendientes: en la cabecera, el acceso, la hoja impresa y el pie. El icono del navegador es el de Gestión Digital. La pantalla de inicio de sesión quedó alineada con la cabecera y el pie, el nombre de DIGER se corrigió en el pie —Dirección de Gestión por Resultados— y se retiró el enlace al Portal de Transparencia del menú.',
  '2026-10-14', '2026-10-14'),

 (N'Mejoras de Gestión Digital por observaciones de los usuarios en Test',
  N'Lo que salió de las primeras revisiones con usuarios. Completitud de fichas rediseñada: panel con la completitud general, listado de 20 en 20, búsqueda, filtros, orden a elegir y encabezado fijo. Publicado filtra las fichas sin avisos. Captura por lotes va por páginas y su selector tiene diseño. La ficha y el llenado asistido ya no se salen del cuadro, la barra del menú ya no se encima en pantallas medianas y el paginador ya no se traba después de publicar. 915 pruebas automáticas en verde.',
  '2026-10-14', '2026-10-15');

INSERT INTO ProyectoActividades
    (EntregableId, Orden, Nombre, Descripcion,
     FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal,
     AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
SELECT @ent,
       (SELECT ISNULL(MAX(Orden), 0) FROM ProyectoActividades WHERE EntregableId = @ent)
         + ROW_NUMBER() OVER (ORDER BY n.IniReal, n.FinReal, n.Nombre),
       n.Nombre, n.Descripcion,
       n.IniReal, n.FinReal, n.IniReal, n.FinReal,
       100, N'Completada', @idJamil, @nomJamil, @ahora, @actor
  FROM @nuevas n
 WHERE NOT EXISTS (SELECT 1 FROM ProyectoActividades a
                    WHERE a.EntregableId = @ent AND a.Nombre = n.Nombre);

DECLARE @insertadas int = @@ROWCOUNT;

-- ══ PASO 2 · La actividad abierta se parte en dos ══════════════════════════
--    «Pruebas de aceptación y puesta en producción» pasa a ser solo la
--    aceptación, que ya empezó. La puesta en producción va aparte porque tiene
--    otro freno: no depende del trabajo sino de una autorización.
UPDATE ProyectoActividades
   SET Nombre          = N'Pruebas de aceptación con usuarios',
       Descripcion     = N'Arrancaron el 14 de octubre en el ambiente de Test, ya con datos reales. Las primeras observaciones —de un compañero revisando Gestión Digital y del propio uso al publicar— ya están corregidas. Falta recorrer los planes de prueba escritos en septiembre y probar el portal con ciudadanos: que se entienda, que se vea bien en un teléfono y que cada quien encuentre su trámite.',
       Estado          = CASE WHEN @pctAceptacion >= 100 THEN N'Completada'
                              WHEN @pctAceptacion > 0    THEN N'EnProceso'
                              ELSE N'Pendiente' END,
       AvancePct       = @pctAceptacion,
       FechaInicioReal = ISNULL(FechaInicioReal, '2026-10-14'),
       FechaInicioPlan = ISNULL(FechaInicioPlan, '2026-10-14'),
       UpdatedAt       = @ahora,
       UpdatedBy       = @actor
 WHERE EntregableId = @ent
   AND Nombre       = N'Pruebas de aceptación y puesta en producción'
   AND NOT EXISTS (SELECT 1 FROM ProyectoActividades x
                    WHERE x.EntregableId = @ent AND x.Nombre = N'Pruebas de aceptación con usuarios');

DECLARE @partida int = @@ROWCOUNT;

INSERT INTO ProyectoActividades
    (EntregableId, Orden, Nombre, Descripcion,
     FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal,
     AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
SELECT @ent, (SELECT ISNULL(MAX(Orden), 0) + 1 FROM ProyectoActividades WHERE EntregableId = @ent),
       N'Puesta en producción',
       N'No ha empezado: espera autorización. Lo que el despliegue en Test dejó probado y listo para repetir: el script de despliegue, el esquema de la base y la guía paso a paso. Al hacerlo, conviene dejar la cadena de conexión y la clave de la API como variables del grupo de aplicaciones de IIS —en Test, un despliegue que reemplazó el archivo de configuración tumbó el portal dos veces—, el grupo siempre encendido para que la sincronización no se detenga, y la dirección del proxy para el límite de peticiones.',
       NULL, NULL, NULL, NULL,
       0, N'Pendiente', @idJamil, @nomJamil, @ahora, @actor
 WHERE NOT EXISTS (SELECT 1 FROM ProyectoActividades a
                    WHERE a.EntregableId = @ent AND a.Nombre = N'Puesta en producción');

DECLARE @produccionNueva int = @@ROWCOUNT;

--    Las dos abiertas, al final y en este orden: primero la aceptación, después
--    producción. Las cerradas del período entraron detrás de la de aceptación, así
--    que se renumera todo de corrido —1, 2, 3…— para no dejar el hueco que deja
--    la aceptación al moverse.
;WITH orden AS (
    SELECT Id, Orden,
           ROW_NUMBER() OVER (ORDER BY
               CASE Nombre WHEN N'Pruebas de aceptación con usuarios' THEN 1
                           WHEN N'Puesta en producción'               THEN 2
                           ELSE 0 END,
               Orden, Id) AS Nuevo
      FROM ProyectoActividades
     WHERE EntregableId = @ent
)
UPDATE orden SET Orden = Nuevo WHERE Orden <> Nuevo;

-- ══ PASO 3 · Riesgos ═══════════════════════════════════════════════════════
--    Se buscan por el comienzo de la descripción, igual que al registrarlos.

-- 3a. El catálogo vacío: resuelto.
UPDATE ProyectoRiesgos
   SET Estado        = N'Cerrado',
       FechaCierre   = '2026-10-14',
       FechaRevision = @hoy,
       Mitigacion    = N'Resuelto el 14 de octubre. El catálogo de instituciones se completó, cada ficha tiene ahora su institución —el Editor la exige de una lista y un script completó las 343 que no la tenían— y Honduras Fácil trae las fichas publicadas de todas las instituciones, no solo de las tres del piloto. En Test el portal ya muestra los trámites publicados.'
 WHERE ProyectoId = @pry AND Estado <> N'Cerrado'
   AND Descripcion LIKE N'El catálogo del portal ciudadano sale vacío%';

DECLARE @cerradoCatalogo int = @@ROWCOUNT;

-- 3b. Nunca se ha desplegado: ya está en Test. Lo que queda es producción, que
--     se registra aparte porque su freno es otro.
UPDATE ProyectoRiesgos
   SET Estado        = N'Cerrado',
       FechaCierre   = '2026-10-14',
       FechaRevision = @hoy,
       Mitigacion    = N'Resuelto el 14 de octubre: Honduras Fácil, Gestión Digital y su API están desplegados en el servidor de Test y funcionando de punta a punta. El paso a producción queda como riesgo aparte, porque ya no depende de decisiones técnicas sino de una autorización.'
 WHERE ProyectoId = @pry AND Estado <> N'Cerrado'
   AND Descripcion LIKE N'Honduras Simple nunca se ha desplegado%';

DECLARE @cerradoDespliegue int = @@ROWCOUNT;

-- 3c. Pruebas con personas: ya empezaron.
UPDATE ProyectoRiesgos
   SET Estado        = N'EnTratamiento',
       FechaRevision = @hoy,
       Mitigacion    = N'En tratamiento desde el 14 de octubre: las pruebas arrancaron en Test con datos reales, y las primeras observaciones de usuarios ya están corregidas. Falta recorrer los planes de prueba escritos —sesenta casos— y probar con ciudadanos.'
 WHERE ProyectoId = @pry AND Estado = N'Abierto'
   AND Descripcion LIKE N'No se ha hecho ninguna prueba con personas%';

DECLARE @enTratamiento int = @@ROWCOUNT;

-- 3d. Los nuevos.
DECLARE @riesgos TABLE (
    Descripcion  nvarchar(500),
    Categoria    nvarchar(20),
    Probabilidad nvarchar(10),
    Impacto      nvarchar(10),
    Estrategia   nvarchar(20),
    Mitigacion   nvarchar(1000),
    Deteccion    date);

INSERT INTO @riesgos VALUES

 (N'Honduras Fácil no tiene todavía autorización para pasar a producción. El trabajo técnico está hecho y probado en Test, pero mientras no se autorice el portal no llega al ciudadano.',
  N'Institucional', N'Alta', N'Alta', N'Mitigar',
  N'Test funciona de punta a punta con datos reales. El despliegue está escrito y probado —script, esquema de la base y guía paso a paso—, así que el día que se autorice se repite lo mismo en producción.',
  '2026-10-15'),

 (N'La configuración del servidor se pierde al desplegar: la cadena de conexión y la clave de la API viven en un archivo que el paquete nuevo reemplaza. En Test pasó dos veces el 14 de octubre, y las dos veces el portal no arrancó.',
  N'Tecnico', N'Media', N'Alta', N'Mitigar',
  N'Pasar esos datos a variables del grupo de aplicaciones de IIS, que el script de despliegue ya sabe escribir: así el archivo se puede reemplazar sin romper nada. Además, dejar el grupo siempre encendido para que la sincronización no se detenga cuando nadie entra. Mientras tanto, en cada despliegue manual se excluye ese archivo.',
  '2026-10-14'),

 (N'El catálogo del portal tiene pocas fichas publicadas. Publicar es un acto deliberado y la mayoría de las fichas de Test siguen sin publicar: un trámite que no se publica no llega al ciudadano aunque esté completo.',
  N'Operativo', N'Alta', N'Media', N'Mitigar',
  N'Publicado ahora filtra las fichas sin avisos, para publicar primero las completas, y Completitud muestra qué le falta a cada una y qué institución va más atrasada. Conviene definir quién publica y con qué criterio.',
  '2026-10-15');

INSERT INTO ProyectoRiesgos
    (ProyectoId, Descripcion, Categoria, Probabilidad, Impacto, Estrategia, Estado,
     Mitigacion, ResponsableId, Responsable, FechaDeteccion, RegistradoPor, RegistradoEn)
SELECT @pry, r.Descripcion, r.Categoria, r.Probabilidad, r.Impacto, r.Estrategia, N'Abierto',
       r.Mitigacion, @idJamil, @nomJamil, r.Deteccion, @actor, @ahora
  FROM @riesgos r
 WHERE NOT EXISTS (SELECT 1 FROM ProyectoRiesgos x
                    WHERE x.ProyectoId = @pry AND LEFT(x.Descripcion, 120) = LEFT(r.Descripcion, 120));

DECLARE @riesgosNuevos int = @@ROWCOUNT;

-- ══ PASO 4 · Recalcular el avance con la regla del dominio ═════════════════
--    La misma de la puesta al día anterior: promedio de los entregables
--    vigentes, y cada entregable el promedio de sus actividades vigentes.
;WITH avancePorEntregable AS (
    SELECT e.ProyectoId, e.Id,
           COALESCE(
             (SELECT CONVERT(int, ROUND(AVG(CONVERT(float, a.AvancePct)), 0))
                FROM ProyectoActividades a
               WHERE a.EntregableId = e.Id AND a.Estado <> N'Cancelada'),
             CASE e.Estado WHEN N'Completado' THEN 100 WHEN N'EnProceso' THEN 50 ELSE 0 END
           ) AS Avance
      FROM ProyectoEntregables e
     WHERE e.ProyectoId = @pry AND e.Estado <> N'Cancelado'
)
UPDATE p
   SET p.AvancePct = x.Pct,
       p.UpdatedAt = @ahora,
       p.UpdatedBy = @actor
  FROM Proyectos p
  CROSS APPLY (
      SELECT CONVERT(int, ROUND(AVG(CONVERT(float, ae.Avance)), 0)) AS Pct
        FROM avancePorEntregable ae
       WHERE ae.ProyectoId = p.Id) x
 WHERE p.Id = @pry;

DECLARE @avance int = (SELECT AvancePct FROM Proyectos WHERE Id = @pry);

-- ══ PASO 5 · Avance reportado ══════════════════════════════════════════════
DECLARE @actAceptacion int = (
    SELECT TOP 1 Id FROM ProyectoActividades
     WHERE EntregableId = @ent AND Nombre = N'Pruebas de aceptación con usuarios');

INSERT INTO ProyectoAvances
    (ProyectoId, EntregableId, ActividadId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo)
SELECT @pry, @ent, @actAceptacion, @ahora, @nomJamil,
       N'Puesta al día del 22 de septiembre al 15 de octubre. Honduras Fácil está desplegado en Test y funcionando de punta a punta: los trámites publicados en Gestión Digital llegan al portal ciudadano por la API. Para llegar ahí se completó el catálogo de instituciones, se escribieron los scripts de esquema y de despliegue, se abrió el portal a todas las instituciones con una sincronización más segura, y se hizo obligatoria la institución de cada ficha —343 fichas completadas—. El portal tiene ya la identidad visual oficial, con el escudo y el logo de DIGER. Arrancaron las pruebas de aceptación, y lo que señalaron los primeros usuarios ya está corregido. 915 pruebas automáticas en verde. Falta terminar la aceptación y pasar a producción.',
       @avance,
       N'El paso a producción espera autorización. Además, antes de hacerlo conviene sacar del archivo de configuración la cadena de conexión y la clave de la API, que en Test se perdieron dos veces al desplegar.'
 WHERE NOT EXISTS (
        SELECT 1 FROM ProyectoAvances
         WHERE ProyectoId = @pry
           AND Descripcion LIKE N'Puesta al día del 22 de septiembre al 15 de octubre.%');

DECLARE @avancesNuevos int = @@ROWCOUNT;

-- ══ PASO 6 · Bitácora ══════════════════════════════════════════════════════
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT @pry, N'ModificacionEstructura',
       N'Puesta al día desde el historial de Honduras Fácil y Gestión Digital: se registran '
       + CONVERT(nvarchar(10), @insertadas) + N' frentes cerrados del 22 de septiembre al 15 de octubre'
       + CASE WHEN @partida > 0 OR @produccionNueva > 0
              THEN N', y la actividad de cierre se parte en dos: pruebas de aceptación, que ya empezaron, y puesta en producción, que espera autorización'
              ELSE N'' END
       + N'.',
       @actor, @ahora
 WHERE @insertadas > 0 OR @partida > 0 OR @produccionNueva > 0;

INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT @pry, N'Riesgo',
       N'Riesgos al día: ' + CONVERT(nvarchar(10), @cerradoCatalogo + @cerradoDespliegue)
       + N' cerrados (catálogo vacío y portal sin desplegar), '
       + CONVERT(nvarchar(10), @enTratamiento) + N' en tratamiento (pruebas con personas) y '
       + CONVERT(nvarchar(10), @riesgosNuevos)
       + N' nuevos: falta de autorización para producción, configuración que se pierde al desplegar y pocas fichas publicadas.',
       @actor, @ahora
 WHERE @cerradoCatalogo + @cerradoDespliegue + @enTratamiento + @riesgosNuevos > 0;

INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT @pry, N'ModificacionFicha',
       N'El avance queda en ' + CONVERT(nvarchar(10), @avance)
       + N' %, calculado con la regla del dominio sobre el árbol de actividades. No llega a 100 a propósito: faltan terminar las pruebas de aceptación y la puesta en producción, que espera autorización.',
       @actor, @ahora
 WHERE @avance <> @avancePrevio;

-- ══ Reporte ════════════════════════════════════════════════════════════════
IF @avance <> @avanceEsperado
    PRINT N'AVISO: el cálculo dio ' + CONVERT(nvarchar(10), @avance) + N' % y se esperaba '
        + CONVERT(nvarchar(10), @avanceEsperado) + N' %. La base de destino no estaba en el estado previsto: '
        + N'revise el detalle de actividades antes de aplicar.';
ELSE
    PRINT N'El avance quedó en ' + CONVERT(nvarchar(10), @avance) + N' %, como se esperaba.';

PRINT N'Actividades nuevas: '     + CONVERT(nvarchar(10), @insertadas + @produccionNueva)
    + N' · Actividad partida: '   + CONVERT(nvarchar(10), @partida)
    + N' · Riesgos cerrados: '    + CONVERT(nvarchar(10), @cerradoCatalogo + @cerradoDespliegue)
    + N' · En tratamiento: '      + CONVERT(nvarchar(10), @enTratamiento)
    + N' · Riesgos nuevos: '      + CONVERT(nvarchar(10), @riesgosNuevos)
    + N' · Avances nuevos: '      + CONVERT(nvarchar(10), @avancesNuevos);

SELECT  a.Orden,
        Actividad = LEFT(a.Nombre, 62),
        a.Estado,
        Pct       = a.AvancePct,
        Inicio    = ISNULL(CONVERT(varchar(10), a.FechaInicioReal, 23), N'—'),
        Fin       = ISNULL(CONVERT(varchar(10), a.FechaFinReal, 23), N'—')
  FROM  ProyectoActividades a
 WHERE  a.EntregableId = @ent
 ORDER BY a.Orden;

SELECT  Riesgo = LEFT(r.Descripcion, 70),
        r.Categoria, r.Probabilidad, r.Impacto, r.Estado
  FROM  ProyectoRiesgos r
 WHERE  r.ProyectoId = @pry
 ORDER BY r.Id;

SELECT  Proyecto = p.Codigo, Nombre = LEFT(p.Nombre, 40),
        Avance = CONVERT(varchar(5), p.AvancePct) + N' %', p.Estado,
        Actividades = (SELECT COUNT(*) FROM ProyectoActividades a WHERE a.EntregableId = @ent),
        Cerradas    = (SELECT COUNT(*) FROM ProyectoActividades a WHERE a.EntregableId = @ent AND a.AvancePct = 100)
  FROM  Proyectos p WHERE p.Id = @pry;

-- ── Aplicar o deshacer ─────────────────────────────────────────────────────
IF @soloSimular = 1
BEGIN
    ROLLBACK;
    PRINT N'';
    PRINT N'MODO SIMULACIÓN: no se guardó nada. Cambie @soloSimular a 0 para aplicar.';
END
ELSE
BEGIN
    COMMIT;
    PRINT N'';
    PRINT N'APLICADO.';
END
