namespace Diger.TramitesEstado.Application.Proyectos.Common;

public sealed record HitoProyectoDto(
    int        Id,
    int        Orden,
    string     Nombre,
    string?    Descripcion,
    DateOnly?  FechaPlan,
    DateOnly?  FechaReal,
    EstadoHito Estado,
    Guid?      ResponsableId,
    string?    Responsable)
{
    public bool EstaAtrasado =>
        FechaPlan.HasValue
        && FechaPlan < DateOnly.FromDateTime(DateTime.UtcNow)
        && Estado is EstadoHito.Pendiente or EstadoHito.EnProceso;
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
    int      Id,
    int?     HitoId,
    string?  HitoNombre,
    DateTime Fecha,
    string   Autor,
    string   Descripcion,
    int      PorcentajeReportado,
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
        TipoEventoProyecto.CambioEstado       => "Cambio de estado",
        TipoEventoProyecto.ModificacionFicha  => "Ficha",
        TipoEventoProyecto.ModificacionHitos  => "Hitos",
        TipoEventoProyecto.CorreccionBitacora => "Corrección de bitácora",
        _                                     => Tipo.ToString()
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
    EstadoProyecto    Estado,
    DateOnly?         FechaInicioPlan,
    DateOnly?         FechaFinPlan,
    DateOnly?         FechaFinReal,
    int               AvancePct,
    int               TotalHitos,
    int               HitosCompletados,
    int               HitosAtrasados,
    DateTime?         UltimoAvance)
{
    /// <summary>Pasó la fecha planificada de cierre y el proyecto sigue abierto.</summary>
    public bool EstaAtrasado =>
        FechaFinPlan.HasValue
        && FechaFinPlan < DateOnly.FromDateTime(DateTime.UtcNow)
        && Estado is EstadoProyecto.Planificado or EstadoProyecto.EnEjecucion or EstadoProyecto.Suspendido;

    /// <summary>Avance según el cronograma: hitos completados sobre el total. Lo verificable,
    /// frente al <see cref="AvancePct"/> que declara el responsable.</summary>
    public int AvanceFisico => TotalHitos == 0 ? 0 : (int)Math.Round(HitosCompletados * 100.0 / TotalHitos);

    /// <summary>Puntos entre lo declarado y lo que muestran los hitos. Positiva: se reporta más de
    /// lo que se cierra.</summary>
    public int Brecha => AvancePct - AvanceFisico;

    /// <summary>Las dos medidas del proyecto dejaron de coincidir. Mismo umbral que el tablero.</summary>
    public bool Divergente => TotalHitos > 0 && Math.Abs(Brecha) >= 30;

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
    EstadoProyecto    Estado,
    DateOnly?         FechaInicioPlan,
    DateOnly?         FechaFinPlan,
    DateOnly?         FechaInicioReal,
    DateOnly?         FechaFinReal,
    int               AvancePct,
    DateTime          CreatedAt,
    string?           CreatedBy,
    IReadOnlyList<HitoProyectoDto>  Hitos,
    IReadOnlyList<EstadoProyecto>   EstadosPosibles)
{
    public int TotalHitos       => Hitos.Count;
    public int HitosCompletados => Hitos.Count(h => h.Estado == EstadoHito.Completado);
    public int HitosAtrasados   => Hitos.Count(h => h.EstaAtrasado);
    public bool EstaCerrado     => Estado is EstadoProyecto.Cerrado or EstadoProyecto.Cancelado;
}

/// <summary>
/// Datos de un hito tal como los manda el editor.
///
/// <para><b>Lleva el Id</b> — <c>0</c> para una fila nueva. El editor ya no reemplaza los hitos en
/// bloque: los reconcilia. Sin el Id habría que borrar y recrear, y cada hito recreado nace con
/// identidad nueva, lo que dejaba en NULL el <c>HitoId</c> de todos los avances imputados a él
/// (la FK está en SetNull). Es decir: guardar la ficha borraba la imputación de la bitácora.</para>
///
/// <para>No trae <c>Orden</c> a propósito. El orden solo lo cambia el responsable del proyecto,
/// por <c>ReordenarHitosCommand</c>; guardar la ficha conserva el que ya tenían y manda los
/// hitos nuevos al final. Si el orden viajara acá, cualquiera con permiso de edición podría
/// reordenar y se saltaría esa restricción.</para>
/// </summary>
public sealed record HitoInput(
    int        Id,
    string     Nombre,
    string?    Descripcion,
    DateOnly?  FechaPlan,
    DateOnly?  FechaReal,
    EstadoHito Estado,
    Guid?      ResponsableId,
    string?    Responsable);

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
