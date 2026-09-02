namespace Diger.TramitesEstado.Application.Proyectos.Common;

/// <summary>
/// Una actividad de la que depende otra: tiene que terminar antes (dependencia Fin → Comienzo).
/// Se proyecta con el entregable del que cuelga porque la dependencia puede cruzarlos, y sin eso
/// la ficha mostraría dos actividades homónimas de ramas distintas sin forma de distinguirlas.
/// </summary>
public sealed record PredecesoraDto(
    int             Id,
    string          Nombre,
    string          Entregable,
    EstadoActividad Estado,
    DateOnly?       FechaFinPlan)
{
    /// <summary>Todavía no terminó, así que sigue bloqueando. Misma regla que
    /// <c>ActividadProyecto.SigueAbierta</c>: la cancelada no bloquea a nadie.</summary>
    public bool SigueAbierta => Estado is EstadoActividad.Pendiente or EstadoActividad.EnProceso;
}

/// <summary>
/// Una actividad del árbol, ya proyectada para la vista. El nivel donde se reporta el porcentaje
/// y el único que lleva ventana de ejecución (inicio y fin).
/// </summary>
public sealed record ActividadProyectoDto(
    int             Id,
    int             Orden,
    string          Nombre,
    string?         Descripcion,
    DateOnly?       FechaInicioPlan,
    DateOnly?       FechaFinPlan,
    DateOnly?       FechaInicioReal,
    DateOnly?       FechaFinReal,
    int             AvancePct,
    EstadoActividad Estado,
    Guid?           ResponsableId,
    string?         Responsable,
    IReadOnlyList<PredecesoraDto> Predecesoras,

    /// <summary>Cuándo se creó la fila. <b>Nulo en todo lo anterior al 26-08-2026</b>: hasta esa
    /// fecha la entidad no guardaba auditoría, y las 191 actividades que venían de la carga inicial
    /// y de la conversión de hitos no tienen una fecha real que rescatar. Se muestra vacío en vez
    /// de inventarle la de la migración.</summary>
    DateTime? CreadoEn = null,
    string?   CreadoPor = null)
{
    private static DateOnly Hoy => DateOnly.FromDateTime(DateTime.UtcNow);

    public bool EstaAtrasada =>
        FechaFinPlan.HasValue
        && FechaFinPlan < Hoy
        && Estado is EstadoActividad.Pendiente or EstadoActividad.EnProceso;

    /// <summary>Debía haber arrancado y sigue en cero.</summary>
    public bool NoArrancada =>
        FechaInicioPlan.HasValue && FechaInicioPlan < Hoy && Estado == EstadoActividad.Pendiente;

    /// <summary>Cancelada: no cuenta en el promedio del entregable. La vista la muestra apagada
    /// en vez de esconderla — que exista y no cuente es parte de la historia del proyecto.</summary>
    public bool EstaCancelada => Estado == EstadoActividad.Cancelada;

    /// <summary>
    /// No debería haber arrancado todavía: alguna de las actividades de las que depende sigue
    /// abierta. <b>Es un aviso, no un candado</b> — reportar avance se permite igual. Ver la nota
    /// de <c>DependenciaActividad</c>.
    /// </summary>
    public bool Bloqueada => !EstaCancelada && Predecesoras.Any(p => p.SigueAbierta);

    public IReadOnlyList<PredecesoraDto> PredecesorasPendientes =>
        Predecesoras.Where(p => p.SigueAbierta).ToList();

    /// <summary>Ya se está trabajando en ella pese a estar bloqueada. Es la señal que interesa en
    /// el tablero: o el plan está mal o la dependencia no era tal.</summary>
    public bool ArrancoBloqueada =>
        Bloqueada && Estado is EstadoActividad.EnProceso or EstadoActividad.Completada;

    /// <summary>Su ventana empieza antes de que termine una predecesora. El conflicto ya está
    /// escrito en el cronograma, aunque nadie haya llegado tarde todavía — mismo criterio que
    /// <c>EntregableProyectoDto.DesbordaElPlan</c>.</summary>
    public bool DesfaseIncoherente =>
        FechaInicioPlan is { } ini
        && Predecesoras.Any(p => p.FechaFinPlan is { } fin && ini <= fin);

    public bool SinFechas => FechaInicioPlan is null && FechaFinPlan is null;
}

