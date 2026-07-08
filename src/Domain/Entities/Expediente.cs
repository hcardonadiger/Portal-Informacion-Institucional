namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Aggregate root del Expediente de Digitalización. Es un agregado de captura
/// (formulario de 7 secciones); las propiedades escalares son asignables y las
/// colecciones hijas se reemplazan en bloque al actualizar.
/// </summary>
public sealed class Expediente : BaseAuditableEntity, ISoftDeletable
{
    // ── Soft Delete ───────────────────────────────────────────────
    public bool IsDeleted { get; set; }
    // ── Identificación (controlada) ───────────────────────────────
    public string Codigo        { get; private set; } = default!;
    public string InstitucionId { get; private set; } = default!;
    public string? AreaId       { get; private set; }
    public string? UnidadId     { get; private set; }
    public string Institucion   { get; private set; } = default!; // snapshot

    /// <summary>Id del registro origen (p. ej. Supabase) cuando el expediente fue importado.
    /// Permite reimportar de forma idempotente; null para expedientes creados en el portal.</summary>
    public string? OrigenExternoId { get; set; }

    // ── Apertura ──────────────────────────────────────────────────
    public DateOnly? FechaApertura { get; set; }
    public string    Analista      { get; set; } = default!;
    public string?   DirSede       { get; set; }
    public int       NumTramitesProd { get; set; }

    // ── Contacto institucional ────────────────────────────────────
    public string? ContactoNombre { get; set; }
    public string? ContactoCargo  { get; set; }
    public string? ContactoCorreo { get; set; }
    public string? ContactoTel    { get; set; }

    // ── Marco legal / documental ──────────────────────────────────
    public string? ObsLegal { get; set; }

    // ── Proceso actual (línea base) ───────────────────────────────
    public int?    NumFuncionarios { get; set; }
    public int?    VolumenAnual    { get; set; }
    public string? TiempoObservado { get; set; }
    public string? TiempoNorma     { get; set; }
    public string? DescProceso     { get; set; }
    public string? DocsAdicionales { get; set; }
    public string? ObsFlujo        { get; set; }

    // ── Modelo propuesto ──────────────────────────────────────────
    public int?    FuncionariosDig { get; set; }
    public string? TiempoDig       { get; set; }
    public string? ObsModelo       { get; set; }

    // ── Infraestructura SOL (escalares) ───────────────────────────
    public string? InfraPersonal   { get; set; } // Sí/No/Parcialmente
    public int?    InfraPersonalTI { get; set; }
    public string? InfraRespSol    { get; set; }
    public string? InfraAcomp      { get; set; } // Sí/No
    public string? InfraDcModalidad { get; set; }
    public string? InfraDcVirt      { get; set; }
    public string? InfraDcVirtOtro  { get; set; }
    public string? InfraDcDisp      { get; set; }
    public string? InfraDcObs       { get; set; }
    public string? InfraPlan        { get; set; }

    // ── Estado / cierre ───────────────────────────────────────────
    public EstadoExpediente        EstadoExpediente    { get; set; } = EstadoExpediente.EnExploracion;
    public EstadoLevantamientoExp? EstadoLevantamiento { get; set; }
    public string?  ObsExpediente   { get; set; }
    public string?  ObsLevantamiento { get; set; }
    public string?  ValidadoDiger   { get; set; }
    public string?  ValidadoInst    { get; set; }
    public DateOnly? FechaValidacion { get; set; }
    public string?  NumActa         { get; set; }

    // ── Colecciones hijas ─────────────────────────────────────────
    private readonly List<ExpedienteTramite>      _tramites    = [];
    private readonly List<TramiteRequisito>       _requisitos  = [];
    private readonly List<FlujoNodo>              _flujos      = [];
    private readonly List<FundamentoLegal>        _legal       = [];
    private readonly List<DocumentoSolicitado>    _docsSolicitados   = [];
    private readonly List<DocumentoInterno>       _docsInternos    = [];
    private readonly List<InfraPerfil>            _perfiles    = [];
    private readonly List<InfraCondicion>         _condiciones = [];
    private readonly List<InfraChecklistItem>     _checklistInfra   = [];
    private readonly List<ExpedienteSeccionEstado> _secciones  = [];

    public IReadOnlyCollection<ExpedienteTramite>       Tramites    => _tramites.AsReadOnly();
    public IReadOnlyCollection<TramiteRequisito>        Requisitos  => _requisitos.AsReadOnly();
    public IReadOnlyCollection<FlujoNodo>               Flujos      => _flujos.AsReadOnly();
    public IReadOnlyCollection<FundamentoLegal>         Legal       => _legal.AsReadOnly();
    public IReadOnlyCollection<DocumentoSolicitado>     DocsSolicitados => _docsSolicitados.AsReadOnly();
    public IReadOnlyCollection<DocumentoInterno>        DocsInternos    => _docsInternos.AsReadOnly();
    public IReadOnlyCollection<InfraPerfil>             Perfiles    => _perfiles.AsReadOnly();
    public IReadOnlyCollection<InfraCondicion>          Condiciones => _condiciones.AsReadOnly();
    public IReadOnlyCollection<InfraChecklistItem>      ChecklistInfra => _checklistInfra.AsReadOnly();
    public IReadOnlyCollection<ExpedienteSeccionEstado> Secciones   => _secciones.AsReadOnly();

    private Expediente() { }

    public static Expediente Crear(string codigo, string institucionId, string? areaId, string? unidadId, string institucionNombre, string analista)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(codigo);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionNombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(analista);

        var e = new Expediente
        {
            Codigo        = codigo.Trim().ToUpperInvariant(),
            InstitucionId = institucionId.Trim().ToUpper(),
            AreaId        = areaId?.Trim().ToUpper(),
            UnidadId      = unidadId?.Trim().ToUpper(),
            Institucion   = institucionNombre.Trim(),
            Analista      = analista.Trim()
        };
        e.AddDomainEvent(new ExpedienteCreatedEvent(e.Id, e.Codigo, e.Institucion));
        return e;
    }

    public void MarcarActualizado() => AddDomainEvent(new ExpedienteUpdatedEvent(Id));

    // ── Gestión de colecciones (reemplazo en bloque) ──────────────
    public void LimpiarHijos()
    {
        _tramites.Clear(); _requisitos.Clear(); _flujos.Clear();
        _legal.Clear(); _docsSolicitados.Clear(); _docsInternos.Clear();
        _perfiles.Clear(); _condiciones.Clear(); _checklistInfra.Clear(); _secciones.Clear();
    }

    public void Agregar(ExpedienteTramite t)       => _tramites.Add(t);
    public void Agregar(TramiteRequisito r)        => _requisitos.Add(r);
    public void Agregar(FlujoNodo n)               => _flujos.Add(n);
    public void Agregar(FundamentoLegal l)         => _legal.Add(l);
    public void Agregar(DocumentoSolicitado d)     => _docsSolicitados.Add(d);
    public void Agregar(DocumentoInterno d)        => _docsInternos.Add(d);
    public void Agregar(InfraPerfil p)             => _perfiles.Add(p);
    public void Agregar(InfraCondicion c)          => _condiciones.Add(c);
    public void Agregar(InfraChecklistItem i)      => _checklistInfra.Add(i);
    public void Agregar(ExpedienteSeccionEstado s) => _secciones.Add(s);
}
