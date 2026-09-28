/* ============================================================================
   PRY-2026-25 · Ventanilla Única — Honduras Ágil
   Puesta al día de la ficha con lo hecho entre el 2 y el 21 de septiembre
   ----------------------------------------------------------------------------
   Qué hace, en orden:

     1. El API intermediario pasa de 50 % a cerrado. La v1 de la API pública de
        trámites está entregada y Honduras Simple lee de ella su catálogo entero.
     2. Registra los seis frentes que ninguna ficha recoge todavía: el rediseño
        Amanecer, la búsqueda por relevancia, el registro de búsquedas anónimo,
        los planes de prueba, el manual de uso y el informe de estado.
     3. Abre la actividad de cierre —pruebas de aceptación y puesta en
        producción—, que es la que impide que el proyecto llegue a 100 %.
     4. Registra los cuatro riesgos vivos que el informe del 21 de septiembre
        deja por escrito y que la ficha no tenía.
     5. Recalcula el avance con la regla del dominio y lo deja en 93 %.
     6. Deja constancia en avances y en bitácora.

   Sobre el 93 %: NO se escribe a mano. «Proyecto.RecalcularAvance» se dispara
   sola cada vez que alguien guarda la estructura o reporta un avance, así que
   un porcentaje puesto a dedo dura hasta la primera edición desde el portal.
   Acá sale del árbol: catorce actividades, trece cerradas y la de aceptación
   todavía en cero, dan 1300/14 = 92,857 → 93 %. El día que empiecen las
   pruebas, el número sube solo.

   Sobre las fechas: el reloj de la máquina de desarrollo iba once días
   atrasado hasta el 21 de septiembre, así que los commits de esa semana están
   sellados entre el 7 y el 10. Acá se usan las fechas reales —del 18 al 21—,
   que es lo que el propio informe de estado declara.

   Se puede correr las veces que haga falta: cada actividad y cada riesgo se
   busca antes por su nombre y sólo se crea el que falte.

   ARRANCA EN MODO SIMULACIÓN. Revise el reporte y después cambie @soloSimular
   a 0 para aplicar.

       sqlcmd -S <servidor> -d <base> -b -I -f 65001 -i 2026-09-28_pry_2026_25_puesta_al_dia.sql
   ============================================================================ */

SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ── Parámetros ─────────────────────────────────────────────────────────────
DECLARE @soloSimular bit           = 1;                 -- <<< cambie a 0 para aplicar
DECLARE @actor       nvarchar(200) = N'Jamil Garcia';
DECLARE @avanceEsperado int        = 93;                -- lo que debe dar el cálculo

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

/* El entregable se busca por la actividad del API, que es la que lo identifica
   sin depender del nombre —que ya cambió una vez, cuando se aplanaron los
   entregables espejo—. Si aparece más de uno, se toma el de menor orden. */
DECLARE @ent int = (
    SELECT TOP 1 e.Id
      FROM ProyectoEntregables e
     WHERE e.ProyectoId = @pry
       AND EXISTS (SELECT 1 FROM ProyectoActividades a
                    WHERE a.EntregableId = e.Id
                      AND a.Nombre = N'API intermediario con el portal de digitalización')
     ORDER BY e.Orden);

IF @ent IS NULL
    SET @ent = (SELECT TOP 1 Id FROM ProyectoEntregables
                 WHERE ProyectoId = @pry AND Estado <> N'Cancelado' ORDER BY Orden);

IF @ent IS NULL
BEGIN
    ROLLBACK;
    RAISERROR('PRY-2026-25 no tiene entregable vigente donde colgar las actividades. Corra antes aplanar_entregables_espejo.sql. No se hizo nada.', 16, 1);
    RETURN;
END

-- ── Responsables ───────────────────────────────────────────────────────────
DECLARE @idJamil   uniqueidentifier = (SELECT Id FROM Usuarios WHERE Correo = N'jgarcia@diger.gob.hn');
DECLARE @nomJamil  nvarchar(400)    = (SELECT Nombre FROM Usuarios WHERE Id = @idJamil);

IF @idJamil IS NULL
BEGIN
    ROLLBACK;
    RAISERROR('No existe el usuario jgarcia@diger.gob.hn en esta base. No se hizo nada.', 16, 1);
    RETURN;
END

/* El avance de partida, para no ensuciar la bitácora en una corrida que no
   cambie nada: el guion se puede repetir, la bitácora no debe repetirse. */
