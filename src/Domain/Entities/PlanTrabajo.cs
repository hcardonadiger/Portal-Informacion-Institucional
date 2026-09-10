namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>Plan anual de racionalización de trámites por institución.</summary>
public sealed class PlanTrabajo : BaseAuditableEntity, ISoftDeletable
{
    public bool IsDeleted { get; set; }

    public string InstitucionId { get; private set; } = default!;
    public string Institucion   { get; private set; } = default!;
    public int    Anio          { get; set; }
    public EstadoPlanTrabajo Estado { get; set; } = EstadoPlanTrabajo.Borrador;
    public string? Observaciones { get; set; }
    public Guid?   AprobadoPorId  { get; set; }
    public DateOnly? FechaAprobacion { get; set; }

    private readonly List<MetaTramite> _metas = [];
    public IReadOnlyCollection<MetaTramite> Metas => _metas.AsReadOnly();

    private PlanTrabajo() { }

    public static PlanTrabajo Crear(string institucionId, string institucion, int anio)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucion);
        return new PlanTrabajo
        {
            InstitucionId = institucionId.Trim().ToUpper(),
            Institucion   = institucion.Trim(),
            Anio          = anio
        };
    }
}

/// <summary>Trámite planificado dentro de un plan de trabajo anual.</summary>
public sealed class MetaTramite : BaseEntity
{
    public int    PlanTrabajoId { get; set; }
    public int    Orden         { get; set; }
    public string NombreTramite { get; set; } = default!;
    public DateOnly? FechaEstimadaInicio { get; set; }
    public DateOnly? FechaEstimadaFin    { get; set; }
    public DateOnly? FechaRealFin        { get; set; }
    /// <summary>Usuario del sistema responsable (analista DIGER); null en filas legadas con solo texto.</summary>
    public Guid?     ResponsableId       { get; set; }
    /// <summary>Snapshot del nombre del responsable para visualización e histórico.</summary>
    public string?   Responsable         { get; set; }
    public EstadoMeta Estado             { get; set; } = EstadoMeta.Pendiente;
    public string?   Observaciones       { get; set; }
    public int?      ExpedienteId        { get; set; }
    /// <summary>Índice del trámite dentro del expediente vinculado (ExpedienteTramite.TramiteIndex);
    /// null = la meta se vincula al expediente completo.</summary>
    public int?      ExpedienteTramiteIndex { get; set; }
}
