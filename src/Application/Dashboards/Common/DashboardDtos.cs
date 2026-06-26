using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Dashboards.Common;

/// <summary>Conteo etiquetado para una barra/segmento de gráfico.</summary>
public sealed record ConteoDto(string Etiqueta, int Cantidad);

// ── Resumen general (Inicio) ──────────────────────────────────────────────
public sealed record ResumenDto(
    int TicketsAbiertos, int TicketsEnProgreso, int TicketsCriticos, int TicketsTotal,
    int ExpedientesTotal, int ExpedientesCerrados, int ExpedientesEnProceso,
    int ReunionesTotal, int ReunionesMes,
    int AcuerdosVencidos, int AcuerdosProximos,
    IReadOnlyList<ResumenTicketDto> UltimosTickets);

public sealed record ResumenTicketDto(
    int Id, string Numero, string Titulo, EstadoTicket Estado, PrioridadTicket Prioridad, string? Institucion);

// ── Tickets ───────────────────────────────────────────────────────────────
public sealed record TicketsDashboardDto(
    int Total, int Abiertos, int CriticosAbiertos,
    IReadOnlyList<ConteoDto> PorEstado,
    IReadOnlyList<ConteoDto> PorPrioridad,
    IReadOnlyList<ConteoDto> PorCategoria,
    IReadOnlyList<ConteoDto> PorInstitucion,
    IReadOnlyList<TicketAntiguedadDto> AbiertosAntiguos);

public sealed record TicketAntiguedadDto(
    int Id, string Numero, string Titulo, string? Institucion, int DiasAbierto, PrioridadTicket Prioridad);

// ── Expedientes ───────────────────────────────────────────────────────────
public sealed record ExpedientesDashboardDto(
    int Total, int TramitesTotal, int Cerrados,
    IReadOnlyList<ConteoDto> PorEstado,
    IReadOnlyList<ConteoDto> PorInstitucion);

// ── Reuniones / acuerdos ──────────────────────────────────────────────────
public sealed record ReunionesDashboardDto(
    int Total, int Mes, int AsistentesTotal,
    int AcuerdosVencidos, int AcuerdosProximos, int AcuerdosSinPlazo,
    IReadOnlyList<ConteoDto> PorTipo,
    IReadOnlyList<ConteoDto> PorInstitucion,
    IReadOnlyList<AcuerdoPendienteDto> Acuerdos);

public sealed record AcuerdoPendienteDto(
    string Compromiso, string? Responsable, DateOnly? Plazo, string ReunionTitulo, int ReunionId, bool Vencido);