/// <summary>
/// Un entregable con sus actividades. Lo que antes era un hito.
/// </summary>
/// <param name="AvancePct">Ya calculado por el dominio (promedio de actividades, o la regla
/// 0/50/100 por estado si todavía no tiene ninguna). Viene resuelto y no se recalcula acá para no
/// tener la misma regla escrita en dos lugares.</param>
public sealed record EntregableProyectoDto(
    int              Id,
    int              Orden,
    string           Nombre,
    string?          Descripcion,
    DateOnly?        FechaPlan,
    DateOnly?        FechaReal,
    EstadoEntregable Estado,
    Guid?            ResponsableId,
    string?          Responsable,
    int              AvancePct,
    IReadOnlyList<ActividadProyectoDto> Actividades,

    /// <summary>Ver la nota de <see cref="ActividadProyectoDto.CreadoEn"/>: nulo en lo anterior
    /// al 26-08-2026.</summary>
    DateTime? CreadoEn = null,
    string?   CreadoPor = null)
{
    public bool EstaAtrasado =>
        FechaPlan.HasValue
        && FechaPlan < DateOnly.FromDateTime(DateTime.UtcNow)
        && Estado is EstadoEntregable.Pendiente or EstadoEntregable.EnProceso;

    public int TotalActividades      => Actividades.Count;
    public int ActividadesVigentes   => Actividades.Count(a => !a.EstaCancelada);
    public int ActividadesTerminadas => Actividades.Count(a => a.Estado == EstadoActividad.Completada);
    public int ActividadesAtrasadas  => Actividades.Count(a => a.EstaAtrasada);
    public int ActividadesBloqueadas => Actividades.Count(a => a.Bloqueada);

    /// <summary>Sin desglosar: su avance sale de la regla por estado, no de trabajo reportado.
    /// La ficha lo señala para que se note la diferencia entre «va por la mitad» y «nadie lo
    /// desglosó».</summary>
    public bool SinDesglose => Actividades.Count == 0;

    /// <summary>Ventana que ocupan sus actividades. Informativa: el atraso del entregable se mide
    /// contra <see cref="FechaPlan"/>.</summary>
    public DateOnly? InicioActividades
    {
        get
        {
            var fechas = Actividades.Where(a => a.FechaInicioPlan.HasValue)
                                    .Select(a => a.FechaInicioPlan!.Value).ToList();
            return fechas.Count == 0 ? null : fechas.Min();
        }
    }

    public DateOnly? FinActividades
    {
        get
        {
            var fechas = Actividades.Where(a => a.FechaFinPlan.HasValue)
                                    .Select(a => a.FechaFinPlan!.Value).ToList();
            return fechas.Count == 0 ? null : fechas.Max();
        }
    }

    /// <summary>La última actividad termina después de la fecha comprometida del entregable.
    /// No se bloquea al guardar —el plan puede estar en revisión— pero la ficha lo avisa: es un
    /// atraso que ya está escrito en el cronograma, antes de que venza nada.</summary>
    public bool DesbordaElPlan =>
        FechaPlan is { } plan && FinActividades is { } fin && fin > plan;
}

/// <summary>
/// Una entrada de la bitácora de ejecución.
///
/// <para><b>No expone <c>ArchivoUrl</c> a propósito.</b> La ruta física vive bajo /uploads, que
/// Program.cs sirve como archivo estático sin autorización; si la vista pudiera enlazarla,
/// cualquiera con el enlace bajaría la evidencia de un proyecto interno. La descarga pasa
/// siempre por el handler autenticado de la página, que resuelve la ruta por el Id del avance.</para>
/// </summary>
public sealed record AvanceProyectoDto(
    int       Id,
    int?      EntregableId,
    string?   EntregableNombre,
    int?      ActividadId,
    string?   ActividadNombre,
    DateTime  Fecha,
    string    Autor,
    string    Descripcion,
    int?      PorcentajeReportado,
    string?   Bloqueo,
    string?   ArchivoNombre,
    long?     ArchivoTamano,
    DateTime? EditadoEn  = null,
    string?   EditadoPor = null)
{
    public bool TieneEvidencia => !string.IsNullOrWhiteSpace(ArchivoNombre);

    /// <summary>La entrada fue corregida después de registrarse. La vista lo muestra para que
    /// la bitácora no aparente ser intacta cuando no lo es.</summary>
    public bool FueEditada => EditadoEn.HasValue;

    /// <summary>Movió el porcentaje de una actividad. Las entradas viejas también traen número
    /// pero sin actividad: ahí el porcentaje era del proyecto entero, cuando se declaraba a mano.
    /// La vista las rotula distinto para no dar a entender que aquello se reportó como se reporta
    /// hoy.</summary>
    public bool FijoAvance => PorcentajeReportado.HasValue && ActividadId.HasValue;

    public bool EsDeclaracionHistorica => PorcentajeReportado.HasValue && ActividadId is null;
}