DECLARE @avancePrevio int = (SELECT AvancePct FROM Proyectos WHERE Id = @pry);

-- ══ PASO 1 · El API intermediario queda cerrado ════════════════════════════
--    La v1 de la API pública de trámites está entregada (GestionGD-Api) y
--    Honduras Simple lee de ella su catálogo entero: no tiene datos propios,
--    sólo los replica. El frente que quedó abierto el 24 de agosto se cierra.
--    Se usa el mayor entre lo que ya hay y 100 para no bajar nada si alguien
--    lo movió antes.
UPDATE ProyectoActividades
   SET AvancePct    = 100,
       Estado       = N'Completada',
       FechaFinReal = ISNULL(FechaFinReal, '2026-09-18'),
       FechaFinPlan = ISNULL(FechaFinPlan, '2026-09-18'),
       Descripcion  = ISNULL(NULLIF(Descripcion, N''),
                        N'API pública de trámites v1: el portal ciudadano toma de acá el catálogo completo —fichas, requisitos, pasos y lugares de atención— sin guardar datos propios.'),
       UpdatedAt    = @ahora,
       UpdatedBy    = @actor
 WHERE EntregableId = @ent
   AND Nombre       = N'API intermediario con el portal de digitalización'
   AND AvancePct    < 100;

DECLARE @apiCerrado int = @@ROWCOUNT;

-- ══ PASO 2 · Los frentes que la ficha no recoge ════════════════════════════
--    Seis cerrados y el de aceptación abierto. El abierto va con avance 0 y
--    estado Pendiente a propósito: ninguna de sus dos mitades ha empezado.
DECLARE @nuevas TABLE (
    Nombre      nvarchar(600),
    Descripcion nvarchar(4000),
    IniReal     date,
    FinReal     date,
    Pct         int,
    Estado      nvarchar(60));

INSERT INTO @nuevas VALUES

 (N'Rediseño Amanecer e identidad de Honduras Simple',
  N'Rediseño completo del portal ciudadano en siete etapas: capa de tokens con medición de contraste, cascarón común y pie, portada, catálogo, ficha del trámite, instituciones, ayuda y glosario, cuenta y sesión, Mis Trámites, las pantallas de error 404 y 500 por separado y la hoja impresa. Los cuatro temas quedaron medidos en el navegador. Al cerrar, Amanecer pasa a ser el valor por omisión, se retira la bandera y se borra la piel clásica. Acá el portal deja de llamarse Honduras Ágil de cara al ciudadano y pasa a ser Honduras Simple.',
  '2026-09-18', '2026-09-19', 100, N'Completada'),

 (N'Búsqueda por relevancia del portal ciudadano',
  N'La búsqueda deja de ordenar por nombre y pasa a puntuar por palabras: primero cuántas acierta, después cuánto pesan. Índice propio con una fila por trámite, campo y palabra, que se escribe junto con el catálogo y se rehace solo si cambian las reglas. Encuentra por nombre, descripción, requisitos, pasos y lugares. Incluye sugerencias por prefijo sobre la misma maquinaria que los resultados, desplegable accesible con teclado completo y sin innerHTML, corrección de erratas por distancia de edición acotada con transposición —«pasaporet» cae a una edición de «pasaporte»— y aviso de corrección con salida literal para desdecirse, anunciado a los lectores de pantalla. Se corrige la palabra, no el trámite.',
  '2026-09-20', '2026-09-20', 100, N'Completada'),

 (N'Registro de búsquedas sin identidades',
  N'Se guarda qué se buscó, nunca quién: ni dirección, ni sesión, ni hora, y cualquier número de seis cifras o más queda enmascarado. El término se normaliza y se ordena antes de escribirse. Se suma en memoria y se vuelca cada treinta segundos, con retención de veinticuatro meses. Incluye el extremo de sugerencias con tope de entrada, caché de un minuto y límite por dirección con cabeceras reenviadas. Comprobado sobre datos reales.',
  '2026-09-20', '2026-09-20', 100, N'Completada'),

 (N'Planes de prueba del portal y del ciclo completo',
  N'Tres documentos que suman sesenta casos: el flujo de veintisiete casos por todo el portal —portada, catálogo, ficha, accesibilidad y teléfono—, el plan de la búsqueda con diecinueve pruebas de navegador y diez tareas de base de datos, y el plan manual de catorce comprobaciones de sincronización, base unificada y cronograma. Escritos, repasados contra lo que se vio al ejecutarlos y repartidos entre quien tiene base y quien no. Ninguno se ha estrenado.',
  '2026-09-19', '2026-09-21', 100, N'Completada'),

 (N'Manual de uso de Honduras Simple',
  N'Manual del portal ciudadano con capturas, para quien lo va a usar y para quien lo va a probar.',
  '2026-09-21', '2026-09-21', 100, N'Completada'),

 (N'Informe de estado de los dos sistemas',
  N'Primer documento que mira Honduras Simple y Gestión Digital juntos, porque separados esconden lo que de verdad frena: el portal ciudadano lee su catálogo entero de la API del sistema interno. Deja por escrito qué está hecho, qué falta, los frenos compartidos y las decisiones que esperan. Marca además sus propios huecos: lo de producción sale de respaldos, no de mirar el servidor.',
  '2026-09-21', '2026-09-21', 100, N'Completada'),

 (N'Pruebas de aceptación y puesta en producción',
  N'Lo único que queda abierto, y son dos mitades que no han empezado. Las pruebas con personas: las trescientas veintiuna automáticas dicen que la lógica hace lo que se le pidió, no que se entienda, ni que se vea bien en un teléfono, ni que un ciudadano encuentre su trámite; los dos fallos de la última semana aparecieron sólo en el navegador. Y el despliegue: Honduras Simple no ha salido de la máquina de desarrollo —no hay servidor, ni publicación automática, ni Dockerfile, y el appsettings de producción tiene en blanco la cadena de conexión y la dirección de la API, esperando a que se decida dónde vive.',
  NULL, NULL, 0, N'Pendiente');

