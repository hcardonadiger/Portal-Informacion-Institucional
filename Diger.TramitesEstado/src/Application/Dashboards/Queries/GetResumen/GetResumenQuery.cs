using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetResumen;

public sealed record GetResumenQuery(
    // Alcance del técnico: cuando TecnicoUserId != null, los KPIs de tickets se limitan a sus temas o asignados.
    IReadOnlyList<int>? TecnicoTemaIds = null, int? TecnicoUserId = null) : IRequest<ResumenDto>;

public sealed class GetResumenQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetResumenQuery, ResumenDto>
{
    public async Task<ResumenDto> Handle(GetResumenQuery q, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var primerDiaMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var finMes = primerDiaMes.AddMonths(1).AddDays(-1);

        var tickets = ctx.Tickets.AsQueryable();
        if (q.TecnicoUserId is int tuid)
        {
            var temaIds = q.TecnicoTemaIds ?? [];
            tickets = tickets.Where(t => (t.TemaId != null && temaIds.Contains(t.TemaId.Value)) || t.AsignadoAId == tuid);
        }
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

        // "Abierto" = no cumplido ni cancelado. Los vencidos/próximos solo cuentan compromisos abiertos.
        var acuerdos = ctx.Reuniones.SelectMany(r => r.Acuerdos)
            .Where(a => a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado);
        var acuerdosVencidos = await acuerdos.CountAsync(a => a.Plazo != null && a.Plazo < hoy, ct);
        var acuerdosProximos = await acuerdos.CountAsync(a => a.Plazo != null && a.Plazo >= hoy, ct);

        var ultimos = await tickets
            .OrderByDescending(t => t.CreatedAt)
            .Take(6)
            .Select(t => new ResumenTicketDto(t.Id, t.Numero, t.Titulo, t.Estado, t.Prioridad, t.Institucion))
            .ToListAsync(ct);

        // Tendencia de tickets creados (mes actual vs anterior) + serie 12 meses.
        var iniMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var iniMesAnt = iniMes.AddMonths(-1);
        var creadosMes = await tickets.CountAsync(t => t.CreatedAt >= iniMes, ct);
        var creadosMesAnt = await tickets.CountAsync(t => t.CreatedAt >= iniMesAnt && t.CreatedAt < iniMes, ct);
        var desde12 = iniMes.AddMonths(-11);
        var porMesRaw = (await tickets.Where(t => t.CreatedAt >= desde12)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, C = g.Count() }).ToListAsync(ct))
            .Select(x => (x.Year, x.Month, x.C));

        return new ResumenDto(
            ticketsAbiertos, ticketsEnProgreso, ticketsCriticos, ticketsTotal,
            expedientesTotal, expedientesCerrados, expedientesTotal - expedientesCerrados,
            reunionesTotal, reunionesMes,
            acuerdosVencidos, acuerdosProximos,
            new TendenciaDto(creadosMes, creadosMesAnt),
            SerieMensual.Ultimos12(porMesRaw),
            ultimos);
    }
}
