using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Tickets.Common;

// Clase mutable para enlace de modelo (Razor) y transporte al comando.
public sealed class TicketFormDto
{
    public string          Titulo      { get; set; } = string.Empty;
    public string?         Descripcion { get; set; }
    public int?            TemaId      { get; set; } // tema/categoría del catálogo administrable
    public string?         TemaOtro    { get; set; }
    public PrioridadTicket Prioridad   { get; set; } = PrioridadTicket.Media;

    public int? InstitucionId { get; set; }
    public int? ExpedienteId  { get; set; }
    /// <summary>Ids del catálogo (TramiteDefinicion) de los trámites afectados por el incidente.</summary>
    public List<int> TramiteIds { get; set; } = [];
    // El reportante se obtiene del usuario que registra el ticket (no se captura en el formulario).
}

public sealed record TicketListItemDto(
    int Id, string Numero, string Titulo, EstadoTicket Estado, PrioridadTicket Prioridad,
    string? Tema, string? TemaOtro, int? HorasSla, bool SlaVencido, string? Institucion, string? AsignadoA,
    DateTime FechaCreacion, int NumComentarios);

public sealed record TicketComentarioDto(int Id, TipoComentarioTicket Tipo, string Autor, string Texto, DateTime Fecha);

/// <summary>Metadatos de un archivo ya guardado, para transportarlos al comando.</summary>
public sealed record AdjuntoInput(string Nombre, string Url, long Tamano);

public sealed record AdjuntoDto(int Id, int? ComentarioId, string Nombre, string Url, long Tamano);

public sealed record TicketDetailDto(
    int Id, string Numero, string Titulo, string? Descripcion,
    int? TemaId, string? Tema, string? TemaOtro, int? HorasSla, PrioridadTicket Prioridad, EstadoTicket Estado,
    int? InstitucionId, string? Institucion, int? ExpedienteId, string? ExpedienteCodigo,
    string? ReportanteNombre, string? ReportanteCorreo, string? ReportanteTelefono,
    int? AsignadoAId, string? AsignadoA, DateTime? FechaResolucion, string? NotaResolucion,
    DateTime FechaCreacion, string? CreadoPor, IReadOnlyList<string> Tramites,
    IReadOnlyList<AdjuntoDto> Adjuntos,
    IReadOnlyList<TicketComentarioDto> Comentarios);

public sealed record UsuarioAsignableDto(int Id, string Nombre, RolUsuario Rol);

/// <summary>Opción de tema para selectores (creación/edición/asignación), su SLA y su categoría.</summary>
public sealed record TemaOpcionDto(int Id, string Nombre, int HorasResolucion, bool Activo, int? CategoriaId, string? Categoria);

/// <summary>Opción de categoría (nivel superior) para selectores.</summary>
public sealed record CategoriaOpcionDto(int Id, string Nombre);