/// <summary>Una entrada de la auditoría del proyecto. A diferencia de la bitácora de ejecución,
/// esta se escribe sola y no se edita ni se borra.</summary>
public sealed record BitacoraProyectoDto(
    TipoEventoProyecto Tipo,
    string             Detalle,
    string             Actor,
    DateTime           Fecha)
{
    public string Etiqueta => Tipo switch
    {
        TipoEventoProyecto.CambioEstado           => "Cambio de estado",
        TipoEventoProyecto.ModificacionFicha      => "Ficha",
        TipoEventoProyecto.ModificacionEstructura => "Estructura",
        TipoEventoProyecto.CorreccionBitacora     => "Corrección de bitácora",
        TipoEventoProyecto.Documentacion          => "Documentación",
        _                                         => Tipo.ToString()
    };
}

public sealed record RiesgoProyectoDto(
    int              Id,
    string           Descripcion,
    CategoriaRiesgo  Categoria,
    NivelCualitativo Probabilidad,
    NivelCualitativo Impacto,
    EstrategiaRiesgo Estrategia,
    EstadoRiesgo     Estado,
    string?          Mitigacion,
    Guid?            ResponsableId,
    string?          Responsable,
    DateOnly         FechaDeteccion,
    DateOnly?        FechaRevision,
    DateOnly?        FechaCierre,
    string           RegistradoPor)
{
    public int Severidad => (int)Probabilidad * (int)Impacto;

    public NivelCualitativo NivelSeveridad => Severidad >= 6 ? NivelCualitativo.Alta
                                            : Severidad >= 3 ? NivelCualitativo.Media
                                            : NivelCualitativo.Baja;

    public bool EstaAbierto => Estado is EstadoRiesgo.Abierto or EstadoRiesgo.EnTratamiento;

    /// <summary>Sigue abierto y ya pasó su fecha de revisión: nadie lo volvió a mirar.</summary>
    public bool RevisionVencida =>
        FechaRevision.HasValue && FechaRevision < DateOnly.FromDateTime(DateTime.UtcNow) && EstaAbierto;

    /// <summary>Sin mitigación definida pese a que la estrategia exige una acción.</summary>
    public bool SinPlan =>
        EstaAbierto && Estrategia != EstrategiaRiesgo.Aceptar && string.IsNullOrWhiteSpace(Mitigacion);
}

public sealed record InteresadoProyectoDto(
    int              Id,
    Guid             UsuarioId,
    string           Nombre,
    string?          Institucion,
    string?          Cargo,
    string?          Correo,
    RolInteresado    Rol,
    NivelCualitativo Influencia,
    string?          Notas)
{
    public bool EsClave =>
        Influencia == NivelCualitativo.Alta
        && Rol is RolInteresado.Patrocinador or RolInteresado.Regulador or RolInteresado.ContraparteTecnica;
}

