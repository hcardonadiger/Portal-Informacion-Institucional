using Diger.TramitesEstado.Application.Informes.Common;

namespace Diger.TramitesEstado.Application.Informes.Queries;

public sealed record GetInformeInstitucionQuery(
    string?   InstitucionId,
    DateOnly? Desde,
    DateOnly? Hasta
) : IRequest<InformeInstitucionDto>;

public sealed class GetInformeInstitucionQueryHandler(IApplicationDbContext ctx)
    : IRequestHandler<GetInformeInstitucionQuery, InformeInstitucionDto>
{
    public async Task<InformeInstitucionDto> Handle(GetInformeInstitucionQuery q, CancellationToken ct)
    {
        var desdeUtc = q.Desde is { } d ? new DateTime(d.Year, d.Month, d.Day, 0, 0, 0, DateTimeKind.Utc) : (DateTime?)null;
        var hastaUtc = q.Hasta is { } h ? new DateTime(h.Year, h.Month, h.Day, 23, 59, 59, DateTimeKind.Utc) : (DateTime?)null;

        var expedientes = await ctx.Expedientes
            .AsNoTracking()
            .Where(e =>
                (q.InstitucionId == null || e.InstitucionId == q.InstitucionId) &&
                (desdeUtc == null || e.CreatedAt >= desdeUtc) &&
                (hastaUtc == null || e.CreatedAt <= hastaUtc))
            .Include(e => e.Tramites)
            .OrderBy(e => e.Codigo)
            .ToListAsync(ct);

        if (expedientes.Count == 0)
        {
            var nombreVacio = q.InstitucionId != null
                ? await ctx.Instituciones
                    .Where(i => i.Id == q.InstitucionId)
                    .Select(i => i.Nombre)
                    .FirstOrDefaultAsync(ct) ?? q.InstitucionId
                : "Todas las instituciones";

            return new InformeInstitucionDto(
                q.InstitucionId ?? "",
                nombreVacio,
                q.Desde, q.Hasta, []);
        }

        var expIds = expedientes.Select(e => e.Id).ToList();

        // Avance por (ExpedienteId, TramiteIndex) — excluye marcadores APLICA:*
        var avances = await ctx.ExpedienteEtapaAvances
            .AsNoTracking()
            .Where(a => expIds.Contains(a.ExpedienteId) && !a.SubId.StartsWith("APLICA:"))
            .GroupBy(a => new { a.ExpedienteId, a.TramiteIndex })
            .Select(g => new
            {
                g.Key.ExpedienteId,
                g.Key.TramiteIndex,
                Total       = g.Count(),
                Completados = g.Count(a => a.Estado == 2)
            })
            .ToListAsync(ct);

        var avanceMap = avances.ToDictionary(
            a => (a.ExpedienteId, a.TramiteIndex),
            a => (a.Total, a.Completados));

        // Nombre de institución (snapshot del primer expediente o consulta)
        var institucionNombre = q.InstitucionId != null
            ? await ctx.Instituciones
                .Where(i => i.Id == q.InstitucionId)
                .Select(i => i.Nombre)
                .FirstOrDefaultAsync(ct) ?? expedientes[0].Institucion
            : "Todas las instituciones";

        var items = expedientes.Select(e =>
        {
            var tramites = e.Tramites
                .OrderBy(t => t.TramiteIndex)
                .Select(t =>
                {
                    avanceMap.TryGetValue((e.Id, t.TramiteIndex), out var av);
                    return new InformeTramiteDto(t.TramiteIndex, t.NombreTramite, av.Total, av.Completados);
                })
                .ToList();

            return new InformeExpedienteDto(e.Id, e.Codigo, e.EstadoExpediente, e.Analista,
                e.FechaApertura, e.CreatedAt, tramites);
        }).ToList();

        return new InformeInstitucionDto(
            q.InstitucionId ?? "",
            institucionNombre,
            q.Desde, q.Hasta,
            items);
    }
}
