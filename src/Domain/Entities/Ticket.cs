namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Ticket de soporte de la plataforma SOL (incidencia reportada por una institución).
/// Agregado: escalares de captura editables; estado y asignación se controlan por métodos;
/// el seguimiento (comentarios, cambios de estado) se agrega de forma incremental.
/// </summary>
public sealed class Ticket : BaseAuditableEntity, ISoftDeletable
{
    // ── Soft Delete ───────────────────────────────────────────────
    public bool IsDeleted { get; set; }
    public string Numero { get; private set; } = default!; // TCK-2026-0001 (controlado)
    public string Titulo { get; private set; } = default!;
    public string? Descripcion { get; set; }

    // Tema/categoría administrable (catálogo TemaTicket). Nullable = sin clasificar.
    public int?            TemaId    { get; set; }
    public TemaTicket?     TemaRef   { get; set; } // navegación (para nombre + SLA)
    public PrioridadTicket Prioridad { get; set; } = PrioridadTicket.Media;
    public EstadoTicket    Estado    { get; private set; } = EstadoTicket.Abierto;

    // ── Vínculos (opcionales) ─────────────────────────────────────
    public string? InstitucionId    { get; set; }
    public string? AreaId           { get; set; }
    public string? UnidadId         { get; set; }
    public string? Institucion      { get; set; } // snapshot del nombre
    public int?    ExpedienteId     { get; set; }
    public string? ExpedienteCodigo { get; set; } // snapshot del código

    // ── Reportante (se obtiene del usuario que registra el ticket) ─
    public string? ReportanteNombre   { get; private set; }
    public string? ReportanteCorreo   { get; private set; }
    public string? ReportanteTelefono { get; private set; }

    // ── Autoría ───────────────────────────────────────────────────
    public Guid?      CreadoPorId { get; private set; } // usuario que lo creó (null = sistema)
    public string?   CreadoPor   { get; private set; } // snapshot del nombre

    // ── Asignación / resolución ───────────────────────────────────
    public Guid?     AsignadoAId   { get; private set; }
    public string?   AsignadoA     { get; private set; } // snapshot del nombre del usuario
    public DateTime? FechaResolucion { get; private set; }
    public string?   NotaResolucion  { get; private set; }

    // ── Seguimiento ───────────────────────────────────────────────
    private readonly List<TicketComentario> _comentarios = [];
    public IReadOnlyCollection<TicketComentario> Comentarios => _comentarios.AsReadOnly();

    // ── Trámites afectados por el incidente ───────────────────────
    private readonly List<TicketTramite> _tramites = [];
    public IReadOnlyCollection<TicketTramite> Tramites => _tramites.AsReadOnly();

    // ── Archivos adjuntos (creación o respuesta) ──────────────────
    private readonly List<TicketAdjunto> _adjuntos = [];
    public IReadOnlyCollection<TicketAdjunto> Adjuntos => _adjuntos.AsReadOnly();

