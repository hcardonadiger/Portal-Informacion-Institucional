using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetReunionesDashboard;

public sealed record GetReunionesDashboardQuery(string? InstitucionId = null) : IRequest<ReunionesDashboardDto>;

public sealed class GetReunionesDashboardQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetReunionesDashboardQuery, ReunionesDashboardDto>
{
    public async Task<ReunionesDashboardDto> Handle(GetReunionesDashboardQuery q, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var primerDiaMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var finMes = primerDiaMes.AddMonths(1).AddDays(-1);

        var r = ctx.Reuniones.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(q.InstitucionId)) r = r.Where(x => x.InstitucionId == q.InstitucionId);
        var total = await r.CountAsync(ct);
        var mes = await r.CountAsync(x => x.Fecha >= primerDiaMes && x.Fecha <= finMes, ct);
        var asistentes = total == 0 ? 0 : await r.SumAsync(x => x.Asistentes.Count, ct);

        var acuerdos = r.SelectMany(x => x.Acuerdos);

        // Un compromiso está "abierto" si no fue cumplido ni cancelado.
        var vencidos = await acuerdos.CountAsync(a =>
            a.Plazo != null && a.Plazo < hoy &&
            (a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado), ct);
        var proximos = await acuerdos.CountAsync(a =>
            a.Plazo != null && a.Plazo >= hoy &&
            (a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado), ct);
        var sinPlazo = await acuerdos.CountAsync(a =>
            a.Plazo == null &&
            (a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado), ct);

        var acuerdosTotal = await acuerdos.CountAsync(ct);
        var cumplidos     = await acuerdos.CountAsync(a => a.Estado == EstadoCompromiso.Cumplido, ct);
        var tasa = acuerdosTotal == 0 ? 0 : (int)Math.Round(cumplidos * 100.0 / acuerdosTotal);

        var porEstado = (await acuerdos.GroupBy(a => a.Estado).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct))
            .Select(x => new ConteoDto(x.Key.ToString(), x.C))
            .ToList();

        var porTipo = (await r.GroupBy(x => x.Tipo).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new ConteoDto(string.IsNullOrWhiteSpace(x.Key) ? "Sin tipo" : x.Key!, x.C))
            .ToList();

        var porInstitucion = (await r.GroupBy(x => x.Institucion).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new ConteoDto(string.IsNullOrWhiteSpace(x.Key) ? "Sin institución" : x.Key!, x.C))
            .ToList();

        // Compromisos abiertos con plazo: vencidos primero, luego próximos por fecha.
        var lista = (await r
            .SelectMany(x => x.Acuerdos.Select(a => new
            {
                a.Compromiso, a.Responsable, a.Plazo, a.Estado, RTitulo = x.Titulo, RId = x.Id
            }))
            .Where(a => a.Plazo != null &&
                (a.Estado == EstadoCompromiso.Pendiente || a.Estado == EstadoCompromiso.EnProgreso || a.Estado == EstadoCompromiso.Reprogramado))
            .OrderBy(a => a.Plazo)
            .Take(12)
            .ToListAsync(ct))
            .Select(a => new AcuerdoPendienteDto(
                a.Compromiso, a.Responsable, a.Plazo, a.RTitulo, a.RId, a.Plazo < hoy, a.Estado))
            .ToList();

        var desde12 = primerDiaMes.AddMonths(-11);
        var porMesRaw = (await r.Where(x => x.Fecha != null && x.Fecha >= desde12)
            .GroupBy(x => new { x.Fecha!.Value.Year, x.Fecha!.Value.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, C = g.Count() }).ToListAsync(ct))
            .Select(x => (x.Year, x.Month, x.C));

        return new ReunionesDashboardDto(total, mes, asistentes, vencidos, proximos, sinPlazo,
            porTipo, porInstitucion, SerieMensual.Ultimos12(porMesRaw), lista,
            acuerdosTotal, cumplidos, tasa, porEstado);
    }
}
