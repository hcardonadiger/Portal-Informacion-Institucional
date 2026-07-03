using Diger.TramitesEstado.Domain.Enums;

namespace Diger.TramitesEstado.Application.Tickets.Common;

/// <summary>Cálculo del SLA (tiempo máximo de solución por tema, en horas) para monitoreo.</summary>
public static class TicketSla
{
    public static bool EstaAbierto(EstadoTicket estado) =>
        estado is EstadoTicket.Abierto or EstadoTicket.EnProgreso;

    /// <summary>Horas transcurridas desde la creación (UTC).</summary>
    public static double HorasTranscurridas(DateTime creadoUtc) =>
        Math.Max(0, (DateTime.UtcNow - creadoUtc).TotalHours);

    /// <summary>Vencido = ticket abierto cuyo tema tiene SLA (&gt;0) y ya se superó el tiempo máximo.</summary>
    public static bool Vencido(EstadoTicket estado, int? horasSla, DateTime creadoUtc) =>
        EstaAbierto(estado) && horasSla is int h && h > 0 && HorasTranscurridas(creadoUtc) > h;

    /// <summary>Horas restantes hasta el vencimiento (negativo si ya venció); null si no hay SLA.</summary>
    public static double? HorasRestantes(int? horasSla, DateTime creadoUtc) =>
        horasSla is int h && h > 0 ? h - HorasTranscurridas(creadoUtc) : null;
}