INSERT INTO ProyectoActividades
    (EntregableId, Orden, Nombre, Descripcion,
     FechaInicioPlan, FechaFinPlan, FechaInicioReal, FechaFinReal,
     AvancePct, Estado, ResponsableId, Responsable, CreatedAt, CreatedBy)
SELECT @ent,
       (SELECT ISNULL(MAX(Orden), 0) FROM ProyectoActividades WHERE EntregableId = @ent)
         + ROW_NUMBER() OVER (ORDER BY CASE WHEN n.IniReal IS NULL THEN 1 ELSE 0 END, n.IniReal, n.Nombre),
       n.Nombre, n.Descripcion,
       n.IniReal, n.FinReal, n.IniReal, n.FinReal,
       n.Pct, n.Estado, @idJamil, @nomJamil, @ahora, @actor
  FROM @nuevas n
 WHERE NOT EXISTS (SELECT 1 FROM ProyectoActividades a
                    WHERE a.EntregableId = @ent AND a.Nombre = n.Nombre);

DECLARE @insertadas int = @@ROWCOUNT;

-- ══ PASO 3 · Los riesgos vivos ═════════════════════════════════════════════
--    La ficha no tenía ninguno. Estos cuatro salen del informe del 21 de
--    septiembre; se registran abiertos y con su mitigación, que en tres de los
--    cuatro casos ya está escrita y sólo espera una decisión.
DECLARE @riesgos TABLE (
    Descripcion  nvarchar(2000),
    Categoria    nvarchar(60),
    Probabilidad nvarchar(40),
    Impacto      nvarchar(40),
    Estrategia   nvarchar(60),
    Mitigacion   nvarchar(2000),
    Deteccion    date);

