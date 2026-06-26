namespace Diger.TramitesEstado.Domain.Entities;

/// <summary>
/// Ticket de soporte de la plataforma SOL (incidencia reportada por una institución).
/// Agregado: escalares de captura editables; estado y asignación se controlan por métodos;
/// el seguimiento (comentarios, cambios de estado) se agrega de forma incremental.
/// </summary>
public sealed class Ticket : BaseAuditableEntity
{
    public string Numero { get; private set; } = default!; // TCK-2026-0001 (controlado)
    public string Titulo { get; private set; } = default!;
    public string? Descripcion { get; set; }

    public CategoriaTicket Categoria { get; set; } = CategoriaTicket.Otro;
    public PrioridadTicket Prioridad { get; set; } = PrioridadTicket.Media;
    public EstadoTicket    Estado    { get; private set; } = EstadoTicket.Abierto;

    // ── Vínculos (opcionales) ─────────────────────────────────────
    public int?    InstitucionId    { get; set; }
    public string? Institucion      { get; set; } // snapshot del nombre
    public int?    ExpedienteId     { get; set; }
    public string? ExpedienteCodigo { get; set; } // snapshot del código

    // ── Reportante ────────────────────────────────────────────────
    public string? ReportanteNombre   { get; set; }
    public string? ReportanteCorreo   { get; set; }
    public string? ReportanteTelefono { get; set; }

    // ── Asignación / resolución ───────────────────────────────────
    public int?      AsignadoAId   { get; private set; }
    public string?   AsignadoA     { get; private set; } // snapshot del nombre del usuario
    public DateTime? FechaResolucion { get; private set; }
    public string?   NotaResolucion  { get; private set; }

    // ── Seguimiento ───────────────────────────────────────────────
    private readonly List<TicketComentario> _comentarios = [];
    public IReadOnlyCollection<TicketComentario> Comentarios => _comentarios.AsReadOnly();

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

    public void MarcarActualizado() => UpdatedAt = DateTime.UtcNow;

    /// <summary>Asigna (o reasigna) el ticket a un usuario. Si estaba Abierto, pasa a En progreso.</summary>
    public void Asignar(int? usuarioId, string? nombre, string autor)
    {
        AsignadoAId = usuarioId;
        AsignadoA   = string.IsNullOrWhiteSpace(nombre) ? null : nombre.Trim();
        if (Estado == EstadoTicket.Abierto && usuarioId is not null)
            CambiarEstadoInterno(EstadoTicket.EnProgreso, autor, null);

        _comentarios.Add(TicketComentario.Sistema(
            TipoComentarioTicket.Asignacion, autor,
            AsignadoA is null ? "Asignación removida." : $"Asignado a {AsignadoA}."));
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

    public void AgregarComentario(string autor, string texto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(texto);
        _comentarios.Add(TicketComentario.DeUsuario(autor, texto.Trim()));
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
