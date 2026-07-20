namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Reunión o capacitación documentada (acta + asistencia + acuerdos).
/// Agregado de captura: escalares asignables; hijos reemplazados en bloque.
/// </summary>
public sealed class Reunion : BaseAuditableEntity, ISoftDeletable
{
    // ── Soft Delete ───────────────────────────────────────────────
    public bool IsDeleted { get; set; }
    public string Titulo { get; private set; } = default!;

    /// <summary>Id del registro origen (p. ej. Supabase) cuando la reunión fue importada.
    /// Permite reimportar de forma idempotente; null para reuniones creadas en el portal.</summary>
    public string? OrigenExternoId { get; set; }

    // ── Visibilidad ───────────────────────────────────────────────
    /// <summary>Pública (visible para su institución/alcance) o Privada (solo el creador).</summary>
    public VisibilidadReunion Visibilidad { get; set; } = VisibilidadReunion.Publica;
    /// <summary>Id del usuario que creó la reunión (para las reuniones privadas).</summary>
    public Guid? CreadoPorId { get; set; }

    // ── Hilo (reuniones enlazadas) ────────────────────────────────
    /// <summary>Identificador del hilo al que pertenece la reunión. Las reuniones que comparten
    /// el mismo <see cref="HiloId"/> forman una secuencia enlazada para dar seguimiento a acciones
    /// y tareas a través del tiempo. <c>null</c> = no pertenece a ningún hilo.</summary>
    public Guid? HiloId { get; private set; }

    /// <summary>Enlaza la reunión al hilo indicado.</summary>
    public void EnlazarAHilo(Guid hiloId) => HiloId = hiloId;

    /// <summary>Saca la reunión de su hilo actual.</summary>
    public void SalirDelHilo() => HiloId = null;

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
    public string?   InstitucionId { get; set; } // beneficiaria (opcional; permite "Otro")
    public string?   AreaId        { get; set; }
    public string?   UnidadId      { get; set; }
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
    public int?    SatisfaccionCalificacion { get; set; } // 1-5
    public string? Satisfaccion  { get; set; } // comentario libre
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
    private readonly List<Asistente>          _asistentes  = [];
    private readonly List<AcuerdoReunion>     _acuerdos    = [];
    private readonly List<ReunionInstitucion> _institucionesParticipantes = [];

    public IReadOnlyCollection<Asistente>          Asistentes             => _asistentes.AsReadOnly();
    public IReadOnlyCollection<AcuerdoReunion>     Acuerdos               => _acuerdos.AsReadOnly();
    /// <summary>Instituciones convocadas a la reunión (acumulables). La primera agregada
    /// se conserva como <see cref="InstitucionId"/>/<see cref="Institucion"/> (institución principal),
    /// usada para el alcance institucional, el acta y el tablero.</summary>
    public IReadOnlyCollection<ReunionInstitucion> InstitucionesParticipantes => _institucionesParticipantes.AsReadOnly();

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

    /// <summary>Vacía la lista de instituciones convocadas (reemplazo en bloque).</summary>
    public void LimpiarInstituciones() => _institucionesParticipantes.Clear();

    /// <summary>Agrega una institución convocada, preservando el orden de captura (ignora duplicados).</summary>
    public void AgregarInstitucion(string institucionId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(institucionId);
        if (_institucionesParticipantes.Any(x => x.InstitucionId == institucionId)) return;
        _institucionesParticipantes.Add(new ReunionInstitucion { InstitucionId = institucionId.Trim().ToUpper(), Orden = _institucionesParticipantes.Count });
    }

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

    /// <summary>Pre-registra un invitado por el organizador antes de la reunión.
    /// El pre-registro queda pendiente hasta que el invitado confirme su asistencia (presencialmente o por QR).</summary>
    public Asistente PreRegistrar(string nombre, string? cargo, string? institucion,
        string? departamento, string? correo, string? telefono)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        var a = new Asistente
        {
            Nombre = nombre.Trim(), Cargo = cargo?.Trim(), Institucion = institucion?.Trim(),
            Departamento = departamento?.Trim(), Correo = correo?.Trim().ToLowerInvariant(),
            Telefono = telefono?.Trim(), EsPreregistro = true
        };
        _asistentes.Add(a);
        NumAsistentes = _asistentes.Count;
        return a;
    }

    /// <summary>Confirma o marca como ausente un asistente pre-registrado.</summary>
    public void ConfirmarAsistencia(int asistenteId, bool asistio)
    {
        var a = _asistentes.FirstOrDefault(x => x.Id == asistenteId);
        if (a is not null) a.Confirmado = asistio;
    }

    /// <summary>Elimina un asistente puntual (gestión de la lista en vivo) sin tocar el resto.</summary>
    public void EliminarAsistente(int asistenteId)
    {
        var a = _asistentes.FirstOrDefault(x => x.Id == asistenteId);
        if (a is not null) { _asistentes.Remove(a); NumAsistentes = _asistentes.Count; }
    }
}

/// <summary>Institución convocada a una reunión (lista acumulable). Restringe qué instituciones
/// pueden seleccionarse al pasar asistencia (auto-registro y directorio del organizador).</summary>
public sealed class ReunionInstitucion : BaseEntity
{
    public int    ReunionId     { get; set; }
    /// <summary>FK a Instituciones.Id (string). Coincide con el PK nvarchar(120) de la tabla Instituciones.</summary>
    public string InstitucionId { get; set; } = default!;
    public int    Orden         { get; set; }
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
    public bool      AutoRegistro  { get; set; }
    public DateTime? RegistradoEl  { get; set; }

    // ── Pre-registro ──────────────────────────────────────────────
    /// <summary>true = fue pre-registrado por el organizador antes de la reunión.</summary>
    public bool  EsPreregistro { get; set; }
    /// <summary>null = pendiente de confirmar; true = asistió; false = ausente.</summary>
    public bool? Confirmado    { get; set; }
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