INSERT INTO @riesgos VALUES

 (N'El catálogo del portal ciudadano sale vacío: las 1.057 filas de trámites de la base unificada tienen la institución en blanco y ninguna está publicada. La API filtra y cuenta por esa columna, y Honduras Simple pide su corte piloto —INPREMA, IHTT y CONSUCOOP— por esa misma columna. No es una avería del portal: no hay nada publicado que enseñar.',
  N'Tecnico', N'Alta', N'Alta', N'Mitigar',
  N'El arreglo está escrito y probado: asigna la institución a 473 de los 1.057 trámites, los 57 del piloto incluidos. No se ha ejecutado porque cambia datos de una base que va a producción y espera visto bueno. Medido el 31 de agosto; conviene reconfirmarlo antes de actuar.',
  '2026-08-31'),

 (N'Honduras Simple nunca se ha desplegado. No hay servidor, ni publicación automática, ni Dockerfile, y el appsettings de producción tiene en blanco la cadena de conexión y la dirección de la API. Mientras no se decida dónde vive, el portal no sale de la máquina de desarrollo.',
  N'Operativo', N'Alta', N'Alta', N'Mitigar',
  N'Faltan tres decisiones concretas: servidor, base de datos y dirección de la API. Los dos huecos de configuración están marcados y esperando el dato.',
  '2026-09-21'),

 (N'No se ha hecho ninguna prueba con personas. Las 321 pruebas automáticas comprueban que la lógica hace lo que se le pidió, no que se entienda, ni que se vea bien en un teléfono, ni que un ciudadano encuentre su trámite.',
  N'Operativo', N'Alta', N'Media', N'Mitigar',
  N'Los tres planes están escritos —sesenta casos en total—. El flujo de veintisiete casos se puede empezar sin preparar nada; el de la búsqueda necesita el sandbox listo; el manual depende de que el catálogo tenga fichas publicadas, o sea del riesgo del catálogo vacío.',
  '2026-09-21'),

 (N'El límite de peticiones del autocompletado es para todo el país junto: mientras ProxiesConocidos esté vacío, el tope de sesenta peticiones por minuto lo comparten todos los ciudadanos, y uno escribiendo rápido puede dejar sin servicio a los demás.',
  N'Tecnico', N'Media', N'Media', N'Mitigar',
  N'Degrada con seguridad, así que no bloquea el desarrollo, pero hay que llenarlo antes de publicar: es una línea de configuración con la dirección real del proxy.',
  '2026-09-20');

INSERT INTO ProyectoRiesgos
    (ProyectoId, Descripcion, Categoria, Probabilidad, Impacto, Estrategia, Estado,
     Mitigacion, ResponsableId, Responsable, FechaDeteccion, RegistradoPor, RegistradoEn)
SELECT @pry, r.Descripcion, r.Categoria, r.Probabilidad, r.Impacto, r.Estrategia, N'Abierto',
       r.Mitigacion, @idJamil, @nomJamil, r.Deteccion, @actor, @ahora
  FROM @riesgos r
 WHERE NOT EXISTS (SELECT 1 FROM ProyectoRiesgos x
                    WHERE x.ProyectoId = @pry AND LEFT(x.Descripcion, 120) = LEFT(r.Descripcion, 120));

DECLARE @riesgosNuevos int = @@ROWCOUNT;

-- ══ PASO 4 · El entregable ═════════════════════════════════════════════════
--    Sigue en proceso —la aceptación está abierta— pero no tenía fecha de
--    arranque. Se le pone la del primer frente real del proyecto.
UPDATE ProyectoEntregables
   SET FechaPlan  = ISNULL(FechaPlan, '2026-09-21'),
       UpdatedAt  = @ahora,
       UpdatedBy  = @actor
 WHERE Id = @ent;

--    Y el proyecto tampoco tenía fecha real de arranque, aunque su primer
--    avance está reportado desde el 23 de agosto.
UPDATE Proyectos
   SET FechaInicioReal = ISNULL(FechaInicioReal, '2026-08-23'),
       UpdatedAt       = @ahora,
       UpdatedBy       = @actor
 WHERE Id = @pry;

-- ══ PASO 5 · Recalcular el avance con la regla del dominio ═════════════════
--    Promedio de los entregables vigentes, y cada entregable el promedio de
--    sus actividades vigentes. Es la misma regla de Proyecto.RecalcularAvance
--    y EntregableProyecto.AvanceCalculado, con la salida por Estado cuando el
--    entregable todavía no tiene actividades. Se calcula, no se escribe a
--    mano, para que coincida con lo que el portal muestra al recargar —y para
--    que siga coincidiendo cuando alguien guarde la ficha desde el portal, que
--    dispara este mismo cálculo.
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

-- ══ PASO 6 · Avance reportado ══════════════════════════════════════════════
--    Un solo reporte que cuenta el período, imputado a la actividad que queda
--    abierta, que es donde está la historia que sigue.
DECLARE @actAceptacion int = (
    SELECT TOP 1 Id FROM ProyectoActividades
     WHERE EntregableId = @ent AND Nombre = N'Pruebas de aceptación y puesta en producción');

INSERT INTO ProyectoAvances
    (ProyectoId, EntregableId, ActividadId, Fecha, Autor, Descripcion, PorcentajeReportado, Bloqueo)
