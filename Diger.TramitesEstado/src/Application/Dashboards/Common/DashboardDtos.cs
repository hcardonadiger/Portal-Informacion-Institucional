using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Dashboards.Common;

/// <summary>Conteo etiquetado para una barra/segmento de gráfico.</summary>
public sealed record ConteoDto(string Etiqueta, int Cantidad);

/// <summary>Punto de una serie temporal mensual (etiqueta "MMM yy" + valor).</summary>
public sealed record SerieMensualDto(string Mes, int Cantidad);

/// <summary>Valor actual + valor del período anterior, para indicadores de tendencia ▲▼.</summary>
public sealed record TendenciaDto(int Actual, int Anterior)
{
    public int Delta => Actual - Anterior;
    public int PorcentajeCambio => Anterior == 0 ? (Actual > 0 ? 100 : 0) : (int)Math.Round((Actual - Anterior) * 100.0 / Anterior);
}

// ── Resumen general (Inicio) ──────────────────────────────────────────────
public sealed record ResumenDto(
    int TicketsAbiertos, int TicketsEnProgreso, int TicketsCriticos, int TicketsTotal,
    int ExpedientesTotal, int ExpedientesCerrados, int ExpedientesEnProceso,
    int ReunionesTotal, int ReunionesMes,
    int AcuerdosVencidos, int AcuerdosProximos,
    TendenciaDto TicketsCreados,
    IReadOnlyList<SerieMensualDto> TicketsPorMes,
    IReadOnlyList<ResumenTicketDto> UltimosTickets);

public sealed record ResumenTicketDto(
    int Id, string Numero, string Titulo, EstadoTicket Estado, PrioridadTicket Prioridad, string? Institucion);

// ── Tickets ───────────────────────────────────────────────────────────────
public sealed record TicketsDashboardDto(
    int Total, int Abiertos, int CriticosAbiertos,
    int Resueltos, int DiasPromedioResolucion, int PorcentajeResueltos,
    int SlaVencidos,
    TendenciaDto TendenciaCreados,
    IReadOnlyList<ConteoDto> PorEstado,
    IReadOnlyList<ConteoDto> PorPrioridad,
    IReadOnlyList<ConteoDto> PorCategoria,
    IReadOnlyList<ConteoDto> PorTema,
    IReadOnlyList<ConteoDto> PorInstitucion,
    IReadOnlyList<SerieMensualDto> CreadosPorMes,
    IReadOnlyList<SerieMensualDto> ResueltosPorMes,
    IReadOnlyList<TicketAntiguedadDto> AbiertosAntiguos);

public sealed record TicketAntiguedadDto(
    int Id, string Numero, string Titulo, string? Institucion, int DiasAbierto, PrioridadTicket Prioridad);

// ── Expedientes ───────────────────────────────────────────────────────────
public sealed record ExpedientesDashboardDto(
    int Total, int TramitesTotal, int Cerrados,
    IReadOnlyList<ConteoDto> PorEstado,
    IReadOnlyList<ConteoDto> PorInstitucion,
    IReadOnlyList<SerieMensualDto> CreadosPorMes);

// ── Reuniones / acuerdos ──────────────────────────────────────────────────
public sealed record ReunionesDashboardDto(
    int Total, int Mes, int AsistentesTotal,
    int AcuerdosVencidos, int AcuerdosProximos, int AcuerdosSinPlazo,
    IReadOnlyList<ConteoDto> PorTipo,
    IReadOnlyList<ConteoDto> PorInstitucion,
    IReadOnlyList<SerieMensualDto> PorMes,
    IReadOnlyList<AcuerdoPendienteDto> Acuerdos,
    int AcuerdosTotal, int AcuerdosCumplidos, int TasaCumplimiento,
    IReadOnlyList<ConteoDto> PorEstadoAcuerdo);

public sealed record AcuerdoPendienteDto(
    string Compromiso, string? Responsable, DateOnly? Plazo, string ReunionTitulo, int ReunionId,
    bool Vencido, EstadoCompromiso Estado);

// ── Helper de series mensuales ─────────────────────────────────────────────
public static class SerieMensual
{
    /// <summary>Construye una serie de los últimos 12 meses (rellena con 0 los meses sin datos).</summary>
    public static IReadOnlyList<SerieMensualDto> Ultimos12(IEnumerable<(int Anio, int Mes, int Cantidad)> datos)
    {
        var dict = datos.ToDictionary(d => (d.Anio, d.Mes), d => d.Cantidad);
        var cult = System.Globalization.CultureInfo.GetCultureInfo("es");
        var baseMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var res = new List<SerieMensualDto>(12);
        for (var i = 11; i >= 0; i--)
        {
            var f = baseMes.AddMonths(-i);
            var c = dict.TryGetValue((f.Year, f.Month), out var v) ? v : 0;
            res.Add(new SerieMensualDto(f.ToString("MMM yy", cult), c));
        }
        return res;
    }
}
