namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Reunión o capacitación documentada (acta + asistencia + acuerdos).
/// Agregado de captura: escalares asignables; hijos reemplazados en bloque.
/// </summary>
public sealed class Reunion : BaseAuditableEntity
{
    public string Titulo { get; private set; } = default!;

    /// <summary>Id del registro origen (p. ej. Supabase) cuando la reunión fue importada.
    /// Permite reimportar de forma idempotente; null para reuniones creadas en el portal.</summary>
    public string? OrigenExternoId { get; set; }

    // ── Datos generales ───────────────────────────────────────────
    public DateOnly? Fecha     { get; set; }
    public string?   Hora      { get; set; }
    public string?   Duracion  { get; set; }
    public string?   Modalidad { get; set; }   // Presencial / Virtual / Híbrida / Otro
    public string?   Lugar     { get; set; }
    public int?      InstitucionId { get; set; } // beneficiaria (opcional; permite "Otro")
    public string?   Institucion   { get; set; } // snapshot del nombre
    public string?   Tipo      { get; set; }     // Taller / Seminario / Reunión técnica / …
    public bool      EsCapacitacionPlataforma { get; set; }

    // ── Memoria ───────────────────────────────────────────────────
    public string? ObjetivoAgenda { get; set; }
    public string? Desarrollo     { get; set; }

    // ── Capacitación ──────────────────────────────────────────────
    public string? Tema        { get; set; }
    public string? ObjetivoCap  { get; set; }
    public string? Contenido    { get; set; } // puntos, uno por línea

    // ── Enlace institucional ──────────────────────────────────────
    public string? EpNombre { get; set; }
    public string? EpCargo  { get; set; }
    public string? EpCorreo { get; set; }
    public string? EpTel    { get; set; }

    // ── Facilitador DIGER ─────────────────────────────────────────
    public string? FacNombre { get; set; }
    public string? FacCargo  { get; set; }
    public string? FacCorreo { get; set; }

    // ── Resultados ────────────────────────────────────────────────
    public int?    Convocados    { get; set; }
    public int?    NumAsistentes { get; set; }
    public int?    PctAsistencia { get; set; }
    public string? Satisfaccion  { get; set; }
    public string? Compromisos   { get; set; } // uno por línea

    // ── Validación y evidencias ───────────────────────────────────
    public string? ValDiger    { get; set; }
    public string? ValInst     { get; set; }
    public string? DocsRecursos { get; set; }
    public string? Foto1Url  { get; set; }
    public string? Foto1Desc { get; set; }
    public string? Foto2Url  { get; set; }
    public string? Foto2Desc { get; set; }

    // ── Hijos ─────────────────────────────────────────────────────
    private readonly List<Asistente>      _asistentes = [];
    private readonly List<AcuerdoReunion> _acuerdos   = [];

    public IReadOnlyCollection<Asistente>      Asistentes => _asistentes.AsReadOnly();
    public IReadOnlyCollection<AcuerdoReunion> Acuerdos   => _acuerdos.AsReadOnly();

    private Reunion() { }

    public static Reunion Crear(string titulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);
        var r = new Reunion { Titulo = titulo.Trim() };
        r.AddDomainEvent(new ReunionCreatedEvent(r.Id, r.Titulo));
        return r;
    }

    public void EstablecerTitulo(string titulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);
        Titulo = titulo.Trim();
    }

    public void MarcarActualizada() => AddDomainEvent(new ReunionUpdatedEvent(Id));

    public void LimpiarHijos() { _asistentes.Clear(); _acuerdos.Clear(); }
    public void Agregar(Asistente a)      => _asistentes.Add(a);
    public void Agregar(AcuerdoReunion a) => _acuerdos.Add(a);
}

public sealed class Asistente : BaseEntity
{
    public int     ReunionId   { get; set; }
    public string  Nombre      { get; set; } = default!;
    public string? Cargo       { get; set; }
    public string? Institucion { get; set; }
    public string? Correo      { get; set; }
    public string? Telefono    { get; set; }
}

public sealed class AcuerdoReunion : BaseEntity
{
    public int       ReunionId   { get; set; }
    public int       Orden       { get; set; }
    public string    Compromiso  { get; set; } = default!;
    public string?   Responsable { get; set; }
    public DateOnly? Plazo       { get; set; }
}