SELECT @pry, @ent, @actAceptacion, @ahora, @nomJamil,
       N'Puesta al día del 2 al 21 de septiembre. Cerrado el API intermediario: la v1 de la API pública de trámites está entregada y el portal ciudadano lee de ella su catálogo entero. Cerrados además el rediseño Amanecer en siete etapas —con el que el portal pasa a llamarse Honduras Simple—, la búsqueda por relevancia con sugerencias y corrección de erratas, el registro de búsquedas que no guarda identidades, los tres planes de prueba que suman sesenta casos, el manual de uso con capturas y el primer informe que mira los dos sistemas juntos. 321 pruebas automáticas en verde y compilación sin avisos. Queda abierta la aceptación: ni una sola prueba con personas, y el portal sin desplegar.',
       @avance,
       N'Tres frenos: el catálogo sale vacío mientras los 1.057 trámites tengan la institución en blanco —el arreglo está escrito y espera visto bueno—; no hay servidor ni dirección de API decididos para Honduras Simple; y no se ha hecho ninguna prueba con personas.'
 WHERE NOT EXISTS (
        SELECT 1 FROM ProyectoAvances
         WHERE ProyectoId = @pry
           AND Descripcion LIKE N'Puesta al día del 2 al 21 de septiembre.%');

DECLARE @avancesNuevos int = @@ROWCOUNT;

-- ══ PASO 7 · Bitácora ══════════════════════════════════════════════════════
INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT @pry, N'ModificacionEstructura',
       N'Puesta al día desde el historial de Honduras Simple, GestionGD-Api y Gestión Digital: se registran '
       + CONVERT(nvarchar(10), @insertadas) + N' frentes del 18 al 21 de septiembre'
       + CASE WHEN @apiCerrado > 0 THEN N' y se cierra el API intermediario, que estaba al 50 %' ELSE N'' END
       + N'. Queda abierta la actividad de aceptación —pruebas con personas y despliegue—, que es la que sostiene el avance por debajo de 100.',
       @actor, @ahora
 WHERE @insertadas > 0 OR @apiCerrado > 0;

INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT @pry, N'Riesgo',
       N'Se registran ' + CONVERT(nvarchar(10), @riesgosNuevos)
       + N' riesgos abiertos que la ficha no recogía, tomados del informe de estado del 21 de septiembre: catálogo vacío por institución en blanco, portal sin desplegar, ninguna prueba con personas y límite de peticiones compartido por todo el país.',
       @actor, @ahora
 WHERE @riesgosNuevos > 0;

INSERT INTO BitacoraProyecto (ProyectoId, Tipo, Detalle, Actor, Fecha)
SELECT @pry, N'ModificacionFicha',
       N'El avance queda en ' + CONVERT(nvarchar(10), @avance)
       + N' %, calculado con la regla del dominio sobre el árbol de actividades. No llega a 100 a propósito: faltan las pruebas de aceptación y el despliegue.',
       @actor, @ahora
 WHERE @insertadas > 0 OR @apiCerrado > 0 OR @riesgosNuevos > 0 OR @avance <> @avancePrevio;

-- ══ Reporte ════════════════════════════════════════════════════════════════
IF @avance <> @avanceEsperado
    PRINT N'AVISO: el cálculo dio ' + CONVERT(nvarchar(10), @avance) + N' % y se esperaba '
        + CONVERT(nvarchar(10), @avanceEsperado) + N' %. La base de destino no estaba en el estado previsto: '
        + N'revise el detalle de actividades antes de aplicar.';
ELSE
    PRINT N'El avance quedó en ' + CONVERT(nvarchar(10), @avance) + N' %, como se esperaba.';

PRINT N'Actividades nuevas: ' + CONVERT(nvarchar(10), @insertadas)
    + N' · API cerrado: '     + CONVERT(nvarchar(10), @apiCerrado)
    + N' · Riesgos nuevos: '  + CONVERT(nvarchar(10), @riesgosNuevos)
    + N' · Avances nuevos: '  + CONVERT(nvarchar(10), @avancesNuevos);

SELECT  a.Orden,
        Actividad   = LEFT(a.Nombre, 58),
        a.Estado,
        Pct         = a.AvancePct,
        Inicio      = ISNULL(CONVERT(varchar(10), a.FechaInicioReal, 23), N'—'),
        Fin         = ISNULL(CONVERT(varchar(10), a.FechaFinReal, 23), N'—'),
        Responsable = ISNULL(a.Responsable, N'—')
  FROM  ProyectoActividades a
 WHERE  a.EntregableId = @ent
 ORDER BY a.Orden;

SELECT  Riesgo    = LEFT(r.Descripcion, 70),
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