public sealed record ProyectoListItemDto(
    int               Id,
    string            Codigo,
    string            Nombre,
    string?           Responsable,
    PrioridadProyecto Prioridad,
    AccionProyecto?   Accion,
    EstadoProyecto    Estado,
    DateOnly?         FechaInicioPlan,
    DateOnly?         FechaFinPlan,
    DateOnly?         FechaFinReal,
    int               AvancePct,
    int               TotalEntregables,
    int               EntregablesCompletados,
    int               EntregablesAtrasados,
    int               TotalActividades,
    int               ActividadesAtrasadas,
    DateTime?         UltimoAvance)
{
    /// <summary>Pasó la fecha planificada de cierre y el proyecto sigue abierto.</summary>
    public bool EstaAtrasado =>
        FechaFinPlan.HasValue
        && FechaFinPlan < DateOnly.FromDateTime(DateTime.UtcNow)
        && Estado is EstadoProyecto.Planificado or EstadoProyecto.EnEjecucion or EstadoProyecto.Suspendido;

    /// <summary>Entregables cerrados sobre el total. Es la medida gruesa —lo que está entregado—
    /// frente al <see cref="AvancePct"/>, que promedia el trabajo reportado en las actividades.</summary>
    public int AvanceFisico => TotalEntregables == 0 ? 0 : (int)Math.Round(EntregablesCompletados * 100.0 / TotalEntregables);

    /// <summary>Puntos entre el avance reportado en las actividades y los entregables cerrados.
    /// Positiva: se reporta trabajo que todavía no cierra nada.</summary>
    public int Brecha => AvancePct - AvanceFisico;

    /// <summary>Las dos medidas del proyecto dejaron de coincidir. Mismo umbral que el tablero.</summary>
    public bool Divergente => TotalEntregables > 0 && Math.Abs(Brecha) >= 30;

    /// <summary>Abierto y sin fecha de cierre comprometida. No es ir a tiempo: es no tener contra
    /// qué medirse, y por eso <see cref="EstaAtrasado"/> nunca lo marca.</summary>
    public bool SinLineaBase =>
        FechaFinPlan is null
        && Estado is EstadoProyecto.Planificado or EstadoProyecto.EnEjecucion or EstadoProyecto.Suspendido;

    /// <summary>Abierto y sin reportes en más de 30 días. El síntoma que interesa en un tablero
    /// de seguimiento no es solo el atraso de fechas, sino el proyecto del que nadie informa.</summary>
    public bool SinReportar =>
        Estado is EstadoProyecto.EnEjecucion
        && (UltimoAvance is null || UltimoAvance < DateTime.UtcNow.AddDays(-30));

    /// <summary>En ejecución y con la estructura sin desglosar. Desde que el avance se calcula,
    /// un proyecto sin actividades no puede reportar nada: su porcentaje depende solo del estado
    /// de sus entregables. Es una señal de carga pendiente, no de atraso.</summary>
    public bool SinDesglose => TotalActividades == 0 && Estado == EstadoProyecto.EnEjecucion;
}

public sealed record ProyectoDetailDto(
    int               Id,
    string            Codigo,
    string            Nombre,
    string?           Objetivo,
    string?           InstitucionId,
    string?           AreaId,
    string?           UnidadId,
    Guid?             ResponsableId,
    string?           Responsable,
    PrioridadProyecto Prioridad,
    AccionProyecto?   Accion,
    EstadoProyecto    Estado,
    DateOnly?         FechaInicioPlan,
    DateOnly?         FechaFinPlan,
    DateOnly?         FechaInicioReal,
    DateOnly?         FechaFinReal,
    int               AvancePct,
    DateTime          CreatedAt,
    string?           CreatedBy,
    IReadOnlyList<EntregableProyectoDto> Entregables,
    IReadOnlyList<EstadoProyecto>        EstadosPosibles)
{
    public int TotalEntregables       => Entregables.Count;
    public int EntregablesCompletados => Entregables.Count(e => e.Estado == EstadoEntregable.Completado);
    public int EntregablesAtrasados   => Entregables.Count(e => e.EstaAtrasado);

    public int TotalActividades      => Entregables.Sum(e => e.TotalActividades);
    public int ActividadesAtrasadas  => Entregables.Sum(e => e.ActividadesAtrasadas);
    public int ActividadesTerminadas => Entregables.Sum(e => e.ActividadesTerminadas);
    public int ActividadesBloqueadas => Entregables.Sum(e => e.ActividadesBloqueadas);

    /// <summary>Cuántas dependencias tiene declarado el proyecto. Cero no es un problema: la
    /// mayoría del portafolio se cargó desde actas y nunca declaró ninguna.</summary>
    public int TotalDependencias => Entregables.Sum(e => e.Actividades.Sum(a => a.Predecesoras.Count));

    public bool EstaCerrado => Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado;

    /// <summary>Pasó la fecha planificada de cierre y el proyecto sigue abierto. Misma regla que
    /// en el listado: la ficha y el portafolio no pueden decir cosas distintas del mismo proyecto.</summary>
    public bool EstaAtrasado =>
        FechaFinPlan.HasValue && FechaFinPlan < DateOnly.FromDateTime(DateTime.UtcNow) && !EstaCerrado;

    /// <summary>Abierto y sin fecha de cierre comprometida. No es ir a tiempo: es no tener contra
    /// qué medirse.</summary>
    public bool SinLineaBase => FechaFinPlan is null && !EstaCerrado;

    /// <summary>Todas las actividades del proyecto, con el entregable del que cuelgan. Lo usa el
    /// selector de imputación de la bitácora, que necesita el árbol aplanado.</summary>
    public IEnumerable<(EntregableProyectoDto Entregable, ActividadProyectoDto Actividad)> ActividadesPlanas =>
        Entregables.SelectMany(e => e.Actividades.Select(a => (e, a)));

    /// <summary>Sin una sola actividad cargada. Mientras siga así, el avance del proyecto sale
    /// solo del estado de los entregables — la ficha lo dice para que el 0 % no se lea como un
    /// proyecto detenido.</summary>
    public bool SinDesglose => TotalActividades == 0;
}

