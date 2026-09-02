using Diger.TramitesEstado.Application.Expedientes.Seguimiento;
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

        // Todas las filas de avance (incluidos los marcadores APLICA:*) para reproducir
        // EXACTAMENTE el cálculo ponderado del detalle del expediente
        // (MetodologiaDigitalizacion.Global), y no un conteo simple de pasos.
        var filasAvance = await ctx.ExpedienteEtapaAvances
            .AsNoTracking()
            .Where(a => expIds.Contains(a.ExpedienteId))
            .Select(a => new { a.ExpedienteId, a.TramiteIndex, a.SubId, a.Estado })
            .ToListAsync(ct);

        // Por (ExpedienteId, TramiteIndex): estados + aplica (igual que el seguimiento)
        // → avance ponderado idéntico, más los conteos de pasos para el Excel.
        var avanceMap = filasAvance
            .GroupBy(a => (a.ExpedienteId, a.TramiteIndex))
            .ToDictionary(g => g.Key, g =>
            {
                var estados = new Dictionary<string, int>();
                var aplica  = new Dictionary<string, bool>();
                int total = 0, completados = 0;
                foreach (var f in g)
                {
                    if (f.SubId.StartsWith("APLICA:"))
                    {
                        aplica[f.SubId["APLICA:".Length..]] = f.Estado == 1;
                    }
                    else
                    {
                        estados[f.SubId] = f.Estado;
                        total++;
                        if (f.Estado == 2) completados++;
                    }
                }
                var pct = (int)Math.Round(MetodologiaDigitalizacion.Global(estados, aplica) * 100);
                return (Total: total, Completados: completados, Pct: pct);
            });

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
                    return new InformeTramiteDto(t.TramiteIndex, t.NombreTramite, av.Total, av.Completados, av.Pct);
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
