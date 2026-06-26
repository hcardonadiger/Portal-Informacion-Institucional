using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetReunionesDashboard;

public sealed record GetReunionesDashboardQuery : IRequest<ReunionesDashboardDto>;

public sealed class GetReunionesDashboardQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetReunionesDashboardQuery, ReunionesDashboardDto>
{
    public async Task<ReunionesDashboardDto> Handle(GetReunionesDashboardQuery _, CancellationToken ct)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Now);
        var primerDiaMes = new DateOnly(hoy.Year, hoy.Month, 1);
        var finMes = primerDiaMes.AddMonths(1).AddDays(-1);

        var r = ctx.Reuniones;
        var total = await r.CountAsync(ct);
        var mes = await r.CountAsync(x => x.Fecha >= primerDiaMes && x.Fecha <= finMes, ct);
        var asistentes = total == 0 ? 0 : await r.SumAsync(x => x.Asistentes.Count, ct);

        var acuerdos = r.SelectMany(x => x.Acuerdos);
        var vencidos = await acuerdos.CountAsync(a => a.Plazo != null && a.Plazo < hoy, ct);
        var proximos = await acuerdos.CountAsync(a => a.Plazo != null && a.Plazo >= hoy, ct);
        var sinPlazo = await acuerdos.CountAsync(a => a.Plazo == null, ct);

        var porTipo = (await r.GroupBy(x => x.Tipo).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new ConteoDto(string.IsNullOrWhiteSpace(x.Key) ? "Sin tipo" : x.Key!, x.C))
            .ToList();

        var porInstitucion = (await r.GroupBy(x => x.Institucion).Select(g => new { g.Key, C = g.Count() }).ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new ConteoDto(string.IsNullOrWhiteSpace(x.Key) ? "Sin institución" : x.Key!, x.C))
            .ToList();

        // Acuerdos con plazo: vencidos primero, luego próximos por fecha.
        var lista = (await r
            .SelectMany(x => x.Acuerdos.Select(a => new
            {
                a.Compromiso, a.Responsable, a.Plazo, RTitulo = x.Titulo, RId = x.Id
            }))
            .Where(a => a.Plazo != null)
            .OrderBy(a => a.Plazo)
            .Take(12)
            .ToListAsync(ct))
            .Select(a => new AcuerdoPendienteDto(
                a.Compromiso, a.Responsable, a.Plazo, a.RTitulo, a.RId, a.Plazo < hoy))
            .ToList();

        return new ReunionesDashboardDto(total, mes, asistentes, vencidos, proximos, sinPlazo,
            porTipo, porInstitucion, lista);
    }
}
