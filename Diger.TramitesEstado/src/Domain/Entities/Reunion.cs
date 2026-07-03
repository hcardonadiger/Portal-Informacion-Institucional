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

    // ── Visibilidad ───────────────────────────────────────────────
    /// <summary>Pública (visible para su institución/alcance) o Privada (solo el creador).</summary>
    public VisibilidadReunion Visibilidad { get; set; } = VisibilidadReunion.Publica;
    /// <summary>Id del usuario que creó la reunión (para las reuniones privadas).</summary>
    public int? CreadoPorId { get; set; }

    // ── Auto-registro de asistencia (enlace + QR público) ─────────
    /// <summary>Token público (difícil de adivinar) del enlace de auto-registro de participantes.</summary>
    public Guid RegistroToken  { get; private set; } = Guid.NewGuid();
    /// <summary>Si el formulario público de auto-registro acepta nuevos participantes.</summary>
    public bool RegistroAbierto { get; set; } = true;

    public void RegenerarToken() => RegistroToken = Guid.NewGuid();

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

    /// <summary>Registra un participante desde el formulario público de auto-registro.</summary>
    public Asistente RegistrarAsistente(string nombre, string? cargo, string? institucion,
        string? departamento, string? correo, string? telefono)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        var a = new Asistente
        {
            Nombre = nombre.Trim(), Cargo = cargo?.Trim(), Institucion = institucion?.Trim(),
            Departamento = departamento?.Trim(), Correo = correo?.Trim().ToLowerInvariant(), Telefono = telefono?.Trim(),
            AutoRegistro = true, RegistradoEl = DateTime.UtcNow
        };
        _asistentes.Add(a);
        NumAsistentes = _asistentes.Count;
        return a;
    }

    /// <summary>Elimina un asistente puntual (gestión de la lista en vivo) sin tocar el resto.</summary>
    public void EliminarAsistente(int asistenteId)
    {
        var a = _asistentes.FirstOrDefault(x => x.Id == asistenteId);
        if (a is not null) { _asistentes.Remove(a); NumAsistentes = _asistentes.Count; }
    }
}

public sealed class Asistente : BaseEntity
{
    public int       ReunionId    { get; set; }
    public string    Nombre       { get; set; } = default!;
    public string?   Cargo        { get; set; }
    public string?   Institucion  { get; set; }
    public string?   Departamento { get; set; }
    public string?   Correo       { get; set; }
    public string?   Telefono     { get; set; }

    // ── Trazabilidad del auto-registro ────────────────────────────
    public bool      AutoRegistro { get; set; }
    public DateTime? RegistradoEl { get; set; }
}

public sealed class AcuerdoReunion : BaseEntity
{
    public int       ReunionId   { get; set; }
    public int       Orden       { get; set; }
    public string    Compromiso  { get; set; } = default!;
    public string?   Responsable { get; set; }   // responsable (texto libre)
    public DateOnly? Plazo       { get; set; }

    // ── Seguimiento ───────────────────────────────────────────────
    public EstadoCompromiso Estado            { get; set; } = EstadoCompromiso.Pendiente;
    public DateOnly?        FechaCumplimiento { get; set; }
    public string?          NotaSeguimiento   { get; set; }
    public DateTime?        SeguimientoActualizadoEl  { get; set; }
    public string?          SeguimientoActualizadoPor { get; set; }

    /// <summary>True si el plazo ya venció y el compromiso sigue abierto.</summary>
    public bool EstaVencido(DateOnly hoy) =>
        Plazo is { } p && p < hoy &&
        Estado is EstadoCompromiso.Pendiente or EstadoCompromiso.EnProgreso or EstadoCompromiso.Reprogramado;

    /// <summary>Aplica un cambio de seguimiento. Al marcar Cumplido sin fecha, registra hoy.</summary>
    public void ActualizarSeguimiento(EstadoCompromiso estado, DateOnly? fechaCumplimiento, string? nota, string? actor)
    {
        Estado = estado;
        FechaCumplimiento = estado == EstadoCompromiso.Cumplido
            ? (fechaCumplimiento ?? DateOnly.FromDateTime(DateTime.Today))
            : fechaCumplimiento;
        NotaSeguimiento = string.IsNullOrWhiteSpace(nota) ? null : nota.Trim();
        SeguimientoActualizadoEl  = DateTime.UtcNow;
        SeguimientoActualizadoPor = actor;
    }
}