/// <summary>
/// Datos de un entregable tal como los manda el editor, con sus actividades.
///
/// <para><b>Lleva el Id</b> — <c>0</c> para una fila nueva. El editor no reemplaza la estructura en
/// bloque: la reconcilia. Sin el Id habría que borrar y recrear, y cada entregable recreado nace
/// con identidad nueva, lo que dejaba en NULL el <c>EntregableId</c> de todos los avances imputados
/// a él (la FK está en SetNull). Es decir: guardar la ficha borraba la imputación de la bitácora.</para>
///
/// <para>No trae <c>Orden</c> a propósito. El orden solo lo cambia el responsable del proyecto,
/// por <c>ReordenarEntregablesCommand</c>; guardar la ficha conserva el que ya tenían y manda los
/// nuevos al final. Si el orden viajara acá, cualquiera con permiso de edición podría reordenar y
/// se saltaría esa restricción.</para>
///
/// <para>Tampoco trae el avance ni la fecha real: el primero se calcula desde las actividades y la
/// segunda la fija el cierre.</para>
/// </summary>
public sealed record EntregableInput(
    int              Id,
    string           Nombre,
    string?          Descripcion,
    DateOnly?        FechaPlan,
    EstadoEntregable Estado,
    Guid?            ResponsableId,
    string?          Responsable,
    IReadOnlyList<ActividadInput> Actividades);

/// <summary>
/// Datos de una actividad tal como los manda el editor.
///
/// <para>El <c>AvancePct</c> sí viaja acá, a diferencia del entregable: es el único lugar del árbol
/// donde el número se escribe. Guardar la ficha con un porcentaje distinto lo aplica igual que un
/// reporte de bitácora, salvo que no deja entrada — para eso está el formulario de avance, que
/// además pide decir qué se hizo.</para>
/// </summary>
public sealed record ActividadInput(
    int       Id,
    string    Nombre,
    string?   Descripcion,
    DateOnly? FechaInicioPlan,
    DateOnly? FechaFinPlan,
    int       AvancePct,
    bool      Cancelada,
    Guid?     ResponsableId,
    string?   Responsable,
    IReadOnlyList<int>? Predecesoras = null);

/// <summary>Una opción de los selectores de alcance. <c>Padre</c> es el área a la que pertenece
/// una unidad; null en las áreas.</summary>
public sealed record OpcionAlcanceDto(string Id, string Nombre, string? Padre);

/// <summary>Áreas y unidades disponibles para acotar el alcance de un proyecto.</summary>
public sealed record AlcanceOpcionesDto(
    IReadOnlyList<OpcionAlcanceDto> Areas,
    IReadOnlyList<OpcionAlcanceDto> Unidades);

/// <summary>Metadatos que necesita el handler de descarga para servir la evidencia.
/// Incluye el proyecto para poder volver a su página si el archivo físico ya no está.</summary>
public sealed record EvidenciaAvanceDto(int ProyectoId, string ArchivoNombre, string ArchivoUrl);
