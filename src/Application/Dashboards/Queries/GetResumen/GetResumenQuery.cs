using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetResumen;

public sealed record GetResumenQuery : IRequest<ResumenDto>;

public sealed class GetResumenQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetResumenQuery, ResumenDto>
{
    public async Task<ResumenDto> Handle(GetResumenQuery _, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var primerDiaMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var finMes = primerDiaMes.AddMonths(1).AddDays(-1);

        var tickets = ctx.Tickets;
        var ticketsTotal      = await tickets.CountAsync(ct);
        var ticketsAbiertos   = await tickets.CountAsync(t => t.Estado == EstadoTicket.Abierto, ct);
        var ticketsEnProgreso = await tickets.CountAsync(t => t.Estado == EstadoTicket.EnProgreso, ct);
        var ticketsCriticos   = await tickets.CountAsync(t =>
            t.Prioridad == PrioridadTicket.Critica &&
            (t.Estado == EstadoTicket.Abierto || t.Estado == EstadoTicket.EnProgreso), ct);

        var expedientesTotal    = await ctx.Expedientes.CountAsync(ct);
        var expedientesCerrados = await ctx.Expedientes.CountAsync(e => e.EstadoExpediente == EstadoExpediente.Cerrado, ct);

        var reunionesTotal = await ctx.Reuniones.CountAsync(ct);
        var reunionesMes   = await ctx.Reuniones.CountAsync(r => r.Fecha >= primerDiaMes && r.Fecha <= finMes, ct);

        var acuerdos = ctx.Reuniones.SelectMany(r => r.Acuerdos);
        var acuerdosVencidos = await acuerdos.CountAsync(a => a.Plazo != null && a.Plazo < hoy, ct);
        var acuerdosProximos = await acuerdos.CountAsync(a => a.Plazo != null && a.Plazo >= hoy, ct);

        var ultimos = await tickets
            .OrderByDescending(t => t.CreatedAt)
            .Take(6)
            .Select(t => new ResumenTicketDto(t.Id, t.Numero, t.Titulo, t.Estado, t.Prioridad, t.Institucion))
            .ToListAsync(ct);

        return new ResumenDto(
            ticketsAbiertos, ticketsEnProgreso, ticketsCriticos, ticketsTotal,
            expedientesTotal, expedientesCerrados, expedientesTotal - expedientesCerrados,
            reunionesTotal, reunionesMes,
            acuerdosVencidos, acuerdosProximos,
            ultimos);
    }
}
