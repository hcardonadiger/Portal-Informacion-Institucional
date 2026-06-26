using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Tickets.Common;

// Clase mutable para enlace de modelo (Razor) y transporte al comando.
public sealed class TicketFormDto
{
    public string          Titulo      { get; set; } = string.Empty;
    public string?         Descripcion { get; set; }
    public CategoriaTicket Categoria   { get; set; } = CategoriaTicket.ErrorPlataforma;
    public PrioridadTicket Prioridad   { get; set; } = PrioridadTicket.Media;

    public int? InstitucionId { get; set; }
    public int? ExpedienteId  { get; set; }

    public string? ReportanteNombre   { get; set; }
    public string? ReportanteCorreo   { get; set; }
    public string? ReportanteTelefono { get; set; }
}

public sealed record TicketListItemDto(
    int Id, string Numero, string Titulo, EstadoTicket Estado, PrioridadTicket Prioridad,
    CategoriaTicket Categoria, string? Institucion, string? AsignadoA, DateTime FechaCreacion, int NumComentarios);

public sealed record TicketComentarioDto(TipoComentarioTicket Tipo, string Autor, string Texto, DateTime Fecha);

public sealed record TicketDetailDto(
    int Id, string Numero, string Titulo, string? Descripcion,
    CategoriaTicket Categoria, PrioridadTicket Prioridad, EstadoTicket Estado,
    int? InstitucionId, string? Institucion, int? ExpedienteId, string? ExpedienteCodigo,
    string? ReportanteNombre, string? ReportanteCorreo, string? ReportanteTelefono,
    int? AsignadoAId, string? AsignadoA, DateTime? FechaResolucion, string? NotaResolucion,
    DateTime FechaCreacion, string? CreadoPor, IReadOnlyList<TicketComentarioDto> Comentarios);

public sealed record UsuarioAsignableDto(int Id, string Nombre, RolUsuario Rol);
