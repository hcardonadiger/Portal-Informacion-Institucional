using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetResumen;

/// <summary>
/// Indicadores del Inicio. Solo los transversales: tickets, reuniones y sus
/// acuerdos, cumplimiento y capacitación. El bloque de digitalización de trámites
/// que esta consulta armaba (avance global, por institución, por analista, estado
/// de trámites) salió de acá junto con los filtros de período y estado que solo a
/// él aplicaban; ese detalle lo sirve GetDigitalizacionDashboardQuery, que es
/// donde el dominio se ve completo y no disfrazado de resumen del portal.
/// </summary>
public sealed record GetResumenQuery(
    IReadOnlyList<int>? TecnicoTemaIds = null, Guid? TecnicoUserId = null) : IRequest<ResumenDto>;

public sealed class GetResumenQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetResumenQuery, ResumenDto>
{
    public async Task<ResumenDto> Handle(GetResumenQuery q, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var primerDiaMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var finMes = primerDiaMes.AddMonths(1).AddDays(-1);

        // ── Tickets ──────────────────────────────────────────────────
        var tickets = ctx.Tickets.AsNoTracking();
        if (q.TecnicoUserId is Guid tuid)
        {
            var temaIds = q.TecnicoTemaIds ?? [];
            tickets = tickets.Where(t => (t.TemaId != null && temaIds.Contains(t.TemaId.Value)) || t.AsignadoAId == tuid);
        }

        var ticketsTotal      = await tickets.CountAsync(ct);
        var ticketsAbiertos   = await tickets.CountAsync(t => t.Estado == EstadoTicket.Abierto, ct);
        var ticketsEnProgreso = await tickets.CountAsync(t => t.Estado == EstadoTicket.EnProgreso, ct);
        var ticketsCriticos   = await tickets.CountAsync(t =>
            t.PrioridadRef!.EsCritica &&
            (t.Estado == EstadoTicket.Abierto || t.Estado == EstadoTicket.EnProgreso), ct);

        var ticketsResueltos = await tickets.CountAsync(t => t.Estado == EstadoTicket.Resuelto, ct);

        var resueltosFechas = await tickets
            .Where(t => t.Estado == EstadoTicket.Resuelto && t.FechaResolucion != null)
            .Select(t => new { t.CreatedAt, Resuelto = t.FechaResolucion!.Value })
            .ToListAsync(ct);
        var diasPromedio = resueltosFechas.Count == 0
            ? 0 : (int)Math.Round(resueltosFechas.Average(t => (t.Resuelto - t.CreatedAt).TotalDays));

        var iniMes = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var iniMesAnt = iniMes.AddMonths(-1);
        var creadosMes    = await tickets.CountAsync(t => t.CreatedAt >= iniMes, ct);
        var creadosMesAnt = await tickets.CountAsync(t => t.CreatedAt >= iniMesAnt && t.CreatedAt < iniMes, ct);

        var desde12 = iniMes.AddMonths(-11);
        var porMesRaw = (await tickets.Where(t => t.CreatedAt >= desde12)
            .GroupBy(t => new { t.CreatedAt.Year, t.CreatedAt.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, C = g.Count() }).ToListAsync(ct))
            .Select(x => (x.Year, x.Month, x.C));

        var resueltosMesRaw = (await tickets
            .Where(t => t.Estado == EstadoTicket.Resuelto && t.FechaResolucion != null && t.FechaResolucion >= desde12)
            .GroupBy(t => new { t.FechaResolucion!.Value.Year, t.FechaResolucion!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, C = g.Count() }).ToListAsync(ct))
            .Select(x => (x.Year, x.Month, x.C));

        // ── Reuniones y acuerdos ─────────────────────────────────────
        var reunionesTotal = await ctx.Reuniones.CountAsync(ct);
        var reunionesMes   = await ctx.Reuniones.CountAsync(r => r.Fecha >= primerDiaMes && r.Fecha <= finMes, ct);

        var acuerdos = ctx.Reuniones.SelectMany(r => r.Acuerdos);
        var abiertos = acuerdos.Where(a =>
            a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado);
        var acuerdosVencidos  = await abiertos.CountAsync(a => a.Plazo != null && a.Plazo < hoy, ct);
        var acuerdosProximos  = await abiertos.CountAsync(a => a.Plazo != null && a.Plazo >= hoy, ct);
        var acuerdosTotal     = await acuerdos.CountAsync(ct);
        var acuerdosCumplidos = await acuerdos.CountAsync(a => a.Estado == EstadoCompromiso.Cumplido, ct);
        var tasaCumplimiento  = acuerdosTotal == 0 ? 0 : (int)Math.Round(acuerdosCumplidos * 100.0 / acuerdosTotal);

        // ── Capacitación (conteo simplificado) ───────────────────────
        var correosCap = await ctx.Reuniones.AsNoTracking()
            .Where(x => x.EsCapacitacionPlataforma || (x.Tipo != null && x.Tipo.Contains("Capacitaci")))
            .SelectMany(x => x.Asistentes.Select(a => new { a.Correo, a.Nombre }))
            .ToListAsync(ct);

        var asistenciasCap = correosCap.Count;
        var personasCap = correosCap
            .Where(a => !string.IsNullOrWhiteSpace(a.Nombre))
            .Select(a => !string.IsNullOrWhiteSpace(a.Correo) ? a.Correo.Trim().ToLowerInvariant() : a.Nombre!.Trim().ToLowerInvariant())
            .Distinct()
            .Count();

        return new ResumenDto(
            ticketsAbiertos, ticketsEnProgreso, ticketsCriticos, ticketsTotal,
            ticketsResueltos, diasPromedio,
            new TendenciaDto(creadosMes, creadosMesAnt),
            SerieMensual.Ultimos12(porMesRaw),
            SerieMensual.Ultimos12(resueltosMesRaw),
            reunionesTotal, reunionesMes,
            acuerdosVencidos, acuerdosProximos,
            acuerdosTotal, acuerdosCumplidos, tasaCumplimiento,
            personasCap, asistenciasCap);
    }
}
