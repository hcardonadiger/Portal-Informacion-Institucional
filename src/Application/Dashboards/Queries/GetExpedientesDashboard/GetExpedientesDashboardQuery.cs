using Diger.TramitesEstado.Application.Dashboards.Common;

namespace Diger.TramitesEstado.Application.Dashboards.Queries.GetExpedientesDashboard;

public sealed record GetExpedientesDashboardQuery : IRequest<ExpedientesDashboardDto>;

public sealed class GetExpedientesDashboardQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetExpedientesDashboardQuery, ExpedientesDashboardDto>
{
    public async Task<ExpedientesDashboardDto> Handle(GetExpedientesDashboardQuery _, CancellationToken ct)
    {
        var e = ctx.Expedientes;
        var total = await e.CountAsync(ct);
        var cerrados = await e.CountAsync(x => x.EstadoExpediente == EstadoExpediente.Cerrado, ct);
        var tramites = total == 0 ? 0 : await e.SumAsync(x => x.Tramites.Count, ct);

        var conteoEstado = (await e
            .GroupBy(x => x.EstadoExpediente)
            .Select(g => new { g.Key, C = g.Count() })
            .ToListAsync(ct))
            .ToDictionary(x => x.Key, x => x.C);

        var porEstado = Enum.GetValues<EstadoExpediente>()
            .Select(est => new ConteoDto(EstadoLabel(est), conteoEstado.TryGetValue(est, out var c) ? c : 0))
            .ToList();

        var porInstitucion = (await e
            .GroupBy(x => x.Institucion)
            .Select(g => new { Inst = g.Key, C = g.Count() })
            .ToListAsync(ct))
            .OrderByDescending(x => x.C)
            .Select(x => new ConteoDto(x.Inst, x.C))
            .ToList();

        return new ExpedientesDashboardDto(total, tramites, cerrados, porEstado, porInstitucion);
    }

    private static string EstadoLabel(EstadoExpediente e) => e switch
    {
        EstadoExpediente.EnExploracion   => "En exploración",
        EstadoExpediente.EnLevantamiento => "En levantamiento",
        EstadoExpediente.EnModelado      => "En modelado",
        EstadoExpediente.EnValidacion    => "En validación",
        EstadoExpediente.Cerrado         => "Cerrado",
        _ => e.ToString()
    };
}