    /// <summary>Agrega un adjunto al ticket. <paramref name="comentarioId"/> null = subido en la creación.</summary>
    public void AgregarAdjunto(string nombre, string url, long tamano, int? comentarioId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nombre);
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        _adjuntos.Add(new TicketAdjunto
        {
            NombreArchivo = nombre.Trim(), Url = url.Trim(), Tamano = tamano, ComentarioId = comentarioId
        });
    }

    /// <summary>Reemplaza en bloque los trámites afectados (guarda el nombre como snapshot del catálogo).</summary>
    public void EstablecerTramites(IEnumerable<(int? DefinicionId, string Nombre)> tramites)
    {
        _tramites.Clear();
        foreach (var (id, nombre) in tramites.Where(x => !string.IsNullOrWhiteSpace(x.Nombre)))
            _tramites.Add(new TicketTramite { TramiteDefinicionId = id, Tramite = nombre.Trim() });
    }

    private Ticket() { }

    public static Ticket Crear(string numero, string titulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(numero);
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);
        var t = new Ticket { Numero = numero.Trim().ToUpperInvariant(), Titulo = titulo.Trim() };
        t.AddDomainEvent(new TicketCreatedEvent(t.Id, t.Numero, t.Titulo));
        return t;
    }

    public void EstablecerTitulo(string titulo)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(titulo);
        Titulo = titulo.Trim();
    }

    /// <summary>Registra el autor del ticket (por defecto el usuario actual; "Sistema" si no hay).</summary>
    public void EstablecerCreador(Guid? usuarioId, string? nombre)
    {
        CreadoPorId = usuarioId;
        CreadoPor   = string.IsNullOrWhiteSpace(nombre) ? "Sistema" : nombre.Trim();
    }

    /// <summary>Establece los datos del reportante (se toman del usuario que registra el ticket).</summary>
    public void EstablecerReportante(string? nombre, string? correo, string? telefono = null)
    {
        ReportanteNombre   = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();
        ReportanteCorreo   = string.IsNullOrWhiteSpace(correo) ? null : correo.Trim().ToLowerInvariant();
        ReportanteTelefono = string.IsNullOrWhiteSpace(telefono) ? null : telefono.Trim();
    }

    public void MarcarActualizado() => UpdatedAt = DateTime.UtcNow;

    /// <summary>Asigna (o reasigna) el ticket a un usuario. Si estaba Abierto, pasa a En progreso.</summary>
    public void Asignar(Guid? usuarioId, string? nombre, string actualizadoPor)
    {
        AsignadoAId = usuarioId;
        AsignadoA   = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();
        if (Estado == EstadoTicket.Abierto && usuarioId is not null)
            CambiarEstadoInterno(EstadoTicket.EnProgreso, actualizadoPor, null);

        _comentarios.Add(TicketComentario.Sistema(
            TipoComentarioTicket.Asignacion, actualizadoPor,
            string.IsNullOrWhiteSpace(nombre) ? "El ticket fue desasignado." : $"Asignado a: {nombre}"));
    }

    /// <summary>Cambia el estado registrando el evento de seguimiento. Al resolver guarda fecha y nota.</summary>
    public void CambiarEstado(EstadoTicket nuevo, string autor, string? nota)
    {
        if (nuevo == Estado)
            throw new DomainException($"El ticket ya está en estado «{Estado}».");
        CambiarEstadoInterno(nuevo, autor, nota);
    }

    private void CambiarEstadoInterno(EstadoTicket nuevo, string autor, string? nota)
    {
        var anterior = Estado;
        Estado = nuevo;

        if (nuevo is EstadoTicket.Resuelto or EstadoTicket.Cerrado)
        {
            FechaResolucion ??= DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(nota)) NotaResolucion = nota.Trim();
        }
        else if (nuevo is EstadoTicket.Abierto or EstadoTicket.EnProgreso)
        {
            FechaResolucion = null; // reabierto
        }

        var detalle = $"Estado: {anterior} → {nuevo}." + (string.IsNullOrWhiteSpace(nota) ? "" : $" {nota.Trim()}");
        _comentarios.Add(TicketComentario.Sistema(TipoComentarioTicket.CambioEstado, autor, detalle));
        AddDomainEvent(new TicketEstadoCambiadoEvent(Id, Numero, Estado.ToString()));
    }

    public TicketComentario AgregarComentario(string autor, string texto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texto);
        var c = TicketComentario.DeUsuario(autor, texto.Trim());
        _comentarios.Add(c);
        return c;
    }
}

public sealed class TicketComentario : BaseEntity
{
    public int                  TicketId { get; set; }
    public TipoComentarioTicket Tipo     { get; private set; } = TipoComentarioTicket.Comentario;
    public string               Autor    { get; private set; } = default!;
    public string               Texto    { get; private set; } = default!;
    public DateTime             Fecha    { get; private set; } = DateTime.UtcNow;

    private TicketComentario() { }

    public static TicketComentario DeUsuario(string autor, string texto) =>
        new() { Tipo = TipoComentarioTicket.Comentario, Autor = (autor ?? "—").Trim(), Texto = texto, Fecha = DateTime.UtcNow };

    public static TicketComentario Sistema(TipoComentarioTicket tipo, string autor, string texto) =>
        new() { Tipo = tipo, Autor = (autor ?? "Sistema").Trim(), Texto = texto, Fecha = DateTime.UtcNow };
}

/// <summary>Trámite afectado por el incidente de un ticket (snapshot del nombre del catálogo institucional).</summary>
public sealed class TicketTramite : BaseEntity
{
    public int    TicketId            { get; set; }
    public int?   TramiteDefinicionId { get; set; } // referencia suave al catálogo (se reemplaza en bloque, sin FK)
    public string Tramite             { get; set; } = default!; // snapshot del nombre
}

/// <summary>Archivo adjunto de un ticket (subido en la creación o en una respuesta).</summary>
public sealed class TicketAdjunto : BaseEntity
{
    public int    TicketId      { get; set; }
    public int?   ComentarioId  { get; set; } // referencia suave al comentario (null = subido al crear el ticket)
    public string NombreArchivo { get; set; } = default!; // nombre original
    public string Url           { get; set; } = default!; // ruta relativa en /uploads/tickets
    public long   Tamano        { get; set; } // bytes
}
